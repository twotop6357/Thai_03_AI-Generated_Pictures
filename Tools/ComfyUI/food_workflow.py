"""Build ComfyUI food-photo graphs for 1-6 detected ingredient blocks (stdlib only)."""

import argparse
import json
import secrets
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path


ROOT = Path(__file__).resolve().parent
INGREDIENTS = {
    'Rice': 'Thai jasmine rice with clearly visible individual grains',
    'Egg': 'large pieces of Thai-style crispy fried egg with visible yellow yolk and white egg white',
    'Chicken': 'wok-seared chicken slices with golden edges and white fibrous interiors',
    'Beef': 'thin wok-fried beef strips with browned edges and recognizable meat fibers',
    'Fish': 'a cooked fish fillet with visible silvery skin and flaky white flesh',
    'Shrimp': 'whole peeled cooked pink curled shrimp with recognizable tails',
    'Onion': 'caramelized golden-brown onion petals, visibly cooked and glossy, with curved onion layers',
    'Rice noodle': 'soft cooked Thai flat rice noodles with long visible translucent ivory ribbons and distinct noodle strands',
    'Carrot': 'glossy roasted bright orange carrot rounds with lightly browned edges and circular centers',
    'Potato': 'large cooked golden potato chunks with recognizable pale yellow interiors',
    'Cucumber': 'fresh green cucumber rounds with dark green peel and pale seeded centers',
    'Cabbage': 'wilted sauteed cabbage leaf pieces with seared edges and pale green veins',
    'Chili pepper': "small thin red bird's-eye chili rings with tiny white seeds, not large bell peppers",
    'Mushroom': 'glossy pan-fried brown mushroom halves with dark seared surfaces, recognizable caps and stems',
    'Tofu': 'firm tofu cut into bite-size cubes, gently pan-fried golden edges and clearly visible pale porous white cut faces',
}
ALIASES = {name.replace(" ", "").lower(): name for name in INGREDIENTS}

DISHES = {
    'rice_only': 'ONLY plain freshly steamed Thai jasmine rice, a simple serving of rice grains with no toppings or other food',
    'noodles_only': 'ONLY cooked Thai flat rice noodles, a loose serving of soft ivory noodle ribbons with bare plate around them, no toppings or other food',
    'tofu_only': 'ONLY golden pan-seared firm tofu cubes with visible white porous cut faces, no other food, vegetable base or sauce pool',
    'noodle_stir_fry': 'Thai PAD SEE EW inspired wok-fried flat rice noodles, long glossy noodle ribbons tossed with the selected bite-size pieces, a light amber seasoning glaze and delicate charred noodle edges',
    'noodles_rice_suffix': ', with the selected jasmine rice grains also visibly incorporated at one edge of this SAME plate without hiding the noodles',
    'fried_rice': 'Thai KHAO PAD, loose wok-fried jasmine rice with the selected bite-size ingredients tossed through the grains, lightly toasted and glossy',
    'salad': 'Thai YAM inspired salad, selected fresh cucumber and the other prepared ingredients tossed together with a light glossy dressing, all selected proteins cooked',
    'stir_fry': 'Thai PAD style wok stir-fry of the selected bite-size ingredients, lightly seared and tossed in a thin glossy seasoning glaze',
    'single_ingredient': 'a simple Thai-style preparation of {0} alone, without any other food ingredient',
}
PROMPT_TEMPLATE = (
    'Photorealistic Thai food photograph: {0}. '
    'Exactly {1} selected food ingredients: {2}. '
    'The visible prepared ingredients are: {3}. '
    'One coherent finished Thai dish, with ALL selected ingredients recognizable inside the SAME serving. '
    'Naturally intermix the cut pieces while keeping their recognizable surfaces exposed. '
    'Use only the selected visible foods, without unselected garnishes or side dishes. '
    'No separate ingredient bowls, raw ingredient displays, text or hands. '
    'Close overhead food photograph on one blue-and-white Thai ceramic plate, warm natural light, realistic cooking texture, the full serving in focus.'
)

DETAIL_INGREDIENTS = {
    'Rice': 'Thai jasmine rice with separate elongated grains, delicate grain edges and a light moist sheen',
    'Egg': 'a Thai crispy fried egg with fine lacy golden edges, gently blistered opaque egg white and a rich glossy yellow yolk',
    'Chicken': 'cooked chicken slices with small caramelized sear patches, moist white fibrous interiors and natural irregular cut edges',
    'Beef': 'cooked thin beef strips with fine visible meat fibers, browned seared edges and a light natural juice sheen',
    'Fish': 'a cooked fish fillet with fine silvery skin texture, delicate white flakes separating along natural muscle layers and moist flesh',
    'Shrimp': 'cooked pink-orange curled shrimp with distinct segmented flesh, gently seared edges and recognizable tails',
    'Onion': 'soft glossy caramelized onion petals with translucent curved layers and irregular golden-brown seared edges',
    'Rice noodle': 'cooked flat rice noodle ribbons, long glossy ivory strands with slightly translucent edges, naturally folded and individually visible',
    'Carrot': 'bright orange cooked carrot rounds with fine radial centers, moist cut surfaces and lightly caramelized edges',
    'Potato': 'golden cooked potato chunks with fine crisp browned edges and soft slightly crumbly pale yellow cut interiors',
    'Cucumber': 'crisp cucumber rounds with finely textured dark green peel, translucent pale flesh and distinct wet seed cavities',
    'Cabbage': 'soft sauteed cabbage pieces with thin folded pale green leaves, visible branching veins and gently seared edges',
    'Chili pepper': "small thin red bird's-eye chili rings with delicate glossy red skin, hollow centers and tiny visible white seeds",
    'Mushroom': 'pan-fried brown mushroom halves with irregular caramelized caps, recognizable stems and fine visible gills on cut sides',
    'Tofu': 'pan-seared firm tofu cubes with delicate irregular golden crust, fine porous pale white cut faces and straight cubed edges',
}
DETAIL_PROMPT_TEMPLATE = (
    'Photorealistic Thai food photograph: {0}. '
    'Exactly {1} selected food ingredients: {2}. '
    'The visible prepared ingredients are: {3}. '
    'One coherent finished Thai dish, with ALL selected ingredients recognizable inside the SAME serving. '
    'Naturally intermix the cut pieces while keeping their recognizable surfaces exposed. '
    'Use only the selected visible foods, without unselected garnishes or side dishes. '
    'No separate ingredient bowls, raw ingredient displays, text or hands. '
    'One shallow blue-and-white Thai ceramic plate. '
    'Soft directional light shows delicate sear marks, moist cut faces and realistic fine food texture. '
    'All selected ingredients are sharply focused inside the frame.'
)
DETAIL_DISHES = {
    'rice_only': 'ONLY plain freshly steamed Thai jasmine rice, a simple serving of rice grains with no toppings or other food',
    'noodles_only': 'ONLY cooked Thai flat rice noodles, a loose serving of soft ivory noodle ribbons with bare plate around them, no toppings or other food',
    'tofu_only': 'ONLY golden pan-seared firm tofu cubes with visible white porous cut faces, no other food, vegetable base or sauce pool',
    'noodle_stir_fry': 'Thai PAD SEE EW inspired wok-fried flat rice noodles, long glossy noodle ribbons tossed with the selected bite-size pieces, a light amber seasoning glaze and delicate charred noodle edges',
    'noodles_rice_suffix': ', with the selected jasmine rice grains also visibly incorporated at one edge of this SAME plate without hiding the noodles',
    'fried_rice': 'Thai KHAO PAD, loose wok-fried jasmine rice with the selected bite-size ingredients tossed through the grains, lightly toasted and glossy',
    'salad': 'Thai YAM inspired salad, selected fresh cucumber and the other prepared ingredients tossed together with a light glossy dressing, all selected proteins cooked',
    'stir_fry': 'Thai PAD style wok stir-fry of the selected bite-size ingredients, lightly seared and tossed in a thin glossy seasoning glaze',
    'single_ingredient': 'a simple Thai-style preparation of {0} alone, without any other food ingredient',
}
DETAIL_SINGLE_PROMPT_TEMPLATE = (
    'Close-up Thai food photograph of {0}. '
    'Exactly ONE selected food ingredient: {2}. '
    'Actual food appearance: {3}. '
    'Only this food is served, with bare plate around it. '
    'No additional food ingredients, garnishes or side dishes. '
    'The entire serving is visible, sharp natural cut surfaces, realistic texture, soft diffuse light. '
    'One blue-and-white Thai ceramic plate, tight overhead composition.'
)


PREPARED_INGREDIENTS = {
    'Rice': 'loose separate elongated Thai jasmine rice grains, lightly golden and glossy where wok-fried, naturally mixed with the selected cut pieces',
    'Egg': 'many SHORT golden Thai omelet ribbons already chopped into bite-size strips and mixed between the other pieces, tender yellow cut interiors and crisp lacy edges, not an intact omelet or fried egg',
    'Chicken': 'small thin irregular 2 cm boneless chicken slices cut across the grain, cooked opaque white with lightly browned wok-seared edges, not large grilled breast strips',
    'Beef': 'thin bite-size beef slices cut across the grain, tender browned surfaces and visible fine meat fibers, no whole steak',
    'Fish': 'deboned fish cut into small cooked fillet chunks, flaky white cut faces and occasional slender silvery skin edges, no whole fish',
    'Shrimp': 'cleaned peeled deveined shrimp, heads and shells removed, butterfly-cut curved pink shrimp meat with visible segmented flesh',
    'Onion': 'thin curved onion slices with translucent cooked layers, softened and lightly browned in the wok',
    'Rice noodle': 'Thai flat rice noodles softened and wok-tossed into loose long glossy ribbons, recognizable folded strands intermingled with the other selected pieces',
    'Carrot': 'short matchsticks and thin diagonal slices of orange carrot, lightly cooked with moist recognizable orange cut faces',
    'Potato': 'peeled potato cut into small spoon-size cubes, cooked golden outside with soft pale yellow cut faces',
    'Cucumber': 'fresh cucumber cut into thin diagonal half-moons with dark green peel and visible pale seeded centers, mixed into the salad',
    'Cabbage': 'cabbage cut into short bite-size leaf ribbons, wok-wilted pale green folds with visible veins and lightly seared edges',
    'Chili pepper': "thin diagonal slices of small red Thai bird's-eye chili with glossy red skin and a few tiny pale seeds",
    'Mushroom': 'trimmed brown mushrooms cut into bite-size quarters and thick slices, cooked glossy caps and visible pale cut faces with gills',
    'Tofu': 'firm tofu cut into 1.5 cm cubes, lightly fried golden on the outside with moist finely porous ivory cut interiors, intact recognizable cubes',
}
PREPARED_DISHES = {
    'rice_only': 'ONLY plain freshly steamed Thai jasmine rice, a simple serving of rice grains with no toppings or other food',
    'noodles_only': 'ONLY cooked Thai flat rice noodles, a loose serving of soft ivory noodle ribbons with bare plate around them, no toppings or other food',
    'tofu_only': 'ONLY golden pan-seared firm tofu cubes with visible white porous cut faces, no other food, vegetable base or sauce pool',
    'noodle_stir_fry': 'Thai PAD SEE EW inspired wok-fried flat rice noodles, long glossy noodle ribbons tossed with the selected bite-size pieces, a light amber seasoning glaze and delicate charred noodle edges',
    'noodles_rice_suffix': ', with the selected jasmine rice grains also visibly incorporated at one edge of this SAME plate without hiding the noodles',
    'fried_rice': 'Thai KHAO PAD, loose wok-fried jasmine rice with the selected bite-size ingredients tossed through the grains, lightly toasted and glossy',
    'salad': 'Thai YAM inspired salad, selected fresh cucumber and the other prepared ingredients tossed together with a light glossy dressing, all selected proteins cooked',
    'stir_fry': 'Thai PAD style wok stir-fry of the selected bite-size ingredients, lightly seared and tossed in a thin glossy seasoning glaze',
    'single_ingredient': 'a simple Thai-style preparation of {0} alone, without any other food ingredient',
}
PREPARED_PROMPT_TEMPLATE = (
    'Photorealistic Thai food photograph: {0}. '
    'Exactly {1} selected food ingredients: {2}. '
    'The visible prepared ingredients are: {3}. '
    'One coherent finished Thai dish, with ALL selected ingredients recognizable inside the SAME serving. '
    'Naturally intermix the cut pieces while keeping their recognizable surfaces exposed. '
    'Use only the selected visible foods, without unselected garnishes or side dishes. '
    'No separate ingredient bowls, raw ingredient displays, text or hands. '
    'All ingredients are cleaned and cut as appropriate, ready to eat, naturally irregular in shape and gently wok-seared. '
    'One shallow Thai ceramic plate, close overhead view, natural soft light and believable fine detail.'
)
PREPARED_SINGLE_PROMPT_TEMPLATE = (
    'Close-up Thai food photograph of {0}. '
    'Exactly ONE selected food ingredient: {2}. '
    'Actual food appearance: {3}. '
    'Only this food is served, with bare plate around it. '
    'No additional food ingredients, garnishes or side dishes. '
    'The entire serving is visible, sharp natural cut surfaces, realistic texture, soft diffuse light. '
    'Ready-to-eat preparation in one shallow Thai ceramic plate, close overhead composition.'
)

FOCUS_INGREDIENTS = {
    'Rice': 'loose separate elongated Thai jasmine rice grains, lightly toasted by a hot wok when fried, interspersed with the selected pieces',
    'Egg': 'irregular bite-size torn pieces of Thai khai jiao, a fluffy deeply golden fried omelet with airy bubbles, blistered craggy crisp edges and soft egg interior',
    'Chicken': 'small thin irregular 2 cm boneless chicken slices cut across the grain, cooked opaque white with lightly browned wok-seared edges, not large grilled breast strips',
    'Beef': 'thin bite-size beef slices cut across the grain, tender browned surfaces and visible fine meat fibers, no whole steak',
    'Fish': 'deboned fish cut into small cooked fillet chunks, flaky white cut faces and occasional slender silvery skin edges, no whole fish',
    'Shrimp': 'cleaned peeled deveined shrimp, heads and shells removed, butterfly-cut curved pink shrimp meat with visible segmented flesh',
    'Onion': 'thin curved onion slices with translucent cooked layers, softened and lightly browned in the wok',
    'Rice noodle': 'Thai sen yai flat RICE NOODLE ribbons, soft long translucent ivory strands with light wok-browned edges, loose glossy folds visibly interwoven through the dish',
    'Carrot': 'short matchsticks and thin diagonal slices of orange carrot, lightly cooked with moist recognizable orange cut faces',
    'Potato': 'peeled potato cut into small spoon-size cubes, cooked golden outside with soft pale yellow cut faces',
    'Cucumber': 'fresh cucumber cut into thin diagonal half-moons with dark green peel and visible pale seeded centers, mixed into the salad',
    'Cabbage': 'cabbage cut into short bite-size leaf ribbons, wok-wilted pale green folds with visible veins and lightly seared edges',
    'Chili pepper': "thin diagonal slices of small red Thai bird's-eye chili with glossy red skin and a few tiny pale seeds",
    'Mushroom': 'trimmed brown mushrooms cut into bite-size quarters and thick slices, cooked glossy caps and visible pale cut faces with gills',
    'Tofu': 'bite-size firm TOFU cubes with lightly blistered golden pan-seared surfaces and finely porous WHITE cut faces, angular cubes remain clearly recognizable',
}
FOCUS_DISHES = {
    'rice_only': 'ONLY plain freshly steamed Thai jasmine rice, a simple serving of rice grains with no toppings or other food',
    'noodles_only': 'ONLY cooked Thai flat rice noodles, a loose serving of soft ivory noodle ribbons with bare plate around them, no toppings or other food',
    'tofu_only': 'ONLY golden pan-seared firm tofu cubes with visible white porous cut faces, no other food, vegetable base or sauce pool',
    'noodle_stir_fry': 'Thai PAD SEE EW inspired wok-fried flat rice noodles, long glossy noodle ribbons tossed with the selected bite-size pieces, a light amber seasoning glaze and delicate charred noodle edges',
    'noodles_rice_suffix': ', with the selected jasmine rice grains also visibly incorporated at one edge of this SAME plate without hiding the noodles',
    'fried_rice': 'Thai KHAO PAD, loose wok-fried jasmine rice with the selected bite-size ingredients tossed through the grains, lightly toasted and glossy',
    'salad': 'Thai YAM inspired salad, selected fresh cucumber and the other prepared ingredients tossed together with a light glossy dressing, all selected proteins cooked',
    'stir_fry': 'Thai PAD style wok stir-fry of the selected bite-size ingredients, lightly seared and tossed in a thin glossy seasoning glaze',
    'single_ingredient': 'a simple Thai-style preparation of {0} alone, without any other food ingredient',
}
FOCUS_PROMPT_TEMPLATE = (
    'Photorealistic Thai food photograph: {0}. '
    'Exactly {1} selected food ingredients: {2}. '
    'The visible prepared ingredients are: {3}. '
    'One coherent finished Thai dish, with ALL selected ingredients recognizable inside the SAME serving. '
    'Naturally intermix the cut pieces while keeping their recognizable surfaces exposed. '
    'Use only the selected visible foods, without unselected garnishes or side dishes. '
    'No separate ingredient bowls, raw ingredient displays, text or hands. '
    'Focus on the FOOD itself on one plain pale mint-green low-rim plate. '
    'Authentic Thai cooking textures and naturally overlapping prepared pieces. '
    'The serving fills 90 percent of the frame, all foods visible and in sharp focus. '
    'A narrow plain neutral matte background only. '
    'Soft diffuse light reveals sauce and cut surfaces. '
    'No market, scenery, restaurant interior, people, utensils or cooking pans.'
)
FOCUS_SINGLE_PROMPT_TEMPLATE = (
    'Close-up Thai food photograph of {0}. '
    'Exactly ONE selected food ingredient: {2}. '
    'Actual food appearance: {3}. '
    'Only this food is served, with bare plate around it. '
    'No additional food ingredients, garnishes or side dishes. '
    'The entire serving is visible, sharp natural cut surfaces, realistic texture, soft diffuse light. '
    'One plain pale mint-green low-rim plate. '
    'Food fills almost the entire frame; only a narrow plain neutral matte background. '
    'No market, scenery, people, utensils or cooking pans.'
)


def profile_dishes(profile):
    if profile == "thai-focus-v4":
        return FOCUS_DISHES
    if profile == "thai-prepared-v3":
        return PREPARED_DISHES
    return DETAIL_DISHES if profile == "detail-v2" else DISHES


def profile_data(profile):
    if profile == "legacy":
        return INGREDIENTS, PROMPT_TEMPLATE, 512
    if profile == "detail-v2":
        return DETAIL_INGREDIENTS, DETAIL_PROMPT_TEMPLATE, 640
    if profile == "thai-prepared-v3":
        return PREPARED_INGREDIENTS, PREPARED_PROMPT_TEMPLATE, 640
    if profile == "thai-focus-v4":
        return FOCUS_INGREDIENTS, FOCUS_PROMPT_TEMPLATE, 640
    raise ValueError(f"Unknown workflow profile: {profile}")


def normalize(names):
    if not 1 <= len(names) <= 6:
        raise ValueError("Select 1 to 6 ingredients.")
    result = []
    for name in names:
        key = name.replace(" ", "").lower()
        if key not in ALIASES:
            raise ValueError(f"Unknown ingredient: {name}")
        canonical = ALIASES[key]
        if canonical in result:
            raise ValueError(f"Duplicate ingredient: {canonical}")
        result.append(canonical)
    return result


def make_prompt(names, profile="legacy"):
    names = normalize(names)
    descriptions, template, _ = profile_data(profile)
    dishes = profile_dishes(profile)
    if profile == "detail-v2" and len(names) == 1:
        template = DETAIL_SINGLE_PROMPT_TEMPLATE
    elif profile == "thai-prepared-v3" and len(names) == 1:
        template = PREPARED_SINGLE_PROMPT_TEMPLATE
    elif profile == "thai-focus-v4" and len(names) == 1:
        template = FOCUS_SINGLE_PROMPT_TEMPLATE
    if names == ["Rice"]:
        dish = dishes["rice_only"]
    elif names == ["Rice noodle"]:
        dish = dishes["noodles_only"]
    elif names == ["Tofu"]:
        dish = dishes["tofu_only"]
    elif len(names) == 1:
        dish = dishes["single_ingredient"].format(names[0])
    elif "Rice noodle" in names:
        dish = dishes["noodle_stir_fry"]
    elif "Rice" in names:
        dish = dishes["fried_rice"]
    elif "Cucumber" in names:
        dish = dishes["salad"]
    else:
        dish = dishes["stir_fry"]
    if "Rice noodle" in names and "Rice" in names:
        dish += dishes["noodles_rice_suffix"]
    components = "; ".join(descriptions[name] for name in names)
    return template.format(dish, len(names), ", ".join(names), components)


def build(names, seed, width=512, height=512, profile="legacy"):
    names = normalize(names)
    if width % 16 or height % 16 or not (256 <= width <= 1024 and 256 <= height <= 1024):
        raise ValueError("Dimensions must be multiples of 16 between 256 and 1024.")
    prompt = make_prompt(names, profile)
    api = {}
    ui_nodes = []
    links = []

    def node(number, kind, title, pos, size, widgets, values, inputs, outputs):
        api[str(number)] = {"class_type": kind, "inputs": dict(widgets), "_meta": {"title": title}}
        api[str(number)]["inputs"].update(inputs)
        ui = {"id": number, "type": kind, "title": title, "pos": pos, "size": size,
              "flags": {}, "order": len(ui_nodes), "mode": 0, "inputs": [], "outputs": [],
              "properties": {"Node name for S&R": kind, "cnr_id": "comfy-core"},
              "widgets_values": values}
        for name, (source, slot, data_type) in inputs.items():
            # API uses [node_id, output_slot], UI additionally stores connection types.
            api[str(number)]["inputs"][name] = [str(source), slot]
            link_id = len(links) + 1
            target_slot = len(ui["inputs"])
            ui["inputs"].append({"name": name, "type": data_type, "link": link_id})
            links.append([link_id, source, slot, number, target_slot, data_type])
            source_ui = next(n for n in ui_nodes if n["id"] == source)
            source_ui["outputs"][slot]["links"].append(link_id)
        for data_type in outputs:
            ui["outputs"].append({"name": data_type, "type": data_type, "links": []})
        ui_nodes.append(ui)

    node(1, "UNETLoader", "Distilled 4B FP8 / do not use base", [20, 20], [340, 100],
         {"unet_name": "flux-2-klein-4b-fp8.safetensors", "weight_dtype": "default"},
         ["flux-2-klein-4b-fp8.safetensors", "default"], {}, ["MODEL"])
    node(2, "CLIPLoader", "Qwen 3 4B text encoder", [20, 150], [340, 130],
         {"clip_name": "qwen_3_4b.safetensors", "type": "flux2", "device": "default"},
         ["qwen_3_4b.safetensors", "flux2", "default"], {}, ["CLIP"])
    node(3, "VAELoader", "Flux 2 VAE", [20, 310], [340, 70],
         {"vae_name": "flux2-vae.safetensors"}, ["flux2-vae.safetensors"], {}, ["VAE"])
    node(4, "CLIPTextEncode", f"1-6 detected ingredients: {', '.join(names)}", [400, 20], [550, 470],
         {"text": prompt}, [prompt], {"clip": (2, 0, "CLIP")}, ["CONDITIONING"])
    node(5, "ConditioningZeroOut", "Unused negative at CFG 1", [410, 520], [270, 60], {}, [],
         {"conditioning": (4, 0, "CONDITIONING")}, ["CONDITIONING"])
    node(6, "EmptyFlux2LatentImage", "Size / batch 1 / match scheduler size", [20, 420], [340, 120],
         {"width": width, "height": height, "batch_size": 1}, [width, height, 1], {}, ["LATENT"])
    node(7, "RandomNoise", "Seed / increment per photo", [20, 580], [340, 90],
         {"noise_seed": seed}, [seed, "increment"], {}, ["NOISE"])
    node(8, "KSamplerSelect", "Euler (4 evaluations)", [740, 530], [250, 70],
         {"sampler_name": "euler"}, ["euler"], {}, ["SAMPLER"])
    node(9, "Flux2Scheduler", "4 steps / match latent size", [740, 630], [250, 120],
         {"steps": 4, "width": width, "height": height}, [4, width, height], {}, ["SIGMAS"])
    node(10, "CFGGuider", "CFG 1 / single conditioning pass", [1010, 20], [310, 110], {"cfg": 1.0}, [1.0],
         {"model": (1, 0, "MODEL"), "positive": (4, 0, "CONDITIONING"), "negative": (5, 0, "CONDITIONING")}, ["GUIDER"])
    node(11, "SamplerCustomAdvanced", "Single-pass generation", [1030, 200], [310, 150], {}, [],
         {"noise": (7, 0, "NOISE"), "guider": (10, 0, "GUIDER"), "sampler": (8, 0, "SAMPLER"),
          "sigmas": (9, 0, "SIGMAS"), "latent_image": (6, 0, "LATENT")}, ["LATENT", "LATENT"])
    node(12, "VAEDecode", "Decode once", [1380, 200], [240, 80], {}, [],
         {"samples": (11, 0, "LATENT"), "vae": (3, 0, "VAE")}, ["IMAGE"])
    prefix = {"legacy": "Thai03/Food", "detail-v2": "Thai03/FoodNoodleTofuDefault",
              "thai-prepared-v3": "Thai03/FoodThaiPreparedV3", "thai-focus-v4": "Thai03/FoodNoodleTofuTest"}[profile]
    node(13, "SaveImage", "Finished dish / local PNG", [1660, 20], [420, 500],
         {"filename_prefix": prefix}, [prefix], {"images": (12, 0, "IMAGE")}, [])
    ui_nodes.append({"id": 14, "type": "MarkdownNote", "title": "Usage and acceptance limits",
                     "pos": [1020, 420], "size": [570, 300], "flags": {}, "order": 13, "mode": 0,
                     "inputs": [], "outputs": [], "properties": {}, "widgets_values": [
                         "Select 1-6 unique ingredients with food_workflow.py; unused slots are not filled.\n"
                         f"{width}x{height}, 4 steps, CFG 1, batch 1. No refiner, upscale or remote API.\n"
                         "Generation time on RTX 3060 is UNVERIFIED. Keep models warm, queue empty.\n"
                         "All ingredients are requested inside one finished dish. "
                         "Visual ingredient review is required.\n"
                         "Changing dimensions: update BOTH latent and Flux2Scheduler.\n"
                         "Source: docs.comfy.org/tutorials/flux/flux-2-klein"]})
    ui = {"last_node_id": 14, "last_link_id": len(links), "nodes": ui_nodes, "links": links,
          "groups": [], "config": {}, "extra": {"project": "Thai_03_AI-Generated_Pictures",
          "ingredients": names, "profile": profile, "ds": {"scale": 0.65, "offset": [40, 40]}}, "version": 0.4}
    return api, ui


def request(base, route, data=None, timeout=10):
    payload = None if data is None else json.dumps(data).encode("utf-8")
    req = urllib.request.Request(base + route, data=payload, headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=timeout) as response:
        return json.load(response)


def validate(api, schema):
    for key, node in api.items():
        kind = node["class_type"]
        if kind not in schema or schema[kind].get("api_node", False):
            raise ValueError(f"Missing or nonlocal node: {kind}")
        required = schema[kind]["input"].get("required", {})
        for name, spec in required.items():
            value = node["inputs"].get(name)
            if value is None:
                raise ValueError(f"Missing input {key}.{name}")
            if isinstance(value, list) and len(value) == 2 and str(value[0]) in api:
                source = api[str(value[0])]["class_type"]
                if schema[source]["output"][value[1]] != spec[0]:
                    raise ValueError(f"Connection type mismatch: {key}.{name}")
            elif isinstance(spec[0], list) and value not in spec[0]:
                raise ValueError(f"Unavailable model/option: {key}.{name}={value}")


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


def run_local(api, ui, base, budget):
    parsed = urllib.parse.urlparse(base)
    if parsed.scheme != "http" or parsed.hostname not in ("127.0.0.1", "localhost", "::1"):
        raise ValueError("Only a local ComfyUI HTTP server is allowed.")
    schema = request(base, "/object_info")
    validate(api, schema)
    queue = request(base, "/queue")
    if queue["queue_running"] or queue["queue_pending"]:
        raise ValueError("ComfyUI queue is busy. Wait for existing jobs; nothing was submitted.")
    stats = request(base, "/system_stats")
    started = time.perf_counter()
    accepted = request(base, "/prompt", {"prompt": api, "client_id": "thai03-food-" + secrets.token_hex(6),
                                        "extra_data": {"extra_pnginfo": {"workflow": ui}}})
    prompt_id = accepted["prompt_id"]
    print(f"[FoodWorkflow] Submitted local job {prompt_id}.", flush=True)
    # No automatic retry or global interrupt: other users' jobs must remain untouched.
    while time.perf_counter() - started < budget:
        history = request(base, "/history/" + prompt_id, timeout=min(3, max(0.1, budget - (time.perf_counter() - started))))
        if prompt_id in history:
            record = history[prompt_id]
            elapsed = time.perf_counter() - started
            if elapsed >= budget:
                raise TimeoutError(f"Job {prompt_id} completed after the {budget:g}s acceptance deadline; result rejected.")
            result = {"prompt_id": prompt_id, "wall_seconds": round(elapsed, 3),
                      "deadline_seconds": budget, "under_deadline": elapsed < budget,
                      "gpu": [d["name"] for d in stats.get("devices", [])],
                      "comfyui_version": stats["system"]["comfyui_version"],
                      "ingredients": ui["extra"]["ingredients"],
                      "status": record.get("status"), "outputs": record.get("outputs"),
                      "acceptance": "Timing only; ingredient visibility requires image review. RTX 3060 unverified."}
            if not record.get("status", {}).get("completed"):
                raise RuntimeError(json.dumps(result, ensure_ascii=False))
            return result
        time.sleep(min(0.2, max(0, budget - (time.perf_counter() - started))))
    raise TimeoutError(f"Job {prompt_id} exceeded {budget:g}s; result rejected. It may still run locally. "
                       "No automatic retry or global interruption was sent.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("ingredients", nargs="+", help='1-6 names, e.g. RiceNoodle Egg Shrimp Tofu Carrot ChiliPepper')
    parser.add_argument("--seed", type=int, default=None)
    parser.add_argument("--profile", choices=("legacy", "detail-v2", "thai-prepared-v3", "thai-focus-v4"), default="detail-v2")
    parser.add_argument("--width", type=int, default=None)
    parser.add_argument("--height", type=int, default=None)
    parser.add_argument("--output", type=Path, default=None)
    parser.add_argument("--run", action="store_true", help="Submit one local generation, without retries.")
    parser.add_argument("--export-unity", type=Path, help="Export shared workflow/prompt data as a Unity TextAsset JSON.")
    parser.add_argument("--server", default="http://127.0.0.1:8188")
    parser.add_argument("--budget", type=float, default=29.0, help="Result acceptance deadline, at most 29 seconds.")
    args = parser.parse_args()
    if not 0 < args.budget <= 29:
        parser.error("Budget must be positive and no more than 29 seconds.")
    seed = secrets.randbelow(2**53) if args.seed is None else args.seed
    if not 0 <= seed < 2**64:
        parser.error("Seed must be an unsigned 64-bit integer.")
    output = args.output or ROOT / {"legacy": "Thai_03_Food_Noodle_Tofu_Legacy", "detail-v2": "Thai_03_Food_Noodle_Tofu_Default",
                                 "thai-prepared-v3": "Thai_03_Food_Noodle_Tofu_Prepared", "thai-focus-v4": "Thai_03_Food_Noodle_Tofu_Test"}[args.profile]
    try:
        descriptions, template, default_size = profile_data(args.profile)
        width = default_size if args.width is None else args.width
        height = default_size if args.height is None else args.height
        api, ui = build(args.ingredients, seed, width, height, args.profile)
        write_json(output.with_suffix(".api.json"), api)
        write_json(output.with_suffix(".json"), ui)
        if args.export_unity:
            shared = {"workflow": api, "ingredients": descriptions, "dishes": profile_dishes(args.profile),
                      "prompt_template": template, "profile": args.profile}
            if args.profile == "detail-v2":
                shared["single_ingredient_prompt_template"] = DETAIL_SINGLE_PROMPT_TEMPLATE
            elif args.profile == "thai-prepared-v3":
                shared["single_ingredient_prompt_template"] = PREPARED_SINGLE_PROMPT_TEMPLATE
            elif args.profile == "thai-focus-v4":
                shared["single_ingredient_prompt_template"] = FOCUS_SINGLE_PROMPT_TEMPLATE
            write_json(args.export_unity, shared)
        print(f"[FoodWorkflow] Exported {len(ui['extra']['ingredients'])}-ingredient workflow.", flush=True)
        if args.run:
            result = run_local(api, ui, args.server.rstrip("/"), args.budget)
            write_json(output.with_suffix(".timing.json"), result)
            print(json.dumps(result, ensure_ascii=False, indent=2))
    except (ValueError, RuntimeError, TimeoutError, OSError, urllib.error.URLError) as exc:
        if args.run:
            write_json(output.with_suffix(".timing.json"), {"accepted": False, "error": str(exc)})
        print(f"[FoodWorkflow] {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
