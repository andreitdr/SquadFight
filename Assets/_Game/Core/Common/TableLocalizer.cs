using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// Localizer over in-memory tables (language → key → text), loaded from CSV columns "key,en,ro,…".
    /// Missing keys fall back to English, then to the key. Plural categories follow CLDR for en/ro.
    /// </summary>
    public sealed class TableLocalizer : ILocalizer
    {
        public const string Fallback = "en";
        private readonly Dictionary<string, Dictionary<string, string>> _tables = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        private string _language = Fallback;

        public event Action LanguageChanged;

        public string Language => _language;
        public IEnumerable<string> Languages => _tables.Keys.OrderBy(k => k == Fallback ? "" : k);

        /// <summary>Keys that were requested but missing in the current language (for the missing-keys report).</summary>
        public readonly HashSet<string> MissingRequested = new HashSet<string>();

        public void SetLanguage(string code)
        {
            if (string.IsNullOrEmpty(code) || !_tables.ContainsKey(code)) code = Fallback;
            if (string.Equals(code, _language, StringComparison.OrdinalIgnoreCase)) return;
            _language = code;
            LanguageChanged?.Invoke();
        }

        public void Add(string language, string key, string text)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(text)) return;
            if (!_tables.TryGetValue(language, out var t)) _tables[language] = t = new Dictionary<string, string>();
            t[key] = text;
        }

        /// <summary>Header row: key,&lt;lang&gt;,&lt;lang&gt;… Rows: key followed by texts. Uses a minimal RFC 4180 parser.</summary>
        public void LoadCsv(string csv)
        {
            var rows = ParseCsv(csv);
            if (rows.Count == 0) return;
            var header = rows[0];
            for (var r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Length == 0 || string.IsNullOrWhiteSpace(row[0]) || row[0].StartsWith("#")) continue;
                for (var c = 1; c < header.Length && c < row.Length; c++) Add(header[c].Trim(), row[0].Trim(), row[c]);
            }
        }

        public bool Has(string key) => key != null && ((_tables.TryGetValue(_language, out var t) && t.ContainsKey(key)) || (_tables.TryGetValue(Fallback, out var en) && en.ContainsKey(key)));

        public bool HasIn(string language, string key) => key != null && _tables.TryGetValue(language, out var t) && t.ContainsKey(key);

        public IEnumerable<string> Keys(string language) => _tables.TryGetValue(language, out var t) ? t.Keys : Enumerable.Empty<string>();

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (_tables.TryGetValue(_language, out var t) && t.TryGetValue(key, out var v)) return v;
            MissingRequested.Add(key);
            if (_tables.TryGetValue(Fallback, out var en) && en.TryGetValue(key, out var e)) return e;
            return key;
        }

        public string Format(string key, params object[] args)
        {
            var pattern = Get(key);
            try { return string.Format(Culture, pattern, args); }
            catch (FormatException) { return pattern; }
        }

        public string Plural(string key, long count)
        {
            var category = PluralCategory(_language, count);
            var k = key + "." + category;
            if (!Has(k)) k = key + ".other";
            if (!Has(k)) k = key;
            return Format(k, count);
        }

        public CultureInfo Culture
        {
            get
            {
                try { return CultureInfo.GetCultureInfo(_language); }
                catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
            }
        }

        /// <summary>CLDR cardinal categories for the supported languages (others default to one/other).</summary>
        public static string PluralCategory(string language, long n)
        {
            n = Math.Abs(n);
            switch ((language ?? "").ToLowerInvariant())
            {
                case "ro":
                    if (n == 1) return "one";
                    if (n == 0 || (n % 100 >= 2 && n % 100 <= 19)) return "few";
                    return "other";
                default:
                    return n == 1 ? "one" : "other";
            }
        }

        /// <summary>Maps a system language name/code to a supported language, else English.</summary>
        public string Detect(string systemLanguage)
        {
            if (string.IsNullOrEmpty(systemLanguage)) return Fallback;
            var s = systemLanguage.ToLowerInvariant();
            if (s.StartsWith("romanian") || s == "ro" || s.StartsWith("ro-")) return _tables.ContainsKey("ro") ? "ro" : Fallback;
            foreach (var code in _tables.Keys) if (s == code.ToLowerInvariant() || s.StartsWith(code.ToLowerInvariant() + "-")) return code;
            return Fallback;
        }

        public static List<string[]> ParseCsv(string text)
        {
            var rows = new List<string[]>();
            var row = new List<string>();
            var field = new System.Text.StringBuilder();
            var quoted = false;
            for (var i = 0; i < (text ?? "").Length; i++)
            {
                var ch = text[i];
                if (quoted)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else quoted = false;
                    }
                    else field.Append(ch);
                    continue;
                }
                switch (ch)
                {
                    case '"': quoted = true; break;
                    case ',': row.Add(field.ToString()); field.Clear(); break;
                    case '\r': break;
                    case '\n': row.Add(field.ToString()); field.Clear(); rows.Add(row.ToArray()); row.Clear(); break;
                    default: field.Append(ch); break;
                }
            }
            if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row.ToArray()); }
            return rows;
        }
    }
}
