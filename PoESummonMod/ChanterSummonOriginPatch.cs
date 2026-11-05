using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SkeletonCrew;

namespace PoESummonMod
{
    [HarmonyPatch(typeof(AttackRoar), "CheckSummoning")]
    internal static class ChanterSummonOriginPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int matches = 0;
            foreach (CodeInstruction code in instructions)
            {
                yield return code;
                MethodInfo method = code.operand as MethodInfo;
                if (code.opcode != OpCodes.Call || method == null ||
                    method.DeclaringType != typeof(GameResources) || method.Name != "Instantiate" ||
                    !method.IsGenericMethod || method.GetGenericArguments().Length != 1 ||
                    method.GetGenericArguments()[0] != typeof(Summon))
                    continue;
                // Stack: newly instantiated Summon. Track returns that same object.
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(ChanterSummonOrigin), "Track"));
                matches++;
            }
            if (matches != 1)
                Main.Logger.Error("Chanter invocation origin hook matched " + matches +
                    " spawn sites; unrecognized summons will not be scaled.");
        }
    }
}
