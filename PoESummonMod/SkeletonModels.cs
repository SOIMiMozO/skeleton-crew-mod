using SkeletonCrew;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PoESummonMod
{
    internal static class SkeletonModels
    {
        public static void Apply(CharacterStats skeleton, CharacterStats sourcePrefab, int summonIndex)
        {
            if (summonIndex < 0 || summonIndex > 2)
                return;

            SkeletonModelOption option = Main.Settings.GetModel(summonIndex);
            if (option.ResourceName == null)
            {
                option.ApplyBonuses(skeleton, sourcePrefab);
                return;
            }
            string modelName = option.ResourceName;

            // These are Resources models, not GameResources object bundles.
            GameObject prefab = Resources.Load<GameObject>(
                "Art/Character/Maergh01/" + modelName);
            Transform original = skeleton.transform.Find("Maergh01");
            Animator animator = skeleton.GetComponent<Animator>();
            Animator modelAnimator = prefab != null
                ? prefab.GetComponent<Animator>()
                : null;

            if (original == null || animator == null || modelAnimator == null ||
                modelAnimator.avatar == null || !modelAnimator.avatar.isValid ||
                !modelAnimator.avatar.isHuman ||
                AnimationController.SearchForBoneTransform("primaryWeapon", prefab.transform) == null ||
                AnimationController.SearchForBoneTransform("secondaryWeapon", prefab.transform) == null)
            {
                Main.Logger.Error("Could not configure " + modelName +
                    "; keeping the original human model and applying human bonuses.");
                SkeletonModelCatalog.Find("human").ApplyBonuses(skeleton, sourcePrefab);
                return;
            }

            // Apply runs immediately after instantiation, before Equipment.Start
            // attaches weapons and AnimationController.Start caches the rig.
            GameObject model = Object.Instantiate<GameObject>(prefab);
            model.name = modelName;
            model.transform.SetParent(skeleton.transform, false);
            model.transform.localPosition = original.localPosition;
            model.transform.localRotation = original.localRotation;
            model.transform.localScale = original.localScale;

            foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = original.gameObject.layer;
            }

            // The summon root owns animation events and the combat controller.
            // Only its avatar changes; the imported model must not animate twice.
            model.GetComponent<Animator>().enabled = false;
            original.gameObject.SetActive(false);
            original.SetParent(null, false);
            animator.avatar = modelAnimator.avatar;
            animator.Rebind();

            AnimationController controller = skeleton.GetComponent<AnimationController>();
            if (controller != null)
            {
                controller.RacialBodyTypeOverride = option.BodyType;
                controller.BindComponents();
            }

            AnimationBoneMapper mapper = skeleton.GetComponent<AnimationBoneMapper>();
            if (mapper != null)
            {
                mapper.Reinitialize();
            }

            Object.Destroy(original.gameObject);
            option.ApplyBonuses(skeleton, sourcePrefab);
            Main.Logger.Log("Skeleton " + (summonIndex + 1) + " model set to " + modelName + ".");
        }
    }
}
