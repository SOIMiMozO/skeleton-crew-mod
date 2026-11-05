using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using PoESummonMod;
using UnityEngine;

namespace SkeletonCrew
{
    // Diagnostic trace of every creature created by Summon.SummonCreature
    // (spells, invocations, items such as figurines, scripts), its duration
    // timer, and how it leaves play. Read only: it never changes game state.
    //
    // Vanilla lifetime paths (from Assembly-CSharp):
    //   timer = Summon.Duration, set in SummonCreature
    //   turn-based: Summon.OnTimeElapsed -> GenericAbility.DeductDuration, which
    //     drops the timer to 0 once less than one full round remains
    //     -> DestroyAllSummoned
    //   real time:  Summon.Update -> DestroyAllSummoned / DeactivateSummons
    //   re-cast:    InitiateSummoning -> DestroyAllSummoned (previous summons)
    //   combat end: HandleSummonOnCombatEnd -> DestroyAllSummoned
    //   item:       Equippable.UnEquip / Summon.OnDisable -> DeactivateSummons
    internal static class SummonTrace
    {
        private sealed class Life
        {
            internal string Creature;
            internal string Source;
            internal DateTime Spawned;
            internal int SpawnRound;
            internal int Turns;
            internal string LastCause = "none";
        }

        // Keyed by creature GameObject instance ID.
        private static readonly Dictionary<int, Life> lives = new Dictionary<int, Life>();

        private static AccessTools.FieldRef<Summon, List<Health>> summonsRef;
        private static AccessTools.FieldRef<Summon, GameObject> summonerRef;
        private static AccessTools.FieldRef<Summon, List<AIController>> destroyListRef;
        private static MethodInfo timerGetter;
        private static FieldInfo totalRounds;

        static SummonTrace()
        {
            // Each lookup is independent: a missing member only blanks its field.
            try { summonsRef = AccessTools.FieldRefAccess<Summon, List<Health>>("m_summons"); } catch { }
            try { summonerRef = AccessTools.FieldRefAccess<Summon, GameObject>("m_summoner"); } catch { }
            try { destroyListRef = AccessTools.FieldRefAccess<Summon, List<AIController>>("TacticalDestroyList"); } catch { }
            try { timerGetter = AccessTools.PropertyGetter(typeof(Summon), "m_DurationTimer"); } catch { }
            try { totalRounds = AccessTools.Field(typeof(TacticalRoundStartEvent), "TotalRounds"); } catch { }
        }

        // ---- helpers ----

        private static string Id(UnityEngine.Object obj) { return RechargeDiagnostics.Id(obj); }

        private static string F(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Safe(Func<string> value)
        {
            try { return value(); } catch (Exception e) { return "err:" + e.GetType().Name; }
        }

        internal static List<Health> Summons(Summon summon)
        {
            try { return summon == null || summonsRef == null ? null : summonsRef(summon); }
            catch { return null; }
        }

        internal static int Count(Summon summon)
        {
            List<Health> list = Summons(summon);
            return list == null ? 0 : list.Count;
        }

        private static GameObject Summoner(Summon summon)
        {
            try { return summon == null || summonerRef == null ? null : summonerRef(summon); }
            catch { return null; }
        }

        internal static float Timer(Summon summon)
        {
            try { return summon == null || timerGetter == null ? float.NaN : (float)timerGetter.Invoke(summon, null); }
            catch { return float.NaN; }
        }

        private static int Round()
        {
            try { return totalRounds == null ? -1 : (int)totalRounds.GetValue(null); }
            catch { return -1; }
        }

        private static string RoundTime()
        {
            return Safe(() => F(TacticalModeManager.EffectTimePerRound));
        }

        private static string ListSummons(Summon summon)
        {
            List<Health> list = Summons(summon);
            if (list == null || list.Count == 0) return "[]";
            var sb = new StringBuilder("[");
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                Health h = list[i];
                sb.Append(Id(h));
                if (h != null)
                    sb.Append(Safe(() => " end=" + F(h.CurrentStamina) + " hp=" + F(h.CurrentHealth) +
                        (h.Dead ? " dead" : "") + (h.Unconscious ? " unconscious" : "") +
                        (h.gameObject.activeInHierarchy ? "" : " inactive")));
            }
            return sb.Append("]").ToString();
        }

        // Where the summon came from: ability, spell class, item, Chanter invocation.
        private static string Origin(Summon summon)
        {
            var sb = new StringBuilder();
            GenericAbility ability = null;
            try { ability = summon.AbilityOrigin; } catch { }
            if (ability == null)
                sb.Append("ability=none");
            else
            {
                sb.Append("ability=").Append(Id(ability))
                  .Append(" abilityType=").Append(ability.GetType().Name)
                  .Append(" effectType=").Append(Safe(() => ability.EffectType.ToString()));
                GenericSpell spell = ability as GenericSpell;
                if (spell != null) sb.Append(" spellClass=").Append(Safe(() => spell.SpellClass.ToString()));
            }

            Item item = null;
            try
            {
                item = summon.GetComponentInParent<Item>();
                if (item == null && ability != null) item = ability.GetComponentInParent<Item>();
            }
            catch { }
            sb.Append(" item=").Append(item == null ? "none" : Id(item) + " itemName=\"" + Safe(() => item.Name) + "\"");

            string invocation = ChanterSummonOrigin.Describe(summon);
            if (invocation != null) sb.Append(" chanterInvocation=").Append(invocation);
            return sb.ToString();
        }

        // Compact call chain so a removal can be attributed to its trigger
        // (timer, re-cast, unequip, combat end, ...).
        private static string Caller()
        {
            try
            {
                StackFrame[] frames = new StackTrace(2, false).GetFrames();
                if (frames == null) return "unavailable";
                var sb = new StringBuilder();
                int count = 0;
                foreach (StackFrame frame in frames)
                {
                    MethodBase method = frame.GetMethod();
                    if (method == null) continue;
                    string name = (method.DeclaringType == null ? "" : method.DeclaringType.Name + ".") + method.Name;
                    if (name.IndexOf("SummonTrace", StringComparison.Ordinal) >= 0) continue;
                    if (sb.Length > 0) sb.Append(" < ");
                    sb.Append(name);
                    if (++count == 6) break;
                }
                return sb.ToString();
            }
            catch { return "unavailable"; }
        }

        private static Life Find(GameObject creature)
        {
            Life life;
            return creature != null && lives.TryGetValue(creature.GetInstanceID(), out life) ? life : null;
        }

        // ---- events ----

        internal static void Spawned(Summon summon, CharacterStats prefab, int before)
        {
            try
            {
                if (summon == null) return;
                List<Health> list = Summons(summon);
                Health creature = list != null && list.Count > before ? list[list.Count - 1] : null;
                GameObject summoner = Summoner(summon);
                float timer = Timer(summon);

                var sb = new StringBuilder("SUMMON SPAWN creature=")
                    .Append(creature == null ? "none(not added)" : Id(creature))
                    .Append(" prefab=").Append(prefab == null ? "null" : Safe(() => prefab.name))
                    .Append(" summoner=").Append(Id(summoner))
                    .Append(" source=").Append(Id(summon)).Append(" sourceType=").Append(summon.GetType().Name)
                    .Append(' ').Append(Origin(summon))
                    .Append(" duration=").Append(F(summon.Duration))
                    .Append(" timer=").Append(F(timer))
                    .Append(" roundTime=").Append(RoundTime())
                    .Append(" timerRounds=").Append(Safe(() => TacticalModeManager.SecondsToRounds(timer).ToString()))
                    .Append(" summonerDurationMult=").Append(Safe(() =>
                    {
                        CharacterStats stats = summoner == null ? null : summoner.GetComponent<CharacterStats>();
                        return stats == null ? "n/a" : F(stats.StatEffectDurationMultiplier);
                    }))
                    .Append(" tactical=").Append(Safe(() => TacticalModeManager.IsTacticalModeEnabled.ToString()))
                    .Append(" summonType=").Append(Safe(() => summon.SummonType.ToString()))
                    .Append(" lifetimeExternal=").Append(summon.LifetimeExternallyManaged)
                    .Append(" destroyAfterSummonEnds=").Append(Safe(() => summon.DestroyAfterSummonEnds.ToString()))
                    .Append(" activeSummons=").Append(list == null ? 0 : list.Count)
                    .Append(" round=").Append(Round());
                RechargeDiagnostics.Write(sb.ToString());

                if (creature != null)
                {
                    lives[creature.gameObject.GetInstanceID()] = new Life
                    {
                        Creature = Id(creature),
                        Source = Id(summon),
                        Spawned = DateTime.UtcNow,
                        SpawnRound = Round()
                    };
                }
            }
            catch (Exception e) { RechargeDiagnostics.Write("SUMMON TRACE ERROR spawn " + e); }
        }

        internal static void Tick(Summon summon, object[] args, float before)
        {
            try
            {
                float after = Timer(summon);
                string timeType = args != null && args.Length > 1 && args[1] != null ? args[1].ToString() : "?";
                bool unchanged = before == after || (float.IsNaN(before) && float.IsNaN(after));
                // GameTime/None ticks are ignored by vanilla; log only effective ones.
                if (Count(summon) == 0 && unchanged) return;
                if (unchanged && timeType != "EffectTime") return;
                string elapsed = args != null && args.Length > 2 && args[2] is float ? F((float)args[2]) : "?";
                RechargeDiagnostics.Write("SUMMON TICK source=" + Id(summon) +
                    " caller=" + Id(args != null && args.Length > 0 ? args[0] as UnityEngine.Object : null) +
                    " timeType=" + timeType + " elapsed=" + elapsed +
                    " timer=" + F(before) + "->" + F(after) +
                    " roundTime=" + RoundTime() +
                    " round=" + Round() + " summons=" + ListSummons(summon));
            }
            catch (Exception e) { RechargeDiagnostics.Write("SUMMON TRACE ERROR tick " + e); }
        }

        internal static void Ending(Summon summon, string method, object[] args)
        {
            try
            {
                if (summon == null) return;
                int count = Count(summon);
                if (method == "DestroyTacticalSummons")
                {
                    List<AIController> pending = null;
                    try { pending = destroyListRef == null ? null : destroyListRef(summon); } catch { }
                    if (pending == null || pending.Count == 0) return;
                    count += pending.Count;
                }
                if (count == 0) return;

                string call = method;
                if (args != null && args.Length > 0)
                {
                    var parts = new string[args.Length];
                    for (int i = 0; i < args.Length; i++)
                        parts[i] = args[i] == null ? "null" : args[i].ToString();
                    call += "(" + string.Join(", ", parts) + ")";
                }
                RechargeDiagnostics.Write("SUMMON END cause=" + call + " source=" + Id(summon) +
                    " timer=" + F(Timer(summon)) + " duration=" + F(summon.Duration) +
                    " round=" + Round() + " summons=" + ListSummons(summon) +
                    " via=" + Caller());

                List<Health> list = Summons(summon);
                if (list != null)
                    foreach (Health h in list)
                    {
                        Life life = h == null ? null : Find(h.gameObject);
                        if (life != null) life.LastCause = method;
                    }
            }
            catch (Exception e) { RechargeDiagnostics.Write("SUMMON TRACE ERROR end " + e); }
        }

        internal static void Down(Health health, string kind, GameObject by)
        {
            try
            {
                Life life = health == null ? null : Find(health.gameObject);
                if (life == null) return;
                life.LastCause = kind;
                RechargeDiagnostics.Write("SUMMON " + kind.ToUpperInvariant() + " creature=" + Id(health) +
                    " by=" + Id(by) + " end=" + F(health.CurrentStamina) + " hp=" + F(health.CurrentHealth) +
                    " round=" + Round());
            }
            catch (Exception e) { RechargeDiagnostics.Write("SUMMON TRACE ERROR down " + e); }
        }

        internal static void Gone(CharacterStats stats, string evt)
        {
            try
            {
                if (stats == null) return;
                int key = stats.gameObject.GetInstanceID();
                Life life;
                if (!lives.TryGetValue(key, out life)) return;
                int round = Round();
                RechargeDiagnostics.Write("SUMMON GONE creature=" + life.Creature + " event=" + evt +
                    " source=" + life.Source + " lastCause=" + life.LastCause +
                    " lived=" + F((float)(DateTime.UtcNow - life.Spawned).TotalSeconds) + "s" +
                    " rounds=" + life.SpawnRound + "->" + round +
                    " (" + (round >= 0 && life.SpawnRound >= 0 ? (round - life.SpawnRound).ToString() : "?") + ")" +
                    " turns=" + life.Turns);
                if (evt == "OnDestroy") lives.Remove(key);
            }
            catch (Exception e) { RechargeDiagnostics.Write("SUMMON TRACE ERROR gone " + e); }
        }

        internal static void Turn(UnityEngine.Object actor)
        {
            try
            {
                Component component = actor as Component;
                Life life = component == null ? null : Find(component.gameObject);
                if (life != null) life.Turns++;
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(Summon), "SummonCreature",
        new[] { typeof(CharacterStats), typeof(Vector3), typeof(Faction) })]
    internal static class SummonTraceSpawnPatch
    {
        private static void Prefix(Summon __instance, out int __state)
        {
            __state = SummonTrace.Count(__instance);
        }

        private static void Postfix(Summon __instance, CharacterStats __0, int __state)
        {
            SummonTrace.Spawned(__instance, __0, __state);
        }
    }

    [HarmonyPatch]
    internal static class SummonTraceTickPatch
    {
        private static MethodBase TargetMethod()
        {
            // Summon declares one OnTimeElapsed(AIController, TimeType, float).
            return AccessTools.DeclaredMethod(typeof(Summon), "OnTimeElapsed");
        }

        private static void Prefix(Summon __instance, out float __state)
        {
            __state = SummonTrace.Timer(__instance);
        }

        private static void Postfix(Summon __instance, object[] __args, float __state)
        {
            SummonTrace.Tick(__instance, __args, __state);
        }
    }

    [HarmonyPatch]
    internal static class SummonTraceEndPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            // Re-cast (InitiateSummoning), combat end, unequip and Summon.OnDisable all
            // funnel into these; the logged call chain names the trigger.
            foreach (string name in new[] { "DestroyAllSummoned", "DeactivateSummons", "DestroyTacticalSummons", "OnDestroy" })
            {
                MethodBase method = AccessTools.DeclaredMethod(typeof(Summon), name);
                if (method != null) yield return method;
            }
        }

        private static void Prefix(MethodBase __originalMethod, Summon __instance, object[] __args)
        {
            SummonTrace.Ending(__instance, __originalMethod.Name, __args);
        }
    }

    [HarmonyPatch]
    internal static class SummonTraceDownPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(Health), "HandleDeath");
            yield return AccessTools.DeclaredMethod(typeof(Health), "HandleUnconscious");
        }

        private static void Prefix(MethodBase __originalMethod, Health __instance, GameObject __0)
        {
            SummonTrace.Down(__instance, __originalMethod.Name == "HandleDeath" ? "death" : "unconscious", __0);
        }
    }

    [HarmonyPatch]
    internal static class SummonTraceGonePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(CharacterStats), "OnDisable");
            yield return AccessTools.DeclaredMethod(typeof(CharacterStats), "OnDestroy");
        }

        private static void Prefix(MethodBase __originalMethod, CharacterStats __instance)
        {
            SummonTrace.Gone(__instance, __originalMethod.Name);
        }
    }
}
