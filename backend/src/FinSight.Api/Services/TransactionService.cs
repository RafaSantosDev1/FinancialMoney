using FinSight.Data;
using FinSight.Domain;
using FinSight.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Services;

public class TransactionService(FinSightDbContext db)
{
    private IQueryable<Transaction> Filtered(TransactionQuery query) =>
        db.Transactions.AsNoTracking().Where(t =>
            (query.From == null || t.Date >= query.From) &&
            (query.To == null || t.Date <= query.To) &&
            (query.CategoryIds == null || query.CategoryIds.Count == 0 || query.CategoryIds.Contains(t.CategoryId)) &&
            (query.Type == null || t.Type == query.Type) &&
            (query.AccountId == null || t.AccountId == query.AccountId) &&
            (string.IsNullOrWhiteSpace(query.Search) || t.Description.ToLower().Contains(query.Search.ToLower())));

    public async Task<PagedResult<TransactionDto>> QueryAsync(TransactionQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var totalCount = await Filtered(query).CountAsync(ct);

        var sortBy = query.SortBy?.ToLowerInvariant();
        IQueryable<Transaction> ordered = (sortBy, query.Descending) switch
        {
            ("amount", false) => Filtered(query).OrderBy(t => t.Amount),
            ("amount", true) => Filtered(query).OrderByDescending(t => t.Amount),
            ("description", false) => Filtered(query).OrderBy(t => t.Description),
            ("description", true) => Filtered(query).OrderByDescending(t => t.Description),
            ("category", false) => Filtered(query).OrderBy(t => t.Category!.Name),
            ("category", true) => Filtered(query).OrderByDescending(t => t.Category!.Name),
            (_, false) => Filtered(query).OrderBy(t => t.Date),
            (_, true) => Filtered(query).OrderByDescending(t => t.Date)
        };

        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(Projection)
            .ToListAsync(ct);

        return new PagedResult<TransactionDto>(items, page, pageSize, totalCount, (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<TransactionDto?> GetAsync(int id, CancellationToken ct = default) =>
        await db.Transactions.AsNoTracking().Where(t => t.Id == id).Select(Projection).FirstOrDefaultAsync(ct);

    public async Task<TransactionDto> CreateAsync(TransactionCreateRequest request, CancellationToken ct = default)
    {
        Validate(request.Date, request.Description, request.Amount, request.CategoryId, request.AccountId);

        var transaction = new Transaction
        {
            Date = request.Date,
            Description = request.Description.Trim(),
            Amount = request.Amount,
            Type = request.Type,
            CategoryId = request.CategoryId,
            AccountId = request.AccountId,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(transaction.Id, ct))!;
    }

    public async Task<TransactionDto?> UpdateAsync(int id, TransactionUpdateRequest request, CancellationToken ct = default)
    {
        var transaction = await db.Transactions.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (transaction is null) return null;

        Validate(request.Date, request.Description, request.Amount, request.CategoryId, request.AccountId);

        transaction.Date = request.Date;
        transaction.Description = request.Description.Trim();
        transaction.Amount = request.Amount;
        transaction.Type = request.Type;
        transaction.CategoryId = request.CategoryId;
        transaction.AccountId = request.AccountId;
        transaction.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var transaction = await db.Transactions.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (transaction is null) return false;
        db.Transactions.Remove(transaction);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private void Validate(DateOnly date, string? description, decimal amount, int categoryId, int? accountId)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(description)) errors.Add("The description is required.");
        if (description is { Length: > 300 }) errors.Add("The description must have at most 300 characters.");
        if (amount <= 0m) errors.Add("The amount must be greater than zero.");
        if (amount > 9_999_999_999m) errors.Add("The amount is too large.");
        if (date.Year is < 2000 or > 2100) errors.Add("The date must be between 2000 and 2100.");

        var category = db.Categories.AsNoTracking().FirstOrDefault(c => c.Id == categoryId);
        if (category is null) errors.Add($"Category {categoryId} does not exist.");
        else if (category.IsArchived) errors.Add($"Category \"{category.Name}\" is archived.");

        if (accountId is not null && !db.Accounts.AsNoTracking().Any(a => a.Id == accountId))
            errors.Add($"Account {accountId} does not exist.");

        if (errors.Count > 0) throw ApiException.BadRequest("The transaction is invalid.", errors);
    }

    private static readonly System.Linq.Expressions.Expression<Func<Transaction, TransactionDto>> Projection =
        t => new TransactionDto(
            t.Id, t.Date, t.Description, t.Amount, t.Type, t.CategoryId, t.Category!.Name, t.Category.Color,
            t.AccountId, t.Account != null ? t.Account.Name : null, t.Notes, t.ExternalReference,
            t.ImportBatchId, t.CreatedAt);
}
