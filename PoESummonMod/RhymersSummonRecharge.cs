using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SkeletonCrew
{
    // Runtime-only progress: a fresh depletion always starts a fresh cycle.
    internal sealed class RhymersRechargeProgress
    {
        internal readonly float DamageRequired;
        internal readonly int Multiplier;
        internal const int KillsRequired = 2;
        private float damage;
        private readonly HashSet<int> kills = new HashSet<int>();

        internal RhymersRechargeProgress(int summonerLevel, int multiplier)
        {
            Multiplier = Math.Max(2, multiplier);
            // Two skeletons: 26 Endurance each at level 1, +8 each per level.
            DamageRequired = (52f + 16f * (Math.Max(1, summonerLevel) - 1)) * Multiplier;
        }

        internal string TooltipText()
        {
            // Round down progress so a nearly-complete target never looks complete.
            double displayedDamage = Math.Floor(damage * 10.0) / 10.0;
            return string.Format(System.Globalization.CultureInfo.CurrentCulture,
                "Next charge\nDamage: {0:0.#} / {1:0.#}\nKills: {2} / {3}\nMultiplier: ×{4}",
                displayedDamage, DamageRequired, Math.Min(kills.Count, KillsRequired), KillsRequired, Multiplier);
        }

        internal bool Record(float amount, bool killed, int targetId)
        {
            if (!float.IsNaN(amount) && !float.IsInfinity(amount) && amount > 0f)
                damage = Math.Min(DamageRequired, damage + amount);
            if (killed)
                kills.Add(targetId);
            RechargeDiagnostics.Write("PROGRESS target=" + targetId + " added=" + amount +
                " killed=" + killed + " damage=" + damage + "/" + DamageRequired +
                " kills=" + kills.Count + "/" + KillsRequired);
            return damage >= DamageRequired && kills.Count >= KillsRequired;
        }
    }

    internal sealed class RhymersRechargeBattle
    {
        internal int Multiplier = 2;
        internal RhymersRechargeProgress Start(int level)
        {
            return new RhymersRechargeProgress(level, Multiplier);
        }
        internal void Restored() { Multiplier++; }
    }

    internal static class RhymersSummonRecharge
    {
        internal sealed class Cycle
        {
            internal GenericAbility Ability;
            internal GameObject Owner;
            internal RhymersRechargeProgress Progress;
            internal RhymersRechargeBattle Battle;
        }

        internal sealed class Hit
        {
            internal readonly List<Cycle> Cycles = new List<Cycle>();
            internal float Vitality;
            internal bool UsesStamina;
            internal int TargetId;
        }

        private static readonly Dictionary<GenericAbility, Cycle> cycles =
            new Dictionary<GenericAbility, Cycle>();
        private static readonly Dictionary<GenericAbility, RhymersRechargeBattle> battles =
            new Dictionary<GenericAbility, RhymersRechargeBattle>();

        internal static void Initialize()
        {
            ResetBattle(null, EventArgs.Empty);
            GameState.OnCombatEnd -= ResetBattle;
            GameState.OnCombatEnd += ResetBattle;
        }

        private static void ResetBattle(object sender, EventArgs args)
        {
            RechargeDiagnostics.Write("RESET cycles=" + cycles.Count + " battles=" + battles.Count);
            cycles.Clear();
            battles.Clear();
        }

        internal static bool Enabled => Main.Settings != null && Main.Settings.EnableRhymersRecharge;

        internal static string GetTooltipProgress(GenericAbility ability)
        {
            if (!Enabled || ability == null) return null;
            Cycle cycle;
            if (!cycles.TryGetValue(ability, out cycle) || ability.Owner != cycle.Owner ||
                ability.UsesLeft() != 0) return null;
            return cycle.Progress.TooltipText();
        }

        internal static bool IsRhymer(GenericAbility ability)
        {
            if (ability == null) return false;
            string name = ability.gameObject.name;
            return string.Equals(name, "px1_rhymers_summon", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "px1_rhymers_summon(Clone)", StringComparison.OrdinalIgnoreCase);
        }

        internal static void Clear() { cycles.Clear(); }
        internal static void Remove(GenericAbility ability) { cycles.Remove(ability); }
        internal static void Destroy(GenericAbility ability)
        {
            Remove(ability);
            battles.Remove(ability);
        }

        internal static void Start(GenericAbility ability, int previousUses)
        {
            if (!Enabled || previousUses <= 0 || ability.UsesLeft() != 0 || ability.Owner == null)
                return;
            CharacterStats stats = ability.Owner.GetComponent<CharacterStats>();
            if (stats == null) return;
            RhymersRechargeBattle battle;
            if (!battles.TryGetValue(ability, out battle))
                battles[ability] = battle = new RhymersRechargeBattle();
            cycles[ability] = new Cycle
            {
                Ability = ability,
                Owner = ability.Owner,
                Progress = battle.Start(stats.Level),
                Battle = battle
            };
            RechargeDiagnostics.Write("TRACK ability=" + RechargeDiagnostics.Id(ability) +
                " owner=" + RechargeDiagnostics.Id(ability.Owner) + " level=" + stats.Level +
                " multiplier=" + battle.Multiplier + " required=" + cycles[ability].Progress.DamageRequired);
        }

        internal static void Validate(GenericAbility ability)
        {
            if (!Enabled) { Clear(); return; }
            Cycle cycle;
            if (cycles.TryGetValue(ability, out cycle) &&
                (ability.Owner != cycle.Owner || ability.UsesLeft() > 0))
                Remove(ability);
        }

        internal static bool IsConstruct(CharacterStats.Class characterClass, CharacterStats.Subrace subrace)
        {
            return characterClass == CharacterStats.Class.Animat ||
                characterClass == CharacterStats.Class.FleshConstruct ||
                characterClass == CharacterStats.Class.Eyeless ||
                subrace == CharacterStats.Subrace.Advanced_Construct;
        }

        internal static bool IsContributingSkeleton(CharacterStats.Class characterClass,
            AIController.AISummonType summonType)
        {
            return characterClass == CharacterStats.Class.Skeleton &&
                summonType == AIController.AISummonType.Summoned;
        }

        private static GameObject GetContributorOwner(GameObject attacker)
        {
            RechargeDiagnostics.Write("ATTRIBUTION attacker=" + RechargeDiagnostics.Id(attacker));
            CharacterStats stats = attacker.GetComponent<CharacterStats>();
            AIController ai = GameUtilities.FindActiveAIController(attacker);
            if (stats != null && ai != null && ai.Summoner != null &&
                IsContributingSkeleton(stats.CharacterClass, ai.SummonType))
            {
                RechargeDiagnostics.Write("SKELETON owner=" + RechargeDiagnostics.Id(ai.Summoner));
                return ai.Summoner;
            }
            return attacker;
        }

        // Snapshot before damage/death changes faction, health, or combat state.
        internal static Hit Before(Health target, GameObject attacker)
        {
            if (!Enabled || cycles.Count == 0 || attacker == null || target == null ||
                target.Dead || target.Unconscious || !target.CanBeTargeted)
                return null;
            RechargeDiagnostics.Write("HIT attacker=" + RechargeDiagnostics.Id(attacker) +
                " target=" + RechargeDiagnostics.Id(target) + " activeCycles=" + cycles.Count);
            Faction faction = attacker.GetComponent<Faction>();
            CharacterStats stats = target.GetComponent<CharacterStats>();
            if (faction == null || !faction.IsHostile(target.gameObject) ||
                (stats != null && IsConstruct(stats.CharacterClass, stats.CharacterSubrace)))
                return null;

            GameObject contributorOwner = GetContributorOwner(attacker);
            // A charmed skeleton must not earn credit by attacking its owner's allies.
            Faction ownerFaction = contributorOwner.GetComponent<Faction>();
            if (ownerFaction == null || !ownerFaction.IsHostile(target.gameObject)) return null;

            var hit = new Hit
            {
                UsesStamina = target.CurrentStamina > 0f,
                Vitality = target.CurrentStamina > 0f ? target.CurrentStamina : target.CurrentHealth,
                TargetId = target.GetInstanceID()
            };
            foreach (Cycle cycle in cycles.Values)
            {
                if (cycle.Owner == contributorOwner && cycle.Ability != null &&
                    cycle.Ability.Owner == contributorOwner && cycle.Ability.UsesLeft() == 0)
                    hit.Cycles.Add(cycle);
            }
            return hit.Cycles.Count == 0 ? null : hit;
        }

        internal static void After(Health target, Hit hit, bool damage)
        {
            if (hit == null || !Enabled || target == null) return;
            // Count actual vitality lost once (not both Health and Endurance),
            // excluding resisted damage, invulnerability, healing, and overkill.
            float remaining = hit.UsesStamina ? target.CurrentStamina : target.CurrentHealth;
            float amount = damage ? Mathf.Max(0f, hit.Vitality - Mathf.Max(0f, remaining)) : 0f;
            bool killed = target.ShowDead && target.CanDie;
            foreach (Cycle cycle in hit.Cycles)
            {
                Cycle current;
                if (cycle.Ability == null || !cycles.TryGetValue(cycle.Ability, out current) ||
                    !ReferenceEquals(current, cycle)) continue;
                if (cycle.Ability.Owner != cycle.Owner || cycle.Ability.UsesLeft() != 0)
                {
                    Remove(cycle.Ability);
                    continue;
                }
                if (!cycle.Progress.Record(amount, killed, hit.TargetId)) continue;
                RechargeDiagnostics.Write("RESTORE BEGIN ability=" + RechargeDiagnostics.Id(cycle.Ability) +
                    " owner=" + RechargeDiagnostics.Id(cycle.Owner));
                Remove(cycle.Ability);
                cycle.Ability.GiveUses(1);
                cycle.Battle.Restored();
                RechargeDiagnostics.Write("RESTORE END uses=" + cycle.Ability.UsesLeft() +
                    " nextMultiplier=" + cycle.Battle.Multiplier);
                Main.Logger.Log("Rhymer's Summon: restored one charge after " +
                    cycle.Progress.DamageRequired + " damage and " +
                    RhymersRechargeProgress.KillsRequired + " kills.");
            }
        }
    }

    // The existing tooltip refresh requests this text every frame while visible.
    // Read only: no progress changes, logging, UI timers, or widget replacement.
    [HarmonyPatch(typeof(GenericAbility), "GetTooltipContent", new Type[] { typeof(GameObject) })]
    internal static class RhymersRechargeTooltipPatch
    {
        private static void Postfix(GenericAbility __instance, ref string __result)
        {
            string progress = RhymersSummonRecharge.GetTooltipProgress(__instance);
            if (progress != null) __result = (__result ?? "") + "\n\n" + progress;
        }
    }

    [HarmonyPatch(typeof(GenericAbility), "ActivateCooldown")]
    internal static class RhymersRechargeDepletionPatch
    {
        private static void Prefix(GenericAbility __instance, out int __state)
        {
            __state = RhymersSummonRecharge.Enabled && RhymersSummonRecharge.IsRhymer(__instance)
                ? __instance.UsesLeft() : -1;
        }
        private static void Postfix(GenericAbility __instance, int __state)
        {
            if (__state > 0) RhymersSummonRecharge.Start(__instance, __state);
        }
    }

    [HarmonyPatch(typeof(GenericAbility), "Update")]
    internal static class RhymersRechargeResetPatch
    {
        private static void Postfix(GenericAbility __instance) { RhymersSummonRecharge.Validate(__instance); }
    }

    [HarmonyPatch(typeof(GenericAbility), "OnDestroy")]
    internal static class RhymersRechargeDestroyPatch
    {
        private static void Prefix(GenericAbility __instance) { RhymersSummonRecharge.Destroy(__instance); }
    }

    [HarmonyPatch(typeof(Health), "DoDamage", new Type[] { typeof(DamageInfo), typeof(GameObject) })]
    internal static class RhymersRechargeDamagePatch
    {
        private static void Prefix(Health __instance, GameObject attacker, out RhymersSummonRecharge.Hit __state)
        {
            __state = RhymersSummonRecharge.Before(__instance, attacker);
        }
        private static void Postfix(Health __instance, RhymersSummonRecharge.Hit __state)
        {
            RhymersSummonRecharge.After(__instance, __state, true);
        }
    }

    [HarmonyPatch(typeof(Health), "ApplyDamageDirectly", new Type[]
        { typeof(float), typeof(DamagePacket.DamageType), typeof(GameObject), typeof(StatusEffect), typeof(bool) })]
    internal static class RhymersRechargeDirectDamagePatch
    {
        private static void Prefix(Health __instance, GameObject source, out RhymersSummonRecharge.Hit __state)
        {
            __state = RhymersSummonRecharge.Before(__instance, source);
        }
        private static void Postfix(Health __instance, RhymersSummonRecharge.Hit __state)
        {
            RhymersSummonRecharge.After(__instance, __state, true);
        }
    }

    [HarmonyPatch(typeof(Health), "DestroyKill")]
    internal static class RhymersRechargeInstantKillPatch
    {
        private static void Prefix(Health __instance, GameObject source, out RhymersSummonRecharge.Hit __state)
        {
            __state = RhymersSummonRecharge.Before(__instance, source);
        }
        private static void Postfix(Health __instance, RhymersSummonRecharge.Hit __state)
        {
            RhymersSummonRecharge.After(__instance, __state, false);
        }
    }
}
