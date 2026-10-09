using System.Globalization;
using System.Text;

namespace FinSight.Services;

/// <summary>Normalisation helpers used for matching and duplicate detection.</summary>
public static class TextNormalizer
{
    public static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark)
                continue;
            if (ch is ' ' or '\t' or '\r' or '\n' or '-' or '_' or '/' or '\\' or '.' or ',' or ':' or ';')
            {
                if (sb.Length == 0 || sb[^1] == ' ') continue;
                sb.Append(' ');
                continue;
            }
            sb.Append(ch);
        }
        return sb.ToString().Trim();
    }
}
