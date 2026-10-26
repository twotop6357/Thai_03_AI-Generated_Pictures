"""Thai food-only / solid-faced garlic-slice revision, isolated local comparisons."""
import argparse
import hashlib
import importlib.util
import itertools
import json
from pathlib import Path
import compare_prepared_v3 as runner
import historical_food_workflow as food

ROOT = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("v3_before_revision", ROOT / "backups/2026-10-22_pre_thai_street_v4/Tools/ComfyUI/food_workflow.py")
old = importlib.util.module_from_spec(spec)
spec.loader.exec_module(old)
CASES = [
    ("six_before", "thai-prepared-v3", ["Rice", "Egg", "Shrimp", "Garlic", "Mushroom", "Coconut milk"], 6102201),
    ("six_focus", "thai-focus-v4", ["Rice", "Egg", "Shrimp", "Garlic", "Mushroom", "Coconut milk"], 6102201),
    ("garlic_before", "thai-prepared-v3", ["Garlic"], 6102221),
    ("garlic_focus", "thai-focus-v4", ["Garlic"], 6102221),
    ("two_focus", "thai-focus-v4", ["Shrimp", "Garlic"], 6102222),
    ("three_focus", "thai-focus-v4", ["Rice", "Egg", "Garlic"], 6102223),
    ("four_focus", "thai-focus-v4", ["Shrimp", "Onion", "Chili pepper", "Coconut milk"], 6102224),
    ("five_focus", "thai-focus-v4", ["Rice", "Chicken", "Onion", "Carrot", "Garlic"], 6102202),
]


def check_data():
    rows = {}
    for profile in ("legacy", "detail-v2", "thai-prepared-v3", "thai-focus-v4"):
        sha = hashlib.sha256()
        count = 0
        for size in range(1, 7):
            for names in itertools.combinations(food.INGREDIENTS, size):
                prompt = food.make_prompt(names, profile)
                if profile != "thai-focus-v4":
                    assert prompt == old.make_prompt(names, profile)
                sha.update((prompt + "\n").encode("utf-8")); count += 1
        rows[profile] = dict(combinations=count, prompt_sha256=sha.hexdigest())
    return rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--final", action="store_true")
    parser.add_argument("--revision", choices=("initial", "garlic-germ"), default="initial")
    args = parser.parse_args()
    report_dir = ROOT / "reports/2026-10-22_thai_focus_v4"
    runner.OUTPUT = ROOT.parents[1] / "Temp/ComfyThaiFocusV4"
    if args.final:
        report_dir = report_dir / "final"; runner.OUTPUT = runner.OUTPUT / "final"
    if args.revision != "initial":
        report_dir = report_dir / args.revision; runner.OUTPUT = runner.OUTPUT / args.revision
    runner.OUTPUT_PREFIX = "Thai03/FocusV4Tests/"
    runner.OUTPUT.mkdir(parents=True, exist_ok=True)
    stats = food.request(runner.BASE, "/system_stats"); stats.get("system", {}).pop("argv", None)
    report = dict(date="2026-10-22", system_stats=stats, prompt_parity=check_data(), runs=[], target_rtx3060_verified=False)
    schema = food.request(runner.BASE, "/object_info")
    for case in CASES:
        if args.final and case[1] != "thai-focus-v4": continue
        try:
            row = runner.run_case(case, schema); report["runs"].append(row)
            print(f"[ThaiFocusComparison] {row['case']}: {row['seconds_through_download']:.3f}s", flush=True)
        except Exception as ex:
            report["failure"] = dict(case=case[0], error=str(ex)); food.write_json(report_dir / "comparison.json", report); raise
        food.write_json(report_dir / "comparison.json", report)


if __name__ == "__main__": main()
