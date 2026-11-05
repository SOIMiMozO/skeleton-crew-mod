using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace PoESummonMod
{
    internal static class ChanterSummonOrigin
    {
        private sealed class Origin
        {
            public string Invocation;
            public GameObject Owner;
        }

        private static readonly ConditionalWeakTable<Summon, Origin> Origins =
            new ConditionalWeakTable<Summon, Origin>();

        // AttackRoar creates a separate Summon object and does not propagate its
        // AbilityOrigin. Capture the actual cast here, before asynchronous spawning.
        internal static Summon Track(Summon summon, AttackRoar attack)
        {
            if (summon == null || attack == null)
                return summon;
            GenericSpell spell = attack.AbilityOrigin as GenericSpell;
            if (spell == null || spell.SpellClass != CharacterStats.Class.Chanter ||
                spell.Owner == null || spell.Owner != attack.Owner)
                return summon;
            string invocation = NormalizeName(spell.gameObject.name);
            if (ExpectedCreature(invocation) == null)
                return summon;
            Origins.Remove(summon);
            Origins.Add(summon, new Origin { Invocation = invocation, Owner = spell.Owner });
            return summon;
        }

        internal static bool Matches(Summon source, string creature, GameObject owner)
        {
            Origin origin;
            return source != null && Origins.TryGetValue(source, out origin) &&
                origin.Owner == owner &&
                string.Equals(ExpectedCreature(origin.Invocation), creature, StringComparison.Ordinal);
        }

        // Diagnostic only: the Chanter invocation recorded for this Summon, if any.
        internal static string Describe(Summon source)
        {
            Origin origin;
            return source != null && Origins.TryGetValue(source, out origin) ? origin.Invocation : null;
        }

        private static string NormalizeName(string name)
        {
            const string clone = "(Clone)";
            if (name != null && name.EndsWith(clone, StringComparison.Ordinal))
                name = name.Substring(0, name.Length - clone.Length);
            return name == null ? "" : name.ToLowerInvariant();
        }

        private static string ExpectedCreature(string invocation)
        {
            switch (invocation)
            {
                case "but_reny_darets_ghost": return "CRE_Phantom_Summon";
                case "gernisc_slew_the_beast": return "CRE_Wurm_Summon";
                case "my_son_do_you_see": return "CRE_Will_o_Wisp_Summon";
                case "gerniscs_beast_lit_the_night": return "CRE_Drake_Summon";
                case "oh_but_knock_not": return "CRE_Ogre_Summon";
                default: return null;
            }
        }
    }
}
