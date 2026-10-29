using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Builds the runtime localizer from Resources/Localization/*.csv (UI strings + generated content strings).</summary>
    public static class LocalizationLoader
    {
        public static readonly string[] Tables = { "Localization/strings_ui", "Localization/strings_content", "Localization/strings_dialogue" };

        public static TableLocalizer Load(string savedLanguage)
        {
            var loc = new TableLocalizer();
            foreach (var t in Tables)
            {
                var asset = Resources.Load<TextAsset>(t);
                if (asset != null) loc.LoadCsv(asset.text);
                else Debug.LogWarning("[Localization] missing table " + t);
            }
            var lang = string.IsNullOrEmpty(savedLanguage) ? loc.Detect(Application.systemLanguage.ToString()) : savedLanguage;
            loc.SetLanguage(lang);
            return loc;
        }
    }
}
