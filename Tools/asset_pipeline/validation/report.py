"""Decision on Unity's validation report: ready, repair geometry, or a person's review.

Unity measures the imported prefab (validation.json written by AIAssetValidator); this module
applies the brief's budget and the pipeline's tolerances so the rules live in one place.
"""

LENGTH_TOLERANCE = 0.03     # ships: hull length within 3% of the class
DRAFT_TOLERANCE = 0.08      # ships: keel depth within 8% of the class draft


def evaluate(report: dict, brief: dict, config) -> dict:
    failures, warnings = [], []
    repair = False
    budget = brief["budget"]

    for check in report.get("checks", []):
        if check.get("ok"):
            continue
        text = f"{check.get('id')}: {check.get('message', '')}".strip()
        (failures if check.get("severity", "error") == "error" else warnings).append(text)

    lods = report.get("lods", [])
    if not lods:
        failures.append("prefab bez LODGroup lub bez rendererów")
    else:
        if lods[0].get("triangles", 0) > budget["triangles"]:
            failures.append(f"LOD0 {lods[0]['triangles']} trójkątów > budżet {budget['triangles']}")
            repair = True
        for a, b in zip(lods, lods[1:]):
            if b.get("triangles", 0) >= a.get("triangles", 0):
                failures.append(f"LOD{b['level']} nie upraszcza LOD{a['level']}")
                repair = True
        if len(lods) < brief["lods"]:
            warnings.append(f"{len(lods)} z {brief['lods']} poziomów LOD")
    if report.get("materialCount", 0) > budget["materials"]:
        failures.append(f"{report['materialCount']} materiałów > budżet {budget['materials']}")
    if report.get("rendererCount", 0) > budget["renderers"]:
        failures.append(f"{report['rendererCount']} rendererów > budżet {budget['renderers']}")
    for texture in report.get("textures", []):
        if texture.get("maxSize", 0) > budget["textureSize"]:
            failures.append(f"tekstura {texture.get('path')} {texture['maxSize']} px > {budget['textureSize']} px")
    density = report.get("texelDensity")
    if density and budget.get("texelDensity") and density < budget["texelDensity"] * 0.5:
        warnings.append(f"gęstość tekseli {density:.0f} px/m przy celu {budget['texelDensity']} px/m")

    if brief["category"] == "ship":
        ship = config.ship(brief["shipClass"])
        size = report.get("bounds", {}).get("size", [0, 0, 0])
        minimum = report.get("bounds", {}).get("min", [0, 0, 0])
        if abs(size[2] - ship["length"]) > ship["length"] * LENGTH_TOLERANCE:
            failures.append(f"długość {size[2]:.2f} m, klasa {ship['length']} m")
        if abs(-minimum[1] - ship["draft"]) > ship["draft"] * DRAFT_TOLERANCE:
            failures.append(f"stępka na {minimum[1]:.2f} m, zanurzenie klasy {ship['draft']} m")
        oars = max(2.5, ship["length"] * 0.28)
        if size[0] > ship["beam"] + 2 * oars + 0.5:
            failures.append(f"szerokość {size[0]:.2f} m przekracza burtę i wiosła ({ship['beam'] + 2 * oars:.1f} m)")
        if size[0] < ship["beam"] * 0.8:
            failures.append(f"szerokość {size[0]:.2f} m węższa niż burta klasy {ship['beam']} m")
        if report.get("forwardChecked") is False:
            warnings.append("kierunek dziobu ustalony automatycznie: sprawdź podgląd")
    if not report.get("preview"):
        warnings.append("brak podglądu (render niedostępny w tym trybie)")
    return {"passed": not failures, "failures": failures, "warnings": warnings, "repair": repair and bool(failures)}
