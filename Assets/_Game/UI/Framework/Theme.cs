using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Visual style for code-built UI. Lives in Resources/Theme. Swap sprites/colors here for final art.</summary>
    [CreateAssetMenu(menuName = "Game/Config/UI Theme", fileName = "Theme")]
    public sealed class Theme : ScriptableObject
    {
        public const string ResourcePath = "Theme";

        [Header("Font")]
        public TMP_FontAsset font;
        public float fontSmall = 34;
        public float fontBody = 42;
        public float fontLarge = 52;
        public float fontTitle = 64;
        public float fontHuge = 96;

        [Header("Colors")]
        public Color background = new Color(0.08f, 0.09f, 0.13f);
        public Color panel = new Color(0.14f, 0.16f, 0.22f);
        public Color panelAlt = new Color(0.19f, 0.21f, 0.29f);
        public Color primary = new Color(0.96f, 0.69f, 0.20f);
        public Color primaryText = new Color(0.12f, 0.08f, 0.02f);
        public Color secondary = new Color(0.27f, 0.45f, 0.78f);
        public Color text = new Color(0.95f, 0.95f, 0.97f);
        public Color textDim = new Color(0.65f, 0.68f, 0.76f);
        public Color positive = new Color(0.35f, 0.85f, 0.45f);
        public Color negative = new Color(0.95f, 0.35f, 0.35f);
        public Color warning = new Color(1f, 0.8f, 0.25f);
        public Color overlay = new Color(0, 0, 0, 0.7f);
        public Color redDot = new Color(0.95f, 0.2f, 0.25f);
        public Color locked = new Color(0.25f, 0.25f, 0.3f);

        [Header("Layout (reference pixels at 1080x1920)")]
        public float spacing = 24;
        public float padding = 36;
        public float buttonHeight = 140;
        /// <summary>48dp ≈ 48 × (1080/411) ≈ 126 reference px on a typical phone.</summary>
        public float minTouchTarget = 126;

        [Header("Sprites")]
        public Sprite rounded;
        public Sprite pixel;
        public Sprite circle;
        public Sprite ring;
        public Sprite diamond;
        public Sprite triangle;
        public Sprite star;
        public Sprite shard;
        public Sprite softDot;
        public Sprite iconWeapon, iconHelmet, iconChest, iconGloves, iconBoots, iconRing;

        [Header("Motion")]
        public float screenTransition = 0.22f;
        public float popupTransition = 0.18f;

        public static Theme Load() => Resources.Load<Theme>(ResourcePath);

        public Sprite SlotIcon(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Weapon: return iconWeapon;
                case EquipmentSlot.Helmet: return iconHelmet;
                case EquipmentSlot.Chest: return iconChest;
                case EquipmentSlot.Gloves: return iconGloves;
                case EquipmentSlot.Boots: return iconBoots;
                default: return iconRing;
            }
        }

        /// <summary>Shape per rarity so rarity is readable without color (colorblind support).</summary>
        public Sprite RarityShape(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return circle;
                case Rarity.Uncommon: return diamond;
                case Rarity.Rare: return triangle;
                case Rarity.Epic: return shard;
                default: return star;
            }
        }
    }
}
