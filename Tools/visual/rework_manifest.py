#!/usr/bin/env python3
"""VisualReworkManifest: every asset of the visual refresh (Greece, 4th c. BC), what changes and how it is accepted.

    python3 Tools/visual/rework_manifest.py write   # docs/art/VisualReworkManifest.json from the entries below
    python3 Tools/visual/rework_manifest.py check   # coverage and fields; exit 1 on a gap

The check covers every assetId of docs/art/AssetManifest.json and docs/art/EnvironmentAssetManifest.json, every ship
class of the pipeline's dimensions (= ShipCatalog), every crew role of docs/art/CrewRoles.json and every PortBuilding.
Meshy spending comes from docs/art/MeshyLedger.json. Standard library only.
"""
import argparse
import json
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "docs", "art", "VisualReworkManifest.json")
LEDGER = os.path.join(ROOT, "docs", "art", "MeshyLedger.json")

SCOPES = {
    "keep": "good as it is; no change without a reason",
    "retexture": "new textures on the same geometry",
    "material": "material correction (PBR values, masks, wear)",
    "geometry-fix": "repair or detail of the existing geometry",
    "replace": "new model in place of the old one",
    "animation": "animation added or improved",
    "add-missing": "a missing element of the presentation",
    "exclude": "outside the refresh (protected)",
}
E6 = "E6 late classical, c. 400-323 BC"
E1 = "E1 Late Helladic IIIC (earlier profile, inactive)"
ATTICA = "R-LAD Attica (Piraeus, Zea, Laurion)"
AEGEAN = "Aegean (game map)"

ENTRIES = []


def entry(asset_id, use, current, scope, criteria, *, epoch=E6, region=AEGEAN, textures=(), materials=(), animations=(),
          links=(), lod="", covers=(), status="planned"):
    ENTRIES.append({
        "assetId": asset_id, "use": use, "profile": {"epoch": epoch, "region": region}, "current": current,
        "scope": list(scope), "textures": list(textures), "materials": list(materials), "animations": list(animations),
        "links": list(links), "lod": lod, "criteria": list(criteria), "covers": list(covers), "status": status,
    })


SHIP_LINKS = ["GreekShips.cs", "ShipVisual.cs", "Looks/*", "Art/Ships/Shaders/ShipPaint.shadergraph"]
SHIP_TEX = ["wood planks (fir/pine) CC0 scan", "pitch", "linen sail", "bronze (ram, fittings)", "rope", "painted eye and parasemon"]
SHIP_MAT = ["ShipPaint palette (shared, SRP Batcher)", "wet, charred and bronze states"]
SHIP_CRIT = ["class dimensions, attack/boarding/crew points and colliders unchanged (tests)",
             "silhouette reads at the strategic camera; side colour visible", "no plastic gloss; wear placed by function",
             "variants (ShipLook) keep their identity", "water_guard check passes"]
SHIP_ANIM = ["rowing stroke", "brailing and furling the sail in battle", "damage, list, sinking, break-up"]

entry("ship.Cutter", "ShipClass.Cutter: gathering, scouting", "GreekShips generator, LH IIIC boat; pipeline prefab with flat colours",
      ["geometry-fix", "retexture", "material"], SHIP_CRIT, textures=SHIP_TEX, materials=SHIP_MAT, animations=SHIP_ANIM,
      links=SHIP_LINKS, lod="game: single; prefab 3 LOD", covers=["Cutter"])
entry("ship.Ram", "ShipClass.Ram: ramming; trial ship of the refresh", "GreekShips LH IIIC galley; Meshy retexture prefab (not used by the game)",
      ["geometry-fix", "retexture", "material", "animation"],
      SHIP_CRIT + ["trireme features in the class's 20 m: eyes, finned ram, outrigger, side screens, aphlaston (scale compromise documented)"],
      textures=SHIP_TEX, materials=SHIP_MAT, animations=SHIP_ANIM + ["ram run: sprint, brace, impact, back-water"],
      links=SHIP_LINKS, lod="game: single; prefab 3 LOD", covers=["Ram"])
entry("ship.CrewDestroyer", "archers' galley", "GreekShips LH IIIC galley", ["geometry-fix", "retexture", "material"],
      SHIP_CRIT + ["trireme with archers' deck"], textures=SHIP_TEX, materials=SHIP_MAT, animations=SHIP_ANIM,
      links=SHIP_LINKS, lod="game: single; prefab 3 LOD", covers=["CrewDestroyer"])
entry("ship.Boarder", "boarding galley", "GreekShips LH IIIC fifty-oar galley", ["geometry-fix", "retexture", "material"],
      SHIP_CRIT + ["broad decked four or five (tetreres/penteres) with marines"], textures=SHIP_TEX, materials=SHIP_MAT,
      animations=SHIP_ANIM + ["boarding: hooks, haul alongside, crossing"], links=SHIP_LINKS, lod="game: single; prefab 3 LOD",
      covers=["Boarder"])
entry("ship.Merchant", "transport and bridgeheads", "GreekShips merchantman with wooden topsides", ["geometry-fix", "retexture", "material"],
      SHIP_CRIT + ["holkas after the Kyrenia ship: round hull, brailed square sail, amphora cargo"], textures=SHIP_TEX,
      materials=SHIP_MAT, animations=SHIP_ANIM + ["load lies lower"], links=SHIP_LINKS, lod="game: single; prefab 3 LOD",
      covers=["Merchant"])
entry("ship.ShipKiller", "bolt-thrower galley", "GreekShips galley with machine stands", ["geometry-fix", "retexture", "material", "animation"],
      SHIP_CRIT + ["oxybeles (torsion bolt-shooter) on deck: no longer an anachronism in the 4th c. (early interpretation)"],
      textures=SHIP_TEX, materials=SHIP_MAT, animations=SHIP_ANIM + ["machine: winding, loading, release, recoil"],
      links=SHIP_LINKS, lod="game: single; prefab 3 LOD", covers=["ShipKiller"])
entry("ship.Bomber", "heavy stone-thrower galley", "GreekShips galley with castles", ["geometry-fix", "retexture", "material", "animation"],
      SHIP_CRIT + ["lithobolos on a heavy ship (Tyre 332 BC; early interpretation)"], textures=SHIP_TEX, materials=SHIP_MAT,
      animations=SHIP_ANIM + ["machine: arm down, stone, release"], links=SHIP_LINKS, lod="game: single; prefab 3 LOD", covers=["Bomber"])
entry("ship.variants", "individual looks of ships of one class", "ShipLook workshops, grain, cloth, marks, gear (procedural-verified)",
      ["keep", "material"], ["deterministic from the ship id; saved beside the quick save", "4th c. workshop palettes and parasemon"],
      links=["Looks/*"], lod="LODGroup on the look's details")
entry("ship.slipway-stages", "ship build progress on a slipway/ship shed", "ShipyardVisual stages; shipwrights primitive",
      ["geometry-fix", "animation"], ["stages follow the build progress; never finish production themselves",
                                       "assembly visible (keel, planking, frames, deck); no scaling from zero"],
      links=["ShipyardVisual.cs"], animations=["adze, auger, carrying planks"], region=ATTICA)

CREW_LINKS = ["Crew/CrewBody.cs", "Crew/CrewLooks.cs", "Crew/CrewPose.cs", "Crew/CrewAboard.cs"]
CREW_CRIT = ["realistic anatomy and proportions (CrewGeometryTests)", "dress and gear of the 4th c. by role, not one armour for all",
             "one crew material (palette atlas), instanced; CPU budget measured", "certainty level in CrewRoles.json"]
entry("character.warrior", "epibatai (marines) and soldiers on deck", "segment body, LH IIIC kit", ["replace", "animation"],
      CREW_CRIT + ["pilos helmet, linothorax or muscle cuirass, aspis with a city blazon, spear and sword"], links=CREW_LINKS,
      animations=["thrust, guard, brace at the ram, climb over rails, fall"], lod="LOD0-2 (17/11/6 bones)", covers=["Marine", "WarriorRower", "Commander"])
entry("character.archer", "Cretan archers of an archers' bank", "segment body, LH IIIC kit", ["replace", "animation"],
      CREW_CRIT + ["bow, quiver, light dress"], links=CREW_LINKS, animations=["nock, draw, aim, loose at weapon_fired"],
      lod="LOD0-2", covers=["BankArcher"])
entry("character.peltast", "javelin men of a javelin bank", "segment body as warrior", ["add-missing", "animation"],
      CREW_CRIT + ["pelte shield, javelins, exomis"], links=CREW_LINKS, animations=["reach, wind-up, throw at weapon_fired"],
      lod="LOD0-2", covers=["BankJavelin"])
entry("character.rower", "rowers and sailors", "segment body, loincloth", ["material", "animation"],
      CREW_CRIT + ["exomis or loincloth; hands on the oar handle through the whole stroke"], links=CREW_LINKS,
      animations=["one stroke rhythm per ship (RowingStroke)", "back-water"], lod="LOD0-2",
      covers=["Rower", "SailHand", "LineHand", "BowHand", "Wounded"])
entry("character.officers", "helmsman (kybernetes), bow officer (prorates), skipper", "segment body", ["material", "animation"],
      CREW_CRIT + ["keleustes and trieraules only as looks of men already aboard"], links=CREW_LINKS,
      animations=["tillers, look-out, command gestures"], lod="LOD0-2", covers=["Helmsman", "Lookout", "Skipper"])
entry("character.machine-crew", "crews of bolt and stone throwers", "segment body", ["material", "animation"],
      CREW_CRIT + ["winding, loading, aiming, release in the sim's timing"], links=CREW_LINKS, lod="LOD0-2", covers=["MachineCrew"])
entry("character.worker", "shipwrights, miners, woodcutters, porters ashore", "CrewAshore on the crew body; primitive tools",
      ["material", "animation"], CREW_CRIT + ["tools of the trade; work never changes stock or production speed"],
      links=["Crew/CrewAshore.cs", "ResourceSiteVisual.cs", "ShipyardVisual.cs"], animations=["adze, pick, carry, haul, wash ore"],
      lod="LOD0-2", region=ATTICA)

FX_CRIT = ["effect matches the material (wood splinters, cloth shreds, rope snap, metal contact)", "no gunpowder explosions",
           "priority in a big battle; never hides units, target or UI", "pooled; own materials, not the water's spray"]
entry("fx.missiles", "bolts, stones, arrows and javelins in flight", "pooled meshes from World.Projectiles", ["material", "animation"],
      FX_CRIT + ["launch from the hand or machine; flight time as the simulation"], links=["BattleEffects.cs"])
entry("fx.impacts", "hits and misses", "Shuriken splinters, flash spheres", ["replace"],
      FX_CRIT + ["a miss never looks like a hit"], links=["BattleEffects.cs", "ShipVisual.cs"])
entry("fx.sinking", "ship_sunk: settle, break-up, wreckage", "ShipFate about 10 s, debris max 120", ["material", "animation"],
      FX_CRIT + ["break-up into hull sections with the ship's materials"], links=["BattleEffects.cs"])
entry("fx.ram-contact", "ram impact", "flash and splinters", ["add-missing"], FX_CRIT + ["breach at the struck place; crew jolt"],
      links=["BattleEffects.cs", "ShipVisual.cs"])
entry("fx.boarding", "hooks, lines, crossing, fighting on the enemy deck", "haul by translation; no crossing", ["add-missing", "animation"],
      ["broadside to broadside, never bow-first", "boarders cross the rails and fight on the enemy deck",
       "picture only: simulation results unchanged"], links=["Crew/CrewDirector.cs", "Crew/CrewAboard.cs", "CrewVisual.cs"])
entry("fx.building-destruction", "ruin and destruction of port buildings", "PortBuildings ruin state", ["add-missing", "animation"],
      FX_CRIT + ["break-up into modules, falling tiles, dust; follows the game's state"], links=["PortBuildings.cs"])
entry("wreck.cargo", "World.Wrecks: salvageable cargo", "procedural jars and ingots", ["replace"],
      ["4th c. amphorae (Rhodian, Samian) and lead"], links=["BattleEffects.cs"])

BUILD_CRIT = ["4th c. construction: stone socle, mudbrick or ashlar, timber, tiles, plaster, polychromy (not all white marble)",
              "footprint and interaction points unchanged", "construction stages by assembly from the game's progress; no scaling",
              "ruin follows the game's state", "shared material library; LOD"]
entry("building.store", "store and houses of a port", "procedural limestone boxes", ["replace"], BUILD_CRIT + ["courtyard houses (Olynthus) and a stoa"],
      links=["PortBuildings.cs"], region=ATTICA, covers=["Store"])
entry("building.workshop", "ProjectKind.Workshop (weapons)", "procedural shed with forge", ["replace", "animation"],
      BUILD_CRIT + ["ergasterion with forge and bellows"], links=["PortBuildings.cs"], region=ATTICA, covers=["Workshop"])
entry("building.town", "home port town", "procedural terraces, walls, towers", ["replace"],
      BUILD_CRIT + ["ashlar circuit walls with towers, tiled roofs"], links=["PortBuildings.cs"], region=ATTICA, covers=["Town"])
entry("building.school", "School of Sages (research)", "colonnade, pediment, emissive flame", ["replace"],
      BUILD_CRIT + ["gymnasion with a Doric peristyle (Academy, Lyceum)"], links=["PortBuildings.cs"], region=ATTICA, covers=["SageSchool"])
entry("building.dock", "dock (repairs for silver)", "shear legs and windlass", ["replace"],
      BUILD_CRIT + ["ship shed (neosoikos) after Zea: ramp, colonnade, roof"], links=["PortBuildings.cs"], region=ATTICA, covers=["Dock"])
entry("building.shipyard", "settlement shipyard and slipways", "ramp and posts from primitives", ["replace"],
      BUILD_CRIT + ["ship sheds and slipway of the 4th c."], links=["ShipyardVisual.cs", "Environment/ShipyardProps.cs"], region=ATTICA)
entry("building.outpost", "outpost / bridgehead", "placeholder", ["replace"], BUILD_CRIT + ["field camp: palisade, tents, beached boats"],
      links=["BattleController.cs"])
entry("building.jetty", "jetties at sites and settlements", "[PLACEHOLDER] primitive jetty", ["replace"],
      BUILD_CRIT + ["timber jetty or stone quay"], links=["ResourceSiteVisual.cs", "BattleController.cs"])

SITE_CRIT = ["animations never change stock or production speed", "state follows Stock and Capacity", "no glowing silver"]
entry("site.silver", "silver mine site", "mine primitives; glossy ore", ["replace", "animation"],
      SITE_CRIT + ["Laurion 4th c.: shaft and gallery mouth, ore baskets, washery (ergasterion) with cistern, furnace, slag",
                   "rock, ore, concentrate and metal told apart"], links=["ResourceSiteVisual.cs", "Environment/OreOutcrop.cs"], region=ATTICA)
entry("site.wood", "timber yard", "TreeModels trees, primitive log piles", ["replace", "animation"],
      SITE_CRIT + ["fir and pine logs, hewn beams, planks, end grain (Theophrastus HP 5.7)"], links=["ResourceSiteVisual.cs", "TreeModels.cs"])
entry("environment.coast", "coast rocks and shore", "CC0 coast model and Poly Haven boulders (LODs merged, x4 triangles)",
      ["replace", "geometry-fix"], ["geology by region (limestone, marble, schist)", "map topology and fields unchanged",
                                    "boulder LOD merge fixed"], links=["ShoreShape.cs", "Editor/ShoreArt.cs", "BattleController.ModelOf"])
entry("rock.attica-set", "rock modules (limestone, marble, Laurion schist)", "missing", ["add-missing"],
      ["4-6 Meshy variants with LOD; strata, cracks, erosion"], links=["ShoreShape.cs", "Environment/*"], region=ATTICA)

ENV = [("veg.olive.cultivated", "keep"), ("veg.olive.wild", "keep"), ("veg.pine.halepensis", "add-missing"), ("veg.pine.brutia", "add-missing"),
       ("veg.cypress.wild", "add-missing"), ("veg.cypress.columnar", "add-missing"), ("veg.quercus.coccifera", "add-missing"),
       ("veg.pistacia.lentiscus", "add-missing"), ("veg.phrygana", "add-missing"), ("veg.halophytes", "add-missing"),
       ("coast.limestone.notch", "keep"), ("coast.marble-schist", "add-missing"), ("coast.beachrock", "keep"),
       ("coast.pebble-beach", "add-missing"), ("coast.sand-cove", "add-missing"), ("coast.terra-rossa", "add-missing"),
       ("ore.laurion.outcrop", "geometry-fix"), ("ore.face.E1", "keep"), ("ore.face.E5", "add-missing"), ("ore.heap", "material"),
       ("ore.washery.E5", "add-missing"), ("yard.slipway.E1", "keep"), ("yard.tools", "material"), ("yard.shed.zea2", "add-missing")]
for env_id, scope in ENV:
    early = env_id.endswith(".E1")
    entry(env_id, "environment manifest item (EnvironmentAssetManifest.json)", "see EnvironmentAssetManifest.json",
          [scope], ["species and geology of the region and epoch; excluded post-antique species stay out",
                    "works with the match wind (vegetation)", "map topology unchanged"],
          epoch=E1 if early else E6, region=ATTICA, links=["Environment/*", "docs/art/EnvironmentAssetManifest.json"])

PROP_CRIT = ["4th c. forms; Meshy model with LOD from Blender", "shared materials; no unique large texture per object"]
for prop_id, use in [("prop.amphora", "transport amphorae (Rhodian, Samian, Chian)"), ("prop.pithos", "storage jars"),
                     ("prop.anchor", "wooden-stock anchors with lead stock"), ("prop.basket-net", "baskets, nets, rope coils"),
                     ("prop.herm-stele", "herms and stelae at harbours and sanctuaries")]:
    entry(prop_id, use, "missing or procedural (ShipLookLibrary gear)", ["add-missing"], PROP_CRIT, links=["Looks/ShipLookLibrary.cs"])

entry("textures.ship-look", "baked ship textures", "procedural baker", ["material"], ["CC0 scans for wood, pitch, linen, rope; bronze"],
      links=["Editor/ShipTextureBaker.cs"])
entry("shader.ship-paint", "ship paint graph", "HDRP Lit graph with renderer value", ["keep", "material"],
      ["wet and charred bits wired to existing systems only"], links=["Art/Ships/Shaders/ShipPaint.shadergraph"])
entry("materials.pbr-library", "shared PBR material library", "partial", ["material"],
      ["HDRP mask R=metal G=AO B=detail A=smoothness; sRGB only for colour; normals tested"], links=["Art/*"])
entry("shaders.catalog", "shader catalogue", "one custom graph", ["keep"], ["no new shader without a measured need"], links=["Art/Ships/Shaders"])
entry("materials.masonry", "limestone ashlar and lime plaster of the port's buildings", "the shore's cliff texture tinted", ["material"],
      ["courses run level on upright faces (MeshBuilder.Level)", "HDRP mask R=metal G=AO B=detail A=smoothness; sRGB only for colour",
       "the keywords the boulders and the olive bark already use"],
      links=["Editor/BuildingTextureBaker.cs", "Art/Buildings/Resources/Buildings"], region=ATTICA)
entry("audio", "sound of the battle and the world", "missing (no audio layer)", ["exclude"],
      ["no audio layer exists; out of this refresh until decided"], status="out-of-scope")
entry("water.system", "the sea: HDRP water, deformation, foam, wakes, spray, marine simulation", "protected", ["exclude"],
      ["water_guard check passes (docs/art/WaterProtection.md)"], links=["Tools/visual/water_guard.py"], status="protected")


# Progress of single entries (assetId -> status), kept apart from their definitions above.
STATUS = {
    "ship.Ram": "in progress: 4th c. features in the generator (ram, eyes, volute, aphlaston; commit 10c5c70)",
    "character.warrior": "in progress: 4th c. kit (pilos, Corinthian and Boeotian helmets, linothorax, aspis; commit 9ed303b)",
    "character.peltast": "in progress: pelte and caps (commit 9ed303b)",
    "prop.amphora": "first model ready: Meshy MCP -> Blender -> Unity (prop_amphora_rhodian)",
    "fx.boarding": "done: broadside haul, oars in, the front crosses and fights on the other deck, the dead lie where they fell, return over the rails (BoardingPresentationTests 3/3)",
    "rock.attica-set": "in the game: rock_attica_limestone_a and rock_pentelic_marble_a among the shore boulders (AegeanArt); "
                       "sea-stack and Laurion schist previews rejected",
    "environment.coast": "in progress: boulder merge fixed (finest level of detail only, was every level: x4 triangles); "
                         "Attic rocks among the boulders",
    "building.school": "trial done: hall in a 4x6 Doric peristyle (fluted shafts with entasis, echinus, abacus), painted entablature "
                       "(blue triglyphs, regulae and mutules; red taenia and raking sima; blue tympana), ashlar crepis; rises column "
                       "by column and stands as one mesh (PortMasonryTests)",
    "building.store": "in progress: ashlar store and plastered house on stone socles rising in courses, tiled roofs with antefixes; "
                      "yard of pithoi, amphorae and one-armed anchors with lead-filled stocks",
    "building.town": "in progress: regular ashlar curtain with a parapet, square towers with loopholes and gabled tiled roofs (R-MESSENE)",
    "building.workshop": "in progress: tiled roof with antefixes",
    "prop.pithos": "procedural (Lathe): egg-shaped jar, rope bands, rolled rim; the Meshy preview (a barrel) rejected",
    "prop.anchor": "procedural: one-armed oak anchor, iron-shod tip, lead-filled wooden stock (R-ANCHORS); the Meshy preview "
                   "(an admiralty anchor) rejected",
    "site.wood": "in progress: fir log stacks, Aleppo pines and columnar cypresses from the pipeline at the timber yards (AegeanArt); "
                 "felled as before, no wind sway yet",
    "site.silver": "in progress: 4th c. ore workshop (flat-washing platform, stand tank, channel, settling tanks), open cistern, "
                   "mortars, schist rock behind the adit (R-LAURION-WORKSHOPS)",
    "veg.pine.halepensis": "Meshy model in the timber yards (veg_pinus_halepensis_a); coast groves still the old meshes",
    "veg.cypress.columnar": "Meshy model in the timber yards (veg_cupressus_sempervirens); coast groves still the old meshes",
    "building.ruin": "collapse into the ruin when a settlement falls in sight: sinking walls, the roof falling in, dust, flying tiles",
    "fx.building-destruction": "done: walls sink and lean, the roof falls in, two clouds of dust, tiles thrown, the ruin shows "
                               "through the dust (2.6 s of game time; PortMasonryTests)",
    "materials.masonry": "done: ashlar and plaster baked in code (BuildingTextureBaker), Ashlar.mat and Stucco.mat in Resources",
}
for _entry in ENTRIES:
    _entry["status"] = STATUS.get(_entry["assetId"], _entry["status"])

def ledger():
    with open(LEDGER, encoding="utf-8") as handle:
        return json.load(handle)


def manifest():
    book = ledger()
    spent = sum(t["credits"] for t in book["tasks"])
    by_asset = {}
    for task in book["tasks"]:
        by_asset.setdefault(task["asset"], []).append(task["meshyTask"])
    return {
        "schema": "morza-bogow/visual-rework-manifest@1",
        "anchor": {"epoch": E6, "region": ATTICA, "earlier": E1,
                   "decision": "user 2026-11-10: Greece of the 4th c. BC (first 7th c., then corrected to the 4th c.)"},
        "scopes": SCOPES,
        "meshy": {"approvedCredits": book["approvedCredits"], "extraWithoutAsking": book["extraWithoutAsking"], "spentCredits": spent,
                  "tasksByAsset": by_asset},
        "assets": ENTRIES,
    }


def required_ids():
    art = os.path.join(ROOT, "docs", "art")
    ids = set()
    with open(os.path.join(art, "AssetManifest.json"), encoding="utf-8") as handle:
        ids |= {a["assetId"] for a in json.load(handle)["assets"]}
    with open(os.path.join(art, "EnvironmentAssetManifest.json"), encoding="utf-8") as handle:
        ids |= {a["id"] for a in json.load(handle)["assets"]}
    with open(os.path.join(ROOT, "Tools", "asset_pipeline", "config", "project_dimensions.json"), encoding="utf-8") as handle:
        ids |= set(json.load(handle)["ships"].keys())
    with open(os.path.join(art, "CrewRoles.json"), encoding="utf-8") as handle:
        ids |= {r["rola"] for r in json.load(handle)["role"]}
    with open(os.path.join(ROOT, "Assets", "MorzaBogow", "Runtime", "Presentation", "PortBuildings.cs"), encoding="utf-8") as handle:
        enum = re.search(r"enum PortBuilding \{([^}]*)\}", handle.read())
    ids |= {name.strip() for name in enum.group(1).split(",")}
    return ids


def problems():
    found = []
    covered = {e["assetId"] for e in ENTRIES} | {c for e in ENTRIES for c in e["covers"]}
    for missing in sorted(required_ids() - covered):
        found.append(f"brak wpisu dla {missing}")
    seen = set()
    for e in ENTRIES:
        if e["assetId"] in seen:
            found.append(f"powtórzony wpis {e['assetId']}")
        seen.add(e["assetId"])
        for field in ("use", "current", "scope", "criteria"):
            if not e[field]:
                found.append(f"{e['assetId']}: puste pole {field}")
        for scope in e["scope"]:
            if scope not in SCOPES:
                found.append(f"{e['assetId']}: nieznany zakres {scope}")
    if os.path.exists(OUT):
        with open(OUT, encoding="utf-8") as handle:
            if json.load(handle) != manifest():
                found.append("docs/art/VisualReworkManifest.json różni się od generatora: uruchom write")
    return found


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("command", choices=["write", "check"])
    args = parser.parse_args(argv)
    if args.command == "write":
        with open(OUT, "w", encoding="utf-8", newline="\n") as handle:
            json.dump(manifest(), handle, indent=1, ensure_ascii=False)
            handle.write("\n")
        print(f"Zapisano {len(ENTRIES)} wpisów: docs/art/VisualReworkManifest.json")
    found = problems()
    for problem in found:
        print(problem)
    print(f"Manifest: {len(ENTRIES)} wpisów, {'BRAKI: ' + str(len(found)) if found else 'pokrycie pełne'}; "
          f"Meshy wydane {manifest()['meshy']['spentCredits']} z {ledger()['approvedCredits']} kr.")
    return 1 if found else 0


if __name__ == "__main__":
    sys.exit(main())
