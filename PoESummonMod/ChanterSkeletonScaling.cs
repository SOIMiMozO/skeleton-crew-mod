using SkeletonCrew;
using System;
using UnityEngine;
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

            if (GameState.s_playerCharacter != null &&
                summoner == GameState.s_playerCharacter.gameObject)
            {
                string skeletonName = GetSkeletonName(summonIndex);

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
    }
}