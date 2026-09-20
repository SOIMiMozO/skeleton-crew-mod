namespace SkeletonCrew
{
    public sealed class SkeletonModelOption
    {
        public readonly string Id;
        public readonly string Label;
        public readonly string ResourceName;
        public readonly CharacterStats.Race BodyType;
        public readonly int Might;
        public readonly int Dexterity;
        public readonly int Constitution;
        public readonly int Resolve;
        public readonly int Perception;
        public readonly string BonusDescription;

        public SkeletonModelOption(string id, string label, string resourceName,
            CharacterStats.Race bodyType, string bonusDescription,
            int might = 0, int dexterity = 0, int constitution = 0,
            int resolve = 0, int perception = 0)
        {
            Id = id;
            Label = label;
            ResourceName = resourceName;
            BodyType = bodyType;
            BonusDescription = bonusDescription;
            Might = might;
            Dexterity = dexterity;
            Constitution = constitution;
            Resolve = resolve;
            Perception = perception;
        }

        public void ApplyBonuses(CharacterStats skeleton, CharacterStats sourcePrefab)
        {
            // Always use the untouched summon prefab as the baseline.
            skeleton.BaseMight = sourcePrefab.BaseMight + Might;
            skeleton.BaseDexterity = sourcePrefab.BaseDexterity + Dexterity;
            skeleton.BaseConstitution = sourcePrefab.BaseConstitution + Constitution;
            skeleton.BaseResolve = sourcePrefab.BaseResolve + Resolve;
            skeleton.BasePerception = sourcePrefab.BasePerception + Perception;
        }
    }

    public static class SkeletonModelCatalog
    {
        // Add models and their bonuses here. A null resource keeps the original model.
        // Keep IDs stable because saved settings refer to them.
        public static readonly SkeletonModelOption[] Options =
        {
            new SkeletonModelOption("human", "Human", null, CharacterStats.Race.Human,
                "+1 Might, +1 Resolve", might: 1, resolve: 1),
            new SkeletonModelOption("aumaua", "Aumaua", "Maergh01_AUM", CharacterStats.Race.Aumaua,
                "+2 Might", might: 2),
            new SkeletonModelOption("dwarf", "Dwarf", "Maergh01_DWA", CharacterStats.Race.Dwarf,
                "+1 Might, -1 Dexterity, +2 Constitution", might: 1, dexterity: -1, constitution: 2),
            new SkeletonModelOption("orlan", "Orlan", "Maergh01_ORL", CharacterStats.Race.Orlan,
                "-1 Might, +1 Resolve, +2 Perception", might: -1, resolve: 1, perception: 2)
        };

        public static SkeletonModelOption Find(string id)
        {
            foreach (SkeletonModelOption option in Options)
                if (option.Id == id)
                    return option;
            return Options[0];
        }
    }
}
