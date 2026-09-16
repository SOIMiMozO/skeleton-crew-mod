using HarmonyLib;
using SkeletonCrew;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace PoESummonMod
{
    [HarmonyPatch]
    public static class SummonCreaturePatch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(Summon),
                "SummonCreature",
                new Type[]
                {
                    typeof(CharacterStats),
                    typeof(UnityEngine.Vector3),
                    typeof(Faction)
                });
        }

        static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            MethodInfo applyMethod =
                AccessTools.Method(
                    typeof(ChanterSkeletonScaling),
                    nameof(ChanterSkeletonScaling.Apply));

            FieldInfo summonerField =
                AccessTools.Field(
                    typeof(Summon),
                    "m_summoner");

            FieldInfo summonsField =
                AccessTools.Field(
                    typeof(Summon),
                    "m_summons");

            MethodInfo countGetter =
                AccessTools.PropertyGetter(
                    typeof(List<Health>),
                    "Count");

            if (applyMethod == null ||
                summonerField == null ||
                summonsField == null ||
                countGetter == null)
            {
                if (applyMethod == null)
                    Main.Logger.Error(
                        "Could not find ChanterSkeletonScaling.Apply.");

                if (summonerField == null)
                    Main.Logger.Error(
                        "Could not find Summon.m_summoner.");

                if (summonsField == null)
                    Main.Logger.Error(
                        "Could not find Summon.m_summons.");

                if (countGetter == null)
                    Main.Logger.Error(
                        "Could not find List<Health>.Count.");

                Main.Logger.Error(
                    "SummonCreature patch not applied. " +
                    "Using original game method.");

                foreach (CodeInstruction code in codes)
                {
                    yield return code;
                }

                yield break;
            }

            bool patched = false;

            for (int i = 0; i < codes.Count; i++)
            {
                yield return codes[i];

                if (!patched &&
                    codes[i].opcode == OpCodes.Call &&
                    codes[i].operand is MethodInfo method &&
                    method.Name == "Instantiate" &&
                    method.DeclaringType == typeof(GameResources) &&
                    method.IsGenericMethod &&
                    method.GetGenericArguments().Length == 1 &&
                    method.GetGenericArguments()[0] ==
                        typeof(CharacterStats))
                {
                    if (i + 1 < codes.Count &&
                        IsStoreLocal(codes[i + 1]))
                    {
                        // Original:
                        // characterStats =
                        //     GameResources.Instantiate<CharacterStats>(
                        //         character);

                        // Execute original stloc.
                        i++;
                        yield return codes[i];

                        // characterStats
                        yield return LoadLocalFromStore(codes[i]);

                        // character
                        yield return new CodeInstruction(
                            OpCodes.Ldarg_1);

                        // this.m_summoner
                        yield return new CodeInstruction(
                            OpCodes.Ldarg_0);

                        yield return new CodeInstruction(
                            OpCodes.Ldfld,
                            summonerField);

                        // this.m_summons.Count
                        yield return new CodeInstruction(
                            OpCodes.Ldarg_0);

                        yield return new CodeInstruction(
                            OpCodes.Ldfld,
                            summonsField);

                        yield return new CodeInstruction(
                            OpCodes.Callvirt,
                            countGetter);

                        // Apply(
                        //     characterStats,
                        //     character,
                        //     m_summoner,
                        //     m_summons.Count);
                        yield return new CodeInstruction(
                            OpCodes.Call,
                            applyMethod);

                        patched = true;

                        Main.Logger.Log(
                            "SummonCreature transpiler injection created.");
                    }
                }
            }

            if (!patched)
            {
                Main.Logger.Error(
                    "SummonCreature transpiler could not find injection point!");
            }
        }

        private static bool IsStoreLocal(
            CodeInstruction instruction)
        {
            return instruction.opcode == OpCodes.Stloc ||
                   instruction.opcode == OpCodes.Stloc_S ||
                   instruction.opcode == OpCodes.Stloc_0 ||
                   instruction.opcode == OpCodes.Stloc_1 ||
                   instruction.opcode == OpCodes.Stloc_2 ||
                   instruction.opcode == OpCodes.Stloc_3;
        }

        private static CodeInstruction LoadLocalFromStore(
            CodeInstruction store)
        {
            if (store.opcode == OpCodes.Stloc_0)
                return new CodeInstruction(OpCodes.Ldloc_0);

            if (store.opcode == OpCodes.Stloc_1)
                return new CodeInstruction(OpCodes.Ldloc_1);

            if (store.opcode == OpCodes.Stloc_2)
                return new CodeInstruction(OpCodes.Ldloc_2);

            if (store.opcode == OpCodes.Stloc_3)
                return new CodeInstruction(OpCodes.Ldloc_3);

            if (store.opcode == OpCodes.Stloc_S)
                return new CodeInstruction(
                    OpCodes.Ldloc_S,
                    store.operand);

            if (store.opcode == OpCodes.Stloc)
                return new CodeInstruction(
                    OpCodes.Ldloc,
                    store.operand);

            throw new InvalidOperationException(
                "Instruction is not a local-variable store.");
        }
    }
}