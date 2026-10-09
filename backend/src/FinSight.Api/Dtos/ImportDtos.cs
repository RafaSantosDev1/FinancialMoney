using FinSight.Domain;

namespace FinSight.Dtos;

public static class ImportConstants
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;
    public const int MaxRows = 20_000;
    public const int MaxPreviewRows = 25;
    public const int MaxReportedErrors = 100;

    public static readonly char[] CandidateDelimiters = [',', ';', '\t', '|'];
}

public enum ImportRowStatus
{
    Valid = 0,
    Invalid = 1,
    Duplicate = 2
}

public record ImportOptionsDto(
    char? Delimiter,
    string? DateFormat,
    char? DecimalSeparator,
    bool HasHeader,
    TransactionType? DefaultType);

public record ColumnMappingDto(
    int DateColumn = -1,
    int DescriptionColumn = -1,
    int AmountColumn = -1,
    int? TypeColumn = null,
    int? CategoryColumn = null,
    int? AccountColumn = null,
    int? ReferenceColumn = null,
    int? DebitColumn = null,
    int? CreditColumn = null)
{
    public bool UsesDebitCredit => DebitColumn.HasValue || CreditColumn.HasValue;
}

public record ImportPreviewRequest(
    string FileName,
    string Content,
    ImportOptionsDto Options,
    ColumnMappingDto? Mapping);

public record ImportPreviewRowDto(
    int LineNumber,
    DateOnly? Date,
    string? DateRaw,
    string? Description,
    decimal? Amount,
    decimal? DebitAmount,
    decimal? CreditAmount,
    TransactionType? Type,
    string? CategoryName,
    string? AccountName,
    string? Reference,
    ImportRowStatus Status,
    IReadOnlyList<string> Errors);

public record ImportValidationSummaryDto(int TotalRows, int ValidRows, int InvalidRows, int DuplicateRows);

public record ImportRowErrorDto(int LineNumber, IReadOnlyList<string> Errors);

public record ImportDuplicateGroupDto(
    string Key,
    IReadOnlyList<int> FileLineNumbers,
    int ExistingMatches);

public record ImportPreviewResponse(
    string FileName,
    char Delimiter,
    bool HasHeader,
    IReadOnlyList<string> Headers,
    ColumnMappingDto DetectedMapping,
    ColumnMappingDto EffectiveMapping,
    string DetectedDateFormat,
    char DetectedDecimalSeparator,
    ImportValidationSummaryDto Summary,
    IReadOnlyList<ImportPreviewRowDto> Rows,
    IReadOnlyList<ImportRowErrorDto> Errors,
    IReadOnlyList<ImportDuplicateGroupDto> DuplicateGroups,
    IReadOnlyList<string> Warnings,
    bool AmbiguousTypeInference,
    string TypeInferenceSource);

public record ImportConfirmRequest(
    string FileName,
    string Content,
    ImportOptionsDto Options,
    ColumnMappingDto Mapping,
    bool ImportDuplicates = false);

public record ImportResultDto(
    int BatchId,
    string FileName,
    DateTime ImportedAt,
    int TotalRows,
    int ImportedRows,
    int RejectedRows,
    int DuplicateRows,
    IReadOnlyList<ImportRowErrorDto> RejectedRowsDetail,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> CreatedCategories,
    IReadOnlyList<string> CreatedAccounts);

public record ImportBatchDto(
    int Id,
    string FileName,
    DateTime ImportedAt,
    int TotalRows,
    int ImportedRows,
    int RejectedRows,
    int DuplicateRows,
    IReadOnlyList<TransactionDto> SampleTransactions);
