using Game.App;
using Game.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace Game.Tests.EditMode.UnityTests
{
    public class BuildConfigTests
    {
        [TestCase(2048, 8, QualityTier.Low)]
        [TestCase(3072, 8, QualityTier.Low)]
        [TestCase(6144, 4, QualityTier.Low)]
        [TestCase(4096, 8, QualityTier.Medium)]
        [TestCase(8192, 8, QualityTier.High)]
        public void QualityTier_Detection(int ram, int cores, QualityTier expected) => Assert.AreEqual(expected, QualityTiers.Detect(ram, cores));

        [Test]
        public void PlayerSettings_MobileRequirements()
        {
            ProjectSetup.ApplyPlayerSettings();
            Assert.AreEqual(ScriptingImplementation.IL2CPP, PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android));
            Assert.AreEqual(AndroidArchitecture.ARM64, PlayerSettings.Android.targetArchitectures);
            Assert.AreEqual(UIOrientation.Portrait, PlayerSettings.defaultInterfaceOrientation);
            Assert.IsFalse(PlayerSettings.Android.forceInternetPermission);
            Assert.AreEqual(3, EditorBuildSettings.scenes.Length);
        }

        [Test]
        public void Version_FromFile()
        {
            // Release "1.2.0" or pre-release "1.2.0-beta.1" / "0.0.1-alpha1"; iOS gets the numeric part only.
            StringAssert.IsMatch(@"^\d+\.\d+\.\d+(-(alpha|beta|rc)\.?\d+)?$", BuildScript.VersionName);
            StringAssert.IsMatch(@"^\d+\.\d+\.\d+$", BuildScript.NumericVersionName);
            Assert.Greater(BuildScript.BuildNumber, 0);
        }
    }
}
