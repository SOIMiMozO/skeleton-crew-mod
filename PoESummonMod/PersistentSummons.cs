using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace SkeletonCrew
{
    // Rhymer's Summon skeletons and item summons (figurines, etc.):
    //
    // 1) Coexistence. Vanilla Summon.DestroyAllSummoned walks the summoner's whole
    //    AIController.SummonedCreatureList and removes every Summoned-type creature,
    //    whichever Summon created it. Re-casting, a timer expiring or combat ending on
    //    any one source therefore wipes the creatures of every other source. While a
    //    source runs DestroyAllSummoned, creatures owned by a different, still-existing
    //    source are hidden from that list whenever either source is protected, so each
    //    source only removes its own creatures. Two ordinary spell summons keep the
    //    vanilla one-replaces-the-other rule.
    //
    // 2) No time limit. Vanilla sets m_DurationTimer = Duration per spawned creature;
    //    real-time Update and turn-based OnTimeElapsed only count down while it is > 0.
    //    Protected sources get 0, so their creatures stay until combat end, death, the
    //    summoner dying, the source being re-used or the item being unequipped.
    internal static class PersistentSummons
    {
        // Creature GameObject -> the Summon that created it.
        private static readonly Dictionary<int, KeyValuePair<GameObject, Summon>> owners =
            new Dictionary<int, KeyValuePair<GameObject, Summon>>();

        private sealed class Kind { internal bool Protected; }
        private static readonly ConditionalWeakTable<Summon, Kind> kinds =
            new ConditionalWeakTable<Summon, Kind>();

        private static AccessTools.FieldRef<Summon, List<Health>> summonsRef;
        private static AccessTools.FieldRef<Summon, GameObject> summonerRef;

        static PersistentSummons()
        {
            try { summonsRef = AccessTools.FieldRefAccess<Summon, List<Health>>("m_summons"); } catch { }
            try { summonerRef = AccessTools.FieldRefAccess<Summon, GameObject>("m_summoner"); } catch { }
            try { timerGetter = AccessTools.PropertyGetter(typeof(Summon), "m_DurationTimer"); } catch { }
            try { timerSetter = AccessTools.PropertySetter(typeof(Summon), "m_DurationTimer"); } catch { }
            if (summonsRef == null || summonerRef == null || timerGetter == null || timerSetter == null)
                Main.Logger.Error("PersistentSummons: Summon members missing; coexistence/unlimited duration may be inactive.");
        }

        private static readonly System.Reflection.MethodInfo timerGetter;
        private static readonly System.Reflection.MethodInfo timerSetter;

        internal static bool CoexistEnabled => Main.Settings != null && Main.Settings.EnableSummonCoexistence;
        internal static bool UnlimitedEnabled => Main.Settings != null && Main.Settings.EnableUnlimitedSummonDuration;

        internal static bool IsProtected(Summon summon)
        {
            if (summon == null || summon.SummonType != AIController.AISummonType.Summoned)
                return false;
            Kind kind;
            if (kinds.TryGetValue(summon, out kind))
                return kind.Protected;
            bool result = Classify(summon);
            kinds.Add(summon, new Kind { Protected = result });
            return result;
        }

        private static bool Classify(Summon summon)
        {
            try
            {
                GenericAbility ability = summon.AbilityOrigin;
                if (RhymersSummonRecharge.IsRhymer(ability))
                    return true;
                if (summon.EquippableOrigin != null || summon.GetComponentInParent<Item>() != null)
                    return true;
                return ability != null && ability.GetComponentInParent<Item>() != null;
            }
            catch (Exception e)
            {
                RechargeDiagnostics.Write("PERSIST ERROR classify " + e);
                return false;
            }
        }

        // ---- ownership tracking ----

        internal static int Count(Summon summon)
        {
            try
            {
                List<Health> list = summon == null || summonsRef == null ? null : summonsRef(summon);
                return list == null ? 0 : list.Count;
            }
            catch { return 0; }
        }

        internal static void Spawned(Summon summon, int before)
        {
            try
            {
                if (summon == null || summonsRef == null) return;
                List<Health> list = summonsRef(summon);
                if (list == null || list.Count <= before) return;
                Health creature = list[list.Count - 1];
                if (creature == null) return;

                GameObject go = creature.gameObject;
                owners[go.GetInstanceID()] = new KeyValuePair<GameObject, Summon>(go, summon);
                Prune();

                if (CoexistEnabled)
                {
                    DismissIfUnslotted(summon, creature);
                    if (creature == null) return;
                }

                if (UnlimitedEnabled && timerGetter != null && timerSetter != null && IsProtected(summon))
                {
                    float timer = (float)timerGetter.Invoke(summon, null);
                    if (timer > 0f)
                    {
                        // OnTimeElapsed / Update only count down while the timer is > 0.
                        timerSetter.Invoke(summon, new object[] { 0f });
                        RechargeDiagnostics.Write("PERSIST UNLIMITED source=" + RechargeDiagnostics.Id(summon) +
                            " creature=" + RechargeDiagnostics.Id(creature) + " timer=" + timer + "->0");
                    }
                }
            }
            catch (Exception e) { RechargeDiagnostics.Write("PERSIST ERROR spawn " + e); }
        }

        private static void Prune()
        {
            if (owners.Count < 32) return;
            var dead = new List<int>();
            foreach (var entry in owners)
                if (entry.Value.Key == null || entry.Value.Value == null)
                    dead.Add(entry.Key);
            foreach (int key in dead) owners.Remove(key);
        }

        private static Summon OwnerOf(GameObject creature)
        {
            KeyValuePair<GameObject, Summon> entry;
            if (creature == null || !owners.TryGetValue(creature.GetInstanceID(), out entry))
                return null;
            // Unity null: the source Summon was destroyed, fall back to vanilla cleanup.
            return entry.Key == creature && entry.Value != null ? entry.Value : null;
        }

        // A creature another, still-existing source owns, where either side is protected.
        private static bool IsForeign(Summon source, bool sourceProtected, GameObject creature)
        {
            Summon owner = OwnerOf(creature);
            if (owner == null || ReferenceEquals(owner, source))
                return false;
            return sourceProtected || IsProtected(owner);
        }

        // ---- SummonsCountNotGreaterThan activation prerequisite ----
        //
        // Figurines (and similar abilities) require "summons count <= Value", and
        // vanilla counts every Summoned creature of the character, so a figurine
        // cannot even be activated while Rhymer's skeletons are out. Recount without
        // other sources' creatures, using the same rule as DestroyAllSummoned.
        internal static void AdjustSummonCount(PrerequisiteData prereq, GameObject abilityObject,
            GameObject target, ref bool result)
        {
            try
            {
                if (!CoexistEnabled || prereq == null ||
                    prereq.Type != PrerequisiteType.SummonsCountNotGreaterThan || abilityObject == null)
                    return;
                Summon source = abilityObject.GetComponent<Summon>();
                if (source == null) source = abilityObject.GetComponentInChildren<Summon>();
                if (source == null || source.SummonType != AIController.AISummonType.Summoned)
                    return;

                GameObject summoner = null;
                GenericAbility ability = abilityObject.GetComponent<GenericAbility>();
                if (ability != null) summoner = ability.Owner;
                if (summoner == null) summoner = target;
                AIController ai = summoner == null ? null : GameUtilities.FindActiveAIController(summoner);
                List<GameObject> list = ai == null ? null : ai.SummonedCreatureList;
                if (list == null || list.Count == 0) return;

                bool sourceProtected = IsProtected(source);
                int counted = 0, skipped = 0;
                foreach (GameObject creature in list)
                {
                    if (creature == null) continue;
                    AIController creatureAi = creature.GetComponent<AIController>();
                    if (creatureAi == null || creatureAi.SummonType != AIController.AISummonType.Summoned)
                        continue;
                    if (IsForeign(source, sourceProtected, creature)) skipped++;
                    else counted++;
                }
                if (skipped == 0) return;

                bool passes = counted <= prereq.Value;
                bool adjusted = prereq.IsNot ? !passes : passes;
                // Evaluated every frame for the UI: log only when the outcome changes.
                string state = counted + "/" + skipped + "/" + result + "->" + adjusted;
                string last;
                int key = source.GetInstanceID();
                if (adjusted != result && (!lastPrereq.TryGetValue(key, out last) || last != state))
                    RechargeDiagnostics.Write("PERSIST PREREQ source=" + RechargeDiagnostics.Id(source) +
                        " counted=" + counted + " ignored=" + skipped + " max=" + prereq.Value +
                        " result=" + result + "->" + adjusted);
                lastPrereq[key] = state;
                result = adjusted;
            }
            catch (Exception e) { RechargeDiagnostics.Write("PERSIST ERROR prereq " + e); }
        }

        private static readonly Dictionary<int, string> lastPrereq = new Dictionary<int, string>();

        // ---- party summon slots ----
        //
        // Vanilla gives each party member n exactly three summon slots
        // (12+n, 18+n, 24+n of the 30-entry PartyMembers array). With several
        // sources out at once a fourth summon gets slot -1, and selecting it at the
        // start of its turn throws, freezing turn-based combat. Borrow any free
        // summon slot (12..29) instead; if there is none, dismiss the extra summon.
        private const int FirstSummonSlot = 12;
        private const int SlotCount = 30;

        internal static int FindFreeSummonSlot(PartyMemberAI summon)
        {
            PartyMemberAI[] members = PartyMemberAI.PartyMembers;
            if (members == null) return -1;
            int limit = Math.Min(SlotCount, members.Length);
            for (int i = FirstSummonSlot; i < limit; i++)
                if (members[i] == summon) return i;
            for (int i = FirstSummonSlot; i < limit; i++)
                if (members[i] == null) return i;
            return -1;
        }

        // After spawning: a party summon left without a slot cannot take a turn.
        internal static void DismissIfUnslotted(Summon source, Health creature)
        {
            try
            {
                if (creature == null) return;
                PartyMemberAI party = creature.GetComponent<PartyMemberAI>();
                if (party == null || !party.enabled || party.Slot >= 0) return;

                RechargeDiagnostics.Write("PERSIST NOSLOT source=" + RechargeDiagnostics.Id(source) +
                    " creature=" + RechargeDiagnostics.Id(creature) + " dismissed (all summon slots in use)");
                List<Health> list = summonsRef == null ? null : summonsRef(source);
                if (list != null) list.Remove(creature);
                GameObject summoner = summonerRef == null ? null : summonerRef(source);
                AIController ai = summoner == null ? null : GameUtilities.FindActiveAIController(summoner);
                if (ai != null && ai.SummonedCreatureList != null) ai.SummonedCreatureList.Remove(creature.gameObject);
                PartyMemberAI.RemoveFromActiveParty(party, true);
                GameUtilities.Destroy(creature.gameObject);
            }
            catch (Exception e) { RechargeDiagnostics.Write("PERSIST ERROR noslot " + e); }
        }

        // ---- DestroyAllSummoned scoping ----

        internal sealed class Hidden
        {
            internal List<GameObject> List;
            internal readonly List<KeyValuePair<int, GameObject>> Items = new List<KeyValuePair<int, GameObject>>();
        }

        internal static Hidden HideForeign(Summon source)
        {
            try
            {
                if (!CoexistEnabled || source == null || summonerRef == null ||
                    source.SummonType != AIController.AISummonType.Summoned)
                    return null;
                GameObject summoner = summonerRef(source);
                if (summoner == null) return null;
                AIController ai = GameUtilities.FindActiveAIController(summoner);
                List<GameObject> list = ai == null ? null : ai.SummonedCreatureList;
                if (list == null || list.Count == 0) return null;

                bool sourceProtected = IsProtected(source);
                Hidden hidden = null;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    GameObject creature = list[i];
                    if (!IsForeign(source, sourceProtected, creature))
                        continue;
                    if (hidden == null) hidden = new Hidden { List = list };
                    hidden.Items.Add(new KeyValuePair<int, GameObject>(i, creature));
                    list.RemoveAt(i);
                }
                if (hidden != null)
                    RechargeDiagnostics.Write("PERSIST KEEP source=" + RechargeDiagnostics.Id(source) +
                        " kept=" + hidden.Items.Count);
                return hidden;
            }
            catch (Exception e)
            {
                RechargeDiagnostics.Write("PERSIST ERROR hide " + e);
                return null;
            }
        }

        internal static void Restore(Hidden hidden)
        {
            if (hidden == null || hidden.List == null) return;
            try
            {
                // Items were collected from the end; reinsert in ascending index order.
                for (int i = hidden.Items.Count - 1; i >= 0; i--)
                {
                    GameObject creature = hidden.Items[i].Value;
                    if (creature == null || hidden.List.Contains(creature)) continue;
                    int index = Math.Min(hidden.Items[i].Key, hidden.List.Count);
                    hidden.List.Insert(index, creature);
                }
            }
            catch (Exception e) { RechargeDiagnostics.Write("PERSIST ERROR restore " + e); }
        }
    }

    [HarmonyPatch(typeof(Summon), "SummonCreature",
        new[] { typeof(CharacterStats), typeof(Vector3), typeof(Faction) })]
    internal static class PersistentSummonsSpawnPatch
    {
        private static void Prefix(Summon __instance, out int __state)
        {
            __state = PersistentSummons.Count(__instance);
        }

        // Runs before the trace postfix, so SUMMON SPAWN logs the final timer.
        [HarmonyPriority(Priority.High)]
        private static void Postfix(Summon __instance, int __state)
        {
            PersistentSummons.Spawned(__instance, __state);
        }
    }

    [HarmonyPatch(typeof(Summon), "DestroyAllSummoned")]
    internal static class PersistentSummonsDestroyPatch
    {
        // Runs after the trace prefix, so SUMMON END still lists the full state.
        [HarmonyPriority(Priority.Low)]
        private static void Prefix(Summon __instance, out PersistentSummons.Hidden __state)
        {
            __state = PersistentSummons.HideForeign(__instance);
        }

        private static void Finalizer(PersistentSummons.Hidden __state)
        {
            PersistentSummons.Restore(__state);
        }
    }

    [HarmonyPatch(typeof(PartyMemberAI), "GetNextAvailableSummonSlot")]
    internal static class PersistentSummonsSlotPatch
    {
        private static void Postfix(PartyMemberAI __instance, PartyMemberAI summon, ref int __result)
        {
            // Only secondary (summon) members borrow, and only slots 12..29: primary
            // slots 0..5 and companion slots 6..11 are never handed out. The
            // small-party XP bonus counts non-Secondary members, so it is unaffected.
            if (__result >= 0 || !PersistentSummons.CoexistEnabled || summon == null || !summon.Secondary) return;
            try
            {
                int slot = PersistentSummons.FindFreeSummonSlot(summon);
                if (slot < 0) return;
                RechargeDiagnostics.Write("PERSIST SLOT summoner=" + RechargeDiagnostics.Id(__instance) +
                    " summon=" + RechargeDiagnostics.Id(summon) + " borrowed=" + slot);
                __result = slot;
            }
            catch (Exception e) { RechargeDiagnostics.Write("PERSIST ERROR slot " + e); }
        }
    }

    [HarmonyPatch(typeof(PrerequisiteData), "Check",
        new[] { typeof(GameObject), typeof(GameObject), typeof(GameObject) })]
    internal static class PersistentSummonsPrerequisitePatch
    {
        private static void Postfix(PrerequisiteData __instance, GameObject __0, GameObject __1, ref bool __result)
        {
            PersistentSummons.AdjustSummonCount(__instance, __0, __1, ref __result);
        }
    }

    // Tooltip: show "until combat ends" instead of the vanilla duration.
    [HarmonyPatch(typeof(Summon), "GetDurationString")]
    internal static class PersistentSummonsTooltipPatch
    {
        private static void Postfix(Summon __instance, ref string __result)
        {
            try
            {
                if (!PersistentSummons.UnlimitedEnabled || __instance.Duration <= 0f ||
                    string.IsNullOrEmpty(__result) || !PersistentSummons.IsProtected(__instance))
                    return;
                string time = GUIUtils.CombatTime(__instance.Duration, "#0", true);
                if (!string.IsNullOrEmpty(time) && __result.Contains(time))
                    __result = __result.Replace(time, "until combat ends");
            }
            catch { }
        }
    }
}
