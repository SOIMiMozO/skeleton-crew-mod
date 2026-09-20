using System;
using System.Reflection;
using UnityModManagerNet;

namespace SkeletonCrew
{
    public static class SummonEquipmentQuality
    {
        private static bool warned;

        public static int GetBonusLevel(CharacterStats summoner)
        {
            var mod = UnityModManager.FindMod("Multiclass");
            if (mod == null || !mod.Active)
                return summoner.Level;

            try
            {
                Type type = mod.Assembly.GetType("Multiclass.MulticlassExtensions", true);
                MethodInfo method = type.GetMethod("GetMulticlassLevel",
                    BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(CharacterStats), typeof(CharacterStats.Class) }, null);
                if (method == null || method.ReturnType != typeof(int))
                    throw new MissingMethodException(type.FullName, "GetMulticlassLevel");
                return (int)method.Invoke(null, new object[] { summoner, CharacterStats.Class.Chanter });
            }
            catch (Exception ex)
            {
                if (!warned)
                {
                    Main.Logger.Error("Could not read Multiclass chanter level; using total level. " + ex.Message);
                    warned = true;
                }
                return summoner.Level;
            }
        }

        public static string GetTier(int bonusLevel)
        {
            return bonusLevel >= 16 ? "legendary" : bonusLevel >= 12 ? "superb"
                : bonusLevel >= 8 ? "exceptional" : bonusLevel >= 4 ? "fine" : null;
        }

        public static string GetModifierPrefab(int bonusLevel, bool shield)
        {
            string tier = GetTier(bonusLevel);
            if (tier == null)
                return null;
            // Legendary quality is supplied by The White March II.
            return (tier == "legendary" ? "px2_" : "") + tier +
                (shield ? "_shield" : "_weapon");
        }

        public static void Apply(Equippable item, int bonusLevel)
        {
            if (item == null)
                return;
            string prefabName = GetModifierPrefab(bonusLevel, item.GetComponent<Shield>() != null);
            if (prefabName == null)
                return;

            ItemMod modifier = GameResources.LoadPrefab<ItemMod>(prefabName, false);
            if (modifier == null || !modifier.IsQualityMod)
            {
                Main.Logger.Error("Could not load quality modifier " + prefabName +
                    "; keeping base equipment.");
                return;
            }

            // Initialize existing mods first. AttachItemMod replaces any existing
            // quality mod and keeps unrelated enchantments intact.
            item.Init();
            item.AttachItemMod(modifier);
            Main.Logger.Log("Applied " + prefabName + " to " + item.name + ".");
        }

        public static int GetArmorBonus(int bonusLevel)
        {
            return bonusLevel >= 16 ? 8 : bonusLevel >= 12 ? 4
                : bonusLevel >= 8 ? 2 : bonusLevel >= 4 ? 1 : 0;
        }
    }
}
