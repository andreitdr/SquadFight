using System.Linq;
using Game.Data;
using Game.EditorTools;
using Game.EditorTools.Content;
using NUnit.Framework;

namespace Game.Tests.EditMode.UnityTests
{
    /// <summary>Checks the real project content (build gate) and the importer.</summary>
    public class ContentAssetTests
    {
        [Test]
        public void RealGameConfig_ValidatesWithoutErrors()
        {
            var report = ConfigValidatorMenu.Validate(ConfigValidatorMenu.LoadGameConfig());
            Assert.IsFalse(report.HasErrors, report.ToString());
        }

        [Test]
        public void GameConfig_LoadsFromResources()
        {
            var cdb = ConfigDatabase.LoadFromResources();
            Assert.IsNotNull(cdb);
            Assert.GreaterOrEqual(cdb.Database.Heroes.Count, 8);
            Assert.GreaterOrEqual(cdb.Database.Equipment.Count, 12);
            Assert.GreaterOrEqual(cdb.Database.Enemies.Count, 3);
            Assert.GreaterOrEqual(cdb.Database.Fights.Count, 5);
            Assert.IsTrue(cdb.Database.Fights.All(f => !string.IsNullOrEmpty(f.ChapterId)));
            Assert.IsNotNull(cdb.GetHeroAsset(cdb.Database.Heroes[0].Id).portrait);
        }

        [Test]
        public void BuildHook_BlocksInvalidConfig()
        {
            var broken = UnityEngine.ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
                Assert.Throws<UnityEditor.Build.BuildFailedException>(() => ConfigValidationBuildHook.Check(broken));
                Assert.DoesNotThrow(() => ConfigValidationBuildHook.Check(ConfigValidatorMenu.LoadGameConfig()));
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
                UnityEngine.Object.DestroyImmediate(broken);
            }
        }

        [Test]
        public void Importer_PreviewOfCommittedSources_IsClean()
        {
            var report = ContentImporter.Import(dryRun: true);
            CollectionAssert.IsEmpty(report.Errors, report.ToString());
            CollectionAssert.IsEmpty(report.Created, "sources and assets are out of sync: re-run the importer\n" + report);
            CollectionAssert.IsEmpty(report.Updated, "sources and assets are out of sync: re-run the importer\n" + report);
        }
    }
}
