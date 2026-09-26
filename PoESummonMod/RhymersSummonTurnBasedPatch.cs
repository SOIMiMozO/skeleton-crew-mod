using System;
using HarmonyLib;
using UnityEngine;

namespace SkeletonCrew
{
    // Rhymer's animationless attack completes its turn before its delayed impact.
    // Allow the already-launched ability to activate at impact, so vanilla Apply
    // consumes the charge. Do not change readiness while selecting a new action.
    [HarmonyPatch(typeof(Summon), "OnImpact", new Type[]
    {
        typeof(GameObject), typeof(Vector3)
    })]
    internal static class RhymersSummonTurnBasedImpactPatch
    {
        [ThreadStatic]
        internal static GenericAbility ImpactAbility;

        private static void Prefix(Summon __instance, out GenericAbility __state)
        {
            __state = ImpactAbility;
            ImpactAbility = null;

            GenericAbility ability = __instance.AbilityOrigin;
            if (ability == null ||
                !TacticalModeManager.IsTacticalModeEnabled ||
                __instance.OwnerAI == null ||
                !TacticalModeManager.IsInTacticalCombat(__instance.OwnerAI) ||
                ability.CooldownType != GenericAbility.CooldownMode.PerEncounter)
            {
                return;
            }

            string abilityName = ability.gameObject.name;
            if (string.Equals(abilityName, "px1_rhymers_summon",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(abilityName, "px1_rhymers_summon(Clone)",
                    StringComparison.OrdinalIgnoreCase))
            {
                ImpactAbility = ability;
            }
        }

        // A finalizer also restores the scope if vanilla or another patch throws.
        private static void Finalizer(GenericAbility __state)
        {
            ImpactAbility = __state;
        }
    }

    [HarmonyPatch(typeof(GenericAbility), "CalculateWhyNotReady")]
    internal static class RhymersSummonTurnBasedReadinessPatch
    {
        private static void Postfix(GenericAbility __instance,
            ref GenericAbility.NotReadyValue ___m_reason_not_ready)
        {
            if (!ReferenceEquals(__instance,
                    RhymersSummonTurnBasedImpactPatch.ImpactAbility))
            {
                return;
            }

            // The action was paid for at launch; auto-end-turn may also have
            // advanced to the next actor. Keep exhausted charges, prerequisites,
            // death, and every other readiness restriction intact.
            ___m_reason_not_ready &= ~(GenericAbility.NotReadyValue.NoAction |
                GenericAbility.NotReadyValue.NotYourTurn);
        }
    }
}
