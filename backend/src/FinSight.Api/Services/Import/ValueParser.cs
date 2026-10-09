using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FinSight.Domain;

namespace FinSight.Services.Import;

public static partial class ValueParser
{
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy",
        "dd.MM.yyyy", "d.M.yyyy", "MM/dd/yyyy", "M/d/yyyy",
        "d MMM yyyy", "d MMMM yyyy", "MMM d, yyyy", "MMMM d, yyyy",
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy HH:mm:ss",
        "d MMM yyyy HH:mm", "MMMM d, yyyy h:mm tt"
    ];

    private static readonly (string Format, bool DayFirstAmbiguous)[] DetectionFormats =
    [
        ("yyyy-MM-dd HH:mm:ss", false),
        ("yyyy-MM-dd", false),
        ("yyyy/MM/dd", false),
        ("dd/MM/yyyy", true),
        ("d/M/yyyy", true),
        ("dd.MM.yyyy", true),
        ("d.M.yyyy", true),
        ("dd-MM-yyyy", true),
        ("yyyy-MM-ddTHH:mm:ss", false),
        ("MM/dd/yyyy", true),
        ("dd/MM/yyyy HH:mm:ss", true),
        ("dd/MM/yyyy HH:mm", true)
    ];

    public static DateOnly? ParseDate(string? raw, string? format)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var value = CleanCell(raw)!;

        if (!string.IsNullOrWhiteSpace(format))
        {
            if (DateOnly.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d;
            if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return DateOnly.FromDateTime(dt);
            return null;
        }

        return ParseDateAuto(value!);
    }

    public static DateOnly? ParseDateAuto(string value)
    {
        foreach (var format in DateFormats)
        {
            if (DateOnly.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d;
            if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return DateOnly.FromDateTime(dt);
        }
        return null;
    }

    public static DateDetectionResult DetectDateFormat(IEnumerable<string?> samples)
    {
        var values = samples.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => CleanCell(s)!).ToList();
        var results = new List<(string Format, int Matches, bool IsAmbiguousFormat)>();
        foreach (var (format, isAmbiguous) in DetectionFormats)
        {
            var matches = values.Count(v =>
                DateOnly.TryParseExact(v, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
                DateTime.TryParseExact(v, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out _));
            if (matches > 0) results.Add((format, matches, isAmbiguous));
        }
        if (results.Count == 0) return new DateDetectionResult(null, false);

        var best = results.OrderByDescending(r => r.Matches).First();
        // Ambiguity: both a day-first and a month-first format explain the same samples.
        var dayFirst = results.Where(r => r.IsAmbiguousFormat && r.Format.Contains("dd/")).OrderByDescending(r => r.Matches).FirstOrDefault();
        var monthFirst = results.Where(r => r.IsAmbiguousFormat && r.Format.Contains("MM/")).OrderByDescending(r => r.Matches).FirstOrDefault();
        var ambiguous = dayFirst != default && monthFirst != default &&
                        dayFirst.Matches == monthFirst.Matches && best.Matches == dayFirst.Matches;

        return new DateDetectionResult(best.Format, ambiguous);
    }

    public static char DetectDecimalSeparator(IEnumerable<string?> samples)
    {
        var values = samples.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => CleanCell(s)!).ToList();
        var comma = values.Count(v => v.Contains(','));
        var dot = values.Count(v => v.Contains('.'));
        if (comma > dot) return ',';
        if (dot > comma) return '.';
        return ',';
    }

    public static AmountParseResult ParseAmount(string? raw, char decimalSeparator)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return AmountParseResult.Fail("The amount is empty.");

        var value = CleanCell(raw)!;
        var negative = false;

        // Accounting style: trailing minus or parentheses mean negative.
        if (value.EndsWith('-'))
        {
            negative = true;
            value = value[..^1].Trim();
        }
        else if (value.StartsWith('(') && value.EndsWith(')'))
        {
            negative = true;
            value = value[1..^1].Trim();
        }

        // Remove currency symbols and codes.
        value = CurrencySymbolRegex().Replace(value, string.Empty);
        value = CurrencyCodeRegex().Replace(value, string.Empty);
        value = value.Replace("\u00A0", string.Empty).Replace(" ", string.Empty);

        if (value.Length == 0)
            return AmountParseResult.Fail($"\"{raw.Trim()}\" is not a valid amount.");

        var hasComma = value.Contains(',');
        var hasDot = value.Contains('.');

        if (hasComma && hasDot)
        {
            // The separator appearing last is the decimal separator.
            var lastComma = value.LastIndexOf(',');
            var lastDot = value.LastIndexOf('.');
            var decimalChar = lastComma > lastDot ? ',' : '.';
            var thousandsChar = decimalChar == ',' ? '.' : ',';
            value = value.Replace(thousandsChar.ToString(), string.Empty);
            value = value.Replace(decimalChar, '.');
        }
        else if (hasComma)
        {
            if (decimalSeparator == ',') value = value.Replace(',', '.');
            else value = value.Replace(",", string.Empty);
        }
        else if (hasDot)
        {
            if (decimalSeparator == '.') { /* keep */ }
            else value = value.Replace(".", string.Empty);
        }

        if (value.StartsWith('-'))
        {
            negative = true;
            value = value[1..];
        }
        else if (value.StartsWith('+'))
        {
            value = value[1..];
        }

        if (!DecimalRegex().IsMatch(value))
            return AmountParseResult.Fail($"\"{raw.Trim()}\" is not a valid amount.");

        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount))
            return AmountParseResult.Fail($"\"{raw.Trim()}\" is not a valid amount.");

        if (amount == 0m)
            return AmountParseResult.Fail("The amount cannot be zero.");

        return new AmountParseResult(negative ? -amount : amount, true, null);
    }

    public static TransactionType? ParseTypeWord(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var key = TextNormalizer.NormalizeKey(raw) switch
        {
            "income" or "receita" or "receitas" or "credit" or "credito" or "creditos" or "entrada"
                or "entradas" or "salary" or "salario" or "vencimento" or "deposito" or "deposito inicial"
                or "reembolso" or "venda" or "vendas" or "rendimento" or "rendimentos" or "entrada de dinheiro" => TransactionType.Income,
            "expense" or "despesa" or "despesas" or "debit" or "debito" or "debitos" or "saida" or "saidas"
                or "pagamento" or "pagamentos" or "compra" or "compras" or "withdrawal" or "gasto" or "gastos"
                or "custo" or "custos" or "saida de dinheiro" => TransactionType.Expense,
            _ => (TransactionType?)null
        };
        return key;
    }

    private static string? CleanCell(string? raw) =>
        raw is null ? null : raw.Trim().Trim('"').Replace("\u00A0", " ").Trim();

    [GeneratedRegex("[€$£¥]")]
    private static partial Regex CurrencySymbolRegex();

    [GeneratedRegex(@"\b(EUR|USD|GBP|BRL|AUD|CAD|CHF)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CurrencyCodeRegex();

    [GeneratedRegex(@"^\d+(\.\d+)?$")]
    private static partial Regex DecimalRegex();
}

public sealed record AmountParseResult(decimal SignedAmount, bool Success, string? Error)
{
    public static AmountParseResult Fail(string error) => new(0m, false, error);
}

public sealed record DateDetectionResult(string? Format, bool Ambiguous);


