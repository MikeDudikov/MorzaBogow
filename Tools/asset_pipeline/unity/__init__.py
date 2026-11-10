"""Unity adapter: writes the import request and runs the editor in batch mode.

Unity.exe -batchmode -projectPath <project> -executeMethod MorzaBogow.Editor.AIAssets.AIAssetBatch.<Import|Validate>
          -aiRequest <request.json> -logFile <log> -quit

No -nographics, so the validator can render a preview. One editor per project: the run waits while
another Unity instance has the project open or the quality gate is running (it hashes Assets).
"""
import json
import pathlib
import re
import subprocess

from .. import __version__
from ..core.errors import Fatal, NeedsConfiguration, Retryable
from ..core.paths import on_windows, project_relative, to_windows
from ..providers.base import capability

BUSY_QUERY = (
    "$root = [regex]::Escape('{root}'); "
    "Get-CimInstance Win32_Process | Where-Object {{ ($_.Name -eq 'Unity.exe' -and $_.CommandLine -match $root) -or "
    "($_.CommandLine -match 'quality-gate\\.ps1') }} | ForEach-Object {{ $_.Name + ' ' + $_.ProcessId }}"
)


class Unity:
    name = "unity"
    paid = False

    def __init__(self, config):
        self.config = config
        self.settings = config.provider("unity")
        self.env = self.settings.get("pathEnv", "UNITY_PATH")

    def path(self):
        return self.config.tool_path(self.env, self.settings.get("defaultPath"))

    def capability(self) -> dict:
        found = self.path()
        return capability("Unity 6000.3.28f1 (batch)", ["importing", "validating"], "available" if found else "needs_configuration",
                          "edytor zainstalowany; import i walidacja przez AIAssetBatch (testy EditMode i przebieg E2E w docs/ASSET_PIPELINE.md)",
                          "Import modelu i tekstur, materiały HDRP/Lit z maską, prefab z korzeniem logicznym i wizualnym, LODGroup, raport JSON i podgląd.",
                          [self.env], self.config)

    def busy(self):
        """Another editor on this project or a running quality gate; None when free or unknown."""
        shell = "powershell" if on_windows() else "powershell.exe"
        script = BUSY_QUERY.format(root=to_windows(self.config.root).replace("'", "''"))
        try:
            result = subprocess.run([shell, "-NoProfile", "-NonInteractive", "-Command", script], capture_output=True, text=True, timeout=60)
        except (OSError, subprocess.SubprocessError):
            return None
        lines = [l.strip() for l in result.stdout.splitlines() if l.strip()]
        return ", ".join(lines) or None

    def request(self, ctx) -> dict:
        brief, artifacts, root = ctx.brief, ctx.artifacts, ctx.config.root
        geometry = artifacts.get("geometry") or {}
        files = geometry.get("lods") or []
        rig = artifacts.get("rig") or {}
        if rig.get("unity"):
            files = [rig["unity"]]
        if not files:
            raise Fatal("Brak plików geometrii do importu.")
        heights = ctx.config.lod_heights(len(files))
        textures = artifacts.get("textures") or {}
        materials = textures.get("materials") or (artifacts.get("model") or {}).get("materials") or []
        dims = dict(brief.get("dimensions") or {})
        if brief["category"] == "ship":
            ship = ctx.config.ship(brief["shipClass"])
            dims = {"length": ship["length"], "width": ship["beam"], "draft": ship["draft"]}
        provenance = {
            "pipeline": __version__,
            "job": ctx.job["id"],
            "briefSha256": ctx.job["brief_sha256"],
            "generation": brief["generation"]["provider"],
            "texturing": textures.get("provider"),
            "geometry": geometry.get("method"),
            "remoteTasks": [t["remote_id"] for t in ctx.store.tasks(ctx.job["id"]) if t.get("remote_id")],
        }
        return {
            "schema": "morza-bogow/unity-import@1",
            "jobId": ctx.job["id"],
            "assetId": brief["id"],
            "displayName": brief.get("name", brief["id"]),
            "category": brief["category"],
            "shipClass": brief.get("shipClass", ""),
            "outputFolder": f"{ctx.config.generated_folder}/{brief['id']}",
            "lods": [{"file": to_windows(root / f), "screenRelativeHeight": heights[i]} for i, f in enumerate(files)],
            "textures": [{"file": to_windows(root / p), "role": role} for role, p in (textures.get("maps") or {}).items()],
            "materials": [{"name": m["name"], "baseColor": m["baseColor"], "metallic": m.get("metallic", 0.0), "smoothness": m.get("smoothness", 0.5)}
                          for m in materials],
            "forward": geometry.get("forward", "+Z"),
            "normalised": geometry.get("method") in ("blender", "blockout"),
            "forwardChecked": bool(geometry.get("forwardChecked", geometry.get("method") == "blockout")),
            "teamColour": brief.get("teamColour", []),
            "dimensions": {k: float(dims.get(k, 0.0)) for k in ("length", "width", "height", "draft")},
            "budget": brief["budget"],
            "textureSize": brief["texturing"]["size"],
            "rigged": brief["rigging"]["provider"] != "none",
            "humanoid": bool(brief["rigging"].get("humanoid", False)),
            "animations": [to_windows(root / p) for p in (artifacts.get("animations") or {}).get("files", [])],
            "reportPath": to_windows(ctx.dir / "validation.json"),
            "resultPath": to_windows(ctx.dir / "unity_result.json"),
            "previewPath": to_windows(ctx.dir / "preview.png"),
            "provenanceJson": json.dumps(provenance, ensure_ascii=False),
        }

    def export_blockout(self, ctx, ship_class: str, out: pathlib.Path, asset_id: str) -> dict:
        """The game's ship of a class exported by Unity (AIAssetBatch.ExportShipBlockout); returns its manifest."""
        exe = self.path()
        if exe is None:
            raise NeedsConfiguration("Nie znaleziono Unity 6000.3.28f1 (UNITY_PATH).", [self.env])
        busy = self.busy()
        if busy:
            raise Retryable(f"Projekt zajęty ({busy}); eksport poczeka.", retry_after=120)
        log = ctx.dir / "unity_export.log"
        manifest = out / f"{asset_id}.json"
        manifest.unlink(missing_ok=True)
        args = [str(exe), "-batchmode", "-nographics", "-projectPath", to_windows(ctx.config.root),
                "-executeMethod", "MorzaBogow.Editor.AIAssets.AIAssetBatch.ExportShipBlockout",
                "-aiShip", ship_class, "-aiOut", to_windows(out), "-aiId", asset_id, "-logFile", to_windows(log), "-quit"]
        ctx.keep_alive()
        try:
            code = subprocess.run(args, capture_output=True, timeout=900).returncode
        except subprocess.TimeoutExpired:
            raise Fatal("Eksport Unity przekroczył 15 min.") from None
        text = log.read_text(encoding="utf-8", errors="replace") if log.exists() else ""
        if "another Unity instance is running" in text or "It looks like another Unity instance" in text:
            raise Retryable("Projekt otwarty w innym edytorze Unity.", retry_after=120)
        if code != 0 or not manifest.exists():
            errors = re.findall(r"error CS\d+:[^\n]*", text)
            raise Fatal("Eksport Unity: " + (" | ".join(dict.fromkeys(errors))[:600] if errors else f"kod {code}"))
        return json.loads(manifest.read_text(encoding="utf-8"))

    def run(self, ctx, method: str, request_path: pathlib.Path) -> dict:
        exe = self.path()
        if exe is None:
            raise NeedsConfiguration("Nie znaleziono Unity 6000.3.28f1 (UNITY_PATH).", [self.env])
        busy = self.busy()
        if busy:
            raise Retryable(f"Projekt zajęty ({busy}); import poczeka.", retry_after=120)
        log = ctx.dir / f"unity_{method.lower()}.log"
        result_path = ctx.dir / "unity_result.json"
        result_path.unlink(missing_ok=True)
        args = [str(exe), "-batchmode", "-projectPath", to_windows(ctx.config.root),
                "-executeMethod", f"MorzaBogow.Editor.AIAssets.AIAssetBatch.{method}",
                "-aiRequest", to_windows(request_path), "-logFile", to_windows(log), "-quit"]
        ctx.keep_alive()
        try:
            code = subprocess.run(args, capture_output=True, timeout=1800).returncode
        except subprocess.TimeoutExpired:
            raise Fatal(f"Unity {method} przekroczył 30 min; log {project_relative(log, self.config.root)}") from None
        text = log.read_text(encoding="utf-8", errors="replace") if log.exists() else ""
        if "another Unity instance is running" in text or "It looks like another Unity instance" in text:
            raise Retryable("Projekt otwarty w innym edytorze Unity.", retry_after=120)
        result = json.loads(result_path.read_text(encoding="utf-8")) if result_path.exists() else {}
        if code != 0 or not result.get("ok"):
            errors = re.findall(r"error CS\d+:[^\n]*", text)
            if errors:
                raise Fatal("Unity: błędy kompilacji projektu: " + " | ".join(dict.fromkeys(errors))[:600])
            message = result.get("error") or " | ".join([l for l in text.splitlines() if "Exception" in l or "[AIAssets]" in l][-6:])
            raise Fatal(f"Unity {method} (kod {code}): {message[:600]}")
        return {
            "prefab": result["prefab"],
            "report": project_relative(ctx.dir / "validation.json", self.config.root),
            "preview": project_relative(ctx.dir / "preview.png", self.config.root) if (ctx.dir / "preview.png").exists() else None,
            "log": project_relative(log, self.config.root),
            "stale": False,
        }
