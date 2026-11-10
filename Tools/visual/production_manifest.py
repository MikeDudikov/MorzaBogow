#!/usr/bin/env python3
"""ArtProductionManifest: every asset of the visual refresh with its production record and one persistent task state.

    python3 Tools/visual/production_manifest.py write   # docs/art/ArtProductionManifest.json
    python3 Tools/visual/production_manifest.py check   # coverage, states and the vertical slice's fields; exit 1 on a gap

What an asset is, what changes and how it is accepted come from docs/art/VisualReworkManifest.json (rework_manifest.py);
Meshy task ids and credits from docs/art/MeshyLedger.json. The task state is one of STATES and is kept here, so work can be
resumed: an asset's state, its next action and its limitations are the hand-over. The vertical slice (one building, one
ship) is described in full; the rest carries its state and next action until it is worked on. Standard library only.
"""
import argparse
import json
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "docs", "art", "ArtProductionManifest.json")
REWORK = os.path.join(ROOT, "docs", "art", "VisualReworkManifest.json")
LEDGER = os.path.join(ROOT, "docs", "art", "MeshyLedger.json")

STATES = {
    "planned": "described, not started",
    "research": "sources being read",
    "generating": "a generator (Meshy, ComfyUI, code) is making it",
    "preparing": "geometry being prepared (Blender, generator code)",
    "texturing": "materials and textures",
    "rigging": "rig",
    "animating": "animation",
    "fracturing": "split into elements, damage stages, debris",
    "importing": "import into Unity, prefabs, integration",
    "validating": "tests and previews passed; awaiting measurement in the build or the user's look",
    "ready": "accepted",
    "needs_review": "rejected or doubtful after the allowed attempts; a person decides",
    "blocked": "cannot go on (protected, out of scope or waiting on another decision)",
}

# The vertical slice: one building and one ship, complete before anything is produced in quantity.
SLICE = {
    "building.school": {
        "class": "building (PortBuilding.SageSchool): hall of learning in a Doric peristyle",
        "dating": "E6, c. 400-323 BC (Academy, Lyceum); interpretation of a gymnasium hall",
        "dimensions": "crepis 5 x 8.2 m in three steps (1 m); 4 x 6 columns 3.3 m, lower diameter 0.52 m; hall 2.1 x 5.2 m inside; "
                      "eaves 5.4 m; footprint unchanged (PortMasonryTests)",
        "materials": ["ashlar limestone (Buildings/Ashlar)", "lime plaster and whitewash (Buildings/Stucco)",
                      "sun-dried brick under the plaster (mudbrick tint)", "terracotta tiles", "timber rafters and door",
                      "Egyptian blue and red ochre paint"],
        "construction": "crepis and hall socle (foundations); per column 5 drums and a capital on bed joints with a central pin "
                        "(R-DRUM-JOINTS); architrave and frieze-cornice blocks per intercolumniation on two capitals; hall walls of "
                        "brick in 8 stretches on the stone socle; roof in 4 bays a side on the frieze and the hall's wall; 2 pediments",
        "movingParts": "none in use; in destruction: falling columns (as one piece from the joint that gave), blocks, roof bays, walls",
        "attachmentPoints": "work spots while it rises (PortBuildings.WorkSpots); research fire before the front",
        "animations": ["rises column by column and course by course from the game's progress",
                       "falls element by element in game time (Topple, Drop, Crumble, CaveIn)"],
        "damageZones": "every element is a zone: 16 columns (weak joint fixed per column), 16 spans x 2 blocks, 8 walls, 8 roof bays, "
                       "2 pediments, the door; scars on the faces nearest the shots",
        "destructionStages": "Intact < 8% of the hull lost <= SurfaceDamage (scars) < 30% <= LocalFailure (1-3 own failures) "
                             "< 60% <= PartialCollapse (4-9) < fallen = WreckOrRuin -> Settled",
        "lod": "standing: one mesh (Whole) until an element fails; damaged: Standing + Scars + Rubble, one mesh each at rest; "
               "no separate LOD levels (to measure)",
        "integration": "PortBuildings.School.cs, Destruction/*; BattleController: projectile_hit_settlement -> Struck, Unseen when "
                       "out of sight, DestructionBook beside a quick save",
        "tests": ["BuildingDestructionTests (A, B, C, E, F, G, H, I, L, M, out of sight)", "PortMasonryTests",
                  "DestructionPresentationTests", "PortPresentationTests.PortViews_LeaveTheSimulationUntouched"],
        "results": "artifacts-log/destruction/school-*.png (intact, surface, local, partial, falling, ruin; close and strategic)",
        "limitations": ["the dock still falls as one piece into its seeded ruin; the other buildings break as the School does (PortBuildings.Breakables)",
                        "mudbrick is a tint of the plaster texture, not a brick texture of its own",
                        "scars are flat patches, not carved geometry",
                        "cost of a whole collapse not yet measured in the Windows build"],
        "nextAction": "measure a port's collapse in the build (smoke report); split the School's own model build over frames",
        "stages": [{"state": "fracturing", "date": "2026-11-10", "commit": "6af648b"},
                   {"state": "validating", "date": "2026-11-10", "evidence": "BuildingDestructionTests 11/11, previews in artifacts-log/destruction"}],
        "config": "DamagePlan thresholds; StructureDamageView.MaxBodiesPerStructure 56, MaxBodies 112, MaxChunks 12",
    },
    "fx.ship-damage": {
        "class": "effect on every ship class with a mast (ShipVisual)",
        "dating": "E6; warships' masts in battle: the light mast (R-SAILS-ASHORE, interpretation)",
        "dimensions": "the class's mast (GreekShips.BuildRig): height = MastHeight of the form, radius 0.012 L + 0.08 m",
        "materials": ["the ship's look: wood, ochre, rope; sail in the owner's dye"],
        "construction": "mast stepped on the keelson (R-KYRENIA-MASTSTEP), yard bowed at the masthead, forestay and backstay",
        "movingParts": "the fallen mast with yard and sail pivots on its snap point; two loose lines from stem and stern to the masthead",
        "attachmentPoints": "GreekShipParts.MastFoot, SailTop; the stem and stern heads (Sheer, Z) for the lines",
        "animations": ["the mast tips over the listing side in 1.5 s of game time, slow at first", "the sail goes slack",
                       "it then lies still in the hull's frame and moves only with the hull"],
        "damageZones": "breaches on the struck side (Hit), arrows (StickArrows), sail tears and the mast (hull share), broken posts and "
                       "the bent ram (module HP)",
        "destructionStages": "hull share >= 0.6: breaches and tears only; < 0.4 (PartialCollapse): the mast down; ship_sunk: ShipFate "
                             "(break-up, wreckage) with the fallen mast going down where it lies; >= 0.5 again in dock: a new mast",
        "lod": "the stump and spars are single meshes in the ship's materials; ropes are one shared unit mesh scaled",
        "integration": "ShipVisual.Mast.cs (from SetCondition and AnimateSail, not from the water's methods), BattleController."
                       "MastEffects (splinters, water column), BattleEffects.ShipFate skips its own mast fall for a mast already down",
        "tests": ["ShipDestructionTests (fall side, moves with the hull, dock repair and pool reset, first seen down)",
                  "BattleControllerTests (Visual_DoesNotChangeSimulationResult)", "water_guard check"],
        "results": "artifacts-log/destruction/ship-*-stages*.png (preview without water: what lies under the waterline shows)",
        "limitations": ["oars are not broken afloat; planks fly only as splinters", "crew may stand where the fallen mast lies",
                        "the sail is drawn even though warships left their large sails ashore (R-SAILS-ASHORE)"],
        "nextAction": "furl or leave the sail in battle (art direction); broken oars on the rammed side",
        "stages": [{"state": "fracturing", "date": "2026-11-10", "commit": "9810ecc"},
                   {"state": "validating", "date": "2026-11-10", "evidence": "ShipDestructionTests 4/4, previews in artifacts-log/destruction"}],
        "config": "ShipVisual.MastBreaksBelow 0.4, MastMendedAbove 0.5, MastFallSeconds 1.5",
    },
}

# The state of the rest, by assetId, where the rework's status says more than "planned".
STATE = {
    "fx.building-destruction": "validating",
    "building.store": "validating",
    "building.town": "validating",
    "building.workshop": "validating",
    "building.ruin": "validating",
    "ship.Ram": "preparing",
    "fx.boarding": "ready",
    "materials.masonry": "ready",
    "prop.amphora": "ready",
    "prop.pithos": "ready",
    "prop.anchor": "ready",
    "rock.attica-set": "ready",
    "prop.herm-stele": "needs_review",
    "prop.basket-net": "needs_review",
    "veg.pine.halepensis": "validating",
    "veg.cypress.columnar": "validating",
    "water.system": "blocked",
    "audio": "blocked",
}

NEXT = {
    "planned": "read the sources, then write a brief (docs/ASSET_PIPELINE.md)",
    "preparing": "finish the generator work in progress and preview it",
    "validating": "measure in the build and show the previews",
    "ready": "none",
    "needs_review": "a person decides: another attempt, a procedural model or none",
    "blocked": "none until the reason goes",
}


def load(path):
    with open(path, encoding="utf-8") as handle:
        return json.load(handle)


def state_of(asset):
    if asset["assetId"] in SLICE:
        return SLICE[asset["assetId"]]["stages"][-1]["state"]
    if asset["assetId"] in STATE:
        return STATE[asset["assetId"]]
    status = asset.get("status", "planned")
    if status in ("protected", "out-of-scope"):
        return "blocked"
    if status.startswith("done"):
        return "ready"
    if status.startswith("in progress") or status.startswith("trial done"):
        return "preparing"
    return "planned"


def manifest():
    rework = load(REWORK)
    ledger = load(LEDGER)
    tasks = {}
    for task in ledger["tasks"]:
        tasks.setdefault(task["asset"], []).append({"meshyTask": task["meshyTask"], "credits": task["credits"]})
    entries = []
    for asset in rework["assets"]:
        asset_id = asset["assetId"]
        state = state_of(asset)
        entry = {
            "assetId": asset_id,
            "use": asset["use"],
            "profile": asset["profile"],
            "scope": asset["scope"],
            "criteria": asset["criteria"],
            "state": state,
            "status": asset["status"],
            "meshy": tasks.get(asset_id, []),
        }
        if asset_id in SLICE:
            entry.update(SLICE[asset_id])
            entry["state"] = state
            entry["slice"] = True
        else:
            entry["nextAction"] = NEXT[state]
            entry["slice"] = False
        entries.append(entry)
    counts = {}
    for entry in entries:
        counts[entry["state"]] = counts.get(entry["state"], 0) + 1
    return {
        "schema": "morza-bogow/art-production-manifest@1",
        "states": STATES,
        "anchor": rework["anchor"],
        "meshy": {"approvedCredits": ledger["approvedCredits"], "spentCredits": sum(t["credits"] for t in ledger["tasks"])},
        "counts": dict(sorted(counts.items())),
        "assets": entries,
    }


SLICE_FIELDS = ("class", "dating", "dimensions", "materials", "construction", "movingParts", "attachmentPoints", "animations",
                "damageZones", "destructionStages", "lod", "integration", "tests", "results", "limitations", "nextAction", "stages")


def problems():
    found = []
    data = manifest()
    ids = [e["assetId"] for e in data["assets"]]
    rework = {a["assetId"] for a in load(REWORK)["assets"]}
    for missing in sorted(rework - set(ids)):
        found.append(f"brak wpisu dla {missing}")
    for entry in data["assets"]:
        if entry["state"] not in STATES:
            found.append(f"{entry['assetId']}: nieznany stan {entry['state']}")
    for asset_id, detail in SLICE.items():
        if asset_id not in rework:
            found.append(f"wycinek {asset_id} nie ma wpisu w VisualReworkManifest")
        for field in SLICE_FIELDS:
            if not detail.get(field):
                found.append(f"wycinek {asset_id}: puste pole {field}")
    if os.path.exists(OUT) and load(OUT) != data:
        found.append("docs/art/ArtProductionManifest.json różni się od generatora: uruchom write")
    return found


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("command", choices=["write", "check"])
    args = parser.parse_args(argv)
    if args.command == "write":
        with open(OUT, "w", encoding="utf-8", newline="\n") as handle:
            json.dump(manifest(), handle, indent=1, ensure_ascii=False)
            handle.write("\n")
    found = problems()
    for problem in found:
        print(problem)
    data = manifest()
    print(f"Manifest produkcji: {len(data['assets'])} pozycji, stany {data['counts']}; {'BRAKI: ' + str(len(found)) if found else 'kompletny'}")
    return 1 if found else 0


if __name__ == "__main__":
    sys.exit(main())
