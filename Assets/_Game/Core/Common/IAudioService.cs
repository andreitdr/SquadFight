using System;

namespace Game.Core
{
    public interface IAudioService
    {
        void PlayMusic(string trackId, float fadeSeconds = 0.6f);
        void StopMusic(float fadeSeconds = 0.6f);
        void PlaySfx(string sfxId);
        /// <summary>Slider values 0..1 (mapped perceptually).</summary>
        void SetMusicVolume(float value);
        void SetSfxVolume(float value);
        bool Muted { get; set; }
    }

    public sealed class NullAudio : IAudioService
    {
        public static readonly NullAudio Instance = new NullAudio();
        public void PlayMusic(string trackId, float fadeSeconds = 0.6f) { }
        public void StopMusic(float fadeSeconds = 0.6f) { }
        public void PlaySfx(string sfxId) { }
        public void SetMusicVolume(float value) { }
        public void SetSfxVolume(float value) { }
        public bool Muted { get; set; }
    }

    /// <summary>Volume math shared by the audio service (and an AudioMixer if one is added later).</summary>
    public static class AudioMath
    {
        public const float MinDb = -80f;

        /// <summary>Linear gain (0..1) → decibels; 0 maps to -80 dB (silence).</summary>
        public static float LinearToDb(float gain) => gain <= 0.0001f ? MinDb : Math.Max(MinDb, 20f * (float)Math.Log10(gain));

        public static float DbToLinear(float db) => db <= MinDb ? 0f : (float)Math.Pow(10, db / 20f);

        /// <summary>Slider position → gain. Squared curve so the slider feels linear to the ear.</summary>
        public static float SliderToGain(float slider)
        {
            if (float.IsNaN(slider)) return 0f;
            var s = Math.Max(0f, Math.Min(1f, slider));
            return s * s;
        }
    }
}
