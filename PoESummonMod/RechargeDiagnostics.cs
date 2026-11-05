using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SkeletonCrew
{
    internal static class RechargeDiagnostics
    {
        private static readonly object gate = new object();
        private static StreamWriter writer;
        private static long sequence;
        internal static string LogPath;
        private static string version = "unknown";

        internal static void Open(string directory)
        {
            try
            {
                lock (gate)
                {
                    if (writer != null) writer.Dispose();
                    writer = null;
                    Directory.CreateDirectory(directory);
                    LogPath = Path.Combine(directory, "recharge-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".log");
                    writer = new StreamWriter(new FileStream(LogPath, FileMode.CreateNew,
                        FileAccess.Write, FileShare.ReadWrite, 4096, FileOptions.WriteThrough));
                    writer.AutoFlush = true;
                    sequence = 0;
                }
                Write("SESSION version=" + version + " UTC timestamps; recharge=" + RhymersSummonRecharge.Enabled);
            }
            catch (Exception e)
            {
                // Logging must never interrupt gameplay or call Unity's logger recursively.
                try { Main.Logger.Error("Cannot open recharge diagnostics: " + e.Message); } catch { }
            }
        }

        internal static void Initialize(string modPath, string modVersion)
        {
            if (!string.IsNullOrEmpty(modVersion)) version = modVersion;
            Open(Path.Combine(modPath, "Logs"));
            Application.logMessageReceivedThreaded -= UnityLog;
            Application.logMessageReceivedThreaded += UnityLog;
            GameState.OnCombatStart -= CombatStart;
            GameState.OnCombatStart += CombatStart;
            GameState.OnCombatEnd -= CombatEnd;
            GameState.OnCombatEnd += CombatEnd;
            Main.Logger.Log("Recharge diagnostic log: " + LogPath);
        }

        private static void CombatStart(object sender, EventArgs e) { Write("COMBAT START"); }
        private static void CombatEnd(object sender, EventArgs e) { Write("COMBAT END"); }
        private static void UnityLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                Write("UNITY " + type + " " + message + Environment.NewLine + stack);
        }

        // Components resolve to their GameObject, so one creature has one ID on
        // every line (TURN, HIT, SUMMON, EQUIPMENT) regardless of which
        // component the caller passed.
        internal static string Id(UnityEngine.Object obj)
        {
            try
            {
                if (obj == null) return "null/destroyed";
                Component component = obj as Component;
                GameObject go = component != null ? component.gameObject : obj as GameObject;
                return go != null ? go.name + "#" + go.GetInstanceID() : obj.name + "#" + obj.GetInstanceID();
            }
            catch { return "unavailable"; }
        }

        internal static void Write(string message)
        {
            lock (gate)
            {
                if (writer == null) return;
                try { writer.WriteLine(DateTime.UtcNow.ToString("O") + " #" + (++sequence) + " " + message); }
                catch
                {
                    try { writer.Dispose(); } catch { }
                    writer = null;
                }
            }
        }
    }

    // Bracket round/turn transitions and the mod callbacks. Finalizers report
    // managed exceptions without swallowing them or changing game behavior.
    [HarmonyPatch]
    internal static class RechargeDiagnosticBoundaryPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(TacticalTurnEvent), "OnPreStart");
            yield return AccessTools.Method(typeof(TacticalTurnEvent), "OnStart");
            yield return AccessTools.Method(typeof(TacticalRoundStartEvent), "OnStart");
            yield return AccessTools.Method(typeof(TacticalRoundStartEvent), "ConsumeRound");
            foreach (string method in new[] { "Start", "Before", "After", "GetContributorOwner", "ResetBattle", "Destroy", "Validate" })
                yield return AccessTools.Method(typeof(RhymersSummonRecharge), method);
        }

        private static void Prefix(MethodBase __originalMethod, object __instance)
        {
            if (__originalMethod.Name != "Validate")
                RechargeDiagnostics.Write("ENTER " + __originalMethod.DeclaringType.Name + "." + __originalMethod.Name);
            TacticalTurnEvent turn = __instance as TacticalTurnEvent;
            if (turn != null)
            {
                RechargeDiagnostics.Write("TURN actor=" + RechargeDiagnostics.Id(turn.Actor));
                if (__originalMethod.Name == "OnStart") SummonTrace.Turn(turn.Actor);
            }
        }

        private static void Finalizer(MethodBase __originalMethod, Exception __exception)
        {
            string method = __originalMethod.DeclaringType.Name + "." + __originalMethod.Name;
            if (__exception != null) RechargeDiagnostics.Write("EXCEPTION " + method + " " + __exception);
            else if (__originalMethod.Name != "Validate") RechargeDiagnostics.Write("EXIT " + method);
        }
    }
}
