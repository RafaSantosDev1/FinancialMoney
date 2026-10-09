using FinSight.Data;
using FinSight.Domain;
using FinSight.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Services;

public class BudgetService(FinSightDbContext db)
{
    public async Task<List<BudgetDto>> ListAsync(int? year, int? month, CancellationToken ct = default) =>
        await db.Budgets.AsNoTracking()
            .Where(b => year == null || b.Year == year)
            .Where(b => month == null || b.Month == month)
            .OrderBy(b => b.Year).ThenBy(b => b.Month).ThenBy(b => b.CategoryId)
            .Select(b => new BudgetDto(b.Id, b.CategoryId, b.Category != null ? b.Category.Name : null,
                b.Year, b.Month, b.LimitAmount, b.IsDemo))
            .ToListAsync(ct);

    public async Task<BudgetDto> CreateAsync(BudgetUpsertRequest request, CancellationToken ct = default)
    {
        await ValidateAsync(request, null, ct);
        var budget = new Budget
        {
            CategoryId = request.CategoryId,
            Year = request.Year,
            Month = request.Month,
            LimitAmount = request.LimitAmount
        };
        db.Budgets.Add(budget);
        await db.SaveChangesAsync(ct);

        return new BudgetDto(budget.Id, budget.CategoryId, await CategoryNameAsync(budget.CategoryId, ct),
            budget.Year, budget.Month, budget.LimitAmount, budget.IsDemo);
    }

    public async Task<BudgetDto?> UpdateAsync(int id, BudgetUpsertRequest request, CancellationToken ct = default)
    {
        var budget = await db.Budgets.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (budget is null) return null;

        await ValidateAsync(request, id, ct);

        budget.CategoryId = request.CategoryId;
        budget.Year = request.Year;
        budget.Month = request.Month;
        budget.LimitAmount = request.LimitAmount;

        await db.SaveChangesAsync(ct);
        return new BudgetDto(budget.Id, budget.CategoryId, await CategoryNameAsync(budget.CategoryId, ct),
            budget.Year, budget.Month, budget.LimitAmount, budget.IsDemo);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var budget = await db.Budgets.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (budget is null) return false;
        db.Budgets.Remove(budget);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<BudgetStatusDto>> StatusAsync(int year, int month, CancellationToken ct = default)
    {
        var budgets = await db.Budgets.AsNoTracking()
            .Include(b => b.Category)
            .Where(b => b.Year == year && b.Month == month)
            .OrderBy(b => b.CategoryId)
            .ToListAsync(ct);

        if (budgets.Count == 0) return [];

        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var spentByCategory = (await db.Transactions.AsNoTracking()
            .Where(t => t.Date >= monthStart && t.Date <= monthEnd && t.Type == TransactionType.Expense)
            .GroupBy(t => t.CategoryId)
            .Select(g => new { CategoryId = g.Key, Spent = g.Sum(t => t.Amount) })
            .ToListAsync(ct))
            .ToDictionary(x => x.CategoryId, x => x.Spent);

        var totalSpent = await db.Transactions.AsNoTracking()
            .Where(t => t.Date >= monthStart && t.Date <= monthEnd && t.Type == TransactionType.Expense)
            .SumAsync(t => t.Amount, ct);

        return budgets.Select(b =>
        {
            // A category budget only counts the spending of that category;
            // a global budget counts the total spending of the month.
            var spent = b.CategoryId is int catId
                ? spentByCategory.TryGetValue(catId, out var categorySpent) ? categorySpent : 0m
                : totalSpent;
            var percent = b.LimitAmount > 0 ? decimal.Round(spent / b.LimitAmount * 100m, 1) : 0m;
            var health = b.LimitAmount <= 0m
                ? BudgetHealth.NoLimit
                : percent >= 100m ? BudgetHealth.Exceeded
                : percent >= 80m ? BudgetHealth.Warning
                : BudgetHealth.Ok;
            return new BudgetStatusDto(
                b.CategoryId, b.Category?.Name, b.Category?.Color, b.Year, b.Month, b.LimitAmount,
                spent, b.LimitAmount - spent, percent, health, b.IsDemo);
        }).ToList();
    }

    private async Task ValidateAsync(BudgetUpsertRequest request, int? existingId, CancellationToken ct)
    {
        var errors = new List<string>();
        if (request.Month is < 1 or > 12) errors.Add("The month must be between 1 and 12.");
        if (request.Year is < 2000 or > 2100) errors.Add("The year must be between 2000 and 2100.");
        if (request.LimitAmount < 0m) errors.Add("The limit must be zero or positive.");

        if (request.CategoryId is not null &&
            !await db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
            errors.Add($"Category {request.CategoryId} does not exist.");

        var conflict = await db.Budgets.AnyAsync(b =>
            b.Year == request.Year && b.Month == request.Month &&
            b.CategoryId == request.CategoryId &&
            (existingId == null || b.Id != existingId), ct);
        if (conflict)
            errors.Add(request.CategoryId is null
                ? $"A global budget already exists for {request.Month:D2}/{request.Year}."
                : $"A budget for this category already exists for {request.Month:D2}/{request.Year}.");

        if (errors.Count > 0) throw ApiException.BadRequest("The budget is invalid.", errors);
    }

    private async Task<string?> CategoryNameAsync(int? categoryId, CancellationToken ct) =>
        categoryId is null ? null : (await db.Categories.AsNoTracking()
            .Where(c => c.Id == categoryId).Select(c => c.Name).FirstOrDefaultAsync(ct));
}
