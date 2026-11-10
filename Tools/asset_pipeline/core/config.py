"""Pipeline settings: project conventions, budgets, limits and provider endpoints.

Credentials are never part of the configuration: only the names of the environment variables that
hold them. `secret()` reads a value at the moment of use and nothing stores or prints it.
"""
import copy
import json
import os
import pathlib

from .errors import NeedsConfiguration
from .paths import PROJECT_ROOT, TOOL_ROOT, from_windows

DEFAULT_CONFIG = TOOL_ROOT / "config" / "pipeline.json"

CATEGORIES = ("ship", "ship_equipment", "building", "ruin", "rock", "vegetation", "character", "creature", "material", "effect")
RIGGED = ("character", "creature")


def _merge(base: dict, extra: dict) -> dict:
    out = copy.deepcopy(base)
    for key, value in (extra or {}).items():
        if isinstance(value, dict) and isinstance(out.get(key), dict):
            out[key] = _merge(out[key], value)
        else:
            out[key] = copy.deepcopy(value)
    return out


class Config:
    def __init__(self, data: dict, root=None, environ=None):
        self.data = data
        self.root = pathlib.Path(root or PROJECT_ROOT).resolve()
        self.environ = os.environ if environ is None else environ
        dims_file = self.root / data["project"]["dimensionsFile"]
        if not dims_file.exists():
            dims_file = TOOL_ROOT / "config" / "project_dimensions.json"
        self.dimensions = json.loads(dims_file.read_text(encoding="utf-8"))

    @classmethod
    def load(cls, path=None, root=None, overrides=None, environ=None):
        path = pathlib.Path(path or os.environ.get("AP_CONFIG") or DEFAULT_CONFIG)
        data = json.loads(path.read_text(encoding="utf-8"))
        return cls(_merge(data, overrides or {}), root=root or os.environ.get("AP_ROOT"), environ=environ)

    # Project conventions ---------------------------------------------------------------------
    @property
    def workspace(self) -> pathlib.Path:
        return self.root / self.data["project"]["workspaceFolder"]

    @property
    def database(self) -> pathlib.Path:
        return self.root / self.data["project"]["databaseFile"]

    @property
    def generated_folder(self) -> str:
        return self.data["project"]["generatedAssetsFolder"]

    @property
    def visual_direction(self) -> str:
        return self.data.get("visualDirection", "")

    def job_dir(self, job_id: str) -> pathlib.Path:
        return self.workspace / "jobs" / job_id

    def budget(self, category: str) -> dict:
        return dict(self.data["budgets"][category])

    def ship(self, ship_class: str) -> dict:
        ships = self.dimensions["ships"]
        if ship_class not in ships:
            raise KeyError(ship_class)
        return dict(ships[ship_class])

    @property
    def ship_classes(self):
        return list(self.dimensions["ships"].keys())

    @property
    def max_lods(self) -> int:
        return len(self.data.get("lodScreenSizes", [0.25, 0.08])) + 1

    def lod_heights(self, count: int):
        """LODGroup screen heights for `count` levels: switches at lodScreenSizes, the last level culled only at
        lodCullScreenSize (tiny), so ships stay visible from the highest strategy camera."""
        switches = list(self.data.get("lodScreenSizes", [0.25, 0.08]))[: max(0, count - 1)]
        return switches + [float(self.data.get("lodCullScreenSize", 0.002))]

    @property
    def limits(self) -> dict:
        return dict(self.data["limits"])

    @property
    def polling(self) -> dict:
        return dict(self.data["polling"])

    def provider(self, name: str) -> dict:
        return dict(self.data["providers"].get(name, {}))

    # Environment -----------------------------------------------------------------------------
    def secret(self, env_name: str, what: str):
        """The value of a credential variable, or NeedsConfiguration naming only the variable."""
        value = self.environ.get(env_name, "").strip()
        if not value:
            raise NeedsConfiguration(f"Brak zmiennej środowiskowej {env_name} ({what}).", [env_name])
        return value

    def is_set(self, env_name: str) -> bool:
        return bool(self.environ.get(env_name, "").strip())

    def tool_path(self, env_name: str, default=None):
        """A program path from an environment variable or the default, as a local path if it exists."""
        raw = self.environ.get(env_name, "").strip() or (default or "")
        if not raw:
            return None
        path = from_windows(raw)
        return path if path.exists() else None
