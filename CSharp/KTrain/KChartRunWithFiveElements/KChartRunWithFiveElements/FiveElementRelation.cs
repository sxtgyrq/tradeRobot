namespace KChartRunWithFiveElements
{
    internal enum FiveElementRelation
    {
        Unrelated,
        Generating,
        Restraining
    }

    internal static class FiveElementRelationCalculator
    {
        public static FiveElementRelation GetRelation(FiveElement left, FiveElement right)
        {
            if (left == right)
            {
                return FiveElementRelation.Unrelated;
            }

            if (IsGeneratingPair(left, right))
            {
                return FiveElementRelation.Generating;
            }

            if (IsRestrainingPair(left, right))
            {
                return FiveElementRelation.Restraining;
            }

            return FiveElementRelation.Unrelated;
        }

        private static bool IsGeneratingPair(FiveElement left, FiveElement right)
        {
            return (left == FiveElement.Metal && right == FiveElement.Water) ||
                   (left == FiveElement.Water && right == FiveElement.Metal) ||
                   (left == FiveElement.Water && right == FiveElement.Wood) ||
                   (left == FiveElement.Wood && right == FiveElement.Water) ||
                   (left == FiveElement.Wood && right == FiveElement.Fire) ||
                   (left == FiveElement.Fire && right == FiveElement.Wood) ||
                   (left == FiveElement.Fire && right == FiveElement.Earth) ||
                   (left == FiveElement.Earth && right == FiveElement.Fire) ||
                   (left == FiveElement.Earth && right == FiveElement.Metal) ||
                   (left == FiveElement.Metal && right == FiveElement.Earth);
        }

        private static bool IsRestrainingPair(FiveElement left, FiveElement right)
        {
            return (left == FiveElement.Metal && right == FiveElement.Wood) ||
                   (left == FiveElement.Wood && right == FiveElement.Metal) ||
                   (left == FiveElement.Wood && right == FiveElement.Earth) ||
                   (left == FiveElement.Earth && right == FiveElement.Wood) ||
                   (left == FiveElement.Earth && right == FiveElement.Water) ||
                   (left == FiveElement.Water && right == FiveElement.Earth) ||
                   (left == FiveElement.Water && right == FiveElement.Fire) ||
                   (left == FiveElement.Fire && right == FiveElement.Water) ||
                   (left == FiveElement.Fire && right == FiveElement.Metal) ||
                   (left == FiveElement.Metal && right == FiveElement.Fire);
        }
    }
}
