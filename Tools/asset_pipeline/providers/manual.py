"""Services without a verified automation interface: Substance 3D Painter, DeepMotion, Cascadeur.

Their documentation could not be reached on 2026-11-10 (DNS failure, 403, no public API page), so
the pipeline does not guess endpoints. The stage writes a hand-off note with the expected file
names, waits in needs_review, and picks the files up from the job's inbox on `ap resume`.
"""
import pathlib
import shutil

from ..core.errors import NeedsReview
from ..core.paths import project_relative
from .base import capability

TEXTURE_PATTERNS = ("*.png", "*.jpg", "*.jpeg", "*.tga", "*.tif", "*.tiff", "*.exr")

NOTES = {
    "texturing": "Wyeksportuj mapy PBR (szablon Unity HDRP albo osobne mapy) z nazwami kończącymi się na _BaseColor, _Normal "
                 "(OpenGL), _Metallic, _Roughness, _AO albo gotową _MaskMap. Rozdzielczość najwyżej {size} px.",
    "rigging": "Wyeksportuj model ze szkieletem jako FBX (metry, Y w górę, bez animacji).",
    "animating": "Wyeksportuj każdy klip jako osobny FBX z tym samym szkieletem; nazwa pliku = nazwa klipu.",
}


class Manual:
    paid = False

    def __init__(self, config, name: str, title: str, stages, why: str):
        self.config = config
        self.name = name
        self.title = title
        self.stages = stages
        self.why = why

    def capability(self) -> dict:
        return capability(self.title, self.stages, "interactive", "nie (brak dostępnej dokumentacji API 2026-11-10)",
                          f"Przekazanie ręczne: {self.why} Pliki do skrzynki zadania, potem ap resume.", (), self.config)

    def collect(self, ctx, stage: str, out: pathlib.Path, patterns=TEXTURE_PATTERNS, many=False) -> dict:
        from ..core.handlers import classify_map
        inbox = ctx.dir / "inbox" / stage
        inbox.mkdir(parents=True, exist_ok=True)
        files = sorted({f for pattern in patterns for f in inbox.glob(pattern)})
        note = NOTES.get(stage, "").format(size=ctx.brief.get("texturing", {}).get("size", 1024))
        if stage == "texturing":
            found = {}
            for f in files:
                role = classify_map(f.name)
                if role and role not in found:
                    found[role] = f
            if "base_color" in found or "mask" in found:
                return self._copy(found, out)
        elif files:
            return self._copy({f.stem: f for f in (files if many else files[:1])}, out)
        handoff = ctx.dir / f"HANDOFF_{stage}.txt"
        handoff.write_text(f"{self.title}: {note}\nSkrzynka: {inbox}\nPotem: ap resume {ctx.job['id']}\n", encoding="utf-8")
        raise NeedsReview(f"{self.title}: {note} Pliki do {project_relative(inbox, ctx.config.root)}, potem: ap resume {ctx.job['id']}")

    @staticmethod
    def _copy(found: dict, out: pathlib.Path) -> dict:
        out.mkdir(parents=True, exist_ok=True)
        return {key: pathlib.Path(shutil.copy2(path, out / path.name)) for key, path in found.items()}
