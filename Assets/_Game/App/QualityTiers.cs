using UnityEngine;

namespace Game.App
{
    public enum QualityTier { Low, Medium, High }

    /// <summary>Auto-selects a quality tier from device RAM/CPU and applies frame-rate and effect budgets.</summary>
    public static class QualityTiers
    {
        public static QualityTier Current { get; private set; } = QualityTier.High;

        /// <summary>Low: ≤ 3 GB RAM or ≤ 4 cores. Medium: ≤ 4 GB. High otherwise (pure function for tests).</summary>
        public static QualityTier Detect(int memoryMb, int cores)
        {
            if (memoryMb <= 3072 || cores <= 4) return QualityTier.Low;
            if (memoryMb <= 4096) return QualityTier.Medium;
            return QualityTier.High;
        }

        public static void Apply()
        {
            Current = Application.isMobilePlatform ? Detect(SystemInfo.systemMemorySize, SystemInfo.processorCount) : QualityTier.High;
            QualitySettings.vSyncCount = 0;
            MenuFrameRate();
        }

        /// <summary>Menus: 30 fps on low-end (battery), 60 otherwise.</summary>
        public static void MenuFrameRate() => Application.targetFrameRate = Current == QualityTier.Low ? 30 : 60;

        /// <summary>Battles always target 60 fps.</summary>
        public static void BattleFrameRate() => Application.targetFrameRate = 60;

        /// <summary>Low tier skips decorative projectiles/sparks.</summary>
        public static bool RichEffects => Current != QualityTier.Low;
    }
}
