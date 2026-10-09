using FinSight.Data;
using FinSight.Domain;
using FinSight.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Services;

public class DashboardQuery
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public List<int>? CategoryIds { get; init; }
    public TransactionType? Type { get; init; }
    public int? AccountId { get; init; }
    public string? Search { get; init; }
}

public class DashboardService(FinSightDbContext db)
{
    private IQueryable<Transaction> Filter(DashboardQuery q) =>
        db.Transactions.AsNoTracking().Where(t =>
            t.Date >= q.From && t.Date <= q.To &&
            (q.CategoryIds == null || q.CategoryIds.Count == 0 || q.CategoryIds.Contains(t.CategoryId)) &&
            (q.Type == null || t.Type == q.Type) &&
            (q.AccountId == null || t.AccountId == q.AccountId) &&
            (string.IsNullOrWhiteSpace(q.Search) || t.Description.ToLower().Contains(q.Search.ToLower())));

    public async Task<DashboardSummaryDto> SummarizeAsync(DashboardQuery query, CancellationToken ct = default)
    {
        var days = FinanceCalculator.DaysInRange(query.From, query.To);
        var months = FinanceCalculator.MonthsInRange(query.From, query.To);

        var baseQuery = Filter(query);
        var incomeTotal = await baseQuery.Where(t => t.Type == TransactionType.Income).SumAsync(t => t.Amount, ct);
        var expenseTotal = await baseQuery.Where(t => t.Type == TransactionType.Expense).SumAsync(t => t.Amount, ct);
        var incomeCount = await baseQuery.CountAsync(t => t.Type == TransactionType.Income, ct);
        var expenseCount = await baseQuery.CountAsync(t => t.Type == TransactionType.Expense, ct);
        var transactionCount = await baseQuery.CountAsync(ct);

        var balance = FinanceCalculator.NetBalance(incomeTotal, expenseTotal);

        var (prevFrom, prevTo) = FinanceCalculator.PreviousPeriod(query.From, query.To);
        var prevQuery = new DashboardQuery
        {
            From = prevFrom,
            To = prevTo,
            CategoryIds = query.CategoryIds,
            Type = query.Type,
            AccountId = query.AccountId,
            Search = query.Search
        };
        var prevBase = Filter(prevQuery);
        var prevIncome = await prevBase.Where(t => t.Type == TransactionType.Income).SumAsync(t => t.Amount, ct);
        var prevExpense = await prevBase.Where(t => t.Type == TransactionType.Expense).SumAsync(t => t.Amount, ct);
        var prevHasData = await prevBase.AnyAsync(ct);
        var prevBalance = FinanceCalculator.NetBalance(prevIncome, prevExpense);

        var budgetUsage = await ComputeBudgetUsageAsync(query, ct);

        return new DashboardSummaryDto(
            query.From,
            query.To,
            days,
            months,
            incomeTotal,
            expenseTotal,
            balance,
            transactionCount,
            incomeCount,
            expenseCount,
            FinanceCalculator.AverageExpensePerTransaction(expenseTotal, expenseCount),
            FinanceCalculator.AverageDailyExpense(expenseTotal, days),
            FinanceCalculator.AverageMonthlyExpense(expenseTotal, months),
            FinanceCalculator.SavingsRate(balance, incomeTotal),
            new PeriodComparisonDto(
                prevFrom,
                prevTo,
                prevIncome,
                prevExpense,
                prevBalance,
                FinanceCalculator.Delta(incomeTotal, prevIncome, higherIsBetter: true),
                FinanceCalculator.Delta(expenseTotal, prevExpense, higherIsBetter: false),
                new NetBalanceDeltaDto(balance, prevBalance, balance - prevBalance,
                    FinanceCalculator.PercentChange(balance, prevBalance)),
                prevHasData),
            budgetUsage);
    }

    public async Task<List<MonthlyTrendPointDto>> MonthlyTrendsAsync(DashboardQuery query, CancellationToken ct = default)
    {
        var rows = await Filter(query)
            .Select(t => new { t.Date, t.Type, t.Amount })
            .ToListAsync(ct);

        var months = new List<(int Year, int Month)>();
        var cursor = new DateOnly(query.From.Year, query.From.Month, 1);
        var last = new DateOnly(query.To.Year, query.To.Month, 1);
        while (cursor <= last)
        {
            months.Add((cursor.Year, cursor.Month));
            cursor = cursor.AddMonths(1);
        }

        var grouped = rows
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .ToDictionary(g => (g.Key.Year, g.Key.Month), g => (
                Income: g.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
                Expenses: g.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount),
                Count: g.Count()));

        return months.Select(m =>
        {
            grouped.TryGetValue(m, out var values);
            return new MonthlyTrendPointDto(
                m.Year, m.Month,
                $"{m.Month:D2}/{m.Year}",
                values.Income,
                values.Expenses,
                values.Income - values.Expenses,
                values.Count);
        }).ToList();
    }

    public async Task<List<CategoryBreakdownItemDto>> CategoryBreakdownAsync(
        DashboardQuery query, TransactionType type = TransactionType.Expense, CancellationToken ct = default)
    {
        var rows = await Filter(query)
            .Where(t => t.Type == type)
            .GroupBy(t => new { t.CategoryId, t.Category!.Name, t.Category.Color })
            .Select(g => new { g.Key.CategoryId, g.Key.Name, g.Key.Color, Amount = g.Sum(t => t.Amount), Count = g.Count() })
            .ToListAsync(ct);

        var total = rows.Sum(r => r.Amount);
        return rows
            .OrderByDescending(r => r.Amount)
            .Select(r => new CategoryBreakdownItemDto(
                r.CategoryId, r.Name, r.Color, r.Amount,
                total > 0 ? decimal.Round(r.Amount / total * 100m, 2) : 0m,
                r.Count))
            .ToList();
    }

    public async Task<List<WeekdaySpendingItemDto>> WeekdaySpendingAsync(DashboardQuery query, CancellationToken ct = default)
    {
        var rows = await Filter(query)
            .Where(t => t.Type == TransactionType.Expense)
            .Select(t => new { t.Date, t.Amount })
            .ToListAsync(ct);

        var labels = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        var days = FinanceCalculator.DaysInRange(query.From, query.To);
        var duration = query.To.ToDateTime(TimeOnly.MinValue) - query.From.ToDateTime(TimeOnly.MinValue);

        return Enumerable.Range(0, 7).Select(i =>
        {
            var dayRows = rows.Where(t => (int)t.Date.DayOfWeek == ((i + 1) % 7)).ToList();
            var occurrences = duration.Days >= 0
                ? Enumerable.Range(0, duration.Days + 1).Count(d => (int)query.From.AddDays(d).DayOfWeek == ((i + 1) % 7))
                : 0;
            var total = dayRows.Sum(t => t.Amount);
            return new WeekdaySpendingItemDto(
                i, labels[i], total,
                occurrences > 0 ? decimal.Round(total / occurrences, 2) : 0m,
                dayRows.Count);
        }).ToList();
    }

    public async Task<List<TopExpenseItemDto>> TopExpensesAsync(DashboardQuery query, int count = 10, CancellationToken ct = default)
    {
        count = Math.Clamp(count, 1, 50);
        return await Filter(query)
            .Where(t => t.Type == TransactionType.Expense)
            .OrderByDescending(t => t.Amount)
            .ThenByDescending(t => t.Date)
            .Take(count)
            .Select(t => new TopExpenseItemDto(
                t.Id, t.Date, t.Description, t.Amount, t.Category!.Name, t.Category.Color, t.Account != null ? t.Account.Name : null))
            .ToListAsync(ct);
    }

    public async Task<List<InsightDto>> InsightsAsync(DashboardQuery query, CancellationToken ct = default)
    {
        var insights = new List<InsightDto>();
        var hasData = await Filter(query).AnyAsync(ct);
        if (!hasData)
        {
            insights.Add(new InsightDto(
                "no-data", "No data for this period",
                "There are no transactions matching the selected filters. Import a CSV file or add a transaction manually.",
                InsightSeverity.Info, false, null));
            return insights;
        }

        var (prevFrom, prevTo) = FinanceCalculator.PreviousPeriod(query.From, query.To);
        var prevQuery = new DashboardQuery
        {
            From = prevFrom, To = prevTo,
            CategoryIds = query.CategoryIds, Type = query.Type, AccountId = query.AccountId, Search = query.Search
        };

        var currentRows = await Filter(query).Select(t => new { t.Date, t.Description, t.Amount, t.Type, t.CategoryId, t.Category!.Name, t.Category.Color }).ToListAsync(ct);
        var expenses = currentRows.Where(t => t.Type == TransactionType.Expense).ToList();

        // 1. Biggest spending category (fact)
        if (expenses.Count > 0)
        {
            var top = expenses.GroupBy(t => new { t.CategoryId, t.Name, t.Color })
                .Select(g => new { g.Key.Name, Amount = g.Sum(t => t.Amount), Count = g.Count() })
                .OrderByDescending(x => x.Amount).First();
            insights.Add(new InsightDto(
                "top-category", "Highest spending category",
                $"{top.Name} accounts for {top.Amount:C} across {top.Count} transactions in this period.",
                InsightSeverity.Info, false, top.Amount));
        }

        // 2. Day with the highest spending volume (fact)
        if (expenses.Count > 0)
        {
            var topDay = expenses.GroupBy(t => t.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(t => t.Amount), Count = g.Count() })
                .OrderByDescending(x => x.Total).First();
            insights.Add(new InsightDto(
                "top-day", "Heaviest spending day",
                $"{topDay.Date:dd/MM/yyyy} was the day with the highest spending: {topDay.Total:C} in {topDay.Count} transactions.",
                InsightSeverity.Info, false, topDay.Total));
        }

        // 3. Largest single expense (fact)
        var largest = expenses.OrderByDescending(t => t.Amount).FirstOrDefault();
        if (largest is not null)
        {
            insights.Add(new InsightDto(
                "largest-expense", "Largest single expense",
                $"{largest.Description} ({largest.Name}) on {largest.Date:dd/MM/yyyy} for {largest.Amount:C}.",
                InsightSeverity.Info, false, largest.Amount));
        }

        // 4. Category that increased the most versus the previous period (fact)
        var prevRows = await Filter(prevQuery).Select(t => new { t.CategoryId, t.Amount, t.Type }).ToListAsync(ct);
        if (prevRows.Count > 0 && expenses.Count > 0)
        {
            var prevByCat = prevRows.Where(t => t.Type == TransactionType.Expense)
                .GroupBy(t => t.CategoryId).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
            var currentByCat = expenses.GroupBy(t => t.CategoryId)
                .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
            var changes = currentByCat
                .Select(kvp => new
                {
                    CategoryId = kvp.Key,
                    Current = kvp.Value,
                    Previous = prevByCat.TryGetValue(kvp.Key, out var p) ? p : 0m
                })
                .Where(x => x.Previous > 0m && x.Current > x.Previous)
                .Select(x => new { x.CategoryId, x.Current, x.Previous, Change = x.Current - x.Previous, Percent = FinanceCalculator.PercentChange(x.Current, x.Previous) })
                .OrderByDescending(x => x.Change)
                .FirstOrDefault();
            if (changes is not null)
            {
                var name = expenses.First(t => t.CategoryId == changes.CategoryId).Name;
                insights.Add(new InsightDto(
                    "category-increase", "Biggest category increase",
                    $"{name} increased {changes.Percent:P1} ({changes.Change:C}) compared with the previous equivalent period.",
                    InsightSeverity.Warning, false, changes.Percent));
            }
        }

        // 5. Concentration of the top three categories (fact)
        if (expenses.Count > 0)
        {
            var total = expenses.Sum(t => t.Amount);
            var top3 = expenses.GroupBy(t => t.Name).OrderByDescending(g => g.Sum(t => t.Amount)).Take(3).ToList();
            if (top3.Count == 3)
            {
                var top3Total = top3.Sum(g => g.Sum(t => t.Amount));
                var share = total > 0 ? decimal.Round(top3Total / total * 100m, 1) : 0m;
                insights.Add(new InsightDto(
                    "top3-share", "Spending concentration",
                    $"The top three categories ({string.Join(", ", top3.Select(g => g.Key))}) represent {share:F1}% of all expenses.",
                    InsightSeverity.Info, false, share));
            }
        }

        // 6. Potential recurring subscriptions (pattern)
        var candidates = expenses
            .GroupBy(t => TextNormalizer.NormalizeKey(t.Description))
            .Where(g => g.Count() >= 3)
            .Select(g => new
            {
                Key = g.Key,
                Sample = g.First(),
                Count = g.Count(),
                Amounts = g.Select(t => t.Amount).OrderBy(x => x).ToList(),
                Dates = g.Select(t => t.Date).OrderBy(d => d).ToList()
            })
            .ToList();

        foreach (var candidate in candidates.Take(3))
        {
            var median = candidate.Amounts[candidate.Amounts.Count / 2];
            var stable = candidate.Amounts.All(a => Math.Abs(a - median) <= median * 0.15m);
            var spanMonths = FinanceCalculator.MonthsInRange(candidate.Dates.First(), candidate.Dates.Last());
            if (stable && spanMonths >= 2)
            {
                insights.Add(new InsightDto(
                    "possible-subscription", "Possible recurring payment",
                    $"{candidate.Sample.Description} appeared {candidate.Count} times with similar amounts around {median:C}. This looks like a recurring payment, but recurrence is not confirmed.",
                    InsightSeverity.Info, true, median));
            }
        }

        // 7. Budget alerts for the months covered by the period
        var budgetInsights = await BudgetAlertInsightsAsync(query, ct);
        insights.AddRange(budgetInsights);

        // 8. Savings rate (fact)
        var income = currentRows.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
        var expenseTotal = expenses.Sum(t => t.Amount);
        var rate = FinanceCalculator.SavingsRate(income - expenseTotal, income);
        if (rate is not null)
        {
            insights.Add(new InsightDto(
                "savings-rate", "Savings rate",
                $"You saved {rate:F1}% of your income in this period.",
                rate >= 0 ? InsightSeverity.Positive : InsightSeverity.Warning, false, rate));
        }

        return insights;
    }

    private async Task<List<InsightDto>> BudgetAlertInsightsAsync(DashboardQuery query, CancellationToken ct)
    {
        var insights = new List<InsightDto>();
        var months = new List<(int Year, int Month)>();
        var cursor = new DateOnly(query.From.Year, query.From.Month, 1);
        var last = new DateOnly(query.To.Year, query.To.Month, 1);
        while (cursor <= last)
        {
            months.Add((cursor.Year, cursor.Month));
            cursor = cursor.AddMonths(1);
        }

        foreach (var (year, month) in months)
        {
            var monthStart = new DateOnly(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var budgets = await db.Budgets.AsNoTracking()
                .Include(b => b.Category)
                .Where(b => b.Year == year && b.Month == month)
                .ToListAsync(ct);
            if (budgets.Count == 0) continue;

            var spentByCategory = (await db.Transactions.AsNoTracking()
                .Where(t => t.Date >= monthStart && t.Date <= monthEnd && t.Type == TransactionType.Expense)
                .GroupBy(t => t.CategoryId)
                .Select(g => new { CategoryId = g.Key, Spent = g.Sum(t => t.Amount) })
                .ToListAsync(ct))
                .ToDictionary(x => x.CategoryId, x => x.Spent);
            var totalSpent = await db.Transactions.AsNoTracking()
                .Where(t => t.Date >= monthStart && t.Date <= monthEnd && t.Type == TransactionType.Expense)
                .SumAsync(t => t.Amount, ct);

            foreach (var budget in budgets.OrderBy(b => b.CategoryId.HasValue))
            {
                // Category budgets count only their own category spending;
                // global budgets count the total spending of the month.
                var spent = budget.CategoryId is int catId && spentByCategory.TryGetValue(catId, out var categorySpent)
                    ? categorySpent
                    : totalSpent;
                var percent = budget.LimitAmount > 0 ? spent / budget.LimitAmount * 100m : 0m;
                var label = budget.Category?.Name ?? "global budget";
                if (percent >= 100m)
                {
                    insights.Add(new InsightDto(
                        $"budget-exceeded-{budget.Id}", "Budget exceeded",
                        $"{label} spending reached {spent:C} against a {budget.LimitAmount:C} limit ({month:D2}/{year}).",
                        InsightSeverity.Danger, false, percent));
                }
                else if (percent >= 80m)
                {
                    insights.Add(new InsightDto(
                        $"budget-warning-{budget.Id}", "Budget nearly reached",
                        $"{label} spending is at {percent:F0}% of the {budget.LimitAmount:C} monthly limit ({month:D2}/{year}).",
                        InsightSeverity.Warning, false, percent));
                }
            }
        }

        return insights;
    }

    public async Task<BudgetUsageDto> ComputeBudgetUsageAsync(DashboardQuery query, CancellationToken ct = default)
    {
        var months = new List<(int Year, int Month)>();
        var cursor = new DateOnly(query.From.Year, query.From.Month, 1);
        var last = new DateOnly(query.To.Year, query.To.Month, 1);
        while (cursor <= last)
        {
            months.Add((cursor.Year, cursor.Month));
            cursor = cursor.AddMonths(1);
        }

        var budgetQuery = db.Budgets.AsNoTracking().AsQueryable();
        if (query.CategoryIds is { Count: > 0 })
            budgetQuery = budgetQuery.Where(b => b.CategoryId == null || query.CategoryIds.Contains(b.CategoryId.Value));

        var firstMonthYear = months[0].Year;
        var firstMonthIndex = months[0].Month;
        var lastMonthYear = months[^1].Year;
        var lastMonthIndex = months[^1].Month;

        var budgets = await budgetQuery
            .Where(b => (b.Year > firstMonthYear || (b.Year == firstMonthYear && b.Month >= firstMonthIndex))
                     && (b.Year < lastMonthYear || (b.Year == lastMonthYear && b.Month <= lastMonthIndex)))
            .ToListAsync(ct);
        if (budgets.Count == 0) return new BudgetUsageDto(false, 0m, 0m, 0m, 0m);

        var totalLimit = budgets.Sum(b => b.LimitAmount);

        var expenseQuery = db.Transactions.AsNoTracking().Where(t =>
            t.Date >= query.From && t.Date <= query.To && t.Type == TransactionType.Expense);
        if (query.CategoryIds is { Count: > 0 })
            expenseQuery = expenseQuery.Where(t => query.CategoryIds.Contains(t.CategoryId));
        if (query.AccountId is not null)
            expenseQuery = expenseQuery.Where(t => t.AccountId == query.AccountId);
        var totalSpent = await expenseQuery.SumAsync(t => t.Amount, ct);

        var remaining = totalLimit - totalSpent;
        var usedPercent = totalLimit > 0 ? decimal.Round(totalSpent / totalLimit * 100m, 1) : 0m;
        return new BudgetUsageDto(true, totalLimit, totalSpent, remaining, usedPercent);
    }
}
