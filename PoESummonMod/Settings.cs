using UnityModManagerNet;

namespace SkeletonCrew
{
    public enum SkeletonWeaponLoadout
    {
        SpearAndShield,
        SabreAndDagger,
        MorningStar,
        SabreAndShield,
        DaggerAndShield
    }

    public class Settings : UnityModManager.ModSettings
    {
        public string SkeletonName1 = "Human Skeleton Uno";
        public string SkeletonName2 = "Human Skeleton Dos";
        public string SkeletonName3 = "Human Skeleton Tres";

        public SkeletonWeaponLoadout SkeletonWeapons1 = SkeletonWeaponLoadout.SpearAndShield;
        public SkeletonWeaponLoadout SkeletonWeapons2 = SkeletonWeaponLoadout.SabreAndDagger;
        public SkeletonWeaponLoadout SkeletonWeapons3 = SkeletonWeaponLoadout.MorningStar;

        public SkeletonHands SkeletonHands1;
        public SkeletonHands SkeletonHands2;
        public SkeletonHands SkeletonHands3;

        public string SkeletonModel1 = "human";
        public string SkeletonModel2 = "orlan";
        public string SkeletonModel3 = "aumaua";

        public SkeletonModelOption GetModel(int index)
        {
            switch (index)
            {
                case 0: return SkeletonModelCatalog.Find(SkeletonModel1);
                case 1: return SkeletonModelCatalog.Find(SkeletonModel2);
                case 2: return SkeletonModelCatalog.Find(SkeletonModel3);
                default: throw new System.ArgumentOutOfRangeException(nameof(index));
            }
        }

        public SkeletonHands GetHands(int index)
        {
            SkeletonHands hands;
            switch (index)
            {
                case 0: hands = SkeletonHands1 ?? (SkeletonHands1 = Migrate(SkeletonWeapons1)); break;
                case 1: hands = SkeletonHands2 ?? (SkeletonHands2 = Migrate(SkeletonWeapons2)); break;
                case 2: hands = SkeletonHands3 ?? (SkeletonHands3 = Migrate(SkeletonWeapons3)); break;
                default: throw new System.ArgumentOutOfRangeException(nameof(index));
            }
            hands.Normalize();
            return hands;
        }

        // Preserve selections saved by the earlier single-loadout dropdown.
        private static SkeletonHands Migrate(SkeletonWeaponLoadout loadout)
        {
            switch (loadout)
            {
                case SkeletonWeaponLoadout.SabreAndDagger:
                    return new SkeletonHands { MainHand = "sabre", OffHand = "dagger" };
                case SkeletonWeaponLoadout.MorningStar:
                    return new SkeletonHands { MainHand = "morning_star01" };
                case SkeletonWeaponLoadout.SabreAndShield:
                    return new SkeletonHands { MainHand = "sabre", OffHand = "shield" };
                case SkeletonWeaponLoadout.DaggerAndShield:
                    return new SkeletonHands { MainHand = "dagger", OffHand = "shield" };
                default:
                    return new SkeletonHands { MainHand = "spear", OffHand = "shield" };
            }
        }

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            for (int i = 0; i < 3; i++)
                GetHands(i);
            Save(this, modEntry);
        }
    }
}
