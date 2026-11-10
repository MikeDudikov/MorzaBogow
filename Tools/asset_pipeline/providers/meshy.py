"""Meshy REST API (https://api.meshy.ai), checked against docs.meshy.ai on 2026-11-10.

Create calls return {"result": "<task id>"}; tasks report PENDING, IN_PROGRESS, SUCCEEDED, FAILED or
CANCELED with `progress`. List endpoints accept page_num/page_size/sort_by=-created_at and give
`created_at` in Unix milliseconds, which the worker uses to find a task whose create answer was
lost. There is no idempotency key, so the pipeline never re-sends a create on its own.
Untested against the live service: no MESHY_API_KEY was available when this was written.
"""
import json
import pathlib
import time
import urllib.parse

from ..core import http
from ..core.errors import Ambiguous, Fatal, NeedsConfiguration, NeedsReview, RateLimited, Retryable
from .base import Status, capability, data_uri

PATHS = {
    "text_to_3d_preview": "/openapi/v2/text-to-3d",
    "text_to_3d_refine": "/openapi/v2/text-to-3d",
    "image_to_3d": "/openapi/v1/image-to-3d",
    "multi_image_to_3d": "/openapi/v1/multi-image-to-3d",
    "retexture": "/openapi/v1/retexture",
    "remesh": "/openapi/v1/remesh",
    "rigging": "/openapi/v1/rigging",
    "animation": "/openapi/v1/animations",
}
TYPES = {"text_to_3d_preview": "text-to-3d-preview", "text_to_3d_refine": "text-to-3d-refine"}
STATES = {"PENDING": "running", "IN_PROGRESS": "running", "SUCCEEDED": "succeeded", "FAILED": "failed", "CANCELED": "canceled"}
MAP_KEYS = {"base_color": "BaseColor", "metallic": "Metallic", "normal": "Normal", "roughness": "Roughness", "emission": "Emission"}
MATCH_KEYS = ("prompt", "text_style_prompt", "texture_prompt", "type")


def _base_kind(kind: str) -> str:
    return "animation" if kind.startswith("animation_") else kind


class Meshy:
    name = "meshy"
    paid = True

    def __init__(self, config, transport=None):
        self.config = config
        self.settings = config.provider("meshy")
        self.base = self.settings.get("baseUrl", "https://api.meshy.ai").rstrip("/")
        self.env = self.settings.get("apiKeyEnv", "MESHY_API_KEY")
        self.http = transport or http
        self.timeout = config.polling.get("requestTimeoutSeconds", 60)

    def capability(self) -> dict:
        configured = self.config.is_set(self.env)
        return capability("Meshy API", ["generating", "texturing", "rigging", "animating"],
                          "available" if configured else "needs_configuration",
                          "dokumentacja 2026-11-10; bez próby na żywo",
                          "Text/Image/Multi-image to 3D, retekstura, remesh, szkielet i animacje. Płatne kredytami: każde zadanie wymaga zatwierdzenia kosztu.",
                          [self.env], self.config)

    def require(self):
        self.config.secret(self.env, "klucz API Meshy")

    def cost(self, kind: str) -> int:
        return int(self.settings.get("credits", {}).get(kind, 0))

    def _headers(self) -> dict:
        return {"Authorization": "Bearer " + self.config.secret(self.env, "klucz API Meshy")}

    def _url(self, kind: str, suffix="") -> str:
        return self.base + PATHS[_base_kind(kind)] + suffix

    # Requests --------------------------------------------------------------------------------
    def _raise_for(self, resp, creating: bool):
        status, text = resp.status, resp.text()
        if status == 429:
            if "NoMorePendingTasks" in text:
                raise RateLimited("Meshy: pełna kolejka równoległych zadań (NoMorePendingTasks).", retry_after=60)
            raise RateLimited("Meshy: limit zapytań (429).", retry_after=http.parse_retry_after(resp.headers.get("retry-after")))
        if status == 402:
            raise NeedsConfiguration("Meshy: brak kredytów albo plan Free bez API (402).", ["Meshy: kredyty lub plan"])
        if status in (401, 403):
            raise NeedsConfiguration(f"Meshy odrzucił klucz ({status}). Sprawdź {self.env}.", [self.env])
        if status == 404 and not creating:
            raise NeedsReview(f"Meshy: zadania nie ma (404); mogło wygasnąć. {text}")
        if 400 <= status < 500:
            raise Fatal(f"Meshy {status}: {text}")
        if creating:
            raise Ambiguous(f"Meshy {status} przy tworzeniu zadania.")
        raise Retryable(f"Meshy {status}: {text}")

    def create(self, kind: str, payload: dict) -> str:
        resp = self.http.request("POST", self._url(kind), headers=self._headers(), json_body=payload, timeout=self.timeout)
        if not resp.ok:
            self._raise_for(resp, creating=True)
        data = resp.json() or {}
        task_id = data.get("result") if isinstance(data, dict) else None
        if not task_id:
            raise Ambiguous("Meshy przyjął żądanie bez identyfikatora zadania.")
        return str(task_id)

    def status(self, kind: str, remote_id: str) -> Status:
        resp = self.http.request("GET", self._url(kind, "/" + urllib.parse.quote(remote_id)), headers=self._headers(), timeout=self.timeout)
        if not resp.ok:
            self._raise_for(resp, creating=False)
        data = resp.json() or {}
        state = STATES.get(str(data.get("status", "")).upper())
        if state is None:
            raise Retryable(f"Meshy: nieznany status {data.get('status')!r}")
        error = (data.get("task_error") or {}).get("message") if isinstance(data.get("task_error"), dict) else data.get("task_error")
        return Status(state, data.get("progress"), data, error or None, data.get("consumed_credits"))

    def find(self, kind: str, summary: dict, since: float) -> list:
        """Tasks on the provider's list that match a create whose answer was lost."""
        query = urllib.parse.urlencode({"page_num": 1, "page_size": 50, "sort_by": "-created_at"})
        resp = self.http.request("GET", self._url(kind, "?" + query), headers=self._headers(), timeout=self.timeout)
        if not resp.ok:
            self._raise_for(resp, creating=False)
        data = resp.json()
        items = data if isinstance(data, list) else (data or {}).get("result") or (data or {}).get("data") or []
        wanted_type = summary.get("type") or TYPES.get(kind)
        found = []
        for item in items:
            created = (item.get("created_at") or 0) / 1000.0
            if created < since - 120:
                continue
            if wanted_type and item.get("type") and item.get("type") != wanted_type:
                continue
            if any(key in summary and key != "type" and (item.get(key) or "") != (summary.get(key) or "") for key in MATCH_KEYS if key in item):
                continue
            if "action_id" in summary and item.get("action_id") not in (None, summary["action_id"]):
                continue
            found.append(str(item.get("id")))
        return found

    # Payloads --------------------------------------------------------------------------------
    def _formats(self):
        formats = list(self.settings.get("targetFormats", ["glb", "fbx"]))
        return formats if "glb" in formats else formats + ["glb"]

    def _polycount(self, triangles: int) -> int:
        return max(100, min(300000, int(triangles * 0.9)))

    def text_preview_payload(self, prompt: str, triangles: int) -> dict:
        return {"mode": "preview", "prompt": prompt, "ai_model": self.settings.get("aiModel", "latest"), "topology": "triangle",
                "target_polycount": self._polycount(triangles), "should_remesh": True, "target_formats": self._formats()}

    def text_refine_payload(self, preview_id: str, texture_prompt: str) -> dict:
        return {"mode": "refine", "preview_task_id": preview_id, "enable_pbr": True, "texture_prompt": texture_prompt,
                "ai_model": self.settings.get("aiModel", "latest"), "target_formats": self._formats()}

    def image_payload(self, references, triangles: int, texture: bool, texture_prompt: str) -> dict:
        images = [data_uri(p, limit=20 * 1024 * 1024) for p in references]
        payload = {"ai_model": self.settings.get("aiModel", "latest"), "topology": "triangle", "target_polycount": self._polycount(triangles),
                   "should_remesh": True, "should_texture": texture, "enable_pbr": texture, "target_formats": self._formats(), "origin_at": "bottom"}
        if len(images) == 1:
            payload["image_url"] = images[0]
        else:
            payload["image_urls"] = images[:4]
        return payload

    def retexture_payload(self, model_path, style: str) -> dict:
        return {"model_url": data_uri(model_path, "application/octet-stream"), "text_style_prompt": style, "enable_original_uv": True,
                "enable_pbr": True, "ai_model": self.settings.get("aiModel", "latest"), "target_formats": ["glb"], "remove_lighting": True}

    def rigging_payload(self, model: dict, ctx, height: float) -> dict:
        if model.get("provider") == "meshy" and model.get("remote_task"):
            return {"input_task_id": model["remote_task"], "height_meters": height}
        glb = model.get("glb")
        if not glb:
            raise Fatal("Szkielet Meshy potrzebuje modelu GLB albo zadania Meshy.")
        return {"model_url": data_uri(ctx.config.root / glb, "application/octet-stream"), "height_meters": height}

    # Results ---------------------------------------------------------------------------------
    def download(self, ctx, task: dict, out: pathlib.Path, textures_only=False, any_model=False) -> dict:
        """Fetch the files of a finished task once; a repeated call returns what is already on disk."""
        from ..core.paths import project_relative
        if task.get("outputs"):
            return task["outputs"]
        data = task.get("response") or {}
        expires = (data.get("expires_at") or 0) / 1000.0
        if expires and expires < time.time():
            raise NeedsReview(f"Meshy: wyniki zadania {task['remote_id']} wygasły. Nowe zadanie (koszt): ap resume {ctx.job['id']} --resubmit")
        out.mkdir(parents=True, exist_ok=True)
        limit = ctx.config.limits.get("maxDownloadBytes", 200 * 1024 * 1024)
        stem = ctx.brief["id"]
        result = {"files": [], "maps": {}}

        def fetch(url, name):
            path = out / name
            if not path.exists():
                try:
                    self.http.download(url, path, timeout=180, max_bytes=limit)
                except http.TransportError as error:
                    raise Retryable(f"Meshy: pobieranie {name}: {error}") from None
                ctx.keep_alive()
            rel = project_relative(path, ctx.config.root)
            result["files"].append(rel)
            return rel

        if not textures_only:
            urls = data.get("model_urls") or {}
            for fmt in ("fbx", "obj", "mtl", "glb"):
                if urls.get(fmt):
                    result[fmt] = fetch(urls[fmt], f"{stem}.{fmt}")
            result["unity"] = result.get("fbx") or result.get("obj")
            result["inspect"] = result.get("glb") or result.get("obj")
            result["glb"] = result.get("glb")
            result["format"] = "fbx" if result.get("fbx") else ("obj" if result.get("obj") else "glb")
        textures = data.get("texture_urls") or []
        first = textures[0] if isinstance(textures, list) and textures else (textures if isinstance(textures, dict) else {})
        for key, role in MAP_KEYS.items():
            url = first.get(key) if isinstance(first, dict) else None
            if url:
                ext = pathlib.PurePosixPath(urllib.parse.urlsplit(url).path).suffix or ".png"
                result["maps"][key] = fetch(url, f"{stem}_{role}{ext}")
        if any_model:
            for url in _model_links(data):
                name = pathlib.PurePosixPath(urllib.parse.urlsplit(url).path).name or f"{stem}.fbx"
                rel = fetch(url, name)
                if rel.lower().endswith(".fbx") and not result.get("unity"):
                    result["unity"] = rel
        ctx.store.update_task(task["id"], status="downloaded", outputs=result)
        return result


def _model_links(data) -> list:
    """Download links to FBX/GLB files anywhere in a task object (rigging and animation results)."""
    links = []

    def walk(value):
        if isinstance(value, dict):
            for item in value.values():
                walk(item)
        elif isinstance(value, list):
            for item in value:
                walk(item)
        elif isinstance(value, str) and value.startswith("http"):
            path = urllib.parse.urlsplit(value).path.lower()
            if path.endswith((".fbx", ".glb")) and value not in links:
                links.append(value)

    walk({k: v for k, v in data.items() if k not in ("thumbnail_url", "texture_urls")})
    return links


def describe(task: dict) -> str:
    return json.dumps({k: task.get(k) for k in ("kind", "status", "remote_id", "progress")}, ensure_ascii=False)
