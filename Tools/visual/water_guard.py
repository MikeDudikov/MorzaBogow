#!/usr/bin/env python3
"""Water guard: fingerprints of the protected water system, so a visual refresh can prove it left the sea alone.

The sea of Morza Bogów (HDRP water surface with deformation from the simulation, foam, wakes, spray, the marine
simulation and the settings and profiles it shares with everything else) stays outside the visual refresh
(docs/art/WaterProtection.md). This script records SHA-256 fingerprints of
  - whole files that belong to the water only, and the rendering settings it shares;
  - single members (methods, constants) of C# files that mix water with other presentation;
  - only the water lines of members that do both (filtered by a pattern);
  - a marked region of a file (the sun, volume and ocean block of the scene builder),
and compares them later. Line endings are normalised, so a CRLF/LF rewrite by Git is not a change.

    python3 Tools/visual/water_guard.py baseline   # write docs/art/WaterProtection.json
    python3 Tools/visual/water_guard.py check      # exit 1 and name every protected part that changed
    python3 Tools/visual/water_guard.py list       # what is protected

Standard library only (WSL Python). The check is advisory: a mismatch names the part, and a person reads its diff.
"""
import argparse
import glob
import hashlib
import json
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BASELINE = os.path.join(ROOT, "docs", "art", "WaterProtection.json")
PRES = "Assets/MorzaBogow/Runtime/Presentation"
WATER_LINES = r"(?i)water|wake|foam|spray|splash|sea(?!son|t|rch)|marine|seakeep|hullmotion|bowwave|decal|wave|surf(?!ace)|shelf|draft|buoy|excluder|weather"

# Whole files: water only, or rendering settings the water shares with everything else.
FILES = [
    "Assets/MorzaBogow/Runtime/Simulation/Marine/*.cs",
    "Assets/MorzaBogow/Runtime/Simulation/World.Marine.cs",
    f"{PRES}/SeaSurfaceRenderer.cs",
    f"{PRES}/WeatherController.cs",
    f"{PRES}/HullWaves.cs",
    f"{PRES}/WaterBatchSampler.cs",
    f"{PRES}/SeakeepingDriver.cs",
    f"{PRES}/WaterDetail.cs",
    f"{PRES}/Resources/SeaSurfaceHeights.compute",
    f"{PRES}/Resources/SeaHeightsDecal.mat",
    "Assets/Art/Placeholders/Placeholder_WaterBowWave.mat",
    "Assets/Art/Placeholders/Placeholder_WaterFoamDisk.mat",
    "Assets/Art/Placeholders/Placeholder_Spray.mat",
    "Assets/Art/Placeholders/Placeholder_SprayDot.asset",
    "Assets/Art/Placeholders/Placeholder_Sand.mat",
    "Assets/Art/Shore/Shore_Seabed.mat",
    "Assets/Art/Shore/Shore_Limestone.mat",
    "Assets/MorzaBogow/Settings/HDRP_*.asset",
    "Assets/MorzaBogow/Settings/Volume_*.asset",
    "Assets/HDRPDefaultResources/DefaultSettingsVolumeProfile.asset",
    "Assets/HDRPDefaultResources/HDRenderPipelineGlobalSettings.asset",
    "ProjectSettings/QualitySettings.asset",
    "ProjectSettings/GraphicsSettings.asset",
]

# Members of mixed files. "lines": keep only the lines matching the pattern (members that also do other work).
MEMBERS = [
    ("Assets/MorzaBogow/Runtime/Presentation/BattleController.cs", [
        ("Awake", WATER_LINES), ("StartMatch", WATER_LINES), ("ApplyMarineSea", None), ("StepSeakeeping", None),
        ("CompareRenderedSea", None), ("CompareHullWaterlines", None), ("KeepWaterOut", None), ("DryHullMesh", None),
        ("RenderSea", None), ("BuildStaticViews", WATER_LINES), ("UpdateWaterPoses", None), ("BreakOnRock", None),
        ("PresentFrame", WATER_LINES)]),
    ("Assets/MorzaBogow/Runtime/Presentation/ShipVisual.cs", [
        ("Create", WATER_LINES), ("ResetForReuse", WATER_LINES), ("Animate", WATER_LINES), ("CreateSpray", None),
        ("WaterDecalAt", None), ("ApplyDraft", None), ("SetWaterPose", None), ("SetPhysicalPose", None), ("Splash", None),
        ("BreakingFor", None), ("EmitSpray", None)]),
    ("Assets/MorzaBogow/Runtime/Presentation/ShoreShape.cs", [
        ("Shelf", None), ("Beach", None), ("SurfPoints", None), ("SurfSpacing", None), ("SurfSize", None)]),
    ("Assets/MorzaBogow/Runtime/Presentation/BattleEffects.cs", [("WaterColumn", None)]),
    ("Assets/MorzaBogow/Editor/BattleSceneBuilder.cs", [
        ("WaterDecalMaterial", None), ("SprayMaterial", None), ("SprayTexture", None)]),
    ("Assets/MorzaBogow/Editor/M0Bootstrap.cs", [
        ("EnsurePipelineAsset", None), ("ConfigureQualityLevels", None), ("EnsureVolumeProfile", None)]),
]

# Regions: from the first line matching start to the first later line matching end, both included.
REGIONS = [
    ("Assets/MorzaBogow/Editor/BattleSceneBuilder.cs", "sun-volume-ocean",
     r'new GameObject\("Sun"\)', r"ocean\.causticsIntensity"),
]


def read(rel):
    with open(os.path.join(ROOT, rel), "rb") as handle:
        data = handle.read()
    return data.replace(b"\r\n", b"\n").decode("utf-8", errors="replace")


def digest(text):
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def _skip(text, i):
    """If a comment, string or char literal starts at i, return the index after it; otherwise None."""
    if text.startswith("//", i):
        end = text.find("\n", i)
        return len(text) if end < 0 else end
    if text.startswith("/*", i):
        end = text.find("*/", i + 2)
        return len(text) if end < 0 else end + 2
    if text.startswith('@"', i) or text.startswith('$@"', i) or text.startswith('@$"', i):
        j = text.index('"', i) + 1
        while j < len(text):
            if text[j] == '"':
                if j + 1 < len(text) and text[j + 1] == '"':
                    j += 2
                    continue
                return j + 1
            j += 1
        return len(text)
    if text[i] == '"' or text.startswith('$"', i):
        j = text.index('"', i) + 1
        while j < len(text) and text[j] != '"':
            j += 2 if text[j] == "\\" else 1
        return j + 1
    if text[i] == "'":
        j = i + 1
        while j < len(text) and text[j] != "'":
            j += 2 if text[j] == "\\" else 1
        return j + 1
    return None


def member_spans(text, name):
    """Spans of every declaration of `name` (all overloads): methods with a body or =>, constants and fields."""
    spans = []
    declaration = re.compile(r"(?m)^[ \t]*(?:\[[^\]\n]*\][ \t]*)*(?:[\w<>\[\],.?()]+[ \t]+)+" + re.escape(name) +
                             r"[ \t]*(?:<[^>\n]*>)?[ \t]*(\(|=(?!=)|=>|\{)")
    for match in declaration.finditer(text):
        start = match.start()
        i = match.start(1)
        depth_paren = 0
        body_end = None
        while i < len(text):
            skipped = _skip(text, i)
            if skipped is not None:
                i = skipped
                continue
            c = text[i]
            if c == "(":
                depth_paren += 1
            elif c == ")":
                depth_paren -= 1
            elif depth_paren == 0 and c == ";":
                body_end = i + 1
                break
            elif depth_paren == 0 and c == "{":
                depth = 0
                while i < len(text):
                    skipped = _skip(text, i)
                    if skipped is not None:
                        i = skipped
                        continue
                    if text[i] == "{":
                        depth += 1
                    elif text[i] == "}":
                        depth -= 1
                        if depth == 0:
                            body_end = i + 1
                            break
                    i += 1
                break
            i += 1
        if body_end:
            spans.append((start, body_end))
    return spans


def member_text(text, name, lines_pattern=None):
    spans = member_spans(text, name)
    if not spans:
        return None
    body = "\n".join(text[a:b] for a, b in spans)
    if lines_pattern:
        keep = re.compile(lines_pattern)
        body = "\n".join(line.strip() for line in body.split("\n") if keep.search(line))
    return body


def region_text(text, start_pattern, end_pattern):
    lines = text.split("\n")
    start = next((i for i, line in enumerate(lines) if re.search(start_pattern, line)), None)
    if start is None:
        return None
    end = next((i for i in range(start, len(lines)) if re.search(end_pattern, lines[i])), None)
    return None if end is None else "\n".join(lines[start:end + 1])


def fingerprints():
    result = {}
    for pattern in FILES:
        matches = sorted(glob.glob(os.path.join(ROOT, pattern)))
        if not matches:
            result[f"file:{pattern}"] = "MISSING"
        for path in matches:
            rel = os.path.relpath(path, ROOT).replace(os.sep, "/")
            result[f"file:{rel}"] = digest(read(rel))
    for rel, members in MEMBERS:
        text = read(rel) if os.path.exists(os.path.join(ROOT, rel)) else None
        for name, lines in members:
            body = member_text(text, name, lines) if text is not None else None
            key = f"member:{rel}#{name}" + (" (water lines)" if lines else "")
            result[key] = "MISSING" if body is None else digest(body)
    for rel, label, start, end in REGIONS:
        text = read(rel) if os.path.exists(os.path.join(ROOT, rel)) else None
        body = region_text(text, start, end) if text is not None else None
        result[f"region:{rel}#{label}"] = "MISSING" if body is None else digest(body)
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("command", choices=["baseline", "check", "list"])
    parser.add_argument("--baseline", default=BASELINE)
    args = parser.parse_args(argv)
    current = fingerprints()
    if args.command == "list":
        for key, value in current.items():
            print(("MISSING  " if value == "MISSING" else "") + key)
        return 0
    if args.command == "baseline":
        missing = [k for k, v in current.items() if v == "MISSING"]
        if missing:
            print("Brak chronionych części (popraw listę przed zapisem):\n  " + "\n  ".join(missing))
            return 2
        with open(args.baseline, "w", encoding="utf-8", newline="\n") as handle:
            json.dump({"schema": "morza-bogow/water-protection@1", "fingerprints": current}, handle, indent=1, sort_keys=True)
            handle.write("\n")
        print(f"Zapisano {len(current)} odcisków: {os.path.relpath(args.baseline, ROOT)}")
        return 0
    with open(args.baseline, encoding="utf-8") as handle:
        saved = json.load(handle)["fingerprints"]
    changed = [k for k in saved if current.get(k) != saved[k]]
    added = [k for k in current if k not in saved]
    for key in changed:
        print(f"ZMIENIONE  {key}")
    for key in added:
        print(f"NOWE       {key}")
    if changed or added:
        print(f"Woda: {len(changed)} zmienionych, {len(added)} nowych części z {len(saved)}. Przeczytaj ich diff.")
        return 1
    print(f"Woda nietknięta: {len(saved)} chronionych części bez zmian.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
