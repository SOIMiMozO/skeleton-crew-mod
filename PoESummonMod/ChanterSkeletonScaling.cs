using SkeletonCrew;
using System;
using UnityEngine;
using System;
using Object = UnityEngine.Object;

namespace PoESummonMod
{
    public static class ChanterSkeletonScaling
    {
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

            if (character.gameObject.name != "CRE_Skeleton_Chanter_Summon")
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

            ApplySkeletonArmor(
                characterStats);

            ApplySkeletonWeapons(
                characterStats,
                summonIndex);

            ApplySkeletonAccessories(characterStats);

            if (Main.Settings.UseFampyrAppearance)
                FampyrAppearance.Apply(characterStats);
        }

        private static void ApplySkeletonAccessories(CharacterStats skeleton)
        {
            Equipment equipment = skeleton.GetComponent<Equipment>();
            if (equipment == null)
            {
                Main.Logger.Error("Could not assign skeleton accessories: no Equipment component.");
                return;
            }

            // Each call creates an independent item for this summon. Keep the
            // existing slot untouched if its test prefab cannot be loaded.
            AssignAccessory("Head", "helm", item => equipment.DefaultEquippedItems.Head = item);
            // NPCAppearance.AttachCape reads Neck, not the legacy Cape field.
            // Cloaks and amulets therefore cannot occupy separate visible slots.
            AssignAccessory("Neck", "cloak_of_protection", item => equipment.DefaultEquippedItems.Neck = item);
            AssignAccessory("Hands", "gloves_fulvano", item => equipment.DefaultEquippedItems.Hands = item);
            AssignAccessory("RightHandRing", "ring_of_deflection", item => equipment.DefaultEquippedItems.RightHandRing = item);
            AssignAccessory("LeftHandRing", "ring_of_protection", item => equipment.DefaultEquippedItems.LeftHandRing = item);
            AssignAccessory("Feet", "boots_of_stability", item => equipment.DefaultEquippedItems.Feet = item);
            AssignAccessory("Waist", "blunting_belt", item => equipment.DefaultEquippedItems.Waist = item);

            // Experimental slots: assignment alone does not grant wizard
            // spellcasting or guarantee pet behavior on a summoned creature.
            AssignAccessory("Grimoire", "grimoire01", item => equipment.DefaultEquippedItems.Grimoire = item);
            AssignAccessory("Pet", "pet_black_cat", item => equipment.DefaultEquippedItems.Pet = item);
        }

        private static void AssignAccessory(string slot, string prefabName, Action<Equippable> assign)
        {
            Equippable item = CreateEquippable(prefabName);
            if (item == null)
            {
                Main.Logger.Error("Skeleton slot " + slot + " unchanged: could not create " + prefabName + ".");
                return;
            }

            // Summoned equipment must not become a source of permanent loot.
            item.NeverDropAsLoot = true;
            assign(item);
            Main.Logger.Log("Skeleton " + slot + " assigned: " + prefabName + ".");
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

                if (!string.IsNullOrEmpty(skeletonName))
                {
                    skeletonName = skeletonName.Trim();

                    if (skeletonName.Length > 0)
                    {
                        characterStats.OverrideName = skeletonName;
                    }
                }
            }

            Main.Logger.Log("Chanter skeleton detected.");

            CharacterStats summonerStats =
                summoner.GetComponent<CharacterStats>();

            if (summonerStats != null)
            {
                characterStats.Level = summonerStats.Level;

                Main.Logger.Log(
                    "Skeleton level set to summoner level: " +
                    summonerStats.Level);
            }

            GameObject armorPrefab =
                GameResources.LoadPrefab<GameObject>(
                    "skeleton_hide_armor",
                    false);

            if (armorPrefab == null)
            {
                Main.Logger.Error(
                    "Could not load skeleton_breastplate_armor.");

                return;
            }

            GameObject armorObject =
                Object.Instantiate<GameObject>(armorPrefab);

            armorObject.name =
                "Skeleton_Breastplate_Chanter_Armor";

            Armor armor =
                armorObject.GetComponent<Armor>();

            if (armor != null)
            {
                armor.DamageThreshhold = 0;
                armor.DamageReduction = 0;

                armor.DtPercBurning = 75;
                armor.DtPercFreezing = 110;
                armor.DtPercCorroding = 110;


                armor.LevelScaling.BaseLevel = 1;
                armor.LevelScaling.LevelIncrement = 1;
                armor.LevelScaling.MaxLevel = 0;
                armor.LevelScaling.DtAdjustment = 0;

                armor.SpeedFactor = 0.8f;

                Main.Logger.Log(
                    "Chanter skeleton armor configured.");
            }
            else
            {
                Main.Logger.Error(
                    "Cloned breastplate has no Armor component.");
            }

            Equippable equippable =
                armorObject.GetComponent<Equippable>();

            Equipment equipment =
                characterStats.GetComponent<Equipment>();

            if (equippable != null && equipment != null)
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
            int summonIndex)
        {
            Equipment equipment =
                skeleton.GetComponent<Equipment>();

            if (equipment == null)
            {
                Main.Logger.Error(
                    "Chanter skeleton has no Equipment component.");

                return;
            }

            switch (summonIndex)
            {
                // Skeleton 1:
                // Replace primary weapon with spear.
                // Keep its default shield in the secondary hand.
                case 0:
                    Equippable spear =
                        CreateEquippable(SpearPrefabName);

                    if (spear != null)
                    {
                        equipment.DefaultEquippedItems.PrimaryWeapon =
                            spear;

                        Main.Logger.Log(
                            "Skeleton 1 equipped with spear + default shield.");
                    }

                    break;

                // Skeleton 2:
                // Replace both hands with sabre and dagger.
                case 1:
                    Equippable primarySabre =
                        CreateEquippable(SabrePrefabName);

                    Equippable secondaryDagger =
                        CreateEquippable(DaggerPrefabName);

                    if (primarySabre != null &&
                        secondaryDagger != null)
                    {
                        equipment.DefaultEquippedItems.PrimaryWeapon =
                            primarySabre;

                        equipment.DefaultEquippedItems.SecondaryWeapon =
                            secondaryDagger;

                        Main.Logger.Log(
                            "Skeleton 2 equipped with sabre and dagger.");
                    }

                    break;

                // Skeleton 3:
                // Two-handed morning star.
                case 2:
                    Equippable morningStar =
                        CreateEquippable(MorningStarPrefabName);

                    if (morningStar != null)
                    {
                        equipment.DefaultEquippedItems.PrimaryWeapon =
                            morningStar;

                        // Remove the skeleton's default shield.
                        equipment.DefaultEquippedItems.SecondaryWeapon =
                            null;

                        Main.Logger.Log(
                            "Skeleton 3 equipped with morning_star_01.");
                    }

                    break;

                default:
                    Main.Logger.Error(
                        "Unexpected skeleton summon index: " +
                        summonIndex);

                    break;
            }
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
                    "Could not load equipment prefab: " +
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
                    "Equipment prefab has no Equippable component: " +
                    prefabName);

                Object.Destroy(weaponObject);

                return null;
            }

            return equippable;
        }
    }
}
