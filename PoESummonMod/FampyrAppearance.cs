using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SkeletonCrew;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PoESummonMod
{
    // Runs before Equipment.Start, so weapons attach to the replacement rig.
    internal static class FampyrAppearance
    {
        private const string DonorPrefab = "cre_px1_fampyr_barbarian";
        private static readonly FieldInfo Initialized = AccessTools.Field(typeof(Equipment), "m_initalizedSet");
        private static readonly FieldInfo Deserialized = AccessTools.Field(typeof(Equipment), "m_hasDeserialized");
        private static readonly FieldInfo CurrentItems = AccessTools.Field(typeof(Equipment), "m_currentItems");

        public static void Apply(CharacterStats skeleton)
        {
            GameObject target = skeleton.gameObject;
            if (target.GetComponent<NPCAppearance>() != null)
                return;

            NPCAppearance appearance = null;
            Transform oldRig = null;
            Transform oldRigParent = null;
            Transform oldMesh = null;
            string oldMeshName = null;
            var originalChildren = new HashSet<Transform>();
            foreach (Transform child in target.transform)
                originalChildren.Add(child);

            SkinnedMeshRenderer[] oldRenderers = target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var rendererStates = new bool[oldRenderers.Length];
            for (int i = 0; i < oldRenderers.Length; i++)
                rendererStates[i] = oldRenderers[i].enabled;

            Animator animator = target.GetComponent<Animator>();
            Avatar oldAvatar = animator != null ? animator.avatar : null;
            RuntimeAnimatorController oldController = animator != null ? animator.runtimeAnimatorController : null;
            Equippable chest = null;
            AppearancePiece oldChestAppearance = null;
            bool changed = false;

            try
            {
                // Read appearance data only; never spawn a donor with its AI,
                // abilities, health or faction alongside the summoned creature.
                GameObject donor = GameResources.LoadPrefab<GameObject>(DonorPrefab, false);
                NPCAppearance source = donor != null ? donor.GetComponent<NPCAppearance>() : null;
                GameObject armor = GameResources.LoadPrefab<GameObject>("breastplate_armor", false);
                Equippable armorItem = armor != null ? armor.GetComponent<Equippable>() : null;
                Equipment equipment = target.GetComponent<Equipment>();
                if (source == null || armorItem == null || equipment == null || Initialized == null ||
                    Deserialized == null || CurrentItems == null || (bool)Initialized.GetValue(equipment))
                    throw new InvalidOperationException("Missing appearance donor/armor, or equipment already initialized.");

                oldRig = FindOriginalVisualRoot(target);
                if (oldRig == null || oldRig == target.transform)
                    throw new InvalidOperationException("Could not isolate the original skeleton rig.");

                chest = equipment.DefaultEquippedItems.Chest;
                if (chest == null)
                    throw new InvalidOperationException("No cloned chest armor to customize.");

                oldChestAppearance = chest.Appearance;
                oldRigParent = oldRig.parent;
                Main.Logger.Log("Fampyr prototype replacing visual root: " + oldRig.name);
                oldMesh = target.transform.Find("Mesh");
                oldMeshName = oldMesh != null ? oldMesh.name : null;
                changed = true;

                // Use a normal humanoid armor mesh, retaining the cloned
                // skeleton armor's DR, scaling and recovery settings.
                chest.Appearance = CopyPiece(armorItem.Appearance);
                oldRig.SetParent(null, true);
                if (oldMesh != null)
                    oldMesh.name = "OriginalSkeletonMesh";
                for (int i = 0; i < oldRenderers.Length; i++)
                    oldRenderers[i].enabled = false;

                appearance = target.AddComponent<NPCAppearance>();
                appearance.enabled = false; // Generate explicitly; avoid a second Start rebuild.
                appearance.primaryColor = source.primaryColor;
                appearance.secondaryColor = source.secondaryColor;
                appearance.skinColor = source.skinColor;
                appearance.hairColor = source.hairColor;
                appearance.headAppearance = CopyPiece(source.headAppearance);
                appearance.hairAppearance = CopyPiece(source.hairAppearance);
                appearance.facialHairAppearance = CopyPiece(source.facialHairAppearance);
                appearance.nudeModelOverride = source.nudeModelOverride;
                appearance.skinOverride = source.skinOverride;
                appearance.hasHead = source.hasHead;
                appearance.hasHair = source.hasHair;
                appearance.hasFacialHair = source.hasFacialHair;
                appearance.ignoreDefaultNudeLegModel = source.ignoreDefaultNudeLegModel;
                appearance.gender = source.gender;
                appearance.race = source.race;
                appearance.subrace = source.subrace;
                appearance.racialBodyType = source.racialBodyType;
                appearance.layer = source.layer;
                appearance.avatar = source.avatar;
                appearance.controller = source.controller;

                // Force Generate to bind the new rig instead of retaining the
                // old skeleton Avatar or its creature animation controller.
                if (animator != null)
                {
                    animator.avatar = null;
                    animator.runtimeAnimatorController = null;
                }
                // CurrentItems normally initializes equipment on first access.
                // During mesh generation use the prepared loadout: this prefab
                // already has a non-null but empty m_currentItems. Blocking
                // initialization alone would generate an unequipped body.
                // The new Animator is not bound until the end of Generate.
                bool wasDeserialized = (bool)Deserialized.GetValue(equipment);
                object previousItems = CurrentItems.GetValue(equipment);
                try
                {
                    Deserialized.SetValue(equipment, true);
                    CurrentItems.SetValue(equipment, equipment.DefaultEquippedItems);
                    Main.Logger.Log("Fampyr appearance loadout: chest=" + chest.name +
                        ", armorType=" + chest.Appearance.armorType +
                        ", head=" + (equipment.DefaultEquippedItems.Head != null ? equipment.DefaultEquippedItems.Head.name : "none") +
                        ", neck=" + (equipment.DefaultEquippedItems.Neck != null ? equipment.DefaultEquippedItems.Neck.name : "none"));
                    appearance.Generate();
                }
                finally
                {
                    CurrentItems.SetValue(equipment, previousItems);
                    Deserialized.SetValue(equipment, wasDeserialized);
                }

                Transform mesh = target.transform.Find("Mesh");
                SkinnedMeshRenderer generated = mesh != null ? mesh.GetComponent<SkinnedMeshRenderer>() : null;
                Animator generatedAnimator = target.GetComponent<Animator>();
                if (generated == null || generated.sharedMesh == null || generated.sharedMesh.vertexCount == 0 ||
                    generatedAnimator == null || generatedAnimator.avatar == null ||
                    !generatedAnimator.avatar.isValid || generatedAnimator.runtimeAnimatorController == null)
                    throw new InvalidOperationException("Appearance generation did not produce a mesh and avatar.");

                Object.Destroy(oldRig.gameObject);
                Main.Logger.Log("Fampyr mesh generated from prepared equipment: vertices=" +
                    generated.sharedMesh.vertexCount + ". Visual appearance requires in-game verification.");
            }
            catch (Exception error)
            {
                if (changed)
                {
                    // Remove only objects generated by this attempt and restore
                    // the original rig before normal equipment initialization.
                    var generatedChildren = new List<Transform>();
                    foreach (Transform child in target.transform)
                        if (!originalChildren.Contains(child)) generatedChildren.Add(child);
                    foreach (Transform child in generatedChildren)
                    {
                        child.SetParent(null, true);
                        child.gameObject.SetActive(false);
                        Object.Destroy(child.gameObject);
                    }
                    if (appearance != null) Object.Destroy(appearance);
                    if (chest != null) chest.Appearance = oldChestAppearance;
                    if (oldRig != null) oldRig.SetParent(oldRigParent, true);
                    if (oldMesh != null) oldMesh.name = oldMeshName;
                    for (int i = 0; i < oldRenderers.Length; i++)
                        if (oldRenderers[i] != null) oldRenderers[i].enabled = rendererStates[i];
                    if (animator != null)
                    {
                        animator.avatar = oldAvatar;
                        animator.runtimeAnimatorController = oldController;
                    }
                    else
                    {
                        Animator added = target.GetComponent<Animator>();
                        if (added != null) Object.DestroyImmediate(added);
                    }
                    AnimationController controller = target.GetComponent<AnimationController>();
                    if (controller != null) controller.BindComponents();
                    AnimationBoneMapper mapper = target.GetComponent<AnimationBoneMapper>();
                    if (mapper != null) mapper.Reinitialize();
                }
                Main.Logger.Error("Fampyr prototype failed; using original skeleton. " + error);
            }
        }

        private static AppearancePiece CopyPiece(AppearancePiece source)
        {
            if (source == null) return null;
            var copy = new AppearancePiece();
            // AppearancePiece contains public value/string fields. Copy the
            // descriptor so generation cannot modify shared prefab data.
            foreach (FieldInfo field in typeof(AppearancePiece).GetFields(BindingFlags.Public | BindingFlags.Instance))
                field.SetValue(copy, field.GetValue(source));
            return copy;
        }

        private static Transform FindOriginalVisualRoot(GameObject target)
        {
            // The Chanter prefab's actual hierarchy is untagged:
            // Maergh01 / Export_Skeleton / Reference, with meshes under
            // Maergh01 / G_Maergh01. The game's helper only finds objects
            // tagged Skeleton, so it returns null for this creature.
            Transform creatureVisuals = target.transform.Find("Maergh01");
            if (creatureVisuals != null &&
                creatureVisuals.Find("Export_Skeleton/Reference") != null &&
                creatureVisuals.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                return creatureVisuals;

            Transform taggedRig = GameUtilities.FindSkeletonTransform(target);
            if (taggedRig != null && taggedRig != target.transform)
                return taggedRig;

            var children = new List<string>();
            foreach (Transform child in target.transform)
                children.Add(child.name);
            Main.Logger.Error("Unrecognized skeleton visual hierarchy. Root children: " +
                string.Join(", ", children.ToArray()));
            return null;
        }
    }
}
