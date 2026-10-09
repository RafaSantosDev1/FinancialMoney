using FinSight.Data;
using FinSight.Domain;
using FinSight.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Services.Import;

internal sealed class ParsedRow
{
    public int LineNumber { get; set; }
    public string[] Cells { get; set; } = [];
    public DateOnly? Date { get; set; }
    public string? DateRaw { get; set; }
    public string? Description { get; set; }
    public decimal? SignedAmount { get; set; }
    public decimal? DebitAmount { get; set; }
    public decimal? CreditAmount { get; set; }
    public TransactionType? Type { get; set; }
    public string? CategoryName { get; set; }
    public string? AccountName { get; set; }
    public string? Reference { get; set; }
    public bool HasNegativeSign { get; set; }
    public bool DebitCreditConflict { get; set; }
    public List<string> Errors { get; } = [];
    public ImportRowStatus Status { get; set; } = ImportRowStatus.Valid;
    public string Key { get; set; } = string.Empty;
}

public class ImportService(FinSightDbContext db)
{
    private const int MinYear = 2000;
    private const int MaxYear = 2100;

    // ---------- Preview ----------

    public async Task<ImportPreviewResponse> PreviewAsync(ImportPreviewRequest request, CancellationToken ct = default)
    {
        var (rows, headers, dataRows, delimiter, hasHeader) = LoadFile(request.Content, request.FileName, request.Options);

        var mapping = request.Mapping ?? HeaderMapper.Detect(headers);
        ValidateMapping(mapping, headers);

        var dateSamples = dataRows.Where(r => mapping.DateColumn < r.Length).Select(r => r[mapping.DateColumn]);
        var amountSamples = dataRows.Where(r => AmountCellIndex(mapping, r) is >= 0)
            .Select(r => r[AmountCellIndex(mapping, r)!.Value]);

        var dateDetection = request.Options.DateFormat is null
            ? ValueParser.DetectDateFormat(dateSamples)
            : new DateDetectionResult(request.Options.DateFormat, false);
        var decimalSeparator = request.Options.DecimalSeparator ?? ValueParser.DetectDecimalSeparator(amountSamples);

        var options = new ImportOptionsDto(delimiter, dateDetection.Format, decimalSeparator, hasHeader, request.Options.DefaultType);

        var parsed = ParseRows(dataRows, mapping, options, headers.Count);
        var warnings = new List<string>();

        var inference = DetermineTypeInference(mapping, parsed);
        ApplyTypeInference(parsed, inference, options);

        MarkInvalidRows(parsed);
        if (inference.Source == "ambiguous")
        {
            warnings.Add("The transaction type cannot be inferred from the file (no sign information, no type column and no debit/credit columns). Choose how every row should be treated before confirming.");
        }
        if (dateDetection is { Ambiguous: true, Format: not null })
        {
            warnings.Add($"The date format is ambiguous; \"{dateDetection.Format}\" was assumed. Confirm it before importing.");
        }

        var duplicateGroups = await DetectDuplicatesAsync(parsed, ct);
        foreach (var group in duplicateGroups)
        {
            if (group.ExistingMatches > 0)
                warnings.Add($"Possible duplicate of {group.ExistingMatches} existing transaction(s): row {group.FileLineNumbers[0]}.");
        }
        var inFileDuplicates = duplicateGroups.Where(g => g.FileLineNumbers.Count > 1).ToList();
        foreach (var group in inFileDuplicates.Take(5))
        {
            warnings.Add($"Rows {string.Join(", ", group.FileLineNumbers)} appear to be duplicates of each other.");
        }

        var summary = new ImportValidationSummaryDto(
            parsed.Count,
            parsed.Count(p => p.Status == ImportRowStatus.Valid),
            parsed.Count(p => p.Status == ImportRowStatus.Invalid),
            parsed.Count(p => p.Status == ImportRowStatus.Duplicate));

        var errors = parsed.Where(p => p.Status == ImportRowStatus.Invalid)
            .Take(ImportConstants.MaxReportedErrors)
            .Select(p => new ImportRowErrorDto(p.LineNumber, (IReadOnlyList<string>)p.Errors))
            .ToList();

        var previewRows = parsed.Take(ImportConstants.MaxPreviewRows)
            .Select(p => new ImportPreviewRowDto(
                p.LineNumber, p.Date, p.DateRaw, p.Description, p.SignedAmount is null ? null : Math.Abs(p.SignedAmount.Value),
                p.DebitAmount is null ? null : Math.Abs(p.DebitAmount.Value),
                p.CreditAmount is null ? null : Math.Abs(p.CreditAmount.Value),
                p.Type, p.CategoryName, p.AccountName, p.Reference, p.Status, (IReadOnlyList<string>)p.Errors))
            .ToList();

        return new ImportPreviewResponse(
            request.FileName, delimiter, hasHeader, headers, mapping, mapping,
            dateDetection.Format ?? string.Empty, decimalSeparator,
            summary, previewRows, errors, duplicateGroups, warnings,
            inference.Source == "ambiguous", inference.Source);
    }

    // ---------- Confirm ----------

    public async Task<ImportResultDto> ConfirmAsync(ImportConfirmRequest request, CancellationToken ct = default)
    {
        var (rows, headers, dataRows, delimiter, hasHeader) = LoadFile(request.Content, request.FileName, request.Options);

        var mapping = request.Mapping;
        ValidateMapping(mapping, headers);

        var dateSamples = dataRows.Where(r => mapping.DateColumn < r.Length).Select(r => r[mapping.DateColumn]);
        var amountSamples = dataRows.Where(r => AmountCellIndex(mapping, r) is >= 0)
            .Select(r => r[AmountCellIndex(mapping, r)!.Value]);
        var dateDetection = request.Options.DateFormat is null
            ? ValueParser.DetectDateFormat(dateSamples)
            : new DateDetectionResult(request.Options.DateFormat, false);
        var decimalSeparator = request.Options.DecimalSeparator ?? ValueParser.DetectDecimalSeparator(amountSamples);

        var options = new ImportOptionsDto(delimiter, dateDetection.Format, decimalSeparator, hasHeader, request.Options.DefaultType);

        var parsed = ParseRows(dataRows, mapping, options, headers.Count);
        var warnings = new List<string>();

        var inference = DetermineTypeInference(mapping, parsed);
        ApplyTypeInference(parsed, inference, options);

        MarkInvalidRows(parsed);
        if (inference.Source == "ambiguous" && options.DefaultType is null)
            throw ApiException.BadRequest(
                "The transaction type could not be inferred from the file. Provide the default treatment for the rows.");

        await DetectDuplicatesAsync(parsed, ct);

        var rowsToImport = parsed
            .Where(p => p.Status is ImportRowStatus.Valid or ImportRowStatus.Duplicate)
            .Where(p => p.Status == ImportRowStatus.Valid || request.ImportDuplicates)
            .ToList();

        var rejected = parsed.Where(p => p.Status == ImportRowStatus.Invalid).ToList();
        var duplicates = parsed.Count(p => p.Status == ImportRowStatus.Duplicate);

        // Resolve categories and accounts (auto-creating unknown ones).
        var categories = await db.Categories.AsNoTracking().ToListAsync(ct);
        var categoryLookup = BuildLookup(categories, c => c.Name);
        var accounts = await db.Accounts.AsNoTracking().ToListAsync(ct);
        var accountLookup = BuildLookup(accounts, a => a.Name);

        var createdCategories = new List<string>();
        var createdAccounts = new List<string>();

        Category ResolveCategory(string? name)
        {
            var key = TextNormalizer.NormalizeKey(name);
            if (key.Length == 0)
            {
                // Rows without a category fall back to the catch-all category.
                key = categoryLookup.ContainsKey("outros") ? "outros"
                    : categoryLookup.ContainsKey("outro") ? "outro"
                    : categoryLookup.ContainsKey("other") ? "other"
                    : "outros";
            }
            if (categoryLookup.TryGetValue(key, out var existing)) return existing;

            var fallbackName = string.IsNullOrWhiteSpace(name) ? "Outros" : name.Trim();
            var trimmedName = fallbackName[..Math.Min(80, fallbackName.Length)];
            var category = new Category
            {
                Name = trimmedName,
                Color = PaletteColor(key),
                AppliesTo = CategoryAppliesTo.Both
            };
            db.Categories.Add(category);
            categoryLookup[key] = category;
            createdCategories.Add(category.Name);
            return category;
        }

        Account? ResolveAccount(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var key = TextNormalizer.NormalizeKey(name);
            if (accountLookup.TryGetValue(key, out var existing)) return existing;
            var account = new Account { Name = name.Trim(), AccountType = AccountType.Other };
            db.Accounts.Add(account);
            accountLookup[key] = account;
            createdAccounts.Add(account.Name);
            return account;
        }

        foreach (var row in rowsToImport)
        {
            var category = ResolveCategory(row.CategoryName);
            if (category.AppliesTo != CategoryAppliesTo.Both &&
                (category.AppliesTo == CategoryAppliesTo.Income) != (row.Type == TransactionType.Income))
            {
                warnings.Add($"Row {row.LineNumber}: category \"{category.Name}\" expects " +
                             $"{(category.AppliesTo == CategoryAppliesTo.Income ? "income" : "expense")} transactions but this row is " +
                             $"{(row.Type == TransactionType.Income ? "income" : "expense")}.");
            }
        }

        var batch = new ImportBatch
        {
            FileName = request.FileName[..Math.Min(260, request.FileName.Length)],
            ImportedAt = DateTime.UtcNow,
            TotalRows = parsed.Count,
            ImportedRows = rowsToImport.Count,
            RejectedRows = rejected.Count,
            DuplicateRows = duplicates
        };

        foreach (var row in rowsToImport)
        {
            var category = ResolveCategory(row.CategoryName);
            var account = ResolveAccount(row.AccountName);
            batch.Transactions.Add(new Transaction
            {
                Date = row.Date!.Value,
                Description = row.Description!,
                Amount = Math.Abs(row.SignedAmount ?? row.DebitAmount ?? row.CreditAmount ?? 0m),
                Type = row.Type!.Value,
                // Existing categories/accounts are linked by key; newly created ones
                // (temporary id 0) are linked through the navigation so the provider
                // assigns the identifier on save.
                CategoryId = category.Id,
                Category = category.Id == 0 ? category : null,
                AccountId = account?.Id,
                Account = account is { Id: 0 } ? account : null,
                Notes = null,
                ExternalReference = row.Reference?[..Math.Min(120, row.Reference.Length)],
                CreatedAt = DateTime.UtcNow
            });
        }

        db.ImportBatches.Add(batch);
        if (db.Database.IsRelational())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        else
        {
            await db.SaveChangesAsync(ct);
        }

        var rejectedDetail = rejected.Take(ImportConstants.MaxReportedErrors)
            .Select(r => new ImportRowErrorDto(r.LineNumber, (IReadOnlyList<string>)r.Errors))
            .ToList();

        return new ImportResultDto(
            batch.Id, batch.FileName, batch.ImportedAt, batch.TotalRows, batch.ImportedRows,
            batch.RejectedRows, batch.DuplicateRows, rejectedDetail, warnings, createdCategories, createdAccounts);
    }

    public async Task<ImportBatchDto?> GetBatchAsync(int id, CancellationToken ct = default)
    {
        var batch = await db.ImportBatches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (batch is null) return null;

        var sample = await db.Transactions.AsNoTracking()
            .Where(t => t.ImportBatchId == id)
            .OrderByDescending(t => t.Date).Take(50)
            .Select(t => new TransactionDto(
                t.Id, t.Date, t.Description, t.Amount, t.Type, t.CategoryId, t.Category!.Name, t.Category.Color,
                t.AccountId, t.Account != null ? t.Account.Name : null, t.Notes, t.ExternalReference,
                t.ImportBatchId, t.CreatedAt))
            .ToListAsync(ct);

        return new ImportBatchDto(
            batch.Id, batch.FileName, batch.ImportedAt, batch.TotalRows, batch.ImportedRows,
            batch.RejectedRows, batch.DuplicateRows, sample);
    }

    // ---------- Internals ----------

    private (List<string[]> Rows, List<string> Headers, List<string[]> DataRows, char Delimiter, bool HasHeader)
        LoadFile(string content, string fileName, ImportOptionsDto options)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw ApiException.BadRequest("The file is empty.");

        var contentBytes = System.Text.Encoding.UTF8.GetByteCount(content);
        if (contentBytes > ImportConstants.MaxFileSizeBytes)
            throw ApiException.BadRequest($"The file exceeds the maximum size of {ImportConstants.MaxFileSizeBytes / (1024 * 1024)} MB.");

        var delimiter = options.Delimiter ?? CsvFileParser.DetectDelimiter(content);
        var rows = CsvFileParser.ParseRows(content, delimiter);
        if (rows.Count == 0)
            throw ApiException.BadRequest("The file does not contain any rows.");

        var hasHeader = options.HasHeader;
        var headers = hasHeader
            ? rows[0].Select((h, i) => string.IsNullOrWhiteSpace(h) ? $"Column {i + 1}" : h.Trim()).ToList()
            : rows[0].Select((_, i) => $"Column {i + 1}").ToList();
        var dataRows = hasHeader ? rows.Skip(1).ToList() : rows;

        if (dataRows.Count > ImportConstants.MaxRows)
            throw ApiException.BadRequest($"The file contains {dataRows.Count} rows; the maximum is {ImportConstants.MaxRows}.");

        return (rows, headers, dataRows, delimiter, hasHeader);
    }

    private static int? AmountCellIndex(ColumnMappingDto mapping, string[] row)
    {
        if (mapping.UsesDebitCredit)
        {
            if (mapping.DebitColumn is int d && d < row.Length && !string.IsNullOrWhiteSpace(row[d])) return d;
            if (mapping.CreditColumn is int c && c < row.Length && !string.IsNullOrWhiteSpace(row[c])) return c;
            return mapping.AmountColumn >= 0 && mapping.AmountColumn < row.Length ? mapping.AmountColumn : null;
        }
        return mapping.AmountColumn >= 0 && mapping.AmountColumn < row.Length ? mapping.AmountColumn : null;
    }

    private static void ValidateMapping(ColumnMappingDto mapping, IReadOnlyList<string> headers)
    {
        var errors = new List<string>();
        if (mapping.DateColumn < 0) errors.Add("Map the transaction date column.");
        if (mapping.DescriptionColumn < 0) errors.Add("Map the description column.");
        if (!mapping.UsesDebitCredit && mapping.AmountColumn < 0)
            errors.Add("Map the amount column (or separate debit and credit columns).");

        foreach (var (column, name) in new (int? Column, string Name)[]
                 {
                     (mapping.TypeColumn, "type"), (mapping.CategoryColumn, "category"),
                     (mapping.AccountColumn, "account"), (mapping.ReferenceColumn, "reference"),
                     (mapping.DebitColumn, "debit"), (mapping.CreditColumn, "credit")
                 })
        {
            if (column is int idx && idx >= headers.Count)
                errors.Add($"The {name} column index {idx} is outside the file.");
        }

        if (errors.Count > 0)
            throw ApiException.BadRequest("The column mapping is incomplete or invalid.", errors);
    }

    private List<ParsedRow> ParseRows(List<string[]> dataRows, ColumnMappingDto mapping, ImportOptionsDto options, int expectedColumnCount)
    {
        var parsed = new List<ParsedRow>();
        for (var i = 0; i < dataRows.Count; i++)
        {
            var lineNumber = i + 1 + (options.HasHeader ? 1 : 0);
            var cells = dataRows[i];
            var row = new ParsedRow { LineNumber = lineNumber, Cells = cells };

            // Rows with more fields than the header usually indicate an unquoted
            // decimal separator conflicting with the delimiter.
            var significantCells = CountSignificantCells(cells);
            if (expectedColumnCount > 0 && significantCells > expectedColumnCount)
                row.Errors.Add(
                    $"The row has {significantCells} fields but the header defines {expectedColumnCount}. " +
                    "Check the file delimiter and decimal separator (values with a decimal comma must be quoted).");


            // Date
            var dateRaw = SafeGet(cells, mapping.DateColumn);
            row.DateRaw = dateRaw;
            if (string.IsNullOrWhiteSpace(dateRaw))
                row.Errors.Add("The transaction date is missing.");
            else if (ValueParser.ParseDate(dateRaw, options.DateFormat) is DateOnly parsedDate)
                row.Date = parsedDate;
            else
                row.Errors.Add($"\"{dateRaw.Trim()}\" is not a valid date for the selected format.");

            // Description
            var description = SafeGet(cells, mapping.DescriptionColumn);
            if (string.IsNullOrWhiteSpace(description))
                row.Errors.Add("The description is missing.");
            else
                row.Description = description.Trim();

            // Category / account / reference
            if (mapping.CategoryColumn is int catIndex && catIndex < cells.Length && !string.IsNullOrWhiteSpace(cells[catIndex]))
                row.CategoryName = cells[catIndex].Trim();
            if (mapping.AccountColumn is int accIndex && accIndex < cells.Length && !string.IsNullOrWhiteSpace(cells[accIndex]))
                row.AccountName = cells[accIndex].Trim();
            if (mapping.ReferenceColumn is int refIndex && refIndex < cells.Length && !string.IsNullOrWhiteSpace(cells[refIndex]))
                row.Reference = cells[refIndex].Trim();

            // Amounts
            if (mapping.UsesDebitCredit)
            {
                var debit = mapping.DebitColumn is int dIdx && dIdx < cells.Length && !string.IsNullOrWhiteSpace(cells[dIdx])
                    ? ValueParser.ParseAmount(cells[dIdx], options.DecimalSeparator ?? ',')
                    : null;
                var credit = mapping.CreditColumn is int cIdx && cIdx < cells.Length && !string.IsNullOrWhiteSpace(cells[cIdx])
                    ? ValueParser.ParseAmount(cells[cIdx], options.DecimalSeparator ?? ',')
                    : null;

                if (debit is { Success: false }) row.Errors.Add(debit.Error!);
                if (credit is { Success: false }) row.Errors.Add(credit.Error!);

                if (debit is { Success: true, SignedAmount: not 0m } && credit is { Success: true, SignedAmount: not 0m })
                {
                    row.DebitCreditConflict = true;
                    row.Errors.Add("Both debit and credit columns contain values; the row is ambiguous.");
                }
                else
                {
                    if (debit is { Success: true, SignedAmount: not 0m })
                    {
                        row.DebitAmount = debit.SignedAmount;
                        row.Type = TransactionType.Expense;
                    }
                    else if (credit is { Success: true, SignedAmount: not 0m })
                    {
                        row.CreditAmount = credit.SignedAmount;
                        row.Type = TransactionType.Income;
                    }
                }
            }
            else
            {
                var amountRaw = SafeGet(cells, mapping.AmountColumn);
                var amount = ValueParser.ParseAmount(amountRaw, options.DecimalSeparator ?? ',');
                if (!amount.Success)
                    row.Errors.Add(amount.Error ?? "The amount is invalid.");
                else
                {
                    row.SignedAmount = amount.SignedAmount;
                    row.HasNegativeSign = amount.SignedAmount < 0m;
                }
            }

            // Type column
            if (!row.DebitCreditConflict && mapping.TypeColumn is int typeIdx && typeIdx < cells.Length &&
                !string.IsNullOrWhiteSpace(cells[typeIdx]))
            {
                var type = ValueParser.ParseTypeWord(cells[typeIdx]);
                if (type is null)
                    row.Errors.Add($"\"{cells[typeIdx].Trim()}\" is not a recognised transaction type.");
                else
                    row.Type = type;
            }

            parsed.Add(row);
        }

        return parsed;
    }

    private static void MarkInvalidRows(List<ParsedRow> parsed)
    {
        foreach (var row in parsed)
            if (row.Errors.Count > 0)
                row.Status = ImportRowStatus.Invalid;
    }

    private static void ApplyTypeInference(List<ParsedRow> parsed, (string Source, TransactionType? DefaultType) inference, ImportOptionsDto options)
    {
        foreach (var row in parsed)
        {
            if (row.Type is not null || row.Errors.Count > 0) continue;

            switch (inference.Source)
            {
                case "sign":
                    if (row.SignedAmount is not null)
                        row.Type = row.SignedAmount < 0m ? TransactionType.Expense : TransactionType.Income;
                    break;
                case "ambiguous" when options.DefaultType is TransactionType defaultType && row.SignedAmount is not null:
                    row.Type = defaultType;
                    break;
            }
        }
    }

    private static (string Source, TransactionType? DefaultType) DetermineTypeInference(ColumnMappingDto mapping, List<ParsedRow> parsed)
    {
        if (mapping.UsesDebitCredit) return ("debitCredit", null);
        if (mapping.TypeColumn is not null)
        {
            var typeWords = parsed.Count(p => p.Type is not null);
            return typeWords >= parsed.Count * 0.8m ? ("typeColumn", null) : ("ambiguous", null);
        }
        var anyNegative = parsed.Any(p => p.HasNegativeSign);
        if (anyNegative) return ("sign", null);
        return ("ambiguous", null);
    }

    private async Task<List<ImportDuplicateGroupDto>> DetectDuplicatesAsync(List<ParsedRow> parsed, CancellationToken ct)
    {
        // First pass: find the earliest row for each key so only later rows are duplicates.
        var firstOccurrence = new Dictionary<string, int>();
        foreach (var row in parsed)
        {
            if (row.Errors.Count > 0) continue;
            row.Key = BuildKey(row);
            if (!firstOccurrence.ContainsKey(row.Key)) firstOccurrence[row.Key] = row.LineNumber;
        }

        var dates = parsed.Where(p => p.Errors.Count == 0 && p.Date.HasValue).Select(p => p.Date!.Value).ToList();
        var existing = new HashSet<string>();
        if (dates.Count > 0)
        {
            var min = dates.Min();
            var max = dates.Max();
            var dbRows = await db.Transactions.AsNoTracking()
                .Where(t => t.Date >= min && t.Date <= max)
                .Select(t => new
                {
                    t.Date, t.Description, t.Amount,
                    t.AccountId, t.Account, t.ExternalReference
                })
                .ToListAsync(ct);

            existing = dbRows
                .Select(t => BuildKey(t.Date, t.Description, t.Amount, t.Account?.Name, t.ExternalReference))
                .ToHashSet();
        }

        var groups = new Dictionary<string, ImportDuplicateGroupDto>();
        foreach (var row in parsed)
        {
            if (row.Errors.Count > 0 || string.IsNullOrEmpty(row.Key)) continue;
            var inDb = existing.Contains(row.Key);
            var isDuplicate = inDb || firstOccurrence[row.Key] != row.LineNumber;
            if (!isDuplicate) continue;

            row.Status = ImportRowStatus.Duplicate;

            if (!groups.TryGetValue(row.Key, out var group))
            {
                group = new ImportDuplicateGroupDto(row.Key, new List<int>(), inDb ? 1 : 0);
                groups[row.Key] = group;
            }
            ((List<int>)group.FileLineNumbers).Add(row.LineNumber);
        }

        return groups.Values.OrderBy(g => g.FileLineNumbers[0]).ToList();
    }

    private static string BuildKey(ParsedRow row)
    {
        var amount = Math.Abs(row.SignedAmount ?? row.DebitAmount ?? row.CreditAmount ?? 0m);
        return BuildKey(row.Date, row.Description, amount, row.AccountName, row.Reference);
    }

    private static string BuildKey(DateOnly? date, string? description, decimal amount, string? account, string? reference)
    {
        if (!string.IsNullOrWhiteSpace(reference))
            return "ref:" + TextNormalizer.NormalizeKey(reference);
        return $"{date:yyyy-MM-dd}|{Math.Round(amount, 2)}|{TextNormalizer.NormalizeKey(description)}" +
               (string.IsNullOrWhiteSpace(account) ? string.Empty : "|" + TextNormalizer.NormalizeKey(account));
    }

    private static int CountSignificantCells(string[] cells)
    {
        var count = cells.Length;
        while (count > 0 && string.IsNullOrWhiteSpace(cells[count - 1])) count--;
        return count;
    }

    /// <summary>
    /// Builds a name lookup that tolerates duplicated names (e.g. a user created
    /// "Housing" and an import created "Housing" again): real entries win over demo ones.
    /// </summary>
    private static Dictionary<string, T> BuildLookup<T>(IEnumerable<T> items, Func<T, string> nameSelector)
        where T : class
    {
        var lookup = new Dictionary<string, T>();
        // Demo entries are inserted first so that real entries win on name collisions.
        foreach (var item in items.OrderByDescending(i => i is Category { IsDemo: true } || i is Account { IsDemo: true }))
        {
            var key = TextNormalizer.NormalizeKey(nameSelector(item));
            if (key.Length == 0) continue;
            lookup[key] = item;
        }
        return lookup;
    }

    private static string? SafeGet(string[] cells, int index)
    {
        if (index < 0 || index >= cells.Length) return null;
        return cells[index];
    }

    private static readonly string[] Palette =
    [
        "#38bdf8", "#a78bfa", "#34d399", "#fb7185", "#fbbf24",
        "#818cf8", "#f472b6", "#2dd4bf", "#fb923c", "#4ade80"
    ];

    private static string PaletteColor(string key)
    {
        if (string.IsNullOrEmpty(key)) return Palette[0];
        var hash = key.Sum(c => c);
        return Palette[Math.Abs(hash) % Palette.Length];
    }
}



