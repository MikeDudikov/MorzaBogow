"""MCP server over stdio (JSON-RPC 2.0, one message per line), standard library only.

Tools create and inspect jobs, start stages and run a detached worker. There is deliberately no
tool that approves credits: spending money is a person's decision, made with `ap approve` in a
terminal. Logs go to stderr; stdout carries protocol messages only.
"""
import json
import sys
import traceback

from .. import __version__
from ..core import brief as briefs
from ..core import service
from ..core.brief import BriefError
from ..core.config import CATEGORIES
from ..core.stages import MANUAL, STATES
from ..providers import capabilities

PROTOCOL_VERSIONS = ("2025-06-18", "2025-03-26", "2024-11-05")

INSTRUCTIONS = (
    "Potok assetów Morza Bogów. Brief -> model -> geometria -> tekstury -> szkielet -> animacje -> Unity (HDRP, prefab, LOD) -> walidacja. "
    "Płatne kroki (Meshy) czekają na zatwierdzenie kosztu przez człowieka w terminalu (ap approve); nie proś o obejście. "
    "Po utworzeniu zadania uruchom assets_run, potem sprawdzaj assets_job_status."
)

JOB = {"type": "string", "description": "Identyfikator zadania albo jego początek"}

TOOLS = [
    {"name": "assets_capabilities", "description": "Macierz możliwości: usługi, etapy, stan konfiguracji (zmienne: ustawiona/brak, bez wartości).",
     "inputSchema": {"type": "object", "properties": {}}},
    {"name": "assets_brief_template", "description": "Szablon briefu assetu dla kategorii.",
     "inputSchema": {"type": "object", "properties": {"category": {"type": "string", "enum": list(CATEGORIES)},
                                                      "shipClass": {"type": "string"}}, "required": ["category"]}},
    {"name": "assets_create_job", "description": "Waliduje brief i dodaje zadania do kolejki. Zwraca szacunek kredytów; płatne kroki czekają na zatwierdzenie przez człowieka.",
     "inputSchema": {"type": "object", "properties": {"brief": {"type": "object", "description": "Brief morza-bogow/asset-brief@1"}}, "required": ["brief"]}},
    {"name": "assets_list_jobs", "description": "Lista zadań, opcjonalnie w danym stanie.",
     "inputSchema": {"type": "object", "properties": {"state": {"type": "string", "enum": list(STATES)}}}},
    {"name": "assets_job_status", "description": "Stan zadania, zadania zdalne, artefakty i ostatnie zdarzenia.",
     "inputSchema": {"type": "object", "properties": {"job": JOB}, "required": ["job"]}},
    {"name": "assets_run", "description": "Uruchamia workera w tle (dla jednego zadania albo całej kolejki). Wraca od razu z PID i logiem.",
     "inputSchema": {"type": "object", "properties": {"job": JOB, "maxSeconds": {"type": "integer", "minimum": 60, "maximum": 14400}}}},
    {"name": "assets_run_stage", "description": "Ustawia zadanie na wybrany etap (np. ponowny import lub walidacja) i uruchamia workera w tle.",
     "inputSchema": {"type": "object", "properties": {"job": JOB, "stage": {"type": "string", "enum": list(MANUAL)}}, "required": ["job", "stage"]}},
    {"name": "assets_resume_job", "description": "Wznawia zadanie zatrzymane w needs_configuration, needs_review lub failed. resubmit=true porzuca nieznane zadania zdalne (nowe kosztują i wymagają zatwierdzonego budżetu).",
     "inputSchema": {"type": "object", "properties": {"job": JOB, "resubmit": {"type": "boolean", "default": False}}, "required": ["job"]}},
    {"name": "assets_cancel_job", "description": "Anuluje zadanie w kolejce (zadania u dostawców nie są usuwane).",
     "inputSchema": {"type": "object", "properties": {"job": JOB}, "required": ["job"]}},
]


class Server:
    def __init__(self, config):
        self.config = config
        self.store = service.open_store(config)

    def call(self, name: str, arguments: dict):
        store = self.store
        if name == "assets_capabilities":
            return capabilities(self.config)
        if name == "assets_brief_template":
            return briefs.template(arguments["category"], arguments.get("shipClass"))
        if name == "assets_create_job":
            try:
                return {"jobs": service.create_jobs(self.config, store, arguments["brief"], self.config.root, 0)}
            except BriefError as error:
                return {"error": "brief", "problems": error.problems}
        if name == "assets_list_jobs":
            state = arguments.get("state")
            return [{"id": j["id"], "state": j["state"], "detail": j["detail"]} for j in store.jobs([state] if state else None)]
        if name == "assets_job_status":
            return service.view(store, service.find_job(store, arguments["job"]))
        if name == "assets_run":
            job = service.find_job(store, arguments["job"])["id"] if arguments.get("job") else None
            return service.spawn_worker(self.config, job, int(arguments.get("maxSeconds", 3600)))
        if name == "assets_run_stage":
            job = service.start_stage(store, service.find_job(store, arguments["job"]), arguments["stage"])
            return {"job": job["id"], "state": job["state"], "worker": service.spawn_worker(self.config, job["id"])}
        if name == "assets_resume_job":
            job = service.resume(store, service.find_job(store, arguments["job"]), bool(arguments.get("resubmit", False)))
            return {"job": job["id"], "state": job["state"], "worker": service.spawn_worker(self.config, job["id"])}
        if name == "assets_cancel_job":
            job = service.cancel(store, service.find_job(store, arguments["job"]))
            return {"job": job["id"], "state": job["state"]}
        raise KeyError(f"Nieznane narzędzie {name}")

    def handle(self, message: dict):
        method, ident = message.get("method"), message.get("id")
        if ident is None:
            return None  # notification (e.g. notifications/initialized)
        if method == "initialize":
            requested = (message.get("params") or {}).get("protocolVersion")
            return _result(ident, {
                "protocolVersion": requested if requested in PROTOCOL_VERSIONS else PROTOCOL_VERSIONS[0],
                "capabilities": {"tools": {"listChanged": False}},
                "serverInfo": {"name": "morza-bogow-assets", "version": __version__},
                "instructions": INSTRUCTIONS,
            })
        if method == "ping":
            return _result(ident, {})
        if method == "tools/list":
            return _result(ident, {"tools": TOOLS})
        if method == "tools/call":
            params = message.get("params") or {}
            try:
                data = self.call(params.get("name"), params.get("arguments") or {})
                return _result(ident, {"content": [{"type": "text", "text": json.dumps(data, ensure_ascii=False, indent=1)}], "isError": False})
            except (KeyError, ValueError) as error:
                return _result(ident, {"content": [{"type": "text", "text": str(error).strip("'\"")}], "isError": True})
            except Exception as error:  # report tool failures to the client instead of dropping the connection
                print(traceback.format_exc(), file=sys.stderr)
                return _result(ident, {"content": [{"type": "text", "text": f"{error.__class__.__name__}: {error}"}], "isError": True})
        return {"jsonrpc": "2.0", "id": ident, "error": {"code": -32601, "message": f"Method not found: {method}"}}


def _result(ident, result):
    return {"jsonrpc": "2.0", "id": ident, "result": result}


def serve(config, stdin=None, stdout=None):
    stdin = stdin or sys.stdin
    stdout = stdout or sys.stdout
    server = Server(config)
    for line in stdin:
        line = line.strip()
        if not line:
            continue
        try:
            message = json.loads(line)
        except json.JSONDecodeError:
            stdout.write(json.dumps({"jsonrpc": "2.0", "id": None, "error": {"code": -32700, "message": "Parse error"}}) + "\n")
            stdout.flush()
            continue
        reply = server.handle(message)
        if reply is not None:
            stdout.write(json.dumps(reply, ensure_ascii=False) + "\n")
            stdout.flush()
