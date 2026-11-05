using System;
using System.Text;
using HarmonyLib;
using SkeletonCrew;
using UnityEngine;

namespace PoESummonMod
{
    internal static class ChanterWurmBurnPresentation
    {
        private static Texture2D icon;
        private static bool iconLoaded;

        internal static bool IsBurn(StatusEffect effect)
        {
            return effect != null && effect.Params != null &&
                effect.Params.Tag == "SkeletonCrew_WurmBurn" &&
                effect.Params.AffectsStat == StatusEffect.ModifiedStat.Damage &&
                effect.Params.DmgType == DamagePacket.DamageType.Burn;
        }

        internal static Texture2D GetIcon()
        {
            if (!iconLoaded)
            {
                iconLoaded = true;
                GenericAbility spell = GameResources.LoadPrefab<GenericAbility>("fan_of_flames", false);
                if (spell != null)
                    icon = spell.Icon;
                if (icon == null)
                    Main.Logger.Error("Could not load the Fan of Flames icon for Wurm Burn.");
            }
            return icon;
        }

        [ThreadStatic]
        private static Tick activeTick;

        internal sealed class Tick
        {
            internal bool Tracked;
            internal Tick Previous;
            internal Health Target;
            internal GameObject Source;
            internal bool UsesEndurance;
            internal float Before;
            internal float RawAmount;
            internal float Minimum;
            internal float DRMultiplier;
            internal DamageInfo Info;
        }

        internal static Tick Capture(Health health, StatusEffect effect, bool hideDamage,
            GameObject source, float amount)
        {
            // Even unrelated nested direct-damage calls must suspend the outer
            // scope; otherwise their mitigation could overwrite its log data.
            var tick = new Tick { Previous = activeTick };
            activeTick = tick;
            if (hideDamage || !IsBurn(effect) || health == null || health.Dead || health.Unconscious)
                return tick;
            bool endurance = health.MaxStamina > 0;
            tick.Tracked = true;
            tick.Target = health;
            tick.Source = source;
            tick.RawAmount = amount;
            tick.UsesEndurance = endurance;
            tick.Before = endurance ? health.CurrentStamina : health.CurrentHealth;
            return tick;
        }

        internal static void End(Tick tick)
        {
            if (tick != null && ReferenceEquals(activeTick, tick))
                activeTick = tick.Previous;
        }

        internal static Tick CaptureMitigation(CharacterStats target, float amount,
            DamagePacket.DamageType type, DamageInfo info, GameObject source, float multiplier)
        {
            Tick tick = activeTick;
            if (tick == null || !tick.Tracked || target == null || target.gameObject != tick.Target.gameObject ||
                source != tick.Source || info != null || type != DamagePacket.DamageType.Burn)
                return null;
            // Read the same inputs as the native helper without replacing its null
            // DamageInfo or running damage/mitigation a second time.
            tick.Info = new DamageInfo
            {
                Target = target.gameObject,
                OtherOwner = source,
                DefendedBy = CharacterStats.DefenseType.None,
                DamageBase = amount,
                DamageType = type,
                Damage = new DamagePacket { Type = type, Minimum = amount, Maximum = amount },
                PreBypassDT = target.CalcDT(type, false) * multiplier,
                DRRating = target.CalcDR(type) * multiplier
            };
            tick.Info.DTRating = tick.Info.PreBypassDT;
            tick.Minimum = target.CalcMinDamage(amount, false, null);
            tick.DRMultiplier = multiplier;
            return tick;
        }

        internal static void RecordMitigation(Tick tick, float result)
        {
            if (tick == null || tick.Info == null)
                return;
            tick.Info.DTAdjustedDamage = result;
            float reduced = (tick.Info.DamageBase - tick.Info.DTRating) *
                Math.Max(0, 1 - tick.Info.DRRating / 100);
            tick.Info.IsMin = !float.IsPositiveInfinity(tick.Info.DTRating) && reduced < tick.Minimum;
        }

        internal static float DamageLost(Tick tick, float after)
        {
            if (tick == null || !tick.Tracked || float.IsNaN(after) || float.IsInfinity(after))
                return 0;
            // Overkill must not report more vitality than the victim had remaining.
            return Math.Max(0, Math.Min(tick.Before, tick.Before - after));
        }

        internal static void Report(Health health, GameObject source, StatusEffect effect, Tick tick)
        {
            if (tick == null || !tick.Tracked || health == null || tick.Info == null || tick.Info.DamageBase <= 0)
                return;
            float lost = DamageLost(tick, tick.UsesEndurance ? health.CurrentStamina : health.CurrentHealth);
            tick.Info.FinalAdjustedDamage = lost;
            string sourceName = source != null ? CharacterStats.NameColored(source) : "Wurm Burn";
            string message = GUIUtils.Format(123, CharacterStats.NameColored(health.gameObject),
                FormatDamage(lost) + " " + GUIUtils.GetDamageTypeString(DamagePacket.DamageType.Burn), sourceName) +
                GUIUtils.Format(1731, "Wurm Burn");
            global::Console.AddBatchedMessage(message, Breakdown(tick, lost),
                AttackBase.GetMessageColor(source == null ? null : source.GetComponent<Faction>(),
                    health.GetComponent<Faction>()), new global::Console.BatchedAttackData(tick.Info));
        }

        internal static string FormatDamage(float value)
        {
            // The old ReportDamage formatter rounded sub-0.5 hits to "0".
            return value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        internal static string Breakdown(Tick tick, float lost)
        {
            DamageInfo info = tick.Info;
            var text = new StringBuilder("Wurm Burn (damage over time)\n");
            text.Append("No new attack roll. The original hit already determines the burn's damage and duration.\n");
            text.Append("Tick damage: ").Append(FormatDamage(tick.RawAmount));
            if (tick.RawAmount > 0)
                text.Append(" x ").Append(FormatDamage(info.DamageBase / tick.RawAmount)).Append(" (source modifiers)");
            text.Append(" = ").Append(FormatDamage(info.DamageBase)).Append(" Burn.\n");
            if (float.IsPositiveInfinity(info.PreBypassDT))
                text.Append("Burn immunity: 0 damage.\n");
            else
            {
                text.Append("DR: ").Append(FormatDamage(info.DTRating));
                if (tick.DRMultiplier > 0)
                    text.Append(" (").Append(FormatDamage(tick.DRMultiplier * 100)).Append("% of ")
                        .Append(FormatDamage(info.DTRating / tick.DRMultiplier)).Append(")");
                text.Append(".\n(").Append(FormatDamage(info.DamageBase)).Append(" - ")
                    .Append(FormatDamage(info.DTRating)).Append(")");
                if (info.DRRating > 0)
                    text.Append(" x ").Append(FormatDamage(Math.Max(0, 1 - info.DRRating / 100)));
                text.Append("; minimum damage: ").Append(FormatDamage(tick.Minimum));
                text.Append(". Result: ").Append(FormatDamage(info.DTAdjustedDamage)).Append(" Burn");
                if (info.IsMin) text.Append(" (minimum damage)");
                text.Append(".\n");
            }
            text.Append(tick.UsesEndurance ? "Endurance lost: " : "Health lost: ")
                .Append(FormatDamage(lost)).Append(".");
            return text.ToString();
        }
    }

    [HarmonyPatch(typeof(StatusEffect), "BundleName", MethodType.Getter)]
    internal static class ChanterWurmBurnNamePatch
    {
        private static bool Prefix(StatusEffect __instance, ref string __result)
        {
            if (!ChanterWurmBurnPresentation.IsBurn(__instance))
                return true;
            __result = "Wurm Burn";
            return false;
        }
    }

    [HarmonyPatch(typeof(StatusEffect), "GetDisplayIcon")]
    internal static class ChanterWurmBurnIconPatch
    {
        private static bool Prefix(StatusEffect __instance, ref Texture2D __result)
        {
            if (!ChanterWurmBurnPresentation.IsBurn(__instance))
                return true;
            Texture2D icon = ChanterWurmBurnPresentation.GetIcon();
            if (icon == null)
                return true;
            __result = icon;
            return false;
        }
    }

    [HarmonyPatch(typeof(Health), "ApplyDamageDirectly", new[] {
        typeof(float), typeof(DamagePacket.DamageType), typeof(GameObject), typeof(StatusEffect), typeof(bool) })]
    internal static class ChanterWurmBurnLogPatch
    {
        private static void Prefix(Health __instance, float __0, GameObject __2, StatusEffect __3, bool __4,
            out ChanterWurmBurnPresentation.Tick __state)
        {
            __state = ChanterWurmBurnPresentation.Capture(__instance, __3, __4, __2, __0);
        }

        private static void Postfix(Health __instance, GameObject __2, StatusEffect __3,
            ChanterWurmBurnPresentation.Tick __state)
        {
            ChanterWurmBurnPresentation.Report(__instance, __2, __3, __state);
        }

        private static void Finalizer(ChanterWurmBurnPresentation.Tick __state)
        {
            ChanterWurmBurnPresentation.End(__state);
        }
    }

    [HarmonyPatch(typeof(CharacterStats), "AdjustDamageByDTDR", new[] {
        typeof(float), typeof(DamagePacket.DamageType), typeof(DamageInfo), typeof(GameObject), typeof(float) })]
    internal static class ChanterWurmBurnMitigationLogPatch
    {
        private static void Prefix(CharacterStats __instance, float __0, DamagePacket.DamageType __1,
            DamageInfo __2, GameObject __3, float __4, out ChanterWurmBurnPresentation.Tick __state)
        {
            __state = ChanterWurmBurnPresentation.CaptureMitigation(__instance, __0, __1, __2, __3, __4);
        }

        private static void Postfix(float __result, ChanterWurmBurnPresentation.Tick __state)
        {
            ChanterWurmBurnPresentation.RecordMitigation(__state, __result);
        }
    }
}
