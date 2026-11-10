"""Blender adapter: runs blender/prepare.py headless (blender -b --factory-startup -P script --python-exit-code 3 -- params.json).

The command line follows the Blender 5.2 manual (checked 2026-11-10). Blender was not installed
when this was written, so the script itself has not run yet: the capability matrix says so.
"""
import json
import pathlib
import subprocess

from ..core.errors import Fatal, NeedsConfiguration
from ..core.paths import on_windows, project_relative, to_windows
from ..providers.base import capability

LOD_RATIOS = (1.0, 0.35, 0.12)


def output_file(out: pathlib.Path, reported: str) -> pathlib.Path:
    """A file Blender reports in its own path style (C:\\... from Windows Blender, /mnt/... or POSIX) as one in `out`.

    PureWindowsPath splits on both separators; a POSIX Path would take a whole C:\\... string for the file's name.
    """
    return out / pathlib.PureWindowsPath(reported).name


class Blender:
    name = "blender"
    paid = False

    def __init__(self, config):
        self.config = config
        self.settings = config.provider("blender")
        self.env = self.settings.get("pathEnv", "BLENDER_PATH")

    def path(self):
        return self.config.tool_path(self.env)

    def available(self) -> bool:
        return self.path() is not None

    def capability(self) -> dict:
        found = self.path()
        return capability("Blender (headless)", ["preparing_geometry"], "available" if found else "needs_configuration",
                          "CLI z podręcznika 5.2 (2026-11-10); skrypt prepare.py jeszcze nieuruchomiony",
                          "Import GLB/FBX/OBJ, osie i skala projektu, punkt zaczepienia, czyszczenie, UV, decymacja do budżetu, LOD, eksport FBX i GLB."
                          + ("" if found else " Nie znaleziono programu."), [self.env], self.config)

    def _native(self, exe: pathlib.Path, path) -> str:
        return to_windows(path) if (str(exe).lower().endswith(".exe") and not on_windows()) else str(path)

    def prepare(self, ctx, source: pathlib.Path, repair) -> dict:
        exe = self.path()
        if exe is None:
            raise NeedsConfiguration("Brak Blendera (BLENDER_PATH).", [self.env])
        brief = ctx.brief
        out = ctx.dir / "geometry"
        out.mkdir(parents=True, exist_ok=True)
        repairs = ctx.job.get("repairs", 0) if repair else 0
        target = dict(brief.get("dimensions") or {})
        if brief["category"] == "ship":
            ship = ctx.config.ship(brief["shipClass"])
            target = {"length": ship["length"], "width": ship["beam"], "draft": ship["draft"]}
        params = {
            "source": self._native(exe, source),
            "out": self._native(exe, out),
            "stem": brief["id"],
            "category": brief["category"],
            "forward": brief["generation"]["forward"],
            "target": target,
            "triangles": int(brief["budget"]["triangles"] * (0.8 ** repairs)),
            "lods": brief["lods"],
            "lodRatios": list(LOD_RATIOS[: brief["lods"]]),
            "report": self._native(exe, out / "report.json"),
        }
        params_path = ctx.dir / "blender_params.json"
        params_path.write_text(json.dumps(params, indent=2, ensure_ascii=False), encoding="utf-8")
        script = self.config.root / self.settings.get("script", "Tools/asset_pipeline/blender/prepare.py")
        log = ctx.dir / "blender.log"
        args = [str(exe), "-b", "--factory-startup", "-P", self._native(exe, script), "--python-exit-code", "3", "--", self._native(exe, params_path)]
        ctx.keep_alive()
        with open(log, "w", encoding="utf-8") as output:
            try:
                code = subprocess.run(args, stdout=output, stderr=subprocess.STDOUT, timeout=1200).returncode
            except subprocess.TimeoutExpired:
                raise Fatal(f"Blender przekroczył 20 min; log {project_relative(log, self.config.root)}") from None
        report_path = out / "report.json"
        if code != 0 or not report_path.exists():
            tail = log.read_text(encoding="utf-8", errors="replace").splitlines()[-12:]
            raise Fatal(f"Blender zakończył się kodem {code}: {' | '.join(tail)[-600:]}")
        report = json.loads(report_path.read_text(encoding="utf-8"))
        return {
            "lods": [project_relative(output_file(out, l["file"]), self.config.root) for l in report["lods"]],
            "glb": project_relative(output_file(out, report["glb"]), self.config.root) if report.get("glb") else None,
            "triangles": [l["triangles"] for l in report["lods"]],
            "bounds": report.get("bounds"),
            "forward": "+Z",
            "forwardChecked": report.get("forwardChecked", False),
        }
