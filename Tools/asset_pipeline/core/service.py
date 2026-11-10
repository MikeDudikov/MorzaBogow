"""Operations shared by the CLI and the MCP server."""
import json
import os
import pathlib
import subprocess
import sys
import time

from . import brief as briefs
from . import handlers
from .paths import TOOL_ROOT, project_relative
from .stages import ACTIVE, LABEL, MANUAL
from .store import Store
from .worker import resume as resume_job

PREREQUISITES = {
    "preparing_geometry": ("model",),
    "texturing": ("model", "geometry"),
    "rigging": ("model", "geometry"),
    "animating": ("model", "geometry"),
    "importing": ("model", "geometry"),
    "validating": ("unity",),
}


def open_store(config) -> Store:
    return Store(config.database)


def create_jobs(config, store, raw: dict, base_dir=None, approve_credits=0) -> list:
    brief = briefs.normalise(raw, config, base_dir)
    estimate = handlers.estimate_credits(brief, config)
    digest = briefs.digest(brief)
    jobs = []
    for variant in range(brief["variants"]):
        job_id = store.create_job(brief, digest, variant, estimate, approve_credits)
        jobs.append({"id": job_id, "estimated_credits": estimate, "approved_credits": approve_credits,
                     "approval_needed": estimate > approve_credits})
    return jobs


def find_job(store, job_ref: str) -> dict:
    job = store.find(job_ref)
    if job is None:
        raise KeyError(f"Nie ma zadania {job_ref}")
    return job


def view(store, job: dict, events=12) -> dict:
    """Job summary without the brief's long text, with remote tasks and recent events."""
    return {
        "id": job["id"],
        "asset": job["asset_id"],
        "category": job["category"],
        "state": job["state"],
        "stateLabel": LABEL.get(job["state"], job["state"]),
        "blockedStage": job["blocked_stage"],
        "detail": job["detail"],
        "needs": job["needs"],
        "attempt": job["attempt"],
        "repairs": job["repairs"],
        "credits": {"estimated": job["estimated_credits"], "approved": job["approved_credits"], "used": store.credits_reserved(job["id"])},
        "nextRunInSeconds": max(0, round(job["next_run_at"] - time.time())) if job["state"] in ACTIVE else None,
        "artifacts": job["artifacts"],
        "tasks": [{k: t[k] for k in ("kind", "provider", "status", "remote_id", "progress", "credits", "consumed_credits", "error")} for t in store.tasks(job["id"])],
        "events": [{"at": time.strftime("%H:%M:%S", time.localtime(e["at"])), "state": e["state"], "level": e["level"], "message": e["message"]}
                   for e in store.events(job["id"], events)],
    }


def approve(store, job: dict, credits: int) -> dict:
    store.update(job["id"], approved_credits=int(credits))
    store.event(job["id"], job["state"], "info", f"Zatwierdzono budżet {int(credits)} kr.")
    return store.get(job["id"])


def start_stage(store, job: dict, command: str) -> dict:
    stage = MANUAL[command]
    missing = [name for name in PREREQUISITES[stage] if not job["artifacts"].get(name)]
    if missing:
        raise ValueError(f"Etap {LABEL[stage]} wymaga wcześniejszych wyników: {', '.join(missing)}")
    artifacts = dict(job["artifacts"])
    if stage == "validating":
        artifacts["unity"] = dict(artifacts["unity"], stale=True)
    if stage == "texturing":
        artifacts.pop("textures", None)
    if stage == "preparing_geometry":
        artifacts.pop("geometry", None)
    store.transition(job["id"], stage, f"Uruchomiono ręcznie: {LABEL[stage]}", artifacts=artifacts, attempt=0,
                     next_run_at=time.time(), needs=None, blocked_stage=None, cancel_requested=0)
    return store.get(job["id"])


def resume(store, job: dict, resubmit=False) -> dict:
    return resume_job(store, job["id"], resubmit=resubmit)


def cancel(store, job: dict) -> dict:
    store.update(job["id"], cancel_requested=1)
    if job["state"] not in ACTIVE:
        store.transition(job["id"], "failed", "Anulowano na żądanie.", "warn", blocked_stage=job["blocked_stage"] or job["state"])
    else:
        store.event(job["id"], job["state"], "warn", "Anulowanie: zadanie zatrzyma się przy następnym kroku.")
    return store.get(job["id"])


def spawn_worker(config, job_id=None, max_seconds=3600) -> dict:
    """A detached worker process (for the MCP server, whose calls must return quickly)."""
    logs = config.workspace / "logs"
    logs.mkdir(parents=True, exist_ok=True)
    log = logs / f"worker-{time.strftime('%Y%m%d-%H%M%S')}.log"
    args = [sys.executable, str(TOOL_ROOT / "ap.py"), "worker", "--until-idle", "--max-seconds", str(max_seconds)]
    if job_id:
        args += ["--job", job_id]
    with open(log, "w", encoding="utf-8") as output:
        process = subprocess.Popen(args, stdout=output, stderr=subprocess.STDOUT, stdin=subprocess.DEVNULL,
                                   start_new_session=(os.name != "nt"), env=dict(os.environ))
    return {"pid": process.pid, "log": project_relative(log, config.root)}


def load_brief_file(path) -> tuple:
    path = pathlib.Path(path)
    return json.loads(path.read_text(encoding="utf-8")), path.parent
