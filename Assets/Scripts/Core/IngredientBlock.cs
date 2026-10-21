namespace Core
{
    public enum IngredientBlock
    {
        Rice,
        Egg,
        Chicken,
        Beef,
        Fish,
        Shrimp,
        Onion,
        Garlic,
        Carrot,
        Potato,
        Cucumber,
        Cabbage,
        ChiliPepper,
        Mushroom,
        CoconutMilk
    }

    public static class IngredientBlockNames
    {
        public static string GetDisplayName(IngredientBlock block)
        {
            switch (block)
            {
                case IngredientBlock.ChiliPepper: return "Chili pepper";
                case IngredientBlock.CoconutMilk: return "Coconut milk";
                default: return block.ToString();
            }
        }
    }
}
