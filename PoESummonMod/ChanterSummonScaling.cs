using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SkeletonCrew;
using UnityEngine;

namespace PoESummonMod
{
    public static class ChanterSummonScaling
    {
        private sealed class Compensation
        {
            public int Levels;
            public float InnateArmorBonus;
        }

        // Only live instances created by the summon hook are registered. Weak keys
        // do not keep destroyed summons alive or modify their shared prefabs.
        private static readonly ConditionalWeakTable<CharacterStats, Compensation> Summons =
            new ConditionalWeakTable<CharacterStats, Compensation>();

        public static void Apply(CharacterStats summon, CharacterStats prefab, GameObject summoner, Summon source)
        {
            if (Main.Settings == null || !Main.Settings.EnableChanterSummonScaling ||
                summon == null || prefab == null || summoner == null)
                return;

            if (!ChanterSummonOrigin.Matches(source, prefab.gameObject.name, summoner))
                return;

            int unlockLevel;
            switch (prefab.gameObject.name)
            {
                case "CRE_Wurm_Summon":
                case "CRE_Will_o_Wisp_Summon":
                    unlockLevel = 5;
                    break;
                case "CRE_Drake_Summon":
                case "CRE_Ogre_Summon":
                    unlockLevel = 9;
                    break;
                case "CRE_Phantom_Summon":
                    unlockLevel = 1;
                    break;
                default:
                    return;
            }

            CharacterStats owner = summoner.GetComponent<CharacterStats>();
            Compensation existing;
            if (owner == null || Summons.TryGetValue(summon, out existing))
                return;

            int chanterLevel = SummonEquipmentQuality.GetBonusLevel(owner, true);
            if (chanterLevel <= 0)
                return;
            int bakedGrade = unlockLevel == 5 ? 1 : unlockLevel == 9 ? 2 : 0;

            int originalLevel = summon.Level;
            int originalScaledLevel = summon.ScaledLevel;
            // Evaluate the unlock point under the same difficulty scaling. This
            // calibration happens once, before the creature is initialized.
            summon.Level = unlockLevel;
            int compensation = unlockLevel == 1 ? 0 :
                Math.Max(0, summon.ScaledLevel - originalScaledLevel);
            summon.Level = Math.Max(1, owner.Level);
            // An item or another mod may make a summon available early. Do not
            // subtract more growth than this creature actually has at that level.
            compensation = Math.Min(compensation, Math.Max(0, summon.ScaledLevel - 1));
            float innateArmorBonus = ChanterSummonEquipment.Apply(summon, chanterLevel, bakedGrade, prefab.gameObject.name);
            Summons.Add(summon, new Compensation { Levels = compensation, InnateArmorBonus = innateArmorBonus });

            // Initialize table-backed stats before Health.Start can fill its pools.
            // The postfix below also reapplies compensation after subsequent
            // CharacterStats.Start/Refresh class-table loads.
            object component = summon;
            DataManager.AdjustFromData(ref component);
            RechargeDiagnostics.Write("SUMMON SCALING creature=" + RechargeDiagnostics.Id(summon) +
                " prefab=" + prefab.gameObject.name +
                " originalLevel=" + originalLevel + " level=" + summon.Level +
                " unlockLevel=" + unlockLevel + " compensatedLevels=" + compensation +
                " chanterLevel=" + chanterLevel + " addedQualityGrades=" + ChanterSummonEquipment.AddedGrades(chanterLevel, bakedGrade) +
                " armorBonus=" + ChanterSummonEquipment.ArmorBonus(chanterLevel, bakedGrade) +
                " endurance=" + summon.Stamina + " health=" + summon.Health);
        }

        internal static float InnateArmorBonus(CharacterStats stats, DamagePacket.DamageType type)
        {
            Compensation compensation;
            // Raw damage bypasses DR. Preserve that behavior and all immunities.
            return stats != null && type != DamagePacket.DamageType.Raw &&
                Summons.TryGetValue(stats, out compensation) ? compensation.InnateArmorBonus : 0;
        }

        internal static void AdjustBaseStats(object component)
        {
            CharacterStats stats = component as CharacterStats;
            Compensation compensation;
            if (stats == null || !Summons.TryGetValue(stats, out compensation))
                return;

            int levels = compensation.Levels;
            // DataManager has just restored these fields from ClassDefenses.
            // Subtract once per table load; never subtract from a previous result.
            stats.MaxHealth -= levels * stats.HealthStaminaPerLevel;
            stats.MaxStamina -= levels * stats.HealthStaminaPerLevel;
            int accuracy = levels * AttackData.Instance.AccuracyPerLevel;
            stats.MeleeAccuracyBonus -= accuracy;
            stats.RangedAccuracyBonus -= accuracy;
            int defense = levels * AttackData.Instance.DefensePerLevel;
            stats.BaseDeflection -= defense;
            stats.BaseFortitude -= defense;
            stats.BaseReflexes -= defense;
            stats.BaseWill -= defense;
        }

        internal static int AbilityAccuracyCompensation(CharacterStats stats, AttackBase attack)
        {
            Compensation compensation;
            // CalculateAccuracy adds one extra point per level for ability attacks,
            // on top of the class accuracy growth compensated in the base fields.
            return stats != null && attack != null &&
                (attack.AbilityOrigin != null || attack.TriggeringAbility != null) &&
                stats.CharacterClass != CharacterStats.Class.PlayerTrap &&
                Summons.TryGetValue(stats, out compensation) ? compensation.Levels : 0;
        }
    }

    [HarmonyPatch(typeof(CharacterStats), "CalcDT",
        new[] { typeof(DamagePacket.DamageType), typeof(bool), typeof(bool) })]
    internal static class ChanterSummonInnateArmorPatch
    {
        private static void Postfix(CharacterStats __instance, DamagePacket.DamageType __0, ref float __result)
        {
            __result += ChanterSummonScaling.InnateArmorBonus(__instance, __0);
        }
    }

    [HarmonyPatch]
    internal static class ChanterSummonBaseStatsPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(DataManager), "AdjustFromData",
                new[] { typeof(object).MakeByRefType() });
        }

        private static void Postfix(object __0)
        {
            ChanterSummonScaling.AdjustBaseStats(__0);
        }
    }

    [HarmonyPatch(typeof(CharacterStats), "CalculateAccuracy",
        new[] { typeof(AttackBase), typeof(GameObject) })]
    internal static class ChanterSummonAbilityAccuracyPatch
    {
        private static void Postfix(CharacterStats __instance, AttackBase __0, ref int __result)
        {
            __result -= ChanterSummonScaling.AbilityAccuracyCompensation(__instance, __0);
        }
    }
}
