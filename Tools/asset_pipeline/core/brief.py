"""Asset brief: what a person asks for, validated and filled with project defaults.

A brief is JSON (schema "morza-bogow/asset-brief@1"). Only `id`, `category` and `description`
are required; ships also need `shipClass`, which fixes their size from the simulation's catalogue.
"""
import hashlib
import json
import pathlib
import re

from .config import CATEGORIES, RIGGED, Config

SCHEMA = "morza-bogow/asset-brief@1"
ID = re.compile(r"^[a-z][a-z0-9_]{2,47}$")

GENERATION = ("meshy", "blockout", "source")
MODES = ("text", "image", "multi_image")
CONCEPT = ("none", "comfyui")
TEXTURING = ("generation", "meshy_retexture", "comfyui", "substance", "provided", "flat")
RIGGING = ("none", "meshy", "manual")
ANIMATION = ("none", "meshy", "deepmotion", "cascadeur", "manual")
FORWARD = ("auto", "+Z", "-Z", "+X", "-X")
MAP_ROLES = ("base_color", "normal", "metallic", "roughness", "occlusion", "mask", "emission", "height")

PROMPT_LIMIT = 800

CATEGORY_HINT = {
    "ship": "an ancient Greek wooden warship",
    "ship_equipment": "a piece of ancient Greek ship equipment",
    "building": "an ancient Greek harbour building",
    "ruin": "weathered ancient Greek ruins",
    "rock": "a sun-bleached Aegean limestone rock",
    "vegetation": "Mediterranean vegetation",
    "character": "an ancient Greek sailor or warrior",
    "creature": "a creature from Greek myth",
    "material": "a tileable surface material",
    "effect": "a stylised visual effect element",
}


class BriefError(ValueError):
    def __init__(self, problems):
        super().__init__("; ".join(problems))
        self.problems = list(problems)


def _choice(section: dict, key: str, allowed, default, problems, where):
    value = section.get(key, default)
    if value not in allowed:
        problems.append(f"{where}.{key}: '{value}' spoza {list(allowed)}")
    return value


def normalise(raw: dict, config: Config, base_dir=None) -> dict:
    """A complete, validated brief. Raises BriefError listing every problem at once."""
    problems = []
    if not isinstance(raw, dict):
        raise BriefError(["brief musi być obiektem JSON"])
    brief = json.loads(json.dumps(raw))
    if brief.get("schema", SCHEMA) != SCHEMA:
        problems.append(f"schema: oczekiwano {SCHEMA}")
    brief["schema"] = SCHEMA

    asset_id = brief.get("id", "")
    if not isinstance(asset_id, str) or not ID.match(asset_id):
        problems.append("id: małe litery, cyfry i _, 3–48 znaków, zaczyna się literą")
    category = brief.get("category")
    if category not in CATEGORIES:
        problems.append(f"category: jedna z {list(CATEGORIES)}")
    description = brief.get("description", "")
    if not isinstance(description, str) or len(description.strip()) < 10:
        problems.append("description: co najmniej 10 znaków opisu")
    brief.setdefault("name", asset_id)

    if category == "ship":
        ship_class = brief.get("shipClass")
        if ship_class not in config.ship_classes:
            problems.append(f"shipClass: jedna z {config.ship_classes}")
        else:
            ship = config.ship(ship_class)
            given = brief.get("dimensions") or {}
            for key, expected in (("length", ship["length"]), ("width", ship["beam"])):
                if key in given and abs(float(given[key]) - expected) > 1e-6:
                    problems.append(f"dimensions.{key}: okręt {ship_class} ma {expected} m z katalogu symulacji")
            brief["dimensions"] = {"length": ship["length"], "width": ship["beam"], "draft": ship["draft"]}
    elif "shipClass" in brief:
        problems.append("shipClass: tylko dla kategorii ship")

    dims = brief.get("dimensions") or {}
    for key, value in dims.items():
        if key not in ("length", "width", "height", "draft") or not isinstance(value, (int, float)) or value <= 0:
            problems.append(f"dimensions.{key}: dodatnia liczba metrów (length, width, height, draft)")
    brief["dimensions"] = dims

    references = brief.get("references") or []
    base = pathlib.Path(base_dir) if base_dir else config.root
    resolved = []
    for ref in references:
        path = (base / ref) if not pathlib.Path(ref).is_absolute() else pathlib.Path(ref)
        if path.suffix.lower() not in (".png", ".jpg", ".jpeg", ".webp"):
            problems.append(f"references: {ref} nie jest obrazem PNG/JPG/WebP")
        elif not path.exists():
            problems.append(f"references: brak pliku {ref}")
        else:
            resolved.append(str(path.resolve()))
    brief["references"] = resolved

    generation = dict(brief.get("generation") or {})
    default_gen = "meshy"
    gen = _choice(generation, "provider", GENERATION, default_gen, problems, "generation")
    mode_default = "multi_image" if len(resolved) > 1 else ("image" if resolved else "text")
    mode = _choice(generation, "mode", MODES, mode_default, problems, "generation")
    if mode != "text" and not resolved and gen == "meshy" and (brief.get("concept") or {}).get("provider", "none") == "none":
        problems.append("generation.mode: image wymaga references albo concept.provider=comfyui")
    forward = _choice(generation, "forward", FORWARD, "auto" if gen == "meshy" else "+Z", problems, "generation")
    if gen == "source":
        source = generation.get("model")
        if not source:
            problems.append("generation.model: ścieżka modelu (FBX, OBJ albo GLB) dla provider=source")
        else:
            path = (base / source) if not pathlib.Path(source).is_absolute() else pathlib.Path(source)
            if path.suffix.lower() not in (".fbx", ".obj", ".glb"):
                problems.append("generation.model: obsługiwane FBX, OBJ, GLB")
            elif not path.exists():
                problems.append(f"generation.model: brak pliku {source}")
            else:
                generation["model"] = str(path.resolve())
    if gen == "blockout" and category != "ship":
        problems.append("generation.provider=blockout: tylko kategoria ship (bryła z wymiarów klasy)")
    generation.update(provider=gen, mode=mode, forward=forward)
    brief["generation"] = generation

    concept = dict(brief.get("concept") or {})
    concept["provider"] = _choice(concept, "provider", CONCEPT, "none", problems, "concept")
    brief["concept"] = concept

    budget = config.budget(category) if category in CATEGORIES else {}
    texturing = dict(brief.get("texturing") or {})
    tex_default = {"meshy": "generation", "blockout": "flat", "source": "provided" if texturing.get("maps") else "flat"}[gen] if gen in GENERATION else "flat"
    tex = _choice(texturing, "provider", TEXTURING, tex_default, problems, "texturing")
    if tex == "generation" and gen != "meshy":
        problems.append("texturing.provider=generation: tylko z generation.provider=meshy")
    size = int(texturing.get("size", budget.get("textureSize", 1024)))
    if size not in (256, 512, 1024, 2048, 4096):
        problems.append("texturing.size: 256, 512, 1024, 2048 albo 4096")
    elif budget and size > budget["textureSize"]:
        problems.append(f"texturing.size: {size} przekracza budżet kategorii {budget['textureSize']}")
    maps = texturing.get("maps") or {}
    for role, ref in maps.items():
        if role not in MAP_ROLES:
            problems.append(f"texturing.maps.{role}: rola spoza {list(MAP_ROLES)}")
            continue
        path = (base / ref) if not pathlib.Path(ref).is_absolute() else pathlib.Path(ref)
        if not path.exists():
            problems.append(f"texturing.maps.{role}: brak pliku {ref}")
        else:
            maps[role] = str(path.resolve())
    if tex == "provided" and not maps:
        problems.append("texturing.provider=provided: podaj texturing.maps")
    tint = texturing.get("tint")
    if tint is not None and (not isinstance(tint, list) or len(tint) != 3
                             or not all(isinstance(v, (int, float)) and 0 <= v <= 1 for v in tint)):
        problems.append("texturing.tint: trzy liczby 0..1 (mnożnik koloru mapy bazowej)")
    texturing.update(provider=tex, size=size, maps=maps)
    brief["texturing"] = texturing

    rigging = dict(brief.get("rigging") or {})
    rig_default = "meshy" if category in RIGGED and gen == "meshy" else ("manual" if category in RIGGED else "none")
    rigging["provider"] = _choice(rigging, "provider", RIGGING, rig_default, problems, "rigging")
    if rigging["provider"] != "none" and category not in RIGGED:
        problems.append(f"rigging: kategoria {category} nie ma szkieletu")
    if rigging["provider"] == "meshy":
        height = rigging.get("heightMeters", dims.get("height"))
        if not height:
            problems.append("rigging.heightMeters: wzrost postaci w metrach dla Meshy")
        rigging["heightMeters"] = height
    brief["rigging"] = rigging

    animation = dict(brief.get("animation") or {})
    animation["provider"] = _choice(animation, "provider", ANIMATION, "none", problems, "animation")
    animation["actions"] = list(animation.get("actions") or [])
    if animation["provider"] != "none" and rigging["provider"] == "none":
        problems.append("animation: wymaga szkieletu (rigging.provider)")
    if animation["provider"] == "meshy" and not all(isinstance(a, int) for a in animation["actions"]):
        problems.append("animation.actions: identyfikatory akcji Meshy (liczby) z biblioteki animacji")
    brief["animation"] = animation

    overrides = brief.get("budget") or {}
    final_budget = dict(budget)
    for key, value in overrides.items():
        if key not in budget:
            problems.append(f"budget.{key}: nieznany klucz")
        elif not isinstance(value, int) or value <= 0:
            problems.append(f"budget.{key}: dodatnia liczba całkowita")
        elif value > budget[key]:
            problems.append(f"budget.{key}: {value} ponad budżet kategorii {budget[key]} (tylko mniej)")
        else:
            final_budget[key] = value
    brief["budget"] = final_budget

    lods = int(brief.get("lods", 3 if category in ("ship", "building", "ruin", "rock", "creature") else 2))
    if not 1 <= lods <= config.max_lods:
        problems.append(f"lods: 1–{config.max_lods}")
    brief["lods"] = lods

    variants = int(brief.get("variants", 1))
    if not 1 <= variants <= config.limits["maxVariants"]:
        problems.append(f"variants: 1–{config.limits['maxVariants']}")
    brief["variants"] = variants

    team = brief.get("teamColour", ["sail", "zagiel", "teamcolor"] if category == "ship" else [])
    brief["teamColour"] = [str(t).lower() for t in team]

    if problems:
        raise BriefError(problems)
    return brief


def digest(brief: dict) -> str:
    return hashlib.sha256(json.dumps(brief, sort_keys=True, ensure_ascii=False).encode("utf-8")).hexdigest()


def prompt(brief: dict, config: Config) -> str:
    """The text prompt for 3D generation: subject, size, description, house style. At most 800 characters."""
    parts = [CATEGORY_HINT.get(brief["category"], "an asset")]
    dims = brief.get("dimensions") or {}
    if brief["category"] == "ship":
        ship = config.ship(brief["shipClass"])
        parts.append(f"about {ship['length']:g} m long and {ship['beam']:g} m wide, low hull, single mast with a square sail")
        if ship.get("ram"):
            parts.append("bronze ram at the bow waterline")
        if ship.get("slots"):
            parts.append(f"{len(ship['slots'])} deck weapon posts")
    elif dims:
        parts.append(", ".join(f"{k} {v:g} m" for k, v in dims.items()))
    parts.append(brief["description"].strip())
    if brief.get("style"):
        parts.append(str(brief["style"]).strip())
    parts.append("game asset, clean readable silhouette, no base, no ground plane")
    text = ". ".join(p.rstrip(". ") for p in parts if p)
    direction = (config.data.get("templates", {}).get("model") or config.visual_direction).strip()
    if direction and len(text) + len(direction) + 2 <= PROMPT_LIMIT:
        text = f"{text}. {direction}"
    return text[:PROMPT_LIMIT]


def texture_prompt(brief: dict, config: Config) -> str:
    """The brief's texture prompt or description and material style. An optional configured finish is appended once;
    the default finish is empty, so the brief decides the surface condition. Keep the provider prompt within 800 chars."""
    templates = config.data.get("templates", {})
    style = templates.get("texture", "Physically based materials under neutral lighting, no baked shadows").strip()
    weathering = (templates.get("weathering") or "").strip()
    own = (brief.get("texturing", {}).get("prompt") or "").strip()
    head = own or brief["description"].strip()
    tail = [] if own else [style]
    if weathering and weathering.lower() not in head.lower():
        tail.append(weathering)
    ending = ". ".join(part.rstrip(". ") for part in tail)
    if not ending:
        return head[:PROMPT_LIMIT]
    room = PROMPT_LIMIT - len(ending) - 2
    head = head[:max(0, room)].rstrip(" .,;")
    return f"{head}. {ending}"[:PROMPT_LIMIT] if head else ending[:PROMPT_LIMIT]


def template(category: str, ship_class=None) -> dict:
    """A starting brief for the CLI and MCP."""
    brief = {
        "schema": SCHEMA,
        "id": f"{category}_example",
        "name": "Przykład",
        "category": category,
        "description": "Describe the asset in a few sentences: shape, materials, colours, details that matter at a distance.",
        "style": "",
        "references": [],
        "generation": {"provider": "meshy", "mode": "text"},
        "concept": {"provider": "none"},
        "texturing": {"provider": "generation"},
        "lods": 3,
        "variants": 1,
    }
    if category == "ship":
        brief["shipClass"] = ship_class or "Ram"
        brief["id"] = f"ship_{(ship_class or 'Ram').lower()}"
    if category in RIGGED:
        brief["rigging"] = {"provider": "meshy", "heightMeters": 1.8}
        brief["animation"] = {"provider": "none", "actions": []}
    return brief
