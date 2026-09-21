namespace SkeletonCrew
{
    public sealed class WeaponOption
    {
        public readonly string Id;
        public readonly string Label;
        public readonly string PrefabName;
        public readonly bool TwoHanded;
        public readonly bool OffHandOnly;
        public readonly string AppearancePrefabStem;

        public WeaponOption(string id, string label, string prefabName,
            bool twoHanded = false, bool offHandOnly = false, string appearancePrefabStem = null)
        {
            Id = id;
            Label = label;
            PrefabName = prefabName;
            TwoHanded = twoHanded;
            OffHandOnly = offHandOnly;
            AppearancePrefabStem = appearancePrefabStem ?? prefabName;
        }

        public string GetAppearancePrefab(int bonusLevel)
        {
            if (PrefabName == null || bonusLevel < 4)
                return PrefabName;
            // Generic visuals stop at Exceptional; enchantments continue
            // independently through Superb and Legendary.
            return AppearancePrefabStem + (bonusLevel >= 8 ? "_exceptional" : "_fine");
        }
    }

    public static class WeaponCatalog
    {
        // Add options here. Keep IDs stable: settings store IDs, not list positions.
        // Shields should use offHandOnly; two-handed weapons are main-hand only.
        public static readonly WeaponOption[] Options =
        {
            new WeaponOption("", "Empty", null),

            // Standard one-handed melee weapons (no unique or creature variants).
            new WeaponOption("battle_axe", "Battle axe", "battle_axe"),
            new WeaponOption("club", "Club", "club"),
            new WeaponOption("dagger", "Dagger", "dagger"),
            new WeaponOption("flail", "Flail", "flail"),
            new WeaponOption("hatchet", "Hatchet", "hatchet"),
            new WeaponOption("mace", "Mace", "mace"),
            new WeaponOption("rapier", "Rapier", "rapier"),
            new WeaponOption("sabre", "Sabre", "sabre"),
            new WeaponOption("spear", "Spear", "spear"),
            new WeaponOption("stiletto", "Stiletto", "stiletto"),
            new WeaponOption("sword", "Sword", "sword"),
            new WeaponOption("war_hammer", "War hammer", "war_hammer"),

            // Standard two-handed melee weapons.
            new WeaponOption("estoc", "Estoc (two-handed)", "estoc", twoHanded: true),
            new WeaponOption("great_sword", "Great sword (two-handed)", "great_sword", twoHanded: true),
            new WeaponOption("morning_star01", "Morning star (two-handed)", "morning_star01", twoHanded: true,
                appearancePrefabStem: "morning_star"),
            new WeaponOption("pike", "Pike (two-handed)", "pike", twoHanded: true),
            new WeaponOption("pollaxe01", "Pollaxe (two-handed)", "pollaxe01", twoHanded: true,
                appearancePrefabStem: "pollaxe"),
            new WeaponOption("quarterstaff", "Quarterstaff (two-handed)", "quarterstaff", twoHanded: true),

            // In Pillars of Eternity 1, ranged weapons occupy both hands,
            // including pistols and implements. Deadfire's rules do not apply.
            new WeaponOption("arbalest", "Arbalest (ranged)", "arbalest", twoHanded: true),
            new WeaponOption("arquebus", "Arquebus (ranged)", "arquebus", twoHanded: true),
            new WeaponOption("blunderbuss", "Blunderbuss (ranged)", "blunderbuss", twoHanded: true),
            new WeaponOption("crossbow", "Crossbow (ranged)", "crossbow", twoHanded: true),
            new WeaponOption("hunting_bow", "Hunting bow (ranged)", "hunting_bow", twoHanded: true),
            new WeaponOption("pistol", "Pistol (ranged)", "pistol", twoHanded: true),
            new WeaponOption("rod", "Rod (ranged)", "rod", twoHanded: true),
            new WeaponOption("sceptre", "Scepter (ranged)", "sceptre", twoHanded: true),
            new WeaponOption("wand", "Wand (ranged)", "wand", twoHanded: true),
            new WeaponOption("war_bow", "War bow (ranged)", "war_bow", twoHanded: true),

            // Keep the original ID so saved shield selections remain small shields.
            new WeaponOption("shield", "Small shield", "shield_small", offHandOnly: true),
            new WeaponOption("shield_medium", "Medium shield", "shield_medium_heater", offHandOnly: true),
            new WeaponOption("shield_large", "Large shield", "shield_large", offHandOnly: true)
        };

        public static WeaponOption Find(string id)
        {
            // Preserve shield selections saved by earlier versions.
            if (id == "default_shield")
                id = "shield";
            foreach (WeaponOption option in Options)
                if (option.Id == id)
                    return option;
            return Options[0];
        }

        public static bool CanEquip(WeaponOption option, bool offHand)
        {
            return offHand ? !option.TwoHanded : !option.OffHandOnly;
        }
    }

    public class SkeletonHands
    {
        public string MainHand = "";
        public string OffHand = "";

        public void Normalize()
        {
            WeaponOption main = WeaponCatalog.Find(MainHand);
            MainHand = WeaponCatalog.CanEquip(main, false) ? main.Id : "";
            WeaponOption off = WeaponCatalog.Find(OffHand);
            OffHand = WeaponCatalog.Find(MainHand).TwoHanded || !WeaponCatalog.CanEquip(off, true)
                ? "" : off.Id;
        }
    }
}
