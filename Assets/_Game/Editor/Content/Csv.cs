using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Game.EditorTools.Content
{
    /// <summary>Minimal RFC 4180 CSV reader/writer. Lines starting with '#' are comments; blank lines are skipped.</summary>
    public static class Csv
    {
        public static List<string[]> ParseRaw(string text)
        {
            var rows = new List<string[]>();
            var row = new List<string>();
            var field = new StringBuilder();
            var inQuotes = false;
            var atLineStart = true;
            var commentLine = false;
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (atLineStart)
                {
                    atLineStart = false;
                    commentLine = ch == '#';
                }
                if (commentLine)
                {
                    if (ch == '\n') { atLineStart = true; commentLine = false; }
                    continue;
                }
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else field.Append(ch);
                    continue;
                }
                switch (ch)
                {
                    case '"': inQuotes = true; break;
                    case ',': row.Add(field.ToString()); field.Clear(); break;
                    case '\r': break;
                    case '\n':
                        row.Add(field.ToString()); field.Clear();
                        if (row.Count > 1 || row[0].Trim().Length > 0) rows.Add(row.ToArray());
                        row.Clear();
                        atLineStart = true;
                        break;
                    default: field.Append(ch); break;
                }
            }
            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                if (row.Count > 1 || row[0].Trim().Length > 0) rows.Add(row.ToArray());
            }
            return rows;
        }

        public static CsvTable Read(string path) => CsvTable.FromText(Path.GetFileName(path), File.ReadAllText(path));

        public static string Escape(string v)
        {
            v ??= "";
            return v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        }

        public static string Write(IEnumerable<string> header, IEnumerable<IEnumerable<string>> rows)
        {
            var sb = new StringBuilder();
            sb.Append(string.Join(",", header.Select(Escape))).Append('\n');
            foreach (var r in rows) sb.Append(string.Join(",", r.Select(Escape))).Append('\n');
            return sb.ToString();
        }
    }

    public sealed class CsvRow
    {
        private readonly CsvTable _table;
        private readonly string[] _cells;
        public readonly int Line;

        public CsvRow(CsvTable table, string[] cells, int line)
        {
            _table = table;
            _cells = cells;
            Line = line;
        }

        public string Context => $"{_table.Name} row {Line}";

        public bool Has(string column) => _table.IndexOf(column) >= 0;

        public string Str(string column, string fallback = "")
        {
            var i = _table.IndexOf(column);
            if (i < 0) return fallback;
            return i < _cells.Length ? _cells[i].Trim() : fallback;
        }

        public string Req(string column)
        {
            var v = Str(column);
            if (string.IsNullOrEmpty(v)) throw new FormatException($"{Context}: column '{column}' is required");
            return v;
        }

        public int Int(string column, int fallback = 0)
        {
            var v = Str(column);
            if (string.IsNullOrEmpty(v)) return fallback;
            if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r)) return r;
            throw new FormatException($"{Context}: '{column}' = '{v}' is not an integer");
        }

        public long Long(string column, long fallback = 0)
        {
            var v = Str(column);
            if (string.IsNullOrEmpty(v)) return fallback;
            if (long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r)) return r;
            throw new FormatException($"{Context}: '{column}' = '{v}' is not an integer");
        }

        public float Float(string column, float fallback = 0)
        {
            var v = Str(column);
            if (string.IsNullOrEmpty(v)) return fallback;
            return ParseFloat(v, $"{Context}: '{column}'");
        }

        public bool Bool(string column, bool fallback = false)
        {
            var v = Str(column).ToLowerInvariant();
            if (v == "") return fallback;
            if (v == "true" || v == "1" || v == "yes" || v == "y") return true;
            if (v == "false" || v == "0" || v == "no" || v == "n") return false;
            throw new FormatException($"{Context}: '{column}' = '{v}' is not a boolean");
        }

        public T Enum<T>(string column, T fallback = default) where T : struct
        {
            var v = Str(column);
            if (string.IsNullOrEmpty(v)) return fallback;
            if (System.Enum.TryParse<T>(v, true, out var r) && System.Enum.IsDefined(typeof(T), r)) return r;
            throw new FormatException($"{Context}: '{column}' = '{v}' is not one of {string.Join("/", System.Enum.GetNames(typeof(T)))}");
        }

        public static float ParseFloat(string v, string context)
        {
            if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var r)) return r;
            throw new FormatException($"{context} = '{v}' is not a number");
        }
    }

    public sealed class CsvTable
    {
        public string Name { get; private set; }
        public string[] Header { get; private set; }
        public readonly List<CsvRow> Rows = new List<CsvRow>();
        private readonly Dictionary<string, int> _index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public static CsvTable FromText(string name, string text)
        {
            var raw = Csv.ParseRaw(text);
            var t = new CsvTable { Name = name };
            if (raw.Count == 0) { t.Header = Array.Empty<string>(); return t; }
            t.Header = raw[0].Select(h => h.Trim()).ToArray();
            for (var i = 0; i < t.Header.Length; i++) t._index[t.Header[i]] = i;
            for (var r = 1; r < raw.Count; r++) t.Rows.Add(new CsvRow(t, raw[r], r + 1));
            return t;
        }

        public int IndexOf(string column) => _index.TryGetValue(column, out var i) ? i : -1;
    }
}
