"""Frozen pre-noodle/tofu catalog for reproducing historical comparison scripts only."""
import importlib.util
from pathlib import Path

path = Path(__file__).resolve().parent / "backups/2026-10-22_pre_noodle_tofu/Tools/ComfyUI/food_workflow.py"
spec = importlib.util.spec_from_file_location("thai03_historical_catalog", path)
_module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(_module)


def __getattr__(name):
    return getattr(_module, name)
