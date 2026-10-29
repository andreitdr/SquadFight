using System;
using Game.Save;

namespace Game.Services
{
    /// <summary>Player settings backed by <see cref="SaveData.Settings"/>; every change is saved and raises <see cref="SettingsChanged"/>.</summary>
    public sealed class SettingsService
    {
        private readonly ServiceContext _ctx;

        public SettingsService(ServiceContext ctx) => _ctx = ctx;

        private SettingsSave S => _ctx.State.Data.Settings;

        public float MusicVolume { get => S.MusicVolume; set => Change(() => S.MusicVolume = Clamp01(value)); }
        public float SfxVolume { get => S.SfxVolume; set => Change(() => S.SfxVolume = Clamp01(value)); }
        public string Language { get => S.Language; set => Change(() => S.Language = value ?? ""); }
        public bool Vibration { get => S.Vibration; set => Change(() => S.Vibration = value); }
        public bool ScreenShake { get => S.ScreenShake; set => Change(() => S.ScreenShake = value); }
        public bool ReduceMotion { get => S.ReduceMotion; set => Change(() => S.ReduceMotion = value); }
        public float BattleSpeed { get => S.BattleSpeed; set => Change(() => S.BattleSpeed = ClampSpeed(value)); }
        public bool AutoSkill { get => S.AutoSkill; set => Change(() => S.AutoSkill = value); }

        public static readonly float[] BattleSpeeds = { 1f, 2f, 3f };

        public static float ClampSpeed(float v)
        {
            if (float.IsNaN(v)) return 1f;
            return Math.Max(1f, Math.Min(3f, (float)Math.Round(v)));
        }

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : Math.Max(0f, Math.Min(1f, v));

        private void Change(Action apply)
        {
            apply();
            _ctx.Commit();
            _ctx.Emit(new SettingsChanged());
        }
    }
}
