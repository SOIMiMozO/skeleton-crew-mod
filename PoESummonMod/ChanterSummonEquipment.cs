using System;
using SkeletonCrew;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PoESummonMod
{
    internal static class ChanterSummonEquipment
    {
        internal static int Grade(int chanterLevel)
        {
            return chanterLevel >= 16 ? 4 : chanterLevel >= 12 ? 3 :
                chanterLevel >= 8 ? 2 : chanterLevel >= 4 ? 1 : 0;
        }

        internal static int AddedGrades(int chanterLevel, int bakedGrade)
        {
            return Math.Max(0, Grade(chanterLevel) - bakedGrade);
        }

        internal static int ArmorBonus(int chanterLevel, int bakedGrade)
        {
            return Math.Max(0, SummonEquipmentQuality.GetArmorBonus(chanterLevel) -
                SummonEquipmentQuality.GetArmorBonus(bakedGrade * 4));
        }

        // Returns innate DR for creatures without armor (the wisp). Armor wearers
        // receive the increase on a private armor instance, preserving resistances.
        internal static float Apply(CharacterStats summon, int chanterLevel, int bakedGrade, string creaturePrefab = null)
        {
            int grades = AddedGrades(chanterLevel, bakedGrade);
            int armorBonus = ArmorBonus(chanterLevel, bakedGrade);
            bool burn = creaturePrefab == "CRE_Wurm_Summon";
            float bypass = creaturePrefab == "CRE_Will_o_Wisp_Summon" ? 5 * (1 + grades) : 0;
            Equipment equipment = summon.GetComponent<Equipment>();
            if (equipment == null)
                return armorBonus;

            if (grades > 0 || burn || bypass > 0)
            {
                equipment.DefaultEquippedItems.PrimaryWeapon = UpgradeWeapon(
                    equipment.DefaultEquippedItems.PrimaryWeapon, summon.transform, grades, bypass, burn);
                equipment.DefaultEquippedItems.SecondaryWeapon = UpgradeWeapon(
                    equipment.DefaultEquippedItems.SecondaryWeapon, summon.transform, grades, bypass, burn);
            }

            Equippable chest = equipment.DefaultEquippedItems.Chest;
            if (armorBonus > 0 && chest != null && chest.GetComponent<Armor>() != null)
            {
                Equippable copy = Clone(chest, summon.transform);
                copy.GetComponent<Armor>().DamageThreshhold += armorBonus;
                equipment.DefaultEquippedItems.Chest = copy;
                return 0;
            }
            return armorBonus;
        }

        private static Equippable UpgradeWeapon(Equippable original, Transform owner, int grades, float bypass, bool burn)
        {
            if (original == null || original.GetComponent<AttackBase>() == null ||
                original.GetComponent<Shield>() != null)
                return original;
            Equippable copy = Clone(original, owner);
            // These fields feed both actual attack damage and the damage tooltip.
            // Per-grade bonuses are additive, not compounded. Abilities and procs
            // on separate objects retain their existing damage and accuracy.
            foreach (AttackBase attack in copy.GetComponents<AttackBase>())
            {
                attack.DamageMultiplier += grades * 0.15f;
                attack.AccuracyBonus += grades * 4;
                attack.DTBypass += bypass;
                if (burn)
                    ChanterWurmBurn.Register(attack, copy);
            }
            return copy;
        }

        private static Equippable Clone(Equippable original, Transform owner)
        {
            GameObject instance = Object.Instantiate<GameObject>(original.gameObject);
            instance.transform.SetParent(owner, false);
            Equippable copy = instance.GetComponent<Equippable>();
            // Reuse this live customized object during equipment initialization.
            copy.Prefab = original.Prefab != null ? original.Prefab : original;
            return copy;
        }
    }
}
