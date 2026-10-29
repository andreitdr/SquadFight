using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Data;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// Music (two sources, cross-fade) + pooled SFX sources. Volumes come from settings (slider → squared gain).
    /// Pauses with the app; Unity/OS handle calls and headphone changes by pausing the audio session.
    /// </summary>
    public sealed class AudioService : MonoBehaviour, IAudioService
    {
        private const int SfxVoices = 10;
        private AudioLibrary _library;
        private readonly AudioSource[] _music = new AudioSource[2];
        private readonly List<AudioSource> _voices = new List<AudioSource>();
        private readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        private int _activeMusic;
        private string _currentTrack;
        private float _musicGain = 0.64f, _sfxGain = 1f;
        private bool _muted;
        private Coroutine _fade;

        public static AudioService Create(GameObject host, AudioLibrary library)
        {
            var a = host.AddComponent<AudioService>();
            a._library = library;
            for (var i = 0; i < 2; i++)
            {
                var s = host.AddComponent<AudioSource>();
                s.loop = true;
                s.playOnAwake = false;
                s.volume = 0;
                a._music[i] = s;
            }
            for (var i = 0; i < SfxVoices; i++)
            {
                var s = host.AddComponent<AudioSource>();
                s.playOnAwake = false;
                a._voices.Add(s);
            }
            return a;
        }

        public bool Muted
        {
            get => _muted;
            set { _muted = value; ApplyMusicVolume(); }
        }

        public void SetMusicVolume(float value)
        {
            _musicGain = AudioMath.SliderToGain(value);
            ApplyMusicVolume();
        }

        public void SetSfxVolume(float value) => _sfxGain = AudioMath.SliderToGain(value);

        private float MusicTarget(AudioLibrary.Entry e) => _muted ? 0 : _musicGain * (e?.volume ?? 1f);

        private void ApplyMusicVolume()
        {
            if (_fade != null) return;
            var s = _music[_activeMusic];
            if (s != null) s.volume = MusicTarget(_library != null ? _library.Music(_currentTrack) : null);
        }

        public void PlayMusic(string trackId, float fadeSeconds = 0.6f)
        {
            if (trackId == _currentTrack || _library == null) return;
            var e = _library.Music(trackId);
            if (e == null || e.clips.Count == 0 || e.clips[0] == null) return;
            _currentTrack = trackId;
            var from = _music[_activeMusic];
            _activeMusic = 1 - _activeMusic;
            var to = _music[_activeMusic];
            to.clip = e.clips[0];
            to.volume = 0;
            to.Play();
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(CrossFade(from, to, MusicTarget(e), fadeSeconds));
        }

        public void StopMusic(float fadeSeconds = 0.6f)
        {
            _currentTrack = null;
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(CrossFade(_music[_activeMusic], null, 0, fadeSeconds));
        }

        private IEnumerator CrossFade(AudioSource from, AudioSource to, float target, float seconds)
        {
            var fromStart = from != null ? from.volume : 0;
            for (var t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                var k = t / Mathf.Max(0.01f, seconds);
                if (from != null) from.volume = Mathf.Lerp(fromStart, 0, k);
                if (to != null) to.volume = Mathf.Lerp(0, target, k);
                yield return null;
            }
            if (from != null) { from.volume = 0; from.Stop(); }
            if (to != null) to.volume = target;
            _fade = null;
        }

        public void PlaySfx(string sfxId)
        {
            if (_muted || _sfxGain <= 0 || _library == null || string.IsNullOrEmpty(sfxId)) return;
            var e = _library.Sfx(sfxId);
            if (e == null || e.clips.Count == 0) return;
            var now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(sfxId, out var last) && now - last < e.cooldown) return;
            _lastPlayed[sfxId] = now;
            var voice = FreeVoice();
            voice.clip = e.clips[e.clips.Count == 1 ? 0 : Random.Range(0, e.clips.Count)];
            voice.volume = _sfxGain * e.volume;
            voice.pitch = 1f + (e.pitchVariance > 0 ? Random.Range(-e.pitchVariance, e.pitchVariance) : 0f);
            voice.Play();
        }

        private AudioSource FreeVoice()
        {
            AudioSource oldest = _voices[0];
            foreach (var v in _voices)
            {
                if (!v.isPlaying) return v;
                if (v.time > oldest.time) oldest = v;
            }
            return oldest; // steal the voice that has played the longest
        }

        private void OnApplicationPause(bool paused) => AudioListener.pause = paused;
        private void OnApplicationFocus(bool focused) { if (focused) AudioListener.pause = false; }
    }

    /// <summary>Short vibrations honouring the Vibration setting. Android uses the Vibrator service; iOS uses the standard buzz for strong cues only.</summary>
    public sealed class MobileHaptics : IHaptics
    {
        private readonly System.Func<bool> _enabled;
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _vibrator;
#endif

        public MobileHaptics(System.Func<bool> enabled) => _enabled = enabled;

        public void Play(HapticKind kind)
        {
            if (!_enabled()) return;
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (_vibrator == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                long ms = kind == HapticKind.Light ? 15 : kind == HapticKind.Medium ? 30 : kind == HapticKind.Success ? 60 : 90;
                _vibrator?.Call("vibrate", ms);
#elif UNITY_IOS && !UNITY_EDITOR
                if (kind == HapticKind.Success || kind == HapticKind.Warning) Handheld.Vibrate();
#endif
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Haptics] " + e.Message);
            }
        }
    }
}
