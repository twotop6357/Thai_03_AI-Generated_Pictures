"""Compare preserved v2 and prepared Thai v3 locally; no visitor history writes."""
import argparse
import hashlib
import importlib.util
import itertools
import json
import time
import urllib.parse
import urllib.request
from pathlib import Path

import historical_food_workflow as current

ROOT = Path(__file__).resolve().parent
BACKUP = ROOT / "backups/2026-10-22_pre_thai_prepared_v3"
REPORT = ROOT / "reports/2026-10-22_thai_prepared_v3"
OUTPUT = ROOT.parents[1] / "Temp/ComfyThaiPreparedV3"
BASE = "http://127.0.0.1:8188"
OUTPUT_PREFIX = "Thai03/PreparedV3Tests/"
spec = importlib.util.spec_from_file_location("preserved_v2", BACKUP / "Tools/ComfyUI/food_workflow.py")
preserved = importlib.util.module_from_spec(spec)
spec.loader.exec_module(preserved)
CASES = [
    ("six_v2", "detail-v2", ["Rice", "Egg", "Shrimp", "Garlic", "Mushroom", "Coconut milk"], 6102201),
    ("six_v3", "thai-prepared-v3", ["Rice", "Egg", "Shrimp", "Garlic", "Mushroom", "Coconut milk"], 6102201),
    ("fried_rice_v2", "detail-v2", ["Rice", "Chicken", "Onion", "Carrot", "Garlic"], 6102202),
    ("fried_rice_v3", "thai-prepared-v3", ["Rice", "Chicken", "Onion", "Carrot", "Garlic"], 6102202),
    ("seafood_v2", "detail-v2", ["Fish", "Shrimp", "Cabbage", "Chili pepper"], 6102203),
    ("seafood_v3", "thai-prepared-v3", ["Fish", "Shrimp", "Cabbage", "Chili pepper"], 6102203),
    ("single_egg_v3", "thai-prepared-v3", ["Egg"], 6102204),
    ("two_v3", "thai-prepared-v3", ["Cucumber", "Garlic"], 6102205),
    ("three_v3", "thai-prepared-v3", ["Beef", "Potato", "Onion"], 6102206),
    ("single_rice_v3", "thai-prepared-v3", ["Rice"], 6102207),
    ("single_coconut_v3", "thai-prepared-v3", ["Coconut milk"], 6102208),
]


def check_data():
    digests = {}
    for profile in ("legacy", "detail-v2", "thai-prepared-v3"):
        sha = hashlib.sha256()
        count = 0
        for length in range(1, 7):
            for names in itertools.combinations(current.INGREDIENTS, length):
                prompt = current.make_prompt(names, profile)
                if profile != "thai-prepared-v3":
                    assert prompt == preserved.make_prompt(names, profile)
                sha.update((prompt + "\n").encode("utf-8"))
                count += 1
        digests[profile] = {"combinations": count, "prompt_sha256": sha.hexdigest()}
    for profile in ("legacy", "detail-v2"):
        assert current.build(["Rice", "Egg"], 42, 640, 640, profile) == preserved.build(["Rice", "Egg"], 42, 640, 640, profile)
    return digests


def run_case(case, schema):
    label, profile, names, seed = case
    queue = current.request(BASE, "/queue")
    if queue["queue_running"] or queue["queue_pending"]:
        raise RuntimeError("Queue is busy; nothing was submitted.")
    api, ui = current.build(names, seed, 640, 640, profile)
    api["13"]["inputs"]["filename_prefix"] = OUTPUT_PREFIX + label
    current.validate(api, schema)
    current.write_json(OUTPUT / (label + ".api.json"), api)
    started = time.perf_counter()
    accepted = current.request(BASE, "/prompt", {"prompt": api, "client_id": "thai03-prepared-test-" + label,
        "extra_data": {"extra_pnginfo": {"workflow": ui, "thai03": {"ingredients": names, "profile": profile}}}})
    ident = accepted["prompt_id"]
    while time.perf_counter() - started < 29:
        record = current.request(BASE, "/history/" + ident, timeout=3).get(ident)
        if record:
            if record.get("status", {}).get("status_str") != "success" or not record["status"].get("completed"):
                raise RuntimeError("Local generation failed: " + json.dumps(record.get("status")))
            image = record["outputs"]["13"]["images"][0]
            with urllib.request.urlopen(BASE + "/view?" + urllib.parse.urlencode(image), timeout=5) as response:
                png = response.read(16 * 1024 * 1024 + 1)
            elapsed = time.perf_counter() - started
            if elapsed >= 29 or len(png) > 16 * 1024 * 1024 or not png.startswith(b"\x89PNG\r\n\x1a\n"):
                raise RuntimeError("Result exceeded deadline or PNG size/type limit.")
            (OUTPUT / (label + ".png")).write_bytes(png)
            return dict(case=label, profile=profile, ingredients=names, seed=seed, prompt_id=ident,
                seconds_through_download=round(elapsed, 3), under_29_seconds=True, comfy_output=image,
                local_image=(OUTPUT / (label + ".png")).relative_to(ROOT.parents[1]).as_posix())
        time.sleep(.15)
    raise TimeoutError("29-second deadline exceeded. No retry/interrupt sent. Prompt: " + ident)


def main():
    global OUTPUT, REPORT
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--final", action="store_true", help="Only test v3, with 1-6 ingredients, in a separate final report.")
    args = parser.parse_args()
    cases = CASES
    if args.final:
        OUTPUT = OUTPUT / "final"
        REPORT = REPORT / "final"
        cases = [(label.replace("_v3", "_final"), profile, names, seed)
                 for label, profile, names, seed in CASES if profile == "thai-prepared-v3"]
    OUTPUT.mkdir(parents=True, exist_ok=True)
    stats = current.request(BASE, "/system_stats")
    stats.get("system", {}).pop("argv", None)
    report = dict(date="2026-10-22", deadline_seconds=29, target_rtx3060_verified=False,
        system_stats=stats, prompt_parity=check_data(), runs=[],
        note="Same seed and size for A/B. No extra weights, refiner, upscale or visitor history writes. Visual inspection required.")
    schema = current.request(BASE, "/object_info")
    for case in cases:
        try:
            row = run_case(case, schema)
            report["runs"].append(row)
            print(f"[PreparedThaiComparison] {row['case']}: {row['seconds_through_download']:.3f}s", flush=True)
        except Exception as exc:
            report["failure"] = dict(case=case[0], error=str(exc))
            current.write_json(REPORT / "comparison.json", report)
            raise
        current.write_json(REPORT / "comparison.json", report)


if __name__ == "__main__":
    main()
