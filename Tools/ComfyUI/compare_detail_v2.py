"""Sequential local A/B test; never writes the Unity visitor gallery or interrupts other jobs."""

import importlib.util
import argparse
import json
import time
import urllib.parse
import urllib.request
from pathlib import Path

import historical_food_workflow as detail


ROOT = Path(__file__).resolve().parent
BACKUP = ROOT / "backups/2026-10-21_pre_detail_v2/Tools/ComfyUI/food_workflow.py"
SPEC = importlib.util.spec_from_file_location("food_v1_snapshot", BACKUP)
legacy = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(legacy)
BASE = "http://127.0.0.1:8188"
OUTPUT = ROOT.parents[1] / "Temp/ComfyFoodDetailV2"
REPORT = ROOT / "reports/2026-10-21_detail_v2/comparison.json"

CASES = [
    ("warmup", "legacy", 512, ["Rice"], 6102100),
    ("six_legacy_512", "legacy", 512, ["Rice", "Egg", "Shrimp", "Garlic", "Mushroom", "Coconut milk"], 6102101),
    ("six_detail_512", "detail-v2", 512, ["Rice", "Egg", "Shrimp", "Garlic", "Mushroom", "Coconut milk"], 6102101),
    ("six_detail_640", "detail-v2", 640, ["Rice", "Egg", "Shrimp", "Garlic", "Mushroom", "Coconut milk"], 6102101),
    ("six_detail_768", "detail-v2", 768, ["Rice", "Egg", "Shrimp", "Garlic", "Mushroom", "Coconut milk"], 6102101),
    ("chicken_legacy_512", "legacy", 512, ["Rice", "Egg", "Chicken", "Carrot", "Onion", "Chili pepper"], 6102102),
    ("chicken_detail_640", "detail-v2", 640, ["Rice", "Egg", "Chicken", "Carrot", "Onion", "Chili pepper"], 6102102),
    ("seafood_legacy_512", "legacy", 512, ["Fish", "Shrimp", "Cabbage", "Chili pepper"], 6102103),
    ("seafood_detail_640", "detail-v2", 640, ["Fish", "Shrimp", "Cabbage", "Chili pepper"], 6102103),
    ("egg_legacy_512", "legacy", 512, ["Egg"], 6102104),
    ("egg_detail_640", "detail-v2", 640, ["Egg"], 6102104),
    ("cucumber_coconut_detail_640", "detail-v2", 640, ["Cucumber", "Coconut milk"], 6102105),
    ("beef_detail_640", "detail-v2", 640, ["Beef", "Potato", "Mushroom", "Onion"], 6102106),
]
FINAL_CASES = [
    ("egg_detail_final", "detail-v2", 640, ["Egg"], 6102104),
    ("cucumber_coconut_detail_final", "detail-v2", 640, ["Cucumber", "Coconut milk"], 6102105),
    ("three_detail_final", "detail-v2", 640, ["Rice", "Egg", "Chicken"], 6102110),
    ("four_detail_final", "detail-v2", 640, ["Fish", "Shrimp", "Cabbage", "Chili pepper"], 6102111),
    ("five_detail_final", "detail-v2", 640, ["Beef", "Garlic", "Onion", "Carrot", "Potato"], 6102112),
    ("six_detail_final", "detail-v2", 640, ["Rice", "Egg", "Shrimp", "Garlic", "Mushroom", "Coconut milk"], 6102113),
    ("rice_single_final", "detail-v2", 640, ["Rice"], 6102114),
    ("coconut_single_final", "detail-v2", 640, ["Coconut milk"], 6102115),
]


def run_case(case, schema):
    label, profile, size, names, seed = case
    queue = detail.request(BASE, "/queue")
    if queue["queue_running"] or queue["queue_pending"]:
        raise RuntimeError("ComfyUI queue is busy; no job was submitted.")
    api, ui = legacy.build(names, seed, size, size) if profile == "legacy" else detail.build(names, seed, size, size, profile)
    api["13"]["inputs"]["filename_prefix"] = "Thai03/DetailV2Tests/" + label
    detail.validate(api, schema)
    detail.write_json(OUTPUT / (label + ".api.json"), api)
    started = time.perf_counter()
    accepted = detail.request(BASE, "/prompt", {"prompt": api, "client_id": "thai03-detail-comparison-" + label,
        "extra_data": {"extra_pnginfo": {"workflow": ui, "thai03": {"ingredients": names, "test": label}}}})
    ident = accepted["prompt_id"]
    while time.perf_counter() - started < 29:
        history = detail.request(BASE, "/history/" + ident, timeout=3)
        if ident in history:
            record = history[ident]
            if record.get("status", {}).get("status_str") != "success" or not record.get("status", {}).get("completed"):
                raise RuntimeError("ComfyUI execution failed: " + json.dumps(record.get("status")))
            image = record["outputs"]["13"]["images"][0]
            query = urllib.parse.urlencode(image)
            with urllib.request.urlopen(BASE + "/view?" + query, timeout=5) as response:
                png = response.read(16 * 1024 * 1024 + 1)
            elapsed = time.perf_counter() - started
            if elapsed >= 29 or len(png) > 16 * 1024 * 1024 or not png.startswith(b"\x89PNG\r\n\x1a\n"):
                raise RuntimeError("Generated image failed the 29-second or PNG acceptance check.")
            file = OUTPUT / (label + ".png")
            file.write_bytes(png)
            return {"case": label, "profile": profile, "size": size, "steps": 4, "seed": seed,
                "ingredients": names, "prompt_id": ident, "seconds_through_download": round(elapsed, 3),
                "under_29_seconds": True, "png_bytes": len(png), "local_image": str(file), "comfy_output": image}
        time.sleep(.15)
    raise TimeoutError("Local job exceeded 29 seconds; no retry or global interrupt sent. Prompt: " + ident)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--final", action="store_true", help="Verify the selected v2 profile with 1-6 ingredients.")
    args = parser.parse_args()
    report_path = REPORT.with_name("final_comparison.json") if args.final else REPORT
    OUTPUT.mkdir(parents=True, exist_ok=True)
    stats = detail.request(BASE, "/system_stats")
    stats.get("system", {}).pop("argv", None)  # 로컬 계정 경로가 공개 검증 기록에 들어가지 않게 한다.
    report = {"date": "2026-10-21", "system_stats": stats,
        "deadline_seconds": 29, "target_rtx3060_verified": False, "runs": [],
        "note": "Comparison includes an initial warmup; final verification uses the running warm server. Resolution changes noise dimensions even with the same seed. Visual review is manual."}
    schema = detail.request(BASE, "/object_info")
    for case in FINAL_CASES if args.final else CASES:
        try:
            row = run_case(case, schema)
            report["runs"].append(row)
            print(f"[FoodDetailComparison] {row['case']}: {row['seconds_through_download']:.3f}s", flush=True)
        except Exception as exc:
            report["failure"] = {"case": case[0], "error": repr(exc)}
            detail.write_json(report_path, report)
            raise
        detail.write_json(report_path, report)
    print("[FoodDetailComparison] All runs completed. Review the PNGs before promoting a profile.", flush=True)


if __name__ == "__main__":
    main()
