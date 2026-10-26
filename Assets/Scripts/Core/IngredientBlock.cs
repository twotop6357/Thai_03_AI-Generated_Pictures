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
        Garlic, // 이전 사진 기록의 ID 7을 보존한다. 새 인식은 허용하지 않는다.
        Carrot,
        Potato,
        Cucumber,
        Cabbage,
        ChiliPepper,
        Mushroom,
        CoconutMilk, // 이전 사진 기록의 ID 14를 보존한다.
        RiceNoodle = 15,
        Tofu = 16
    }

    public static class IngredientBlockNames
    {
        public static System.Collections.Generic.IReadOnlyList<IngredientBlock> ActiveBlocks { get; } =
            System.Array.AsReadOnly(new[] { IngredientBlock.Rice, IngredientBlock.Egg, IngredientBlock.Chicken,
                IngredientBlock.Beef, IngredientBlock.Fish, IngredientBlock.Shrimp, IngredientBlock.Onion,
                IngredientBlock.RiceNoodle, IngredientBlock.Carrot, IngredientBlock.Potato, IngredientBlock.Cucumber,
                IngredientBlock.Cabbage, IngredientBlock.ChiliPepper, IngredientBlock.Mushroom, IngredientBlock.Tofu });

        public static bool IsRecognizable(IngredientBlock block) =>
            block >= IngredientBlock.Rice && block <= IngredientBlock.Tofu &&
            block != IngredientBlock.Garlic && block != IngredientBlock.CoconutMilk;

        public static string GetDisplayName(IngredientBlock block)
        {
            switch (block)
            {
                case IngredientBlock.ChiliPepper: return "Chili pepper";
                case IngredientBlock.CoconutMilk: return "Coconut milk";
                case IngredientBlock.RiceNoodle: return "Rice noodle";
                default: return block.ToString();
            }
        }
    }
}
