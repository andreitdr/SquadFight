using System;

namespace Game.Core
{
    /// <summary>All player-facing text goes through this. Keys are stable ids (e.g. "hero_knight_name").</summary>
    public interface ILocalizer
    {
        /// <summary>Current language code (e.g. "en").</summary>
        string Language { get; }

        /// <summary>Text for <paramref name="key"/>; falls back to English, then to the key itself (never null).</summary>
        string Get(string key);

        bool Has(string key);

        /// <summary><see cref="string.Format(IFormatProvider,string,object[])"/> on the localized pattern.</summary>
        string Format(string key, params object[] args);

        /// <summary>Plural-aware text: looks up "key.one"/"key.few"/"key.other" for the language and formats {0} with the count.</summary>
        string Plural(string key, long count);

        event Action LanguageChanged;
    }
}
