// Standalone regression harness: compile with the production ChanterSummonScaling.cs.
// Stubs model the game's class-table refresh and level formulas; no Unity runtime needed.
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using PoESummonMod;

namespace HarmonyLib
{
    public enum MethodType { Getter }
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch() { }
        public HarmonyPatch(Type type, string name) { }
        public HarmonyPatch(Type type, string name, MethodType methodType) { }
        public HarmonyPatch(Type type, string name, Type[] args) { }
    }
    public static class AccessTools
    {
        public static MethodInfo Method(Type type, string name, Type[] args)
        { return type.GetMethod(name, args); }
    }
}
namespace UnityEngine
{
    public struct Color { }
    public class Texture2D { }
    public class Component
    {
        public GameObject gameObject;
        public Transform transform { get { return gameObject.transform; } }
        public T GetComponent<T>() where T : class { return gameObject.GetComponent<T>(); }
        public T[] GetComponents<T>() where T : class { return gameObject.GetComponents<T>(); }
    }
    public class Transform
    {
        public Transform Parent;
        public void SetParent(Transform parent, bool worldPositionStays) { Parent = parent; }
    }
    public static class Object
    {
        public static T Instantiate<T>(T original) where T : class
        {
            GameObject source = original as GameObject;
            var result = new GameObject { name = source.name + "(Clone)" };
            foreach (Component c in source.Components)
            {
                var copy = (Component)typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(c, null);
                result.Add(copy);
            }
            return result as T;
        }
    }
    public class GameObject
    {
        public string name;
        public CharacterStats Stats;
        public Transform transform = new Transform();
        public List<Component> Components = new List<Component>();
        public void Add(Component c) { c.gameObject = this; Components.Add(c); }
        public T GetComponent<T>() where T : class { return (Stats as T) ?? Components.OfType<T>().FirstOrDefault(); }
        public T[] GetComponents<T>() where T : class { return Components.OfType<T>().ToArray(); }
    }
    public static class Mathf { public static int Max(int a, int b) { return Math.Max(a, b); } }
}
namespace SkeletonCrew
{
    public class Settings { public bool EnableChanterSummonScaling; }
    public class Logger { public void Error(string s) { throw new Exception(s); } }
    public static class Main { public static Settings Settings = new Settings(); public static Logger Logger = new Logger(); }
    public static class RechargeDiagnostics { public static void Write(string s) { } public static string Id(object o) { return ""; } }
    public static class SummonEquipmentQuality
    {
        public static int GetBonusLevel(CharacterStats stats, bool requireChanter)
        {
            return stats.ChanterLevel >= 0 ? stats.ChanterLevel :
                stats.CharacterClass == CharacterStats.Class.Chanter ? stats.Level : 0;
        }
        public static int GetArmorBonus(int level) { return level >= 16 ? 8 : level >= 12 ? 4 : level >= 8 ? 2 : level >= 4 ? 1 : 0; }
    }
}
public class GenericAbility : UnityEngine.Component
{
    public UnityEngine.Texture2D Icon;
    public enum AbilityType { WeaponOrShield }
    public UnityEngine.GameObject Owner;
}
public class GenericSpell : GenericAbility { public CharacterStats.Class SpellClass; }
public class AttackBase : UnityEngine.Component
{
    public static UnityEngine.Color GetMessageColor(Faction a, Faction b) { return new UnityEngine.Color(); }
    public GenericAbility AbilityOrigin, TriggeringAbility;
    public UnityEngine.GameObject Owner;
    public int AccuracyBonus;
    public float DamageMultiplier = 1;
    public float DTBypass;
}
public class AttackRoar : AttackBase { }
public class Summon : AttackBase { }
public class DamagePacket { public enum DamageType { Slash, All, Raw, Burn } public DamageType Type; public float Minimum, Maximum; }
public class DamageInfo
{
    public UnityEngine.GameObject Target, OtherOwner;
    public CharacterStats.DefenseType DefendedBy;
    public DamagePacket Damage;
    public DamagePacket.DamageType DamageType;
    public float DamageBase, DTAdjustedDamage, PreBypassDT, DTRating, DRRating;
    public bool IsMin;
    public AttackBase Attack;
    public bool IsMiss, IsGraze, IsCriticalHit;
    public float FinalAdjustedDamage;
}
public class Faction : UnityEngine.Component { }
public static class GUIUtils
{
    public static string Format(int id, params object[] args) { return id == 123 ? string.Format("{0} takes {1} from {2}", args) : " (" + args[0] + ")"; }
    public static string GetDamageTypeString(DamagePacket.DamageType type) { return type.ToString(); }
}
public static class Console
{
    public class BatchedAttackData { public DamageInfo DamageInfo; public BatchedAttackData(DamageInfo info) { DamageInfo = info; } }
    public class Entry { public string Message, Hover; public DamageInfo Info; }
    public static List<Entry> Entries = new List<Entry>();
    public static void AddBatchedMessage(string msg, string hover, UnityEngine.Color color, BatchedAttackData data)
    { Entries.Add(new Entry { Message = msg, Hover = hover, Info = data.DamageInfo }); }
    public static void WriteLine(string value) { System.Console.WriteLine(value); }
}
public class Health : UnityEngine.Component
{
    public bool Dead, Unconscious;
    public float MaxStamina = 100, CurrentStamina = 100, CurrentHealth = 200;
    public List<float> ReportedDamage = new List<float>();
    public void ReportDamage(string target, float amount, UnityEngine.GameObject source, StatusEffect effect) { ReportedDamage.Add(amount); }
}
public static class GameResources
{
    public static GenericAbility Fan = new GenericAbility { Icon = new UnityEngine.Texture2D() };
    public static T LoadPrefab<T>(string name, bool instantiate) where T : class
    {
        if (name != "fan_of_flames" || instantiate) throw new Exception("Unexpected asset load");
        return Fan as T;
    }
}
public class StatusEffectParams
{
    public enum IntervalRateType { Damage }
    public string Tag;
    public StatusEffect.ModifiedStat AffectsStat;
    public DamagePacket.DamageType DmgType;
    public StatusEffect.ApplyType Apply;
    public IntervalRateType IntervalRate;
    public float Duration, Value, TacticalMultiplier;
    public bool IsHostile;
}
public class StatusEffect
{
    public enum ModifiedStat { Damage }
    public enum ApplyType { ApplyOverTime }
    public StatusEffectParams Params;
    public float Duration;
    public bool m_forceStackable;
    public static StatusEffect Create(UnityEngine.GameObject owner, Equippable equipment, StatusEffectParams p,
        GenericAbility.AbilityType type, DamageInfo hit, bool deleteOnClear)
    {
        return new StatusEffect { Params = p, Duration = p.Duration * (hit.IsGraze ? 0.5f : hit.IsCriticalHit ? 1.5f : 1f) };
    }
}
public class Equippable : UnityEngine.Component { public Equippable Prefab; }
public class Shield : Equippable { }
public class Armor : UnityEngine.Component { public float DamageThreshhold; }
public class Equipment : UnityEngine.Component { public EquipmentSet DefaultEquippedItems = new EquipmentSet(); }
public class EquipmentSet { public Equippable PrimaryWeapon, SecondaryWeapon, Chest; }
public class AttackData
{
    public static AttackData Instance = new AttackData();
    public int AccuracyPerLevel = 3, DefensePerLevel = 3;
}
public class CharacterStats : UnityEngine.Component
{
    public enum DefenseType { None }
    public float DamageThreshold = 10, DamageReduction;
    public float CalcDT(DamagePacket.DamageType type, bool piercing) { return DamageThreshold; }
    public float CalcDR(DamagePacket.DamageType type) { return DamageReduction; }
    public float CalcMinDamage(float amount, bool crushing, DamageInfo info) { return amount * 0.2f; }
    public static string NameColored(UnityEngine.GameObject obj) { return obj.name; }
    public enum Class { Wurm, WillOWisp, Drake, Ogre, Phantom, PlayerTrap, Chanter }
    public Class CharacterClass;
    public int ChanterLevel = -1;
    public float StatDamageHealMultiplier = 1.2f, DifficultyDamageMultiplier = 1.25f;
    public float GetDifficultyDamageMultiplier(Health health) { return DifficultyDamageMultiplier; }
    public List<StatusEffect> Effects = new List<StatusEffect>();
    public bool ApplyStatusEffectImmediate(StatusEffect effect) { Effects.Add(effect); return true; }
    public int Level, HealthStaminaPerLevel;
    public int MeleeAccuracyBonus, RangedAccuracyBonus, BaseDeflection, BaseFortitude, BaseReflexes, BaseWill;
    public float MaxHealth, MaxStamina;
    public float Difficulty = 1, ConstitutionMultiplier = 1;
    public int ScaledLevel { get { return Math.Max(1, (int)Math.Floor(Level * Difficulty)); } }
    public float Stamina { get { return (MaxStamina + (ScaledLevel - 1) * HealthStaminaPerLevel) * ConstitutionMultiplier; } }
    public float Health { get { return (MaxHealth + (ScaledLevel - 1) * HealthStaminaPerLevel) * ConstitutionMultiplier * 6; } }
}
public static class DataManager
{
    public static void AdjustFromData(ref object component)
    {
        var s = (CharacterStats)component;
        int[] health = { 48, 36, 72, 84, 30 };
        int[] growth = { 16, 12, 24, 28, 10 };
        int[] accuracy = { 35, 25, 55, 50, 25 };
        int[] deflection = { 25, 40, 45, 30, 45 };
        int index = (int)s.CharacterClass;
        s.MaxHealth = s.MaxStamina = health[index];
        s.HealthStaminaPerLevel = growth[index];
        s.MeleeAccuracyBonus = s.RangedAccuracyBonus = accuracy[index];
        s.BaseDeflection = deflection[index];
        s.BaseFortitude = s.BaseReflexes = index == 4 ? 35 : 20;
        s.BaseWill = index == 4 ? 40 : 20;
        ChanterSummonScaling.AdjustBaseStats(component);
    }
}
public static class ScalingRegression
{
    private static int assertions;
    private static CharacterStats Create(string name, CharacterStats.Class cls, int level, float difficulty)
    {
        var s = new CharacterStats { CharacterClass = cls, Level = level, Difficulty = difficulty };
        s.gameObject = new UnityEngine.GameObject { name = name, Stats = s };
        object obj = s; DataManager.AdjustFromData(ref obj);
        if (name == "owner") s.CharacterClass = CharacterStats.Class.Chanter;
        return s;
    }
    private static readonly string[] Invocations = { "Gernisc_Slew_The_Beast", "My_Son_Do_You_See", "Gerniscs_Beast_Lit_The_Night", "Oh_But_Knock_Not", "But_Reny_Darets_Ghost" };
    private static Summon Origin(int index, UnityEngine.GameObject owner)
    {
        var spell = new GenericSpell { SpellClass = CharacterStats.Class.Chanter, Owner = owner,
            gameObject = new UnityEngine.GameObject { name = Invocations[index] + "(Clone)" } };
        return ChanterSummonOrigin.Track(new Summon(), new AttackRoar { AbilityOrigin = spell, Owner = owner });
    }
    private static void Equal(float actual, float expected, string label)
    {
        assertions++;
        if (Math.Abs(actual - expected) > 0.001) throw new Exception(label + ": " + actual + " != " + expected);
    }
    private static float[] Snapshot(CharacterStats s)
    {
        int levelBonus = (s.ScaledLevel - 1) * 3;
        return new[] { s.Stamina, s.Health, s.MeleeAccuracyBonus + levelBonus,
            s.RangedAccuracyBonus + levelBonus, s.BaseDeflection + levelBonus,
            s.BaseFortitude + levelBonus, s.BaseReflexes + levelBonus, s.BaseWill + levelBonus };
    }
    public static int Main()
    {
        string[] names = { "CRE_Wurm_Summon", "CRE_Will_o_Wisp_Summon", "CRE_Drake_Summon", "CRE_Ogre_Summon", "CRE_Phantom_Summon" };
        int[] native = { 2, 3, 6, 6, 3 }, unlock = { 5, 5, 9, 9, 1 };
        float[] constitution = { 1.1f, 1f, 1.35f, 1.4f, 1f };
        foreach (float difficulty in new[] { 1f, 0.75f, 1.5f })
        for (int i = 0; i < names.Length; i++)
        {
            SkeletonCrew.Main.Settings.EnableChanterSummonScaling = true;
            var cls = (CharacterStats.Class)i;
            var prefab = Create(names[i], cls, native[i], difficulty);
            var baseline = Create(names[i], cls, i == 4 ? 1 : native[i], difficulty);
            baseline.ConstitutionMultiplier = constitution[i];
            float[] expected = Snapshot(baseline);
            foreach (int target in new[] { unlock[i], 16 })
            {
                var owner = Create("owner", cls, target, difficulty);
                var summon = Create(names[i], cls, native[i], difficulty);
                summon.ConstitutionMultiplier = constitution[i];
                ChanterSummonScaling.Apply(summon, prefab, owner.gameObject, Origin(i, owner.gameObject));
                Equal(summon.Level, target, "summoner level");
                int gained = Math.Max(1, (int)Math.Floor(target * difficulty)) - Math.Max(1, (int)Math.Floor(unlock[i] * difficulty));
                float[] actual = Snapshot(summon);
                for (int stat = 0; stat < actual.Length; stat++)
                {
                    float growth = stat == 0 ? summon.HealthStaminaPerLevel * constitution[i] :
                        stat == 1 ? summon.HealthStaminaPerLevel * constitution[i] * 6 : 3;
                    Equal(actual[stat], expected[stat] + gained * growth, names[i] + " stat " + stat);
                }
                var ability = new AttackBase { AbilityOrigin = new GenericAbility() };
                Equal(actual[2] + summon.ScaledLevel - ChanterSummonScaling.AbilityAccuracyCompensation(summon, ability),
                    expected[2] + baseline.ScaledLevel + gained * 4, "ability accuracy");
                Equal(ChanterSummonScaling.AbilityAccuracyCompensation(summon, new AttackBase()), 0, "weapon accuracy");
                ability.AbilityOrigin = null; ability.TriggeringAbility = new GenericAbility();
                Equal(actual[2] + summon.ScaledLevel - ChanterSummonScaling.AbilityAccuracyCompensation(summon, ability),
                    expected[2] + baseline.ScaledLevel + gained * 4, "triggered ability accuracy");
                SkeletonCrew.Main.Settings.EnableChanterSummonScaling = false;
                for (int refresh = 0; refresh < 3; refresh++)
                {
                    object obj = summon; DataManager.AdjustFromData(ref obj);
                    float[] updated = Snapshot(summon);
                    for (int stat = 0; stat < actual.Length; stat++) Equal(updated[stat], actual[stat], "refresh stable after toggle");
                }
                SkeletonCrew.Main.Settings.EnableChanterSummonScaling = true;
                ChanterSummonScaling.Apply(summon, prefab, owner.gameObject, Origin(i, owner.gameObject));
                Equal(summon.Stamina, actual[0], "duplicate apply ignored");
                Equal(prefab.Level, native[i], "prefab unchanged");
            }
            SkeletonCrew.Main.Settings.EnableChanterSummonScaling = false;
            var disabled = Create(names[i], cls, native[i], difficulty);
            ChanterSummonScaling.Apply(disabled, prefab, baseline.gameObject, Origin(i, baseline.gameObject));
            Equal(disabled.Level, native[i], "disabled");
            SkeletonCrew.Main.Settings.EnableChanterSummonScaling = true;
            var early = Create(names[i], cls, native[i], difficulty);
            var earlyOwner = Create("owner", cls, 1, difficulty);
            ChanterSummonScaling.Apply(early, prefab, earlyOwner.gameObject, Origin(i, earlyOwner.gameObject));
            Equal(early.Level, 1, "early summon follows owner");
            Equal(early.Stamina > 0 ? 1 : 0, 1, "early summon has positive endurance");
        }
        SkeletonCrew.Main.Settings.EnableChanterSummonScaling = true;
        foreach (string name in new[] { "CRE_Skeleton_Chanter_Summon", "CRE_Wurm", "CRE_Snow_Ogre", "CRE_Ghost_Wurm" })
        {
            var excluded = Create(name, CharacterStats.Class.Wurm, 2, 1);
            var owner = Create("owner", CharacterStats.Class.Wurm, 16, 1);
            ChanterSummonScaling.Apply(excluded, excluded, owner.gameObject, Origin(0, owner.gameObject));
            Equal(excluded.Level, 2, "unrelated prefab");
        }
        CheckQualityAndIsolation();
        CheckWurmAndWispEffects();
        CheckBurnPresentation();
        ChanterSummonScaling.Apply(null, null, null, null);
        ChanterSummonScaling.AdjustBaseStats(new object());
        Console.WriteLine("PASS: " + assertions + " assertions (unlock balance, growth, difficulty, refresh, isolation, toggle).");
        return 0;
    }

    private static Equippable Item(bool armor, float value)
    {
        var go = new UnityEngine.GameObject { name = "Shared natural equipment" };
        var item = new Equippable(); go.Add(item);
        if (armor) go.Add(new Armor { DamageThreshhold = value });
        else go.Add(new AttackBase { AccuracyBonus = (int)value });
        return item;
    }

    private static void CheckWurmAndWispEffects()
    {
        foreach (int level in new[] { 5, 7, 8, 11, 12, 15, 16, 20 })
        {
            var wisp = Create("wisp", CharacterStats.Class.WillOWisp, level, 1);
            var equipment = new Equipment(); wisp.gameObject.Add(equipment);
            var claws = Item(false, 0); claws.GetComponent<AttackBase>().DTBypass = 2;
            equipment.DefaultEquippedItems.PrimaryWeapon = equipment.DefaultEquippedItems.SecondaryWeapon = claws;
            ChanterSummonEquipment.Apply(wisp, level, 1, "CRE_Will_o_Wisp_Summon");
            int expected = level >= 16 ? 20 : level >= 12 ? 15 : level >= 8 ? 10 : 5;
            Equal(equipment.DefaultEquippedItems.PrimaryWeapon.GetComponent<AttackBase>().DTBypass, 2 + expected, "wisp primary bypass");
            Equal(equipment.DefaultEquippedItems.SecondaryWeapon.GetComponent<AttackBase>().DTBypass, 2 + expected, "wisp secondary bypass");
            Equal(claws.GetComponent<AttackBase>().DTBypass, 2, "shared claws untouched");
        }
        var wurm = Create("wurm", CharacterStats.Class.Wurm, 5, 1);
        var gear = new Equipment(); wurm.gameObject.Add(gear);
        var original = Item(false, 0); gear.DefaultEquippedItems.PrimaryWeapon = original;
        ChanterSummonEquipment.Apply(wurm, 5, 1, "CRE_Wurm_Summon");
        var attack = gear.DefaultEquippedItems.PrimaryWeapon.GetComponent<AttackBase>();
        attack.Owner = wurm.gameObject;
        var target = Create("target", CharacterStats.Class.Wurm, 5, 1);
        var health = new Health(); target.gameObject.Add(health);
        var hit = new DamageInfo { Attack = attack, FinalAdjustedDamage = 30 };
        for (int outcome = 0; outcome < 3; outcome++)
        {
            hit.IsGraze = outcome == 1; hit.IsCriticalHit = outcome == 2;
            hit.FinalAdjustedDamage = outcome == 1 ? 15 : outcome == 2 ? 45 : 30;
            ChanterWurmBurn.Apply(wurm, target.gameObject, hit);
            var effect = target.Effects.Last();
            Equal(effect.Params.Value * wurm.StatDamageHealMultiplier * wurm.DifficultyDamageMultiplier,
                hit.FinalAdjustedDamage, "burn damage not scaled twice");
            Equal(effect.Duration, outcome == 1 ? 3 : outcome == 2 ? 9 : 6, "native hit duration");
            Equal(effect.Params.TacticalMultiplier, 1, "no second tactical damage bonus");
            Equal(effect.Params.DmgType == DamagePacket.DamageType.Burn ? 1 : 0, 1, "burn damage type");
            Equal(effect.m_forceStackable ? 1 : 0, 1, "each hit keeps its burn");
        }
        Equal(target.Effects.Count, 3, "three successful hits");
        hit.IsMiss = true; ChanterWurmBurn.Apply(wurm, target.gameObject, hit); hit.IsMiss = false;
        health.Dead = true; ChanterWurmBurn.Apply(wurm, target.gameObject, hit); health.Dead = false;
        health.Unconscious = true; ChanterWurmBurn.Apply(wurm, target.gameObject, hit); health.Unconscious = false;
        hit.FinalAdjustedDamage = 0; ChanterWurmBurn.Apply(wurm, target.gameObject, hit);
        hit.FinalAdjustedDamage = 30; hit.Attack = original.GetComponent<AttackBase>(); hit.Attack.Owner = wurm.gameObject;
        ChanterWurmBurn.Apply(wurm, target.gameObject, hit);
        hit.Attack = new AttackBase { Owner = wurm.gameObject }; // A separate spell/proc/DOT cannot trigger it.
        ChanterWurmBurn.Apply(wurm, target.gameObject, hit);
        Equal(target.Effects.Count, 3, "misses, zero damage, dead targets, shared attacks and other attacks ignored");
        Equal(ChanterWurmBurn.UnscaledDamage(float.NaN, 1), 0, "invalid damage");
        Equal(ChanterWurmBurn.UnscaledDamage(30, 0), 0, "zero multiplier");
    }

    private static void CheckBurnPresentation()
    {
        var effect = new StatusEffect { Params = new StatusEffectParams {
            Tag = "SkeletonCrew_WurmBurn", AffectsStat = StatusEffect.ModifiedStat.Damage, DmgType = DamagePacket.DamageType.Burn } };
        var other = new StatusEffect { Params = new StatusEffectParams { Tag = "OtherBurn" } };
        var stats = Create("Target", CharacterStats.Class.Wurm, 5, 1);
        var health = new Health(); stats.gameObject.Add(health);
        var source = new UnityEngine.GameObject { name = "Wurm" };
        Equal(ChanterWurmBurnPresentation.IsBurn(effect) ? 1 : 0, 1, "burn presentation scope");
        Equal(ChanterWurmBurnPresentation.IsBurn(other) ? 1 : 0, 0, "other effect untouched");
        Equal(ReferenceEquals(ChanterWurmBurnPresentation.GetIcon(), GameResources.Fan.Icon) ? 1 : 0, 1, "Fan of Flames icon");
        var namePatch = typeof(ChanterWurmBurnNamePatch).GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static);
        object[] arguments = { effect, "Missing items -1" };
        Equal((bool)namePatch.Invoke(null, arguments) ? 1 : 0, 0, "invalid item name bypassed");
        Equal((string)arguments[1] == "Wurm Burn" ? 1 : 0, 1, "burn label");
        arguments = new object[] { other, "Original" };
        Equal((bool)namePatch.Invoke(null, arguments) ? 1 : 0, 1, "other label preserved");
        var tick = ChanterWurmBurnPresentation.Capture(health, effect, false, source, 8);
        ChanterWurmBurnPresentation.RecordMitigation(ChanterWurmBurnPresentation.CaptureMitigation(stats, 10,
            DamagePacket.DamageType.Burn, null, source, 0.25f), 7.5f);
        health.CurrentStamina = 93;
        ChanterWurmBurnPresentation.Report(health, source, effect, tick);
        Equal(Console.Entries.Last().Info.FinalAdjustedDamage, 7, "actual damage metadata");
        Equal(Console.Entries.Last().Info.DTAdjustedDamage, 7.5f, "mitigation metadata");
        Equal(Console.Entries.Last().Message.Contains("7.0 Burn") ? 1 : 0, 1, "damage type in log");
        Equal(Console.Entries.Last().Hover.Contains("25.0% of 10.0") ? 1 : 0, 1, "native DOT DR fraction");
        Equal(Console.Entries.Last().Hover.Contains("No new attack roll") ? 1 : 0, 1, "no fabricated attack roll");
        ChanterWurmBurnPresentation.End(tick);
        tick = ChanterWurmBurnPresentation.Capture(health, effect, false, source, 0);
        ChanterWurmBurnPresentation.Report(health, source, effect, tick);
        Equal(Console.Entries.Count, 1, "unprocessed zero tick not logged");
        var nested = ChanterWurmBurnPresentation.Capture(health, effect, true, source, 10);
        Equal(nested.Tracked ? 1 : 0, 0, "hidden damage respected");
        ChanterWurmBurnPresentation.End(nested);
        nested = ChanterWurmBurnPresentation.Capture(health, other, false, source, 10);
        Equal(nested.Tracked ? 1 : 0, 0, "unrelated ticks ignored");
        Equal(ChanterWurmBurnPresentation.CaptureMitigation(stats, 10, DamagePacket.DamageType.Burn, null, source, 0.25f) == null ? 1 : 0, 1, "nested unrelated mitigation excluded");
        ChanterWurmBurnPresentation.End(nested);
        Equal(ChanterWurmBurnPresentation.DamageLost(tick, -20), 93, "overkill capped");
        ChanterWurmBurnPresentation.End(tick);
        health.MaxStamina = 0;
        tick = ChanterWurmBurnPresentation.Capture(health, effect, false, source, 12);
        ChanterWurmBurnPresentation.RecordMitigation(ChanterWurmBurnPresentation.CaptureMitigation(stats, 15,
            DamagePacket.DamageType.Burn, null, source, 0.25f), 12.5f);
        health.CurrentHealth = 189;
        ChanterWurmBurnPresentation.Report(health, source, effect, tick);
        Equal(Console.Entries.Last().Info.FinalAdjustedDamage, 11, "health-only target damage");
        ChanterWurmBurnPresentation.End(tick);
        Equal(ChanterWurmBurnPresentation.FormatDamage(0.4f) == "0.4" ? 1 : 0, 1, "one decimal damage");
        Equal(ChanterWurmBurnPresentation.FormatDamage(1.26f) == "1.3" ? 1 : 0, 1, "damage rounds to one decimal");
        tick = ChanterWurmBurnPresentation.Capture(health, effect, false, source, 0.5f);
        ChanterWurmBurnPresentation.RecordMitigation(ChanterWurmBurnPresentation.CaptureMitigation(stats, 0.6f,
            DamagePacket.DamageType.Burn, null, source, 0.25f), 0.12f);
        Equal(tick.Info.IsMin ? 1 : 0, 1, "minimum damage flag");
        Equal(ChanterWurmBurnPresentation.Breakdown(tick, 0.12f).Contains("minimum damage") ? 1 : 0, 1, "minimum damage breakdown");
        ChanterWurmBurnPresentation.End(tick);
        stats.DamageThreshold = float.PositiveInfinity;
        tick = ChanterWurmBurnPresentation.Capture(health, effect, false, source, 10);
        ChanterWurmBurnPresentation.RecordMitigation(ChanterWurmBurnPresentation.CaptureMitigation(stats, 12,
            DamagePacket.DamageType.Burn, null, source, 0.25f), 0);
        ChanterWurmBurnPresentation.Report(health, source, effect, tick);
        Equal(Console.Entries.Last().Hover.Contains("Burn immunity") ? 1 : 0, 1, "true zero explained");
        Equal(Console.Entries.Last().Info.FinalAdjustedDamage, 0, "immunity keeps zero damage");
        ChanterWurmBurnPresentation.End(tick);
        Equal(ChanterWurmBurnPresentation.CaptureMitigation(stats, 12, DamagePacket.DamageType.Burn, null, source, 0.25f) == null ? 1 : 0, 1, "scope restored after tick");
    }

    private static void CheckQualityAndIsolation()
    {
        int[] levels = { 1, 3, 4, 7, 8, 11, 12, 15, 16, 20 };
        int[][] gradeExpected = { new[] {0,0,1,1,2,2,3,3,4,4}, new[] {0,0,0,0,1,1,2,2,3,3}, new[] {0,0,0,0,0,0,1,1,2,2} };
        int[][] drExpected = { new[] {0,0,1,1,2,2,4,4,8,8}, new[] {0,0,0,0,1,1,3,3,7,7}, new[] {0,0,0,0,0,0,2,2,6,6} };
        for (int baked = 0; baked <= 2; baked++)
        for (int j = 0; j < levels.Length; j++)
        {
            int grades = gradeExpected[baked][j];
            Equal(ChanterSummonEquipment.AddedGrades(levels[j], baked), grades, "tier boundary");
            Equal(ChanterSummonEquipment.ArmorBonus(levels[j], baked), drExpected[baked][j], "DR boundary");
            var summon = Create("summon", CharacterStats.Class.Wurm, 16, 1);
            var equipment = new Equipment(); summon.gameObject.Add(equipment);
            var weapon = Item(false, 5);
            var armor = Item(true, 10);
            equipment.DefaultEquippedItems.PrimaryWeapon = weapon;
            equipment.DefaultEquippedItems.SecondaryWeapon = weapon;
            equipment.DefaultEquippedItems.Chest = armor;
            Equal(ChanterSummonEquipment.Apply(summon, levels[j], baked), 0, "armor does not double count innate DR");
            var primary = equipment.DefaultEquippedItems.PrimaryWeapon;
            var secondary = equipment.DefaultEquippedItems.SecondaryWeapon;
            Equal(primary.GetComponent<AttackBase>().AccuracyBonus, 5 + 4 * grades, "natural weapon accuracy");
            Equal(primary.GetComponent<AttackBase>().DamageMultiplier, 1 + 0.15f * grades, "additive damage quality");
            Equal(secondary.GetComponent<AttackBase>().DamageMultiplier, 1 + 0.15f * grades, "second hand quality");
            Equal(weapon.GetComponent<AttackBase>().DamageMultiplier, 1, "shared weapon untouched");
            Equal(armor.GetComponent<Armor>().DamageThreshhold, 10, "shared armor untouched");
            Equal(equipment.DefaultEquippedItems.Chest.GetComponent<Armor>().DamageThreshhold, 10 + drExpected[baked][j], "armor quality");
            if (grades > 0)
            {
                Equal(ReferenceEquals(primary, secondary) ? 1 : 0, 0, "separate live hands");
                Equal(ReferenceEquals(primary.Prefab, weapon) ? 1 : 0, 1, "prefab identity");
                Equal(ReferenceEquals(primary.transform.Parent, summon.transform) ? 1 : 0, 1, "owner lifetime");
            }
        }
        var owner = Create("owner", CharacterStats.Class.Wurm, 16, 1);
        var prefab = Create("CRE_Will_o_Wisp_Summon", CharacterStats.Class.WillOWisp, 3, 1);
        var valid = Origin(1, owner.gameObject);
        foreach (Summon source in new[] { null, new Summon(), Origin(0, owner.gameObject), Origin(1, new UnityEngine.GameObject()) })
        {
            var denied = Create(prefab.gameObject.name, CharacterStats.Class.WillOWisp, 3, 1);
            ChanterSummonScaling.Apply(denied, prefab, owner.gameObject, source);
            Equal(denied.Level, 3, "unproven or mismatched invocation rejected");
        }
        foreach (GenericAbility ability in new GenericAbility[] {
            new GenericAbility { Owner = owner.gameObject, gameObject = new UnityEngine.GameObject { name = "Charged_Spellbind_My_Son_Do_You_See_Ability" } },
            new GenericSpell { SpellClass = CharacterStats.Class.Wurm, Owner = owner.gameObject, gameObject = new UnityEngine.GameObject { name = Invocations[1] } },
            new GenericSpell { SpellClass = CharacterStats.Class.Chanter, Owner = owner.gameObject, gameObject = new UnityEngine.GameObject { name = "Some_Other_Spell" } } })
        {
            var source = ChanterSummonOrigin.Track(new Summon(), new AttackRoar { Owner = owner.gameObject, AbilityOrigin = ability });
            Equal(ChanterSummonOrigin.Matches(source, prefab.gameObject.name, owner.gameObject) ? 1 : 0, 0, "item or other spell rejected");
        }
        var wisp = Create(prefab.gameObject.name, CharacterStats.Class.WillOWisp, 3, 1);
        owner.ChanterLevel = 8; // Total 16, Chanter 8: use only the class level for quality.
        ChanterSummonScaling.Apply(wisp, prefab, owner.gameObject, valid);
        Equal(wisp.Level, 16, "multiclass total level");
        Equal(ChanterSummonScaling.InnateArmorBonus(wisp, DamagePacket.DamageType.All), 1, "armorless wisp class-level DR");
        Equal(ChanterSummonScaling.InnateArmorBonus(wisp, DamagePacket.DamageType.Raw), 0, "raw damage bypass");
        Equal(ChanterSummonScaling.InnateArmorBonus(prefab, DamagePacket.DamageType.All), 0, "unregistered creature no DR");
        var nonChanter = Create("non-chanter", CharacterStats.Class.Wurm, 16, 1);
        var deniedOwner = Create(prefab.gameObject.name, CharacterStats.Class.WillOWisp, 3, 1);
        ChanterSummonScaling.Apply(deniedOwner, prefab, nonChanter.gameObject, Origin(1, nonChanter.gameObject));
        Equal(deniedOwner.Level, 3, "non-chanter owner rejected");
    }
}
