using FinSight.Data;
using FinSight.Domain;
using FinSight.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Services;

public class CategoryService(FinSightDbContext db)
{
    public async Task<List<CategoryDto>> ListAsync(bool includeArchived = true, CancellationToken ct = default) =>
        await db.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Color, c.AppliesTo, c.IsArchived, c.IsDemo))
            .ToListAsync(ct);

    public async Task<CategoryDto> CreateAsync(CategoryUpsertRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw ApiException.BadRequest("The category name is required.");
        if (name.Length > 80)
            throw ApiException.BadRequest("The category name must have at most 80 characters.");
        if (await db.Categories.AnyAsync(c => c.Name.ToLower() == name.ToLower(), ct))
            throw ApiException.Conflict($"A category named \"{name}\" already exists.");

        var category = new Category
        {
            Name = name,
            Color = NormalizeColor(request.Color),
            AppliesTo = request.AppliesTo,
            IsArchived = request.IsArchived
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return new CategoryDto(category.Id, category.Name, category.Color, category.AppliesTo, category.IsArchived, category.IsDemo);
    }

    public async Task<CategoryDto?> UpdateAsync(int id, CategoryUpsertRequest request, CancellationToken ct = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return null;

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw ApiException.BadRequest("The category name is required.");
        if (await db.Categories.AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower(), ct))
            throw ApiException.Conflict($"A category named \"{name}\" already exists.");

        category.Name = name;
        category.Color = NormalizeColor(request.Color);
        category.AppliesTo = request.AppliesTo;
        category.IsArchived = request.IsArchived;

        await db.SaveChangesAsync(ct);
        return new CategoryDto(category.Id, category.Name, category.Color, category.AppliesTo, category.IsArchived, category.IsDemo);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return false;

        var used = await db.Transactions.CountAsync(t => t.CategoryId == id, ct);
        if (used > 0)
            throw ApiException.Conflict(
                $"Category \"{category.Name}\" is used by {used} transaction(s). Archive it instead of deleting it.");

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static string NormalizeColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color)) return "#38bdf8";
        var trimmed = color.Trim();
        if (!trimmed.StartsWith('#') || trimmed.Length is not (4 or 7))
            throw ApiException.BadRequest("The colour must be a hex code such as #38bdf8.");
        return trimmed.ToLowerInvariant();
    }
}

public class AccountService(FinSightDbContext db)
{
    public async Task<List<AccountDto>> ListAsync(bool includeArchived = true, CancellationToken ct = default) =>
        await db.Accounts.AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new AccountDto(a.Id, a.Name, a.AccountType, a.IsArchived, a.IsDemo))
            .ToListAsync(ct);

    public async Task<AccountDto> CreateAsync(AccountUpsertRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw ApiException.BadRequest("The account name is required.");

        var account = new Account { Name = name, AccountType = request.AccountType, IsArchived = request.IsArchived };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
        return new AccountDto(account.Id, account.Name, account.AccountType, account.IsArchived, account.IsDemo);
    }

    public async Task<AccountDto?> UpdateAsync(int id, AccountUpsertRequest request, CancellationToken ct = default)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account is null) return null;
        account.Name = request.Name.Trim();
        account.AccountType = request.AccountType;
        account.IsArchived = request.IsArchived;
        await db.SaveChangesAsync(ct);
        return new AccountDto(account.Id, account.Name, account.AccountType, account.IsArchived, account.IsDemo);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account is null) return false;
        db.Accounts.Remove(account);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
