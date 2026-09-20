using SkeletonCrew;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PoESummonMod
{
    public static class ChanterSkeletonScaling
    {
        private const string SkeletonPrefabName =
            "CRE_Skeleton_Chanter_Summon";

        private const string ArmorPrefabName =
            "skeleton_hide_armor";

        private static string GetSkeletonName(int index)
        {
            switch (index)
            {
                case 0:
                    return Main.Settings.SkeletonName1;

                case 1:
                    return Main.Settings.SkeletonName2;

                case 2:
                    return Main.Settings.SkeletonName3;

                default:
                    return null;
            }
        }

        public static void Apply(
            CharacterStats characterStats,
            CharacterStats character,
            GameObject summoner,
            int summonIndex)
        {
            if (characterStats == null ||
                character == null ||
                summoner == null)
            {
                return;
            }

            // Only modify the Chanter skeleton summon.
            if (character.gameObject.name != SkeletonPrefabName)
            {
                return;
            }

            ApplyCustomName(
                characterStats,
                summoner,
                summonIndex);

            Main.Logger.Log(
                "Chanter skeleton detected. Index: " +
                summonIndex);

            ApplySummonerLevel(
                characterStats,
                summoner);

            SkeletonModels.Apply(characterStats, character, summonIndex);

            CharacterStats summonerStats = summoner.GetComponent<CharacterStats>();
            int bonusLevel = summonerStats == null ? 0 : SummonEquipmentQuality.GetBonusLevel(summonerStats);

            ApplySkeletonArmor(
                characterStats, bonusLevel);

            ApplySkeletonWeapons(
                characterStats,
                summonIndex, bonusLevel);
        }

        private static void ApplyCustomName(
            CharacterStats skeleton,
            GameObject summoner,
            int summonIndex)
        {
            // Custom names are only applied to skeletons
            // summoned by the main player character.
            if (GameState.s_playerCharacter == null ||
                summoner != GameState.s_playerCharacter.gameObject)
            {
                return;
            }

            string skeletonName =
                GetSkeletonName(summonIndex);

            if (string.IsNullOrEmpty(skeletonName))
            {
                return;
            }

            skeletonName = skeletonName.Trim();

            if (skeletonName.Length > 0)
            {
                skeleton.OverrideName = skeletonName;
            }
        }

        private static void ApplySummonerLevel(
            CharacterStats skeleton,
            GameObject summoner)
        {
            CharacterStats summonerStats =
                summoner.GetComponent<CharacterStats>();

            if (summonerStats == null)
            {
                Main.Logger.Error(
                    "Could not get summoner CharacterStats.");

                return;
            }

            skeleton.Level = summonerStats.Level;

            Main.Logger.Log(
                "Skeleton level set to summoner level: " +
                summonerStats.Level);
        }

        private static void ApplySkeletonArmor(
            CharacterStats skeleton, int bonusLevel)
        {
            GameObject armorPrefab =
                GameResources.LoadPrefab<GameObject>(
                    ArmorPrefabName,
                    false);

            if (armorPrefab == null)
            {
                Main.Logger.Error(
                    "Could not load " +
                    ArmorPrefabName +
                    ".");

                return;
            }

            GameObject armorObject =
                Object.Instantiate<GameObject>(
                    armorPrefab);

            armorObject.name =
                "Skeleton_Hide_Chanter_Armor";

            Armor armor =
                armorObject.GetComponent<Armor>();

            if (armor != null)
            {
                armor.DamageThreshhold = SummonEquipmentQuality.GetArmorBonus(bonusLevel);
                armor.DamageReduction = 0;

                armor.DtPercBurning = 75;
                armor.DtPercFreezing = 110;
                armor.DtPercCorroding = 110;

                armor.LevelScaling.BaseLevel = 1;
                armor.LevelScaling.LevelIncrement = 1;
                armor.LevelScaling.MaxLevel = 0;
                armor.LevelScaling.DtAdjustment = 0.5f;

                armor.SpeedFactor = 0.8f;

                Main.Logger.Log(
                    "Chanter skeleton armor configured.");
            }
            else
            {
                Main.Logger.Error(
                    "Cloned skeleton armor has no Armor component.");
            }

            Equippable equippable =
                armorObject.GetComponent<Equippable>();

            Equipment equipment =
                skeleton.GetComponent<Equipment>();

            if (equippable != null &&
                equipment != null)
            {
                equipment.DefaultEquippedItems.Chest =
                    equippable;

                Main.Logger.Log(
                    "Chanter skeleton armor assigned.");
            }
            else
            {
                Main.Logger.Error(
                    "Could not assign Chanter skeleton armor.");
            }
        }

        private static void ApplySkeletonWeapons(
            CharacterStats skeleton,
            int summonIndex, int bonusLevel)
        {
            Equipment equipment =
                skeleton.GetComponent<Equipment>();

            if (equipment == null)
            {
                Main.Logger.Error(
                    "Chanter skeleton has no Equipment component.");

                return;
            }

            if (summonIndex < 0 || summonIndex > 2)
            {
                Main.Logger.Error("Unexpected skeleton summon index: " + summonIndex);
                return;
            }

            // Normalize here too: saved settings cannot bypass hand restrictions.
            SkeletonHands hands = Main.Settings.GetHands(summonIndex);
            WeaponOption main = WeaponCatalog.Find(hands.MainHand);
            WeaponOption off = WeaponCatalog.Find(hands.OffHand);
            string primaryPrefab = main.PrefabName;
            string secondaryPrefab = off.PrefabName;
            Equippable primary = primaryPrefab == null ? null : CreateEquippable(primaryPrefab);
            Equippable secondary = secondaryPrefab == null ? null : CreateEquippable(secondaryPrefab);

            SummonEquipmentQuality.Apply(primary, bonusLevel);
            SummonEquipmentQuality.Apply(secondary, bonusLevel);

            // Resolve each hand independently. A failed load leaves only that hand
            // empty, rather than reverting both hands to the summon's axe/shield.
            equipment.DefaultEquippedItems.PrimaryWeapon = primary;
            equipment.DefaultEquippedItems.SecondaryWeapon = secondary;
            Main.Logger.Log("Skeleton " + (summonIndex + 1) + " equipped with " +
                (primary == null ? "Empty" : primaryPrefab) + " / " +
                (secondary == null ? "Empty" : secondaryPrefab) + ".");
        }
        private static Equippable CreateEquippable(
            string prefabName)
        {
            GameObject prefab =
                GameResources.LoadPrefab<GameObject>(
                    prefabName,
                    false);

            if (prefab == null)
            {
                Main.Logger.Error(
                    "Could not load weapon prefab: " +
                    prefabName);

                return null;
            }

            GameObject weaponObject =
                Object.Instantiate<GameObject>(
                    prefab);

            Equippable equippable =
                weaponObject.GetComponent<Equippable>();

            if (equippable == null)
            {
                Main.Logger.Error(
                    "Weapon prefab has no Equippable component: " +
                    prefabName);

                Object.Destroy(weaponObject);

                return null;
            }

            return equippable;
        }
    }
}
