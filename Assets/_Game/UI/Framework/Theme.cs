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

        [Header("Skin (generated art; empty = fall back to the shapes above)")]
        [Tooltip("Grayscale 9-slices: the theme colors still tint them.")]
        public Sprite panelSprite;
        public Sprite buttonSprite, barBackground, barFill, bubble, bubbleTail, glow, vignette, ribbon;
        [Tooltip("Full-color sprites: drawn untinted.")]
        public Sprite starFull;
        public Sprite starEmpty, redDotSprite, logo, appBackground, battleBackground;
        public Sprite iconGold, iconShard, iconPower, iconLock, iconPause, iconSpeed, iconAuto, iconSkip, iconParty, iconBoss, iconStory;
        public Sprite tabHome, tabCampaign, tabHeroes, tabInventory, tabSettings;
        [Tooltip("Index = Rarity (Common..Legendary).")]
        public Sprite[] rarityGems = new Sprite[0];
        public Sprite[] cardFrames = new Sprite[0];
        public Sprite[] cardBacks = new Sprite[0];
        [Tooltip("Index = HeroClass.")]
        public Sprite[] classIcons = new Sprite[0];
        public Sprite statusStun, statusShield, statusHeal;
        public Sprite statusAttackUp, statusAttackDown, statusDefenseUp, statusDefenseDown, statusSpeedUp, statusSpeedDown, statusCritUp, statusCritDown;

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

        private static Sprite At(Sprite[] list, int i) => list != null && i >= 0 && i < list.Length ? list[i] : null;

        /// <summary>Full-color rarity gem (keeps the colorblind shapes), or null when not skinned.</summary>
        public Sprite RarityGem(Rarity r) => At(rarityGems, (int)r);
        public Sprite CardFrame(Rarity r) => At(cardFrames, (int)r);
        public Sprite CardBack(Rarity r) => At(cardBacks, (int)r);
        public Sprite ClassIcon(HeroClass c) => At(classIcons, (int)c);
        public Sprite Panel => panelSprite != null ? panelSprite : rounded;
        public Sprite ButtonBody => buttonSprite != null ? buttonSprite : rounded;
        public Sprite TabIcon(string tab) =>
            tab == "home" ? tabHome : tab == "campaign" ? tabCampaign : tab == "heroes" ? tabHeroes : tab == "inventory" ? tabInventory : tab == "settings" ? tabSettings : null;

        /// <summary>Status badge for a battle status (stun, or a stat buff/debuff); null when not skinned.</summary>
        public Sprite StatusIcon(string statusId, StatType stat, bool buff)
        {
            if (statusId == "stun") return statusStun;
            switch (stat)
            {
                case StatType.Attack: return buff ? statusAttackUp : statusAttackDown;
                case StatType.Defense: return buff ? statusDefenseUp : statusDefenseDown;
                case StatType.Speed: return buff ? statusSpeedUp : statusSpeedDown;
                case StatType.CritChance:
                case StatType.CritDamage: return buff ? statusCritUp : statusCritDown;
                default: return buff ? statusHeal : statusShield;
            }
        }

        /// <summary>Shape per rarity so rarity is readable without color (colorblind support).</summary>
        public Sprite RarityShape(Rarity r)
        {
            var gem = RarityGem(r);
            if (gem != null) return gem;
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
