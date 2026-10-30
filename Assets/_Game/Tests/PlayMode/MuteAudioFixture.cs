using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.PlayMode
{
    /// <summary>Silences all audio for the whole PlayMode run, whatever volume the save on disk holds.</summary>
    [SetUpFixture]
    public class MuteAudioFixture
    {
        private float _volume;

        [OneTimeSetUp]
        public void Mute()
        {
            _volume = AudioListener.volume;
            AudioListener.volume = 0f;
        }

        [OneTimeTearDown]
        public void Restore() => AudioListener.volume = _volume;
    }
}
