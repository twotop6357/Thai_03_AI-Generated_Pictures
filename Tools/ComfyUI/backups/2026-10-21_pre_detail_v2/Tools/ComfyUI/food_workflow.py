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
    "Rice": "Thai jasmine rice with clearly visible individual grains",
    "Egg": "large pieces of Thai-style crispy fried egg with visible yellow yolk and white egg white",
    "Chicken": "wok-seared chicken slices with golden edges and white fibrous interiors",
    "Beef": "thin wok-fried beef strips with browned edges and recognizable meat fibers",
    "Fish": "a cooked fish fillet with visible silvery skin and flaky white flesh",
    "Shrimp": "whole peeled cooked pink curled shrimp with recognizable tails",
    "Onion": "caramelized golden-brown onion petals, visibly cooked and glossy, with curved onion layers",
    "Garlic": "several peeled ivory garlic cloves with golden seared patches, each a plump curved pointed clove, no papery skins and no round bulbs",
    "Carrot": "glossy roasted bright orange carrot rounds with lightly browned edges and circular centers",
    "Potato": "large cooked golden potato chunks with recognizable pale yellow interiors",
    "Cucumber": "fresh green cucumber rounds with dark green peel and pale seeded centers",
    "Cabbage": "wilted sauteed cabbage leaf pieces with seared edges and pale green veins",
    "Chili pepper": "small thin red bird's-eye chili rings with tiny white seeds, not large bell peppers",
    "Mushroom": "glossy pan-fried brown mushroom halves with dark seared surfaces, recognizable caps and stems",
    "Coconut milk": "a visible glossy creamy coconut-milk sauce coating the dish, with pale ivory coconut-cream swirls clearly visible between the other ingredients",
}
ALIASES = {name.replace(" ", "").lower(): name for name in INGREDIENTS}

DISHES = {
    "rice_only": "a simple serving of freshly steamed Thai jasmine rice",
    "coconut_only": "a Thai-inspired warm coconut-cream dessert with a glossy silky surface",
    "curry": "a Thai-style creamy coconut curry, glossy and appetizing, with large visible cooked ingredients on top",
    "curry_rice_suffix": ", served over jasmine rice on the SAME plate with plenty of rice grains exposed along one edge",
    "fried_rice": "Thai khao pad style wok-fried jasmine rice with large recognizable pieces of the selected ingredients on top",
    "salad": "a Thai yam-inspired composed salad, with crisp cucumber and other ingredients cooked where appropriate",
    "stir_fry": "a Thai-style glossy wok stir-fry with large recognizable pieces, lightly caramelized and ready to eat",
}
PROMPT_TEMPLATE = (
    "Photorealistic Thai food photography for a Bangkok restaurant menu: {0}. "
    "Overhead close-up of ONE coherent finished Thai-inspired dish in ONE shallow blue-and-white Thai ceramic plate, "
    "on a warm teak table with a subtle banana-leaf placemat underneath the plate. "
    "The dish contains {1} selected ingredient types: {2}. "
    "All selected ingredients are large, individually recognizable and fully visible INSIDE the same plated dish: "
    "{3}. Distribute all selected ingredients visibly across the surface of this ONE cooked dish, "
    "integrated naturally with the Thai preparation while keeping every ingredient easy to recognize and uncovered. "
    "No isolated piles of ingredient samples, no compartment tray, no Western steak-and-sides plating. "
    "Every selected ingredient must be visible, not minced, pureed, buried or dissolved. "
    "Use only the selected visible food ingredients; do not add unselected rice, chili, basil, lime or other food garnishes. "
    "No separate ingredient bowls, raw ingredient display, collage, grid, labels, writing or hands. "
    "The food fills most of the frame. Warm natural daylight, realistic moist food textures, rich natural colors, "
    "sharp focus across the entire dish."
)


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


def make_prompt(names):
    names = normalize(names)
    if names == ["Rice"]:
        dish = DISHES["rice_only"]
    elif names == ["Coconut milk"]:
        dish = DISHES["coconut_only"]
    elif "Coconut milk" in names:
        dish = DISHES["curry"]
        if "Rice" in names:
            dish += DISHES["curry_rice_suffix"]
    elif "Rice" in names:
        dish = DISHES["fried_rice"]
    elif "Cucumber" in names:
        dish = DISHES["salad"]
    else:
        dish = DISHES["stir_fry"]
    components = "; ".join(INGREDIENTS[name] for name in names)
    return PROMPT_TEMPLATE.format(dish, len(names), ", ".join(names), components)


def build(names, seed, width=512, height=512):
    names = normalize(names)
    if width % 16 or height % 16 or not (256 <= width <= 1024 and 256 <= height <= 1024):
        raise ValueError("Dimensions must be multiples of 16 between 256 and 1024.")
    prompt = make_prompt(names)
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
    node(13, "SaveImage", "Finished dish / local PNG", [1660, 20], [420, 500],
         {"filename_prefix": "Thai03/Food"}, ["Thai03/Food"], {"images": (12, 0, "IMAGE")}, [])
    ui_nodes.append({"id": 14, "type": "MarkdownNote", "title": "Usage and acceptance limits",
                     "pos": [1020, 420], "size": [570, 300], "flags": {}, "order": 13, "mode": 0,
                     "inputs": [], "outputs": [], "properties": {}, "widgets_values": [
                         "Select 1-6 unique ingredients with food_workflow.py; unused slots are not filled.\n"
                         f"{width}x{height}, 4 steps, CFG 1, batch 1. No refiner, upscale or remote API.\n"
                         "Generation time on RTX 3060 is UNVERIFIED. Keep models warm, queue empty.\n"
                         "All ingredients are requested inside one finished dish. Coconut milk is a visible white sauce; "
                         "its chemical identity cannot be proven from a photo. Visual review is required.\n"
                         "Changing dimensions: update BOTH latent and Flux2Scheduler.\n"
                         "Source: docs.comfy.org/tutorials/flux/flux-2-klein"]})
    ui = {"last_node_id": 14, "last_link_id": len(links), "nodes": ui_nodes, "links": links,
          "groups": [], "config": {}, "extra": {"project": "Thai_03_AI-Generated_Pictures",
          "ingredients": names, "ds": {"scale": 0.65, "offset": [40, 40]}}, "version": 0.4}
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
    parser.add_argument("ingredients", nargs="+", help='1-6 names, e.g. Rice Egg Shrimp Garlic Mushroom "Coconut milk"')
    parser.add_argument("--seed", type=int, default=None)
    parser.add_argument("--width", type=int, default=512)
    parser.add_argument("--height", type=int, default=512)
    parser.add_argument("--output", type=Path, default=ROOT / "Thai_03_Food_Ingredients")
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
    try:
        api, ui = build(args.ingredients, seed, args.width, args.height)
        write_json(args.output.with_suffix(".api.json"), api)
        write_json(args.output.with_suffix(".json"), ui)
        if args.export_unity:
            write_json(args.export_unity, {"workflow": api, "ingredients": INGREDIENTS,
                                         "dishes": DISHES, "prompt_template": PROMPT_TEMPLATE})
        print(f"[FoodWorkflow] Exported {len(ui['extra']['ingredients'])}-ingredient workflow.", flush=True)
        if args.run:
            result = run_local(api, ui, args.server.rstrip("/"), args.budget)
            write_json(args.output.with_suffix(".timing.json"), result)
            print(json.dumps(result, ensure_ascii=False, indent=2))
    except (ValueError, RuntimeError, TimeoutError, OSError, urllib.error.URLError) as exc:
        if args.run:
            write_json(args.output.with_suffix(".timing.json"), {"accepted": False, "error": str(exc)})
        print(f"[FoodWorkflow] {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
