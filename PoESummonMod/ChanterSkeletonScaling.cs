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
            int summonIndex, Summon source)
        {
            if (characterStats == null ||
                character == null ||
                summoner == null)
            {
                return;
            }

            // Route other supported summons through the optional scaling feature.
            if (character.gameObject.name != SkeletonPrefabName)
            {
                ChanterSummonScaling.Apply(characterStats, character, summoner, source);
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
            Equippable armorItem = CreateEquippable(ArmorPrefabName, skeleton.transform);
            if (armorItem == null) return;
            GameObject armorObject = armorItem.gameObject;

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
            Equippable primary = CreateSelectedEquippable(main, bonusLevel, skeleton.transform);
            Equippable secondary = CreateSelectedEquippable(off, bonusLevel, skeleton.transform);

            SummonEquipmentQuality.Apply(primary, bonusLevel);
            SummonEquipmentQuality.Apply(secondary, bonusLevel);

            // Resolve each hand independently. A failed load leaves only that hand
            // empty, rather than reverting both hands to the summon's axe/shield.
            equipment.DefaultEquippedItems.PrimaryWeapon = primary;
            equipment.DefaultEquippedItems.SecondaryWeapon = secondary;
            Main.Logger.Log("Skeleton " + (summonIndex + 1) + " equipped with " +
                (primary == null ? "Empty" : primary.name) + " / " +
                (secondary == null ? "Empty" : secondary.name) + ".");
        }

        private static Equippable CreateSelectedEquippable(WeaponOption option, int bonusLevel, Transform owner)
        {
            if (option.PrefabName == null)
                return null;
            string appearancePrefab = option.GetAppearancePrefab(bonusLevel);
            Equippable item = CreateEquippable(appearancePrefab, owner);
            if (item == null && appearancePrefab != option.PrefabName)
            {
                Main.Logger.Log("Falling back to base appearance for " + option.Label + ".");
                item = CreateEquippable(option.PrefabName, owner);
            }
            return item;
        }
        private static Equippable CreateEquippable(
            string prefabName, Transform owner)
        {
            GameObject prefab =
                GameResources.LoadPrefab<GameObject>(
                    prefabName,
                    false);

            if (prefab == null)
            {
                Main.Logger.Error(
                    "Could not load equipment prefab: " +
                    prefabName);

                return null;
            }

            Equippable original = prefab.GetComponent<Equippable>();
            if (original == null || owner == null)
            {
                Main.Logger.Error("Cannot create equipment without an Equippable prefab and owner: " + prefabName);
                return null;
            }

            GameObject weaponObject =
                Object.Instantiate<GameObject>(
                    prefab);

            // These are live item instances, not new prefabs. Parent immediately
            // so Item.Start never sees a loose world item and owner destruction
            // cleans up the instance even if equipment initialization fails.
            weaponObject.transform.SetParent(owner, false);

            Equippable equippable =
                weaponObject.GetComponent<Equippable>();

            if (equippable == null)
            {
                Main.Logger.Error(
                    "Equipment clone has no Equippable component: " +
                    prefabName);

                Object.Destroy(weaponObject);

                return null;
            }

            // EquipmentSet.InstantiateFromSet now reuses this customized object
            // instead of loading by its name and creating a second instance.
            equippable.Prefab = original;
            RechargeDiagnostics.Write("EQUIPMENT INSTANCE item=" + RechargeDiagnostics.Id(equippable) +
                " prefab=" + RechargeDiagnostics.Id(original) + " owner=" + RechargeDiagnostics.Id(owner) +
                " isPrefab=" + equippable.IsPrefab);
            return equippable;
        }
    }
}
