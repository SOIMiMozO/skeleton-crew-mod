using UnityModManagerNet;

namespace SkeletonCrew
{
    public class Settings : UnityModManager.ModSettings
    {
        public bool UseFampyrAppearance = true;
        public string SkeletonName1 = "Human Skeleton Uno";
        public string SkeletonName2 = "Human Skeleton Dos";
        public string SkeletonName3 = "Human Skeleton Tres";

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
}
