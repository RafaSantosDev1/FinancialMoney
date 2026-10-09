using System.Text;

namespace FinSight.Services.Import;

/// <summary>RFC-4180 style CSV parsing: quoted fields, escaped quotes, CRLF/LF line endings.</summary>
public static class CsvFileParser
{
    public static List<string[]> ParseRows(string content, char delimiter)
    {
        var rows = new List<string[]>();
        var cells = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        void EndRow()
        {
            if (cell.Length == 0 && cells.Count == 0) return; // blank line
            cells.Add(cell.ToString());
            rows.Add([.. cells]);
            cells.Clear();
            cell.Clear();
        }

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cell.Append(c);
                }
                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                cells.Add(cell.ToString());
                cell.Clear();
            }
            else if (c == '\r')
            {
                if (i + 1 < content.Length && content[i + 1] == '\n') i++;
                EndRow();
            }
            else if (c == '\n')
            {
                EndRow();
            }
            else if (c != '\uFEFF')
            {
                cell.Append(c);
            }
        }
        EndRow();

        return rows;
    }

    public static IReadOnlyList<string> RawLines(string content, int maxLines = 20)
    {
        var lines = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        foreach (var c in content)
        {
            if (c == '"') inQuotes = !inQuotes;
            if (!inQuotes && (c == '\n' || c == '\r'))
            {
                if (c == '\r') continue; // handled by \n check
                lines.Add(current.ToString());
                current.Clear();
                if (lines.Count >= maxLines) break;
            }
            else
            {
                current.Append(c);
            }
        }
        if (current.Length > 0 && lines.Count < maxLines) lines.Add(current.ToString());
        return lines;
    }

    public static char DetectDelimiter(string content)
    {
        var lines = RawLines(content);
        char best = ',';
        var bestScore = -1;
        foreach (var candidate in new[] { ',', ';', '\t', '|' })
        {
            var counts = new Dictionary<int, int>();
            foreach (var line in lines)
            {
                var count = CountOutsideQuotes(line, candidate);
                if (count == 0) continue;
                counts.TryGetValue(count, out var n);
                counts[count] = n + 1;
            }
            if (counts.Count == 0) continue;
            var score = counts.Values.Max();
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }
        return best;
    }

    private static int CountOutsideQuotes(string line, char delimiter)
    {
        var count = 0;
        var inQuotes = false;
        foreach (var c in line)
        {
            if (c == '"') inQuotes = !inQuotes;
            else if (!inQuotes && c == delimiter) count++;
        }
        return count;
    }
}
