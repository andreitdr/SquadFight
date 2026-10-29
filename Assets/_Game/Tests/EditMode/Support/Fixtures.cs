using System.IO;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Reads files from Tests/Fixtures in both Unity (project-relative) and the dotnet harness (copied to output).</summary>
    public static class Fixtures
    {
        public static string Read(string name)
        {
            var harness = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", name);
            if (File.Exists(harness)) return File.ReadAllText(harness);
            return File.ReadAllText(Path.Combine("Assets", "_Game", "Tests", "Fixtures", name));
        }

        /// <summary>Folder with the real content CSVs (walks up from the test or working directory to the project root).</summary>
        public static string ContentSource()
        {
            foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    var candidate = Path.Combine(dir.FullName, "Assets", "_Game", "Content", "Source");
                    if (Directory.Exists(candidate)) return candidate;
                    dir = dir.Parent;
                }
            }
            throw new DirectoryNotFoundException("Content/Source not found");
        }

        /// <summary>The shipped UI string table (Resources/Localization/strings_ui.csv).</summary>
        public static string ReadUiStrings()
        {
            var harness = Path.Combine(TestContext.CurrentContext.TestDirectory, "Localization", "strings_ui.csv");
            if (File.Exists(harness)) return File.ReadAllText(harness);
            return File.ReadAllText(Path.Combine("Assets", "_Game", "Resources", "Localization", "strings_ui.csv"));
        }
    }
}
