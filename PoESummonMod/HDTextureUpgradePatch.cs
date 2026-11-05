using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace SkeletonCrew
{
    // Vanilla appends _HD even to textures already upgraded on an earlier pass.
    // Only change this appearance-upgrade call, not the global resource loader.
    [HarmonyPatch(typeof(NPCAppearance), "ReplaceTexturesWithHDTextures")]
    internal static class HDTextureUpgradePatch
    {
        internal static bool AlreadyHD(string requestedName)
        {
            return requestedName != null &&
                requestedName.EndsWith("_HD_HD", StringComparison.OrdinalIgnoreCase);
        }

        internal static Texture LoadUpgrade(string requestedName)
        {
            // A null replacement makes vanilla retain the current texture and
            // material. Do not load the existing HD texture again or clone its material.
            return AlreadyHD(requestedName) ? null : GameResources.LoadTextureBundle(requestedName);
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            MethodInfo original = AccessTools.Method(typeof(GameResources), "LoadTextureBundle");
            MethodInfo replacement = AccessTools.Method(typeof(HDTextureUpgradePatch), "LoadUpgrade");
            int matches = 0;
            foreach (CodeInstruction code in codes)
                if (code.opcode == OpCodes.Call && Equals(code.operand, original)) matches++;
            if (matches != 1)
            {
                Main.Logger.Error("HD texture guard not applied: expected one texture upgrade call, found " + matches);
                return codes;
            }
            foreach (CodeInstruction code in codes)
                if (code.opcode == OpCodes.Call && Equals(code.operand, original)) code.operand = replacement;
            return codes;
        }
    }
}
