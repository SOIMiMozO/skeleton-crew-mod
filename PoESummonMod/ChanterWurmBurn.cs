using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SkeletonCrew;
using UnityEngine;

namespace PoESummonMod
{
    internal static class ChanterWurmBurn
    {
        private static readonly ConditionalWeakTable<AttackBase, Equippable> Weapons =
            new ConditionalWeakTable<AttackBase, Equippable>();

        internal static void Register(AttackBase attack, Equippable weapon)
        {
            Weapons.Remove(attack);
            Weapons.Add(attack, weapon);
        }

        internal static void Apply(CharacterStats attacker, GameObject enemy, DamageInfo damage)
        {
            Equippable weapon;
            if (attacker == null || enemy == null || damage == null || damage.IsMiss ||
                damage.Attack == null || damage.Attack.Owner != attacker.gameObject ||
                !Weapons.TryGetValue(damage.Attack, out weapon) || weapon == null)
                return;
            CharacterStats target = enemy.GetComponent<CharacterStats>();
            Health health = enemy.GetComponent<Health>();
            if (target == null || health == null || health.Dead || health.Unconscious)
                return;

            float value = UnscaledDamage(damage.FinalAdjustedDamage,
                attacker.StatDamageHealMultiplier * attacker.GetDifficultyDamageMultiplier(health));
            if (value <= 0)
                return;

            var parameters = new StatusEffectParams
            {
                Tag = "SkeletonCrew_WurmBurn",
                AffectsStat = StatusEffect.ModifiedStat.Damage,
                DmgType = DamagePacket.DamageType.Burn,
                Apply = StatusEffect.ApplyType.ApplyOverTime,
                IntervalRate = StatusEffectParams.IntervalRateType.Damage,
                Duration = 6f,
                Value = value,
                // This damage already includes the original tactical hit bonus.
                TacticalMultiplier = 1f,
                IsHostile = true
            };
            // Native DOTs adjust duration for graze/crit and Intellect. Their
            // damage ticks apply Might/difficulty, which we removed above once.
            StatusEffect effect = StatusEffect.Create(attacker.gameObject, weapon, parameters,
                GenericAbility.AbilityType.WeaponOrShield, damage, true);
            effect.m_forceStackable = true;
            target.ApplyStatusEffectImmediate(effect);
            RechargeDiagnostics.Write("WURM BURN hitDamage=" + damage.FinalAdjustedDamage +
                " baseDuration=6 duration=" + effect.Duration);
        }

        internal static float UnscaledDamage(float inflicted, float sourceMultiplier)
        {
            if (float.IsNaN(inflicted) || float.IsInfinity(inflicted) || inflicted <= 0 ||
                float.IsNaN(sourceMultiplier) || float.IsInfinity(sourceMultiplier) || sourceMultiplier <= 0)
                return 0;
            return inflicted / sourceMultiplier;
        }
    }

    [HarmonyPatch(typeof(CharacterStats), "TriggerWhenInflictsDamage")]
    internal static class ChanterWurmBurnPatch
    {
        private static void Postfix(CharacterStats __instance, GameObject __0, DamageInfo __1)
        {
            try { ChanterWurmBurn.Apply(__instance, __0, __1); }
            catch (Exception ex) { Main.Logger.Error("Could not apply Chanter wurm burn: " + ex); }
        }
    }
}
