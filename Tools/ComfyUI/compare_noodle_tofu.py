"""Validate current 15-ingredient catalog and generate isolated local noodle/tofu samples."""
import hashlib
import itertools
import json
import time
import urllib.parse
import urllib.request
from pathlib import Path
import food_workflow as food

ROOT = Path(__file__).resolve().parent
OUTPUT = ROOT.parents[1] / "Temp/ComfyNoodleTofu"
REPORT = ROOT / "reports/2026-10-22_noodle_tofu"
BASE = "http://127.0.0.1:8188"
CASES = [
    ("noodle_only", "thai-focus-v4", ["Rice noodle"], 6102231),
    ("tofu_only", "thai-focus-v4", ["Tofu"], 6102232),
    ("noodle_tofu", "thai-focus-v4", ["Rice noodle", "Tofu"], 6102233),
    ("three", "thai-focus-v4", ["Shrimp", "Onion", "Tofu"], 6102234),
    ("four", "thai-focus-v4", ["Rice noodle", "Egg", "Tofu", "Carrot"], 6102235),
    ("five", "thai-focus-v4", ["Rice", "Chicken", "Onion", "Carrot", "Tofu"], 6102236),
    ("six", "thai-focus-v4", ["Rice noodle", "Egg", "Shrimp", "Tofu", "Carrot", "Chili pepper"], 6102237),
    ("default_two", "detail-v2", ["Rice noodle", "Tofu"], 6102233),
    ("default_six", "detail-v2", ["Rice noodle", "Egg", "Shrimp", "Tofu", "Carrot", "Chili pepper"], 6102237),
]


def check_data():
    digests = {}
    assert len(food.INGREDIENTS) == 15
    assert "Garlic" not in food.INGREDIENTS and "Coconut milk" not in food.INGREDIENTS
    for profile in ("legacy", "detail-v2", "thai-prepared-v3", "thai-focus-v4"):
        sha = hashlib.sha256(); count = 0
        for size in range(1, 7):
            for names in itertools.combinations(food.INGREDIENTS, size):
                prompt = food.make_prompt(names, profile)
                assert not any(word in prompt.lower() for word in ("garlic", "coconut", "curry"))
                for name in names: assert name in prompt
                sha.update((prompt + "\n").encode("utf-8")); count += 1
        digests[profile] = dict(combinations=count, prompt_sha256=sha.hexdigest())
    for invalid in ([], ["Garlic"], ["Coconut milk"], ["Tofu", "Tofu"], ["unknown"], list(food.INGREDIENTS)[:7]):
        try: food.make_prompt(invalid, "thai-focus-v4")
        except ValueError: pass
        else: raise AssertionError("Invalid input was accepted")
    return digests


def run_case(case, schema):
    label, profile, names, seed = case
    queue = food.request(BASE, "/queue")
    if queue["queue_running"] or queue["queue_pending"]: raise RuntimeError("Queue busy; nothing submitted.")
    api, ui = food.build(names, seed, 640, 640, profile)
    api["13"]["inputs"]["filename_prefix"] = "Thai03/NoodleTofuTests/" + label
    food.validate(api, schema); food.write_json(OUTPUT / (label + ".api.json"), api)
    started = time.perf_counter()
    accepted = food.request(BASE, "/prompt", {"prompt": api, "client_id": "thai03-noodle-tofu-" + label,
        "extra_data": {"extra_pnginfo": {"workflow": ui, "thai03": {"ingredients": names, "profile": profile}}}})
    ident = accepted["prompt_id"]
    while time.perf_counter() - started < 29:
        record = food.request(BASE, "/history/" + ident, timeout=3).get(ident)
        if record:
            if record.get("status", {}).get("status_str") != "success" or not record["status"].get("completed"):
                raise RuntimeError("Generation failed: " + json.dumps(record.get("status")))
            image = record["outputs"]["13"]["images"][0]
            with urllib.request.urlopen(BASE + "/view?" + urllib.parse.urlencode(image), timeout=5) as response:
                png = response.read(16 * 1024 * 1024 + 1)
            elapsed = time.perf_counter() - started
            if elapsed >= 29 or len(png) > 16 * 1024 * 1024 or not png.startswith(b"\x89PNG\r\n\x1a\n"):
                raise RuntimeError("Result exceeded 29 seconds or failed PNG validation.")
            (OUTPUT / (label + ".png")).write_bytes(png)
            return dict(case=label, profile=profile, ingredients=names, seed=seed, prompt_id=ident,
                seconds_through_download=round(elapsed, 3), comfy_output=image,
                local_image="Temp/ComfyNoodleTofu/" + label + ".png")
        time.sleep(.15)
    raise TimeoutError("29-second deadline exceeded; no automatic retry or global interrupt. Prompt: " + ident)


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    stats = food.request(BASE, "/system_stats"); stats.get("system", {}).pop("argv", None)
    report = dict(date="2026-10-22", prompt_parity=check_data(), system_stats=stats, target_rtx3060_verified=False, runs=[])
    schema = food.request(BASE, "/object_info")
    for case in CASES:
        try:
            row = run_case(case, schema); report["runs"].append(row)
            print(f"[NoodleTofuComparison] {row['case']}: {row['seconds_through_download']:.3f}s", flush=True)
        except Exception as ex:
            report["failure"] = dict(case=case[0], error=str(ex)); food.write_json(REPORT / "comparison.json", report); raise
        food.write_json(REPORT / "comparison.json", report)


if __name__ == "__main__": main()
