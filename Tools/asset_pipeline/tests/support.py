"""Test helpers: a fake clock, a throw-away project root, and local HTTP stand-ins for Meshy and
ComfyUI. The stand-ins exist only for these integration tests; production code never uses them."""
import http.server
import json
import pathlib
import shutil
import tempfile
import threading
import urllib.parse

from asset_pipeline.core.config import Config
from asset_pipeline.core.store import Store


def write_ship_export(out, asset_id, ship_class="Ram"):
    """Stand-in for Unity's ship export (tests only): three OBJ levels of a simple hull and sail, the MTL and the manifest."""
    out = pathlib.Path(out)
    out.mkdir(parents=True, exist_ok=True)
    files, triangles = [], []
    for level, segments in enumerate((8, 4, 2)):
        lines = [f"mtllib {asset_id}.mtl", f"o {asset_id}_LOD{level}"]
        faces = ["usemtl pitch"]
        count = 0
        for i in range(segments + 1):
            z = -10 + 20 * i / segments
            for x, y in ((-2.8, 2.0), (0.0, -1.2), (2.8, 2.0)):
                lines += [f"v {x} {y} {z}", f"vt {i / segments} {0.5 + y / 4}", f"vn {x / 2.8} {0.5 if y > 0 else -1} 0"]
        for i in range(segments):
            for k in range(2):
                a, b, c, d = i * 3 + k + 1, (i + 1) * 3 + k + 1, (i + 1) * 3 + k + 2, i * 3 + k + 2
                faces += [f"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}", f"f {a}/{a}/{a} {c}/{c}/{c} {d}/{d}/{d}"]
                count += 2
        base = (segments + 1) * 3
        lines += [f"v -4 3 1", "vt 0 0", "vn 0 0 1", f"v 4 3 1", "vt 1 0", "vn 0 0 1", f"v 4 9 1", "vt 1 1", "vn 0 0 1", f"v -4 9 1", "vt 0 1", "vn 0 0 1"]
        faces += ["usemtl sail", f"f {base + 1}/{base + 1}/{base + 1} {base + 2}/{base + 2}/{base + 2} {base + 3}/{base + 3}/{base + 3}",
                  f"f {base + 1}/{base + 1}/{base + 1} {base + 3}/{base + 3}/{base + 3} {base + 4}/{base + 4}/{base + 4}"]
        count += 2
        name = f"{asset_id}_LOD{level}.obj"
        (out / name).write_text("\n".join(lines + faces) + "\n", encoding="utf-8")
        files.append(name)
        triangles.append(count)
    (out / f"{asset_id}.mtl").write_text("newmtl pitch\nKd 0.07 0.06 0.05\nnewmtl sail\nKd 0.89 0.85 0.75\n", encoding="utf-8")
    manifest = {"id": asset_id, "shipClass": ship_class, "type": "triakontoros", "lods": files, "triangles": triangles,
                "materials": [{"name": "pitch", "baseColor": [0.075, 0.062, 0.055], "metallic": 0, "smoothness": 0.35},
                              {"name": "sail", "baseColor": [0.89, 0.85, 0.75], "metallic": 0, "smoothness": 0.15}]}
    (out / f"{asset_id}.json").write_text(json.dumps(manifest), encoding="utf-8")
    return manifest


class Clock:
    def __init__(self, start=1_800_000_000.0):
        self.t = start

    def __call__(self):
        return self.t

    def sleep(self, seconds):
        self.t += max(seconds, 0.001)


class Sandbox:
    """A temporary project root with its own workspace and database."""

    def __init__(self, overrides=None, environ=None):
        self.dir = pathlib.Path(tempfile.mkdtemp(prefix="ap-test-"))
        self.clock = Clock()
        self.config = Config.load(root=self.dir, overrides=overrides or {}, environ=environ or {})
        self.store = Store(self.config.database, now=self.clock)

    def close(self):
        self.store.close()
        shutil.rmtree(self.dir, ignore_errors=True)


class _Server:
    def __init__(self, handler_cls):
        self.httpd = http.server.ThreadingHTTPServer(("127.0.0.1", 0), handler_cls)
        self.httpd.owner = self
        self.url = f"http://127.0.0.1:{self.httpd.server_address[1]}"
        self.thread = threading.Thread(target=self.httpd.serve_forever, daemon=True)
        self.thread.start()

    def close(self):
        self.httpd.shutdown()
        self.httpd.server_close()


class _Handler(http.server.BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def reply(self, status, body, headers=None):
        data = body if isinstance(body, bytes) else json.dumps(body).encode()
        self.send_response(status)
        for key, value in (headers or {}).items():
            self.send_header(key, value)
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)


class MockMeshy(_Server):
    """Meshy-like API: tasks finish after `polls_to_finish` GETs; files are served from /files/."""

    def __init__(self, clock, polls_to_finish=2):
        self.clock = clock
        self.polls_to_finish = polls_to_finish
        self.tasks = {}
        self.posts = []
        self.fail_next_post = None  # (status, body, headers)
        self.hide_from_list = False
        self.authorizations = set()
        super().__init__(self._handler())

    def _handler(self):
        owner = self

        class Handler(_Handler):
            def do_POST(self):
                owner.authorizations.add(self.headers.get("Authorization", ""))
                body = json.loads(self.rfile.read(int(self.headers["Content-Length"])) or b"{}")
                path = urllib.parse.urlsplit(self.path).path
                if owner.fail_next_post:
                    status, payload, headers = owner.fail_next_post
                    owner.fail_next_post = None
                    return self.reply(status, payload, headers)
                owner.posts.append((path, body))
                task_id = f"task-{len(owner.posts)}"
                kind = {"preview": "text-to-3d-preview", "refine": "text-to-3d-refine"}.get(body.get("mode"), path.rsplit("/", 1)[-1])
                owner.tasks[task_id] = {"id": task_id, "type": kind, "path": path, "prompt": body.get("prompt", ""),
                                        "text_style_prompt": body.get("text_style_prompt", ""),
                                        "created_at": int(owner.clock() * 1000), "polls": 0}
                self.reply(202, {"result": task_id})

            def do_GET(self):
                parts = urllib.parse.urlsplit(self.path)
                if parts.path.startswith("/files/"):
                    return self.reply(200, b"FILE:" + parts.path.encode())
                last = parts.path.rsplit("/", 1)[-1]
                if last in owner.tasks:
                    task = owner.tasks[last]
                    task["polls"] += 1
                    done = task["polls"] >= owner.polls_to_finish
                    body = {"id": last, "type": task["type"], "status": "SUCCEEDED" if done else "IN_PROGRESS",
                            "progress": 100 if done else 40, "consumed_credits": 20, "expires_at": 0}
                    if done:
                        body["model_urls"] = {fmt: f"{owner.url}/files/{last}.{fmt}" for fmt in ("glb", "fbx")}
                        body["texture_urls"] = [{k: f"{owner.url}/files/{last}_{k}.png?X-Amz-Signature=secret" for k in ("base_color", "normal", "roughness", "metallic")}]
                    return self.reply(200, body)
                if parts.query:
                    items = [] if owner.hide_from_list else sorted(
                        ({k: v for k, v in t.items() if k not in ("path", "polls")} for t in owner.tasks.values()),
                        key=lambda t: -t["created_at"])
                    return self.reply(200, items)
                self.reply(404, {"message": "not found"})

        return Handler


class MockComfy(_Server):
    """ComfyUI-like server: a prompt is queued, then appears in history with one output image."""

    def __init__(self):
        self.prompts = {}
        self.polls = 0
        super().__init__(self._handler())

    def _handler(self):
        owner = self

        class Handler(_Handler):
            def do_POST(self):
                body = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
                owner.prompts[body["prompt_id"]] = body
                self.reply(200, {"prompt_id": body["prompt_id"], "number": len(owner.prompts), "node_errors": {}})

            def do_GET(self):
                parts = urllib.parse.urlsplit(self.path)
                if parts.path == "/queue":
                    return self.reply(200, {"queue_running": [], "queue_pending": [[0, pid] for pid in owner.prompts if owner.polls < 1]})
                if parts.path.startswith("/history/"):
                    pid = parts.path.rsplit("/", 1)[-1]
                    owner.polls += 1
                    if pid in owner.prompts and owner.polls > 1:
                        return self.reply(200, {pid: {"status": {"status_str": "success", "completed": True},
                                                      "outputs": {"9": {"images": [{"filename": "concept_00001_.png", "subfolder": "", "type": "output"}]}}}})
                    return self.reply(200, {})
                if parts.path == "/view":
                    return self.reply(200, b"\x89PNG\r\n\x1a\nfake")
                self.reply(404, {})

        return Handler
