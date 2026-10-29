using Game.EditorTools;
using NUnit.Framework;

namespace Game.Tests.EditMode.UnityTests
{
    public class LocalizationCoverageTests
    {
        [Test]
        public void EveryUsedKey_HasEnglishText()
        {
            var r = LocalizationTools.Analyze();
            Assert.Greater(r.UsedKeys.Count, 200);
            CollectionAssert.IsEmpty(r.MissingEnglish, r.ToString());
        }

        [Test]
        public void Romanian_HasAllUiKeys()
        {
            var r = LocalizationTools.Analyze();
            var loc = LocalizationTools.LoadTables();
            var ui = Game.Core.TableLocalizer.ParseCsv(System.IO.File.ReadAllText(LocalizationTools.UiTable));
            foreach (var row in ui)
            {
                if (row.Length == 0 || row[0] == "key" || row[0].StartsWith("#")) continue;
                Assert.IsTrue(loc.HasIn("ro", row[0]), "ro missing " + row[0]);
            }
        }
    }
}
