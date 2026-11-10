"""ComfyUI server API (routes checked in the ComfyUI server docs on 2026-11-10).

POST /prompt {prompt, client_id, prompt_id} queues a workflow in API format and answers
{prompt_id, number, node_errors}; GET /history/{prompt_id} holds finished outputs; GET /queue lists
running and pending prompts; GET /view?filename&subfolder&type returns an image. The pipeline
chooses the prompt_id itself (uuid5 of job and step), so a lost answer is found by id. Workflows
are the user's own JSON exports ("Save (API)") with {{prompt}}, {{seed}}, {{filename_prefix}}
placeholders. ComfyUI is local and free, so a prompt that never arrived is simply queued again.
Untested against a live server: COMFYUI_URL was not set when this was written.
"""
import json
import pathlib
import urllib.parse
import uuid

from ..core import http
from ..core.errors import Fatal, NeedsConfiguration, Retryable
from .base import Status, capability

NAMESPACE = uuid.UUID("6f1c6c2e-3a54-4d0a-9c3e-8d2f1b7a9e10")


class ComfyUI:
    name = "comfyui"
    paid = False

    def __init__(self, config, transport=None):
        self.config = config
        self.settings = config.provider("comfyui")
        self.env = self.settings.get("urlEnv", "COMFYUI_URL")
        self.http = transport or http
        self.timeout = config.polling.get("requestTimeoutSeconds", 60)

    def capability(self) -> dict:
        workflows = [self.settings.get(k) for k in ("conceptWorkflow", "textureWorkflow")]
        present = [w for w in workflows if w and (self.config.root / w).exists()]
        status = "available" if self.config.is_set(self.env) and present else "needs_configuration"
        return capability("ComfyUI", ["generating (koncept)", "texturing"], status, "dokumentacja tras serwera 2026-11-10; bez próby na żywo",
                          f"Lokalny serwer, workflow użytkownika w formacie API ({len(present)}/2 plików w ArtWorkspace/workflows).",
                          [self.env], self.config)

    def require(self):
        self.base()

    def base(self) -> str:
        url = self.config.environ.get(self.env, "").strip()
        if not url:
            raise NeedsConfiguration(f"Brak {self.env} (adres serwera ComfyUI, np. http://127.0.0.1:8188).", [self.env])
        return url.rstrip("/")

    def prompt_id(self, job_id: str, step: str) -> str:
        return str(uuid.uuid5(NAMESPACE, f"{job_id}:{step}"))

    def workflow(self, key: str, values: dict, prompt_id: str = None) -> dict:
        """The request body for POST /prompt: the user's workflow with placeholders filled in."""
        rel = self.settings.get(key)
        path = self.config.root / rel if rel else None
        if not path or not path.exists():
            raise NeedsConfiguration(f"Brak workflow ComfyUI {rel} (eksport 'Save (API)' z polami {{{{prompt}}}}, {{{{seed}}}}).", [f"comfyui.{key}"])
        workflow = fill(json.loads(path.read_text(encoding="utf-8")), values)
        return {"prompt": workflow, "client_id": "morza-bogow-assets", "prompt_id": prompt_id or values.get("prompt_id") or str(uuid.uuid4())}

    def create(self, kind: str, payload: dict) -> str:
        resp = self.http.request("POST", self.base() + "/prompt", json_body=payload, timeout=self.timeout)
        if resp.status == 400:
            raise Fatal(f"ComfyUI odrzucił workflow: {resp.text()}")
        if not resp.ok:
            raise Retryable(f"ComfyUI {resp.status}: {resp.text()}")
        data = resp.json() or {}
        if data.get("node_errors"):
            raise Fatal("ComfyUI: błędy węzłów: " + json.dumps(data["node_errors"], ensure_ascii=False)[:400])
        return str(data.get("prompt_id") or payload["prompt_id"])

    def status(self, kind: str, prompt_id: str) -> Status:
        resp = self.http.request("GET", self.base() + "/history/" + urllib.parse.quote(prompt_id), timeout=self.timeout)
        if not resp.ok:
            raise Retryable(f"ComfyUI /history {resp.status}")
        entry = (resp.json() or {}).get(prompt_id)
        if entry:
            status = entry.get("status") or {}
            if status.get("status_str") == "error":
                messages = [m for m in status.get("messages", []) if m and m[0] == "execution_error"]
                return Status("failed", 100, entry, json.dumps(messages[-1][1] if messages else status, ensure_ascii=False)[:300])
            if status.get("completed", True):
                return Status("succeeded", 100, entry)
        return Status("running", None, None) if self._queued(prompt_id) else Status("canceled", None, None, "brak w historii i kolejce")

    def _queued(self, prompt_id: str) -> bool:
        resp = self.http.request("GET", self.base() + "/queue", timeout=self.timeout)
        if not resp.ok:
            raise Retryable(f"ComfyUI /queue {resp.status}")
        data = resp.json() or {}
        return any(len(item) > 1 and item[1] == prompt_id for key in ("queue_running", "queue_pending") for item in data.get(key, []))

    def find(self, kind: str, summary: dict, since: float) -> list:
        prompt_id = summary.get("prompt_id")
        if not prompt_id:
            return []
        resp = self.http.request("GET", self.base() + "/history/" + urllib.parse.quote(prompt_id), timeout=self.timeout)
        if resp.ok and (resp.json() or {}).get(prompt_id):
            return [prompt_id]
        return [prompt_id] if self._queued(prompt_id) else []

    def download(self, ctx, task: dict, out: pathlib.Path) -> list:
        from ..core.paths import project_relative
        if task.get("outputs"):
            return [ctx.config.root / p for p in task["outputs"]["files"]]
        out.mkdir(parents=True, exist_ok=True)
        entry = task.get("response") or {}
        files = []
        for node in (entry.get("outputs") or {}).values():
            for image in node.get("images", []):
                if image.get("type", "output") != "output":
                    continue
                query = urllib.parse.urlencode({"filename": image["filename"], "subfolder": image.get("subfolder", ""), "type": "output"})
                path = out / pathlib.Path(image["filename"]).name
                if not path.exists():
                    try:
                        self.http.download(self.base() + "/view?" + query, path, timeout=120)
                    except http.TransportError as error:
                        raise Retryable(f"ComfyUI: pobieranie {image['filename']}: {error}") from None
                files.append(path)
        ctx.store.update_task(task["id"], status="downloaded", outputs={"files": [project_relative(f, ctx.config.root) for f in files]})
        return files


def fill(value, values: dict):
    """Replace {{name}} placeholders in every string of a workflow; a whole-string placeholder keeps the value's type."""
    if isinstance(value, dict):
        return {k: fill(v, values) for k, v in value.items()}
    if isinstance(value, list):
        return [fill(v, values) for v in value]
    if isinstance(value, str):
        for name, replacement in values.items():
            token = "{{" + name + "}}"
            if value == token:
                return replacement
            value = value.replace(token, str(replacement))
        return value
    return value
