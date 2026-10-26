using System;
using System.Collections.Generic;
using System.Globalization;
using Core;
using Newtonsoft.Json.Linq;

namespace Network
{
    /// <summary>Python에서 내보낸 동일 데이터로 태국 음식 프롬프트와 ComfyUI 요청을 만든다.</summary>
    public sealed class ComfyFoodWorkflow
    {
        private readonly JObject _definition;
        private static readonly HashSet<string> LocalNodes = new HashSet<string>
        {
            "UNETLoader", "CLIPLoader", "VAELoader", "CLIPTextEncode", "ConditioningZeroOut",
            "EmptyFlux2LatentImage", "RandomNoise", "KSamplerSelect", "Flux2Scheduler",
            "CFGGuider", "SamplerCustomAdvanced", "VAEDecode", "SaveImage"
        };

        public ComfyFoodWorkflow(string json)
        {
            _definition = JObject.Parse(json);
            if (!(_definition["workflow"] is JObject workflow) ||
                !(_definition["ingredients"] is JObject) || !(_definition["dishes"] is JObject) ||
                string.IsNullOrEmpty((string)_definition["prompt_template"]))
                throw new ArgumentException("Workflow and shared prompt data are required.", nameof(json));

            foreach (var node in workflow.Properties())
            {
                if (!LocalNodes.Contains((string)node.Value["class_type"] ?? string.Empty))
                    throw new ArgumentException("Only the approved local food workflow nodes are allowed.", nameof(json));
            }
        }

        public string BuildPrompt(IReadOnlyList<IngredientBlock> blocks)
        {
            if (blocks == null || blocks.Count < 1 || blocks.Count > BlockDetectionController.Capacity)
                throw new ArgumentException("Select one to six ingredient blocks.", nameof(blocks));

            var names = new string[blocks.Count];
            var descriptions = new string[blocks.Count];
            bool rice = false, coconut = false, cucumber = false, chili = false;
            for (int i = 0; i < blocks.Count; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    if (blocks[i] == blocks[j])
                        throw new ArgumentException("Ingredient blocks must be unique.", nameof(blocks));
                }

                names[i] = IngredientBlockNames.GetDisplayName(blocks[i]);
                descriptions[i] = (string)_definition["ingredients"][names[i]];
                if (string.IsNullOrEmpty(descriptions[i]))
                    throw new ArgumentException("Unknown ingredient block: " + names[i], nameof(blocks));
                rice |= blocks[i] == IngredientBlock.Rice;
                coconut |= blocks[i] == IngredientBlock.CoconutMilk;
                cucumber |= blocks[i] == IngredientBlock.Cucumber;
                chili |= blocks[i] == IngredientBlock.ChiliPepper;
            }

            string dishKey = blocks.Count == 1 && rice ? "rice_only" :
                blocks.Count == 1 && coconut ? "coconut_only" : coconut ? "curry" :
                rice ? "fried_rice" : cucumber ? "salad" : "stir_fry";
            string dish = (string)_definition["dishes"][dishKey];
            if (dishKey == "curry" && chili && _definition["dishes"]["curry_with_chili"] is JValue chiliDish)
                dish = (string)chiliDish;
            if (blocks.Count == 1 && !rice && !coconut &&
                _definition["dishes"]["single_ingredient"] is JValue singleDish)
                dish = string.Format(CultureInfo.InvariantCulture, (string)singleDish, names[0]);
            if (coconut && rice)
                dish += (string)_definition["dishes"]["curry_rice_suffix"];

            string template = blocks.Count == 1 ? (string)_definition["single_ingredient_prompt_template"] : null;
            return string.Format(CultureInfo.InvariantCulture, template ?? (string)_definition["prompt_template"],
                dish, blocks.Count, string.Join(", ", names), string.Join("; ", descriptions));
        }

        public JObject BuildRequest(IReadOnlyList<IngredientBlock> blocks, long seed)
        {
            var prompt = (JObject)_definition["workflow"].DeepClone();
            prompt["4"]["inputs"]["text"] = BuildPrompt(blocks);
            prompt["7"]["inputs"]["noise_seed"] = seed;
            prompt["13"]["inputs"]["filename_prefix"] = "Thai03/UnityFood";
            var names = new JArray();
            for (int i = 0; i < blocks.Count; i++)
                names.Add(IngredientBlockNames.GetDisplayName(blocks[i]));

            return new JObject
            {
                ["prompt"] = prompt,
                ["client_id"] = "thai03-unity-" + Guid.NewGuid().ToString("N"),
                ["extra_data"] = new JObject { ["extra_pnginfo"] = new JObject
                    { ["thai03"] = new JObject { ["ingredients"] = names } } }
            };
        }
    }
}
