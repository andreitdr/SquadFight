using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class AudioMathTests
    {
        [TestCase(1f, 0f)]
        [TestCase(0.5f, -6.0206f)]
        [TestCase(0.1f, -20f)]
        [TestCase(0f, -80f)]
        [TestCase(-1f, -80f)]
        public void LinearToDb(float gain, float db) => Assert.AreEqual(db, AudioMath.LinearToDb(gain), 1e-3f);

        [Test]
        public void DbRoundTrip()
        {
            foreach (var g in new[] { 0.05f, 0.25f, 0.8f, 1f }) Assert.AreEqual(g, AudioMath.DbToLinear(AudioMath.LinearToDb(g)), 1e-4f);
            Assert.AreEqual(0f, AudioMath.DbToLinear(-80f));
        }

        [Test]
        public void SliderCurve_ClampedAndMonotonic()
        {
            Assert.AreEqual(0f, AudioMath.SliderToGain(-2));
            Assert.AreEqual(1f, AudioMath.SliderToGain(3));
            Assert.AreEqual(0.25f, AudioMath.SliderToGain(0.5f), 1e-6f);
            Assert.AreEqual(0f, AudioMath.SliderToGain(float.NaN));
            var prev = -1f;
            for (var s = 0f; s <= 1f; s += 0.05f)
            {
                var g = AudioMath.SliderToGain(s);
                Assert.GreaterOrEqual(g, prev);
                prev = g;
            }
        }

        [Test]
        public void SettingsPersistAcrossRestart()
        {
            var s = TestSession.Create();
            s.Settings.MusicVolume = 0.3f;
            s.Settings.Vibration = false;
            s.Settings.Language = "ro";
            var ser = new Game.Save.SaveSerializer();
            var data = ser.Decode(ser.Encode(s.ExportForSave(), 1, System.DateTime.UtcNow)).Data;
            Assert.AreEqual(0.3f, data.Settings.MusicVolume, 1e-6f);
            Assert.IsFalse(data.Settings.Vibration);
            Assert.AreEqual("ro", data.Settings.Language);
        }
    }
}
