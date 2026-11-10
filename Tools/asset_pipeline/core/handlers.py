"""Stage handlers. Each one is idempotent: it looks at the job's artifacts and provider tasks,
does the next piece of work and returns a short report, or raises an outcome from errors.py
(or worker.Wait while a remote task runs)."""
import json
import pathlib
import shutil

from . import brief as briefs
from .errors import Fatal, NeedsConfiguration, NeedsReview
from .paths import project_relative

MAP_SUFFIXES = {
    "base_color": ("basecolor", "base_color", "albedo", "diffuse", "diff", "color", "col"),
    "normal": ("normal", "nor", "nrm", "normalgl"),
    "metallic": ("metallic", "metalness", "metal"),
    "roughness": ("roughness", "rough"),
    "occlusion": ("ambientocclusion", "occlusion", "ao"),
    "mask": ("maskmap", "mask"),
    "emission": ("emission", "emissive"),
    "height": ("height", "displacement", "disp"),
}


def classify_map(name: str):
    """Role of a texture from its file name (last role word wins), or None."""
    stem = pathlib.Path(name).stem.lower().replace("-", "_").replace(" ", "_")
    tokens = [t for t in stem.split("_") if t]
    for token in reversed(tokens):
        for role, words in MAP_SUFFIXES.items():
            if token in words:
                return role
    return None


def rel(ctx, path) -> str:
    return project_relative(path, ctx.config.root)


def absolute(ctx, path) -> pathlib.Path:
    return ctx.config.root / path


# generating -----------------------------------------------------------------------------------
def generating(ctx):
    model = ctx.artifacts.get("model") or {}
    if model.get("complete"):
        return "Model już pobrany"
    gen = ctx.brief["generation"]
    out = ctx.dir / "model"
    out.mkdir(parents=True, exist_ok=True)

    if gen["provider"] == "source":
        source = pathlib.Path(gen["model"])
        files = [shutil.copy2(source, out / source.name)]
        if source.suffix.lower() == ".obj":
            files += [shutil.copy2(m, out / m.name) for m in source.parent.glob(source.stem + "*.mtl")]
        target = out / source.name
        ctx.artifacts["model"] = {"provider": "source", "format": source.suffix.lower()[1:], "files": [rel(ctx, f) for f in files],
                                  "unity": rel(ctx, target), "inspect": rel(ctx, target) if source.suffix.lower() in (".obj", ".glb") else None,
                                  "complete": True}
        return f"Model z pliku {source.name}"

    if gen["provider"] == "blockout":
        result = ctx.providers["blockout"].build(ctx.brief, out, ctx)
        ctx.artifacts["model"] = {"provider": "blockout", "format": "obj", "files": [rel(ctx, f) for f in result["files"]],
                                  "lods": [rel(ctx, f) for f in result["lods"]], "glb": rel(ctx, result["glb"]),
                                  "unity": rel(ctx, result["lods"][0]), "inspect": rel(ctx, result["lods"][0]),
                                  "materials": result["materials"], "triangles": result["triangles"], "conforms": True, "complete": True}
        return f"Okręt z generatora gry ({ctx.brief['shipClass']}, {result.get('type')}): {result['triangles']} trójkątów na LOD"

    meshy = ctx.providers["meshy"]
    meshy.require()
    references = list(ctx.brief["references"])
    if not references and ctx.brief["concept"]["provider"] == "comfyui" and gen["mode"] != "text":
        references = [concept(ctx)]
    texture_now = ctx.brief["texturing"]["provider"] == "generation"
    text = briefs.prompt(ctx.brief, ctx.config)
    budget = ctx.brief["budget"]
    if gen["mode"] == "text" or not references:
        preview = ctx.remote(meshy, "text_to_3d_preview", meshy.text_preview_payload(text, budget["triangles"]),
                             {"prompt": text, "type": "text-to-3d-preview"}, meshy.cost("text_to_3d_preview"))
        final = preview
        if texture_now:
            final = ctx.remote(meshy, "text_to_3d_refine", meshy.text_refine_payload(preview["remote_id"], briefs.texture_prompt(ctx.brief, ctx.config)),
                               {"prompt": text, "type": "text-to-3d-refine"}, meshy.cost("text_to_3d_refine"))
    else:
        kind = "image_to_3d" if len(references) == 1 else "multi_image_to_3d"
        payload = meshy.image_payload(references, budget["triangles"], texture_now, briefs.texture_prompt(ctx.brief, ctx.config))
        final = ctx.remote(meshy, kind, payload, {"texture_prompt": payload.get("texture_prompt", ""), "type": kind.replace("_", "-")}, meshy.cost(kind))
    files = meshy.download(ctx, final, out)
    ctx.artifacts["model"] = {"provider": "meshy", "remote_task": final["remote_id"], "kind": final["kind"], **files, "complete": True}
    return f"Model z Meshy ({final['kind']}, {final['remote_id']})"


def concept(ctx) -> str:
    existing = (ctx.artifacts.get("concept") or {}).get("image")
    if existing:
        return str(absolute(ctx, existing))
    comfy = ctx.providers["comfyui"]
    comfy.require()
    prompt_id = comfy.prompt_id(ctx.job["id"], "concept")
    workflow = comfy.workflow("conceptWorkflow", {"prompt": ctx.brief["description"].strip() + ". " + ctx.config.data.get("templates", {}).get("concept", "") + " Concept art, side view, plain background.",
                                                   "seed": int(ctx.brief.get("seed", 1234)), "filename_prefix": f"mb_{ctx.job['id']}", "negative": ", ".join(ctx.config.data.get("negativeTerms", []))}, prompt_id)
    task = ctx.remote(comfy, "concept", workflow, {"prompt_id": prompt_id}, 0)
    images = comfy.download(ctx, task, ctx.dir / "concept")
    if not images:
        raise NeedsReview("ComfyUI nie zwrócił obrazu konceptu; sprawdź węzeł SaveImage w workflow.")
    ctx.artifacts["concept"] = {"image": rel(ctx, images[0])}
    return str(images[0])


# preparing_geometry ---------------------------------------------------------------------------
def preparing_geometry(ctx):
    from ..validation import geometry
    model = ctx.artifacts["model"]
    brief = ctx.brief
    budget = brief["budget"]
    repair = (ctx.artifacts.get("validation") or {}).get("repair")

    if model.get("conforms") and not repair:
        reports = [geometry.inspect(absolute(ctx, path)) for path in model["lods"]]
        problems = geometry.lod_problems(reports, budget["triangles"])
        if problems:
            raise Fatal("Bryła nie spełnia budżetu: " + "; ".join(problems))
        ctx.artifacts["geometry"] = {"method": "blockout", "lods": model["lods"][:brief["lods"]], "glb": model.get("glb"),
                                     "triangles": [r["triangles"] for r in reports][:brief["lods"]], "forward": "+Z",
                                     "bounds": reports[0]["bounds"]}
        return f"Geometria w budżecie: {', '.join(str(r['triangles']) for r in reports)} trójkątów"

    blender = ctx.providers["blender"]
    if blender.available():
        # glTF keeps units, axes and PBR maps unambiguous; FBX or OBJ only when no GLB came with the model.
        result = blender.prepare(ctx, absolute(ctx, model.get("glb") or model["unity"]), repair)
        ctx.artifacts["geometry"] = {"method": "blender", **result}
        return f"Blender: {len(result['lods'])} LOD, {', '.join(str(t) for t in result['triangles'])} trójkątów"

    inspect_path = model.get("inspect")
    if inspect_path and not repair:
        report = geometry.inspect(absolute(ctx, inspect_path))
        if report["triangles"] <= budget["triangles"] and report["has_uv"]:
            ctx.log("Bez Blendera: model mieści się w budżecie, będzie jeden poziom LOD. Orientację i skalę ustawi Unity.", "warn")
            ctx.artifacts["geometry"] = {"method": "as_is", "lods": [model["unity"]], "glb": model.get("glb"), "triangles": [report["triangles"]],
                                         "forward": brief["generation"]["forward"], "bounds": report["bounds"]}
            return f"Model w budżecie ({report['triangles']} trójkątów), bez LOD"
        why = f"{report['triangles']} trójkątów przy budżecie {budget['triangles']}" if report["triangles"] > budget["triangles"] else "brak UV"
    else:
        why = "naprawa po walidacji" if repair else "format bez inspekcji (FBX)"
    raise NeedsConfiguration(f"Przygotowanie geometrii wymaga Blendera ({why}). Ustaw BLENDER_PATH na blender.exe (4.2 LTS lub nowszy).", ["BLENDER_PATH"])


# texturing ------------------------------------------------------------------------------------
def texturing(ctx):
    tex = ctx.brief["texturing"]
    provider = tex["provider"]
    model = ctx.artifacts["model"]
    out = ctx.dir / "textures"
    if (ctx.artifacts.get("textures") or {}).get("complete"):
        return "Tekstury już są"

    if provider == "flat":
        ctx.artifacts["textures"] = {"provider": "flat", "maps": {}, "materials": model.get("materials", []), "complete": True}
        return "Kolory płaskie z materiałów modelu (bez map)"
    if provider == "generation":
        maps = model.get("maps") or {}
        if not maps.get("base_color"):
            raise NeedsReview("Model z Meshy nie ma mapy koloru; wybierz texturing.provider=meshy_retexture albo provided.")
        ctx.artifacts["textures"] = {"provider": "generation", "maps": maps, "complete": True}
        return f"Mapy z generowania: {', '.join(sorted(maps))}"
    if provider == "provided":
        out.mkdir(parents=True, exist_ok=True)
        maps = {role: rel(ctx, shutil.copy2(path, out / pathlib.Path(path).name)) for role, path in tex["maps"].items()}
        ctx.artifacts["textures"] = {"provider": "provided", "maps": maps, "complete": True}
        return f"Mapy z briefu: {', '.join(sorted(maps))}"
    if provider == "meshy_retexture":
        meshy = ctx.providers["meshy"]
        meshy.require()
        source = (ctx.artifacts.get("geometry") or {}).get("glb") or model.get("glb")
        if not source:
            raise NeedsConfiguration("Retekstura Meshy potrzebuje modelu GLB (Blender albo bryła ją tworzy).", ["BLENDER_PATH"])
        style = briefs.texture_prompt(ctx.brief, ctx.config)
        original_uv = bool(ctx.brief["texturing"].get("originalUv", True))
        task = ctx.remote(meshy, "retexture", meshy.retexture_payload(absolute(ctx, source), style, original_uv),
                          {"text_style_prompt": style, "type": "retexture"}, meshy.cost("retexture"))
        files = meshy.download(ctx, task, out, textures_only=original_uv)
        ctx.artifacts["textures"] = {"provider": "meshy_retexture", "maps": files["maps"], "remote_task": task["remote_id"], "complete": True}
        if original_uv:
            return f"Retekstura Meshy: {', '.join(sorted(files['maps']))}"
        # Meshy's own UVs live only on the model it returns: its LODs come from that model, through Blender.
        returned = files.get("glb") or files.get("unity")
        if not returned:
            raise NeedsReview("Retekstura Meshy z własnym UV nie zwróciła modelu.")
        blender = ctx.providers["blender"]
        if not blender.available():
            raise NeedsConfiguration("Retekstura z UV od Meshy potrzebuje Blendera do LOD.", ["BLENDER_PATH"])
        result = blender.prepare(ctx, absolute(ctx, returned), False)
        ctx.artifacts["geometry"] = {"method": "blender", **result}
        return f"Retekstura Meshy z jej UV: {', '.join(sorted(files['maps']))}; Blender: {len(result['lods'])} LOD, {', '.join(str(t) for t in result['triangles'])} trójkątów"
    if provider == "comfyui":
        comfy = ctx.providers["comfyui"]
        comfy.require()
        prompt_id = comfy.prompt_id(ctx.job["id"], "texture")
        workflow = comfy.workflow("textureWorkflow", {"prompt": briefs.texture_prompt(ctx.brief, ctx.config), "seed": int(ctx.brief.get("seed", 1234)),
                                                       "filename_prefix": f"mb_{ctx.job['id']}_tex", "negative": ", ".join(ctx.config.data.get("negativeTerms", []))}, prompt_id)
        task = ctx.remote(comfy, "texture", workflow, {"prompt_id": prompt_id}, 0)
        images = comfy.download(ctx, task, out)
        maps = {}
        for image in images:
            role = classify_map(image.name) or ("base_color" if not maps else None)
            if role and role not in maps:
                maps[role] = rel(ctx, image)
        if "base_color" not in maps:
            raise NeedsReview("Workflow tekstur ComfyUI nie dał mapy koloru (nazwa pliku z _basecolor).")
        ctx.artifacts["textures"] = {"provider": "comfyui", "maps": maps, "complete": True}
        return f"Tekstury ComfyUI: {', '.join(sorted(maps))}"
    if provider == "substance":
        maps = ctx.providers["substance"].collect(ctx, "texturing", out)
        ctx.artifacts["textures"] = {"provider": "substance", "maps": {r: rel(ctx, p) for r, p in maps.items()}, "complete": True}
        return f"Mapy z Substance: {', '.join(sorted(maps))}"
    raise Fatal(f"Nieznany dostawca tekstur {provider}")


# rigging / animating --------------------------------------------------------------------------
def rigging(ctx):
    provider = ctx.brief["rigging"]["provider"]
    if provider == "none":
        return "Szkielet niepotrzebny dla tej kategorii"
    if (ctx.artifacts.get("rig") or {}).get("complete"):
        return "Szkielet już jest"
    out = ctx.dir / "rig"
    if provider == "meshy":
        meshy = ctx.providers["meshy"]
        meshy.require()
        model = ctx.artifacts["model"]
        payload = meshy.rigging_payload(model, ctx, ctx.brief["rigging"]["heightMeters"])
        task = ctx.remote(meshy, "rigging", payload, {"type": "rig"}, meshy.cost("rigging"))
        files = meshy.download(ctx, task, out, any_model=True)
        ctx.artifacts["rig"] = {"provider": "meshy", "remote_task": task["remote_id"], "files": files["files"], "unity": files.get("unity"), "complete": True}
        return f"Szkielet Meshy ({task['remote_id']})"
    files = ctx.providers["cascadeur"].collect(ctx, "rigging", out, patterns=("*.fbx",))
    ctx.artifacts["rig"] = {"provider": "manual", "files": [rel(ctx, f) for f in files.values()], "unity": rel(ctx, next(iter(files.values()))), "complete": True}
    return "Szkielet z ręcznego eksportu"


def animating(ctx):
    animation = ctx.brief["animation"]
    provider = animation["provider"]
    if provider == "none":
        return "Animacje niepotrzebne dla tej kategorii"
    if (ctx.artifacts.get("animations") or {}).get("complete"):
        return "Animacje już są"
    out = ctx.dir / "animations"
    if provider == "meshy":
        meshy = ctx.providers["meshy"]
        meshy.require()
        rig_task = (ctx.artifacts.get("rig") or {}).get("remote_task")
        if not rig_task:
            raise Fatal("Animacje Meshy wymagają szkieletu z Meshy.")
        clips = []
        for action in animation["actions"]:
            kind = f"animation_{action}"
            task = ctx.remote(meshy, kind, {"rig_task_id": rig_task, "action_id": action}, {"type": "animation", "action_id": action},
                              meshy.cost("animation_per_action"))
            clips += meshy.download(ctx, task, out / str(action), any_model=True)["files"]
        ctx.artifacts["animations"] = {"provider": "meshy", "files": clips, "complete": True}
        return f"Animacje Meshy: {len(animation['actions'])}"
    files = ctx.providers[provider if provider in ("deepmotion", "cascadeur") else "cascadeur"].collect(ctx, "animating", out, patterns=("*.fbx",), many=True)
    ctx.artifacts["animations"] = {"provider": provider, "files": [rel(ctx, f) for f in files.values()], "complete": True}
    return f"Animacje z ręcznego eksportu: {len(files)}"


# importing / validating -----------------------------------------------------------------------
def importing(ctx):
    unity = ctx.providers["unity"]
    request = unity.request(ctx)
    path = ctx.dir / "unity_request.json"
    path.write_text(json.dumps(request, indent=2, ensure_ascii=False), encoding="utf-8")
    result = unity.run(ctx, "Import", path)
    ctx.artifacts["unity"] = result
    return f"Prefab {result['prefab']}"


def validating(ctx):
    from ..validation import report as reports
    unity_result = ctx.artifacts.get("unity") or {}
    if unity_result.get("stale") or not unity_result.get("report"):
        unity = ctx.providers["unity"]
        path = ctx.dir / "unity_request.json"
        if not path.exists():
            raise Fatal("Brak zlecenia Unity; uruchom najpierw import.")
        unity_result = unity.run(ctx, "Validate", path)
        ctx.artifacts["unity"] = unity_result
    data = json.loads((ctx.config.root / unity_result["report"]).read_text(encoding="utf-8"))
    verdict = reports.evaluate(data, ctx.brief, ctx.config)
    ctx.artifacts["validation"] = verdict
    if verdict["passed"]:
        if verdict["warnings"]:
            ctx.log("Ostrzeżenia walidacji: " + "; ".join(verdict["warnings"][:8]), "warn")
        return f"Gotowe: {unity_result['prefab']} ({len(verdict['warnings'])} ostrzeżeń)"
    limits = ctx.config.limits
    if verdict.get("repair") and ctx.job["repairs"] < limits["maxRepairAttempts"] and ctx.providers["blender"].available():
        ctx.store.update(ctx.job["id"], repairs=ctx.job["repairs"] + 1)
        ctx.next_state = "preparing_geometry"
        return "Naprawa geometrii: " + "; ".join(verdict["failures"][:4])
    raise NeedsReview("Walidacja: " + "; ".join(verdict["failures"][:8]) + f". Raport: {unity_result['report']}")


HANDLERS = {
    "generating": generating,
    "preparing_geometry": preparing_geometry,
    "texturing": texturing,
    "rigging": rigging,
    "animating": animating,
    "importing": importing,
    "validating": validating,
}


def estimate_credits(brief: dict, config) -> int:
    """Expected paid credits for the plan in the brief (Meshy price list in the config)."""
    if brief["generation"]["provider"] != "meshy" and brief["texturing"]["provider"] != "meshy_retexture":
        return 0
    costs = config.provider("meshy").get("credits", {})
    total = 0
    gen = brief["generation"]
    if gen["provider"] == "meshy":
        if gen["mode"] == "text" and not brief["references"] and brief["concept"]["provider"] == "none":
            total += costs.get("text_to_3d_preview", 0)
            if brief["texturing"]["provider"] == "generation":
                total += costs.get("text_to_3d_refine", 0)
        else:
            total += costs.get("image_to_3d", 0)
    if brief["texturing"]["provider"] == "meshy_retexture":
        total += costs.get("retexture", 0)
    if brief["rigging"]["provider"] == "meshy":
        total += costs.get("rigging", 0)
    if brief["animation"]["provider"] == "meshy":
        total += costs.get("animation_per_action", 0) * len(brief["animation"]["actions"])
    return total
