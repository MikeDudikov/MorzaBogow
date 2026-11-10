"""Persistent job queue (SQLite, WAL).

Jobs move through the stages; every remote request is a row in `provider_tasks` written *before*
it is sent (status `submitting`) and updated with the provider's id right after. A worker that dies
anywhere in between leaves enough to continue: a known remote id is polled again, an unknown one is
reconciled against the provider's task list, never sent a second time on its own.
"""
import json
import os
import pathlib
import socket
import sqlite3
import time
import uuid

from .stages import ACTIVE, STATES

SCHEMA_VERSION = 1

DDL = """
CREATE TABLE IF NOT EXISTS meta(key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS jobs(
    id TEXT PRIMARY KEY,
    asset_id TEXT NOT NULL,
    variant INTEGER NOT NULL DEFAULT 0,
    category TEXT NOT NULL,
    brief_json TEXT NOT NULL,
    brief_sha256 TEXT NOT NULL,
    state TEXT NOT NULL,
    blocked_stage TEXT,
    detail TEXT,
    needs_json TEXT,
    attempt INTEGER NOT NULL DEFAULT 0,
    repairs INTEGER NOT NULL DEFAULT 0,
    next_run_at REAL NOT NULL DEFAULT 0,
    lease_owner TEXT,
    lease_until REAL,
    approved_credits INTEGER NOT NULL DEFAULT 0,
    estimated_credits INTEGER NOT NULL DEFAULT 0,
    artifacts_json TEXT NOT NULL DEFAULT '{}',
    cancel_requested INTEGER NOT NULL DEFAULT 0,
    created_at REAL NOT NULL,
    updated_at REAL NOT NULL
);
CREATE INDEX IF NOT EXISTS jobs_due ON jobs(state, next_run_at);
CREATE TABLE IF NOT EXISTS provider_tasks(
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    job_id TEXT NOT NULL REFERENCES jobs(id),
    stage TEXT NOT NULL,
    provider TEXT NOT NULL,
    kind TEXT NOT NULL,
    request_sha256 TEXT NOT NULL,
    request_summary TEXT,
    status TEXT NOT NULL,
    remote_id TEXT,
    credits INTEGER NOT NULL DEFAULT 0,
    consumed_credits INTEGER,
    progress INTEGER,
    submitted_at REAL,
    updated_at REAL NOT NULL,
    response_json TEXT,
    outputs_json TEXT,
    error TEXT
);
CREATE INDEX IF NOT EXISTS tasks_job ON provider_tasks(job_id, kind);
CREATE TABLE IF NOT EXISTS events(
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    job_id TEXT NOT NULL,
    at REAL NOT NULL,
    state TEXT NOT NULL,
    level TEXT NOT NULL,
    message TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS events_job ON events(job_id, id);
"""

# Task statuses. `submitting` and `unknown` are the dangerous ones: the request may exist remotely.
TASK_OPEN = ("submitting", "submitted", "running", "unknown")
TASK_DONE = ("succeeded", "downloaded")


def owner_id() -> str:
    return f"{socket.gethostname()}:{os.getpid()}:{uuid.uuid4().hex[:6]}"


def _owner_alive(owner: str) -> bool:
    """False only when the owner is a process on this host that no longer exists."""
    try:
        host, pid, _ = owner.split(":", 2)
        if host != socket.gethostname():
            return True
        os.kill(int(pid), 0)
        return True
    except ProcessLookupError:
        return False
    except (ValueError, PermissionError, OSError):
        return True


class Store:
    def __init__(self, path, now=time.time):
        self.path = pathlib.Path(path)
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.now = now
        self.db = sqlite3.connect(str(self.path), timeout=30, isolation_level=None)
        self.db.row_factory = sqlite3.Row
        self.db.execute("PRAGMA journal_mode=WAL")
        self.db.execute("PRAGMA foreign_keys=ON")
        self.db.executescript(DDL)
        self.db.execute("INSERT OR IGNORE INTO meta(key, value) VALUES('schema', ?)", (str(SCHEMA_VERSION),))

    def close(self):
        self.db.close()

    # Jobs ------------------------------------------------------------------------------------
    def create_job(self, brief: dict, brief_sha256: str, variant=0, estimated_credits=0, approved_credits=0) -> str:
        now = self.now()
        job_id = f"{brief['id']}-{time.strftime('%Y%m%d-%H%M%S', time.gmtime(now))}-{uuid.uuid4().hex[:4]}" + (f"-v{variant + 1}" if brief.get("variants", 1) > 1 else "")
        self.db.execute(
            "INSERT INTO jobs(id, asset_id, variant, category, brief_json, brief_sha256, state, detail, estimated_credits, approved_credits, created_at, updated_at)"
            " VALUES(?,?,?,?,?,?,?,?,?,?,?,?)",
            (job_id, brief["id"], variant, brief["category"], json.dumps(brief, ensure_ascii=False), brief_sha256, "queued",
             "W kolejce", estimated_credits, approved_credits, now, now))
        self.event(job_id, "queued", "info", f"Utworzono zadanie (szacunek {estimated_credits} kr., zatwierdzone {approved_credits}).")
        return job_id

    def get(self, job_id: str):
        row = self.db.execute("SELECT * FROM jobs WHERE id=?", (job_id,)).fetchone()
        return _job(row) if row else None

    def find(self, prefix: str):
        rows = self.db.execute("SELECT * FROM jobs WHERE id=? OR id LIKE ? ORDER BY created_at DESC", (prefix, prefix + "%")).fetchall()
        if len(rows) == 1:
            return _job(rows[0])
        exact = [r for r in rows if r["id"] == prefix]
        return _job(exact[0]) if exact else None

    def jobs(self, states=None, limit=100):
        if states:
            marks = ",".join("?" * len(states))
            rows = self.db.execute(f"SELECT * FROM jobs WHERE state IN ({marks}) ORDER BY created_at DESC LIMIT ?", (*states, limit)).fetchall()
        else:
            rows = self.db.execute("SELECT * FROM jobs ORDER BY created_at DESC LIMIT ?", (limit,)).fetchall()
        return [_job(r) for r in rows]

    def claim(self, owner: str, lease_seconds: float, job_id=None):
        """Take the next due job (or the given one) under a lease. Expired or dead leases are taken over."""
        now = self.now()
        marks = ",".join("?" * len(ACTIVE))
        self.db.execute("BEGIN IMMEDIATE")
        try:
            if job_id:
                rows = self.db.execute(f"SELECT * FROM jobs WHERE id=? AND state IN ({marks}) AND next_run_at<=?", (job_id, *ACTIVE, now)).fetchall()
            else:
                rows = self.db.execute(
                    f"SELECT * FROM jobs WHERE state IN ({marks}) AND (next_run_at<=? OR cancel_requested=1) ORDER BY next_run_at, created_at",
                    (*ACTIVE, now)).fetchall()
            for row in rows:
                held = row["lease_owner"] and row["lease_until"] and row["lease_until"] > now and row["lease_owner"] != owner
                if held and _owner_alive(row["lease_owner"]):
                    continue
                if held:
                    self._event(row["id"], row["state"], "warn", f"Przejęto dzierżawę po zakończonym procesie {row['lease_owner']}.")
                self.db.execute("UPDATE jobs SET lease_owner=?, lease_until=?, updated_at=? WHERE id=?", (owner, now + lease_seconds, now, row["id"]))
                self.db.execute("COMMIT")
                return self.get(row["id"])
            self.db.execute("COMMIT")
            return None
        except BaseException:
            self.db.execute("ROLLBACK")
            raise

    def extend(self, job_id: str, owner: str, lease_seconds: float):
        self.db.execute("UPDATE jobs SET lease_until=? WHERE id=? AND lease_owner=?", (self.now() + lease_seconds, job_id, owner))

    def release(self, job_id: str, owner: str):
        self.db.execute("UPDATE jobs SET lease_owner=NULL, lease_until=NULL WHERE id=? AND lease_owner=?", (job_id, owner))

    def update(self, job_id: str, **fields):
        if "artifacts" in fields:
            fields["artifacts_json"] = json.dumps(fields.pop("artifacts"), ensure_ascii=False)
        if "needs" in fields:
            needs = fields.pop("needs")
            fields["needs_json"] = json.dumps(needs, ensure_ascii=False) if needs is not None else None
        if "state" in fields and fields["state"] not in STATES:
            raise ValueError(fields["state"])
        fields["updated_at"] = self.now()
        cols = ", ".join(f"{k}=?" for k in fields)
        self.db.execute(f"UPDATE jobs SET {cols} WHERE id=?", (*fields.values(), job_id))

    def transition(self, job_id: str, state: str, detail: str, level="info", **fields):
        self.update(job_id, state=state, detail=detail, **fields)
        self.event(job_id, state, level, detail)

    def next_due(self):
        marks = ",".join("?" * len(ACTIVE))
        row = self.db.execute(f"SELECT MIN(CASE WHEN cancel_requested=1 THEN 0 ELSE next_run_at END) FROM jobs WHERE state IN ({marks})", ACTIVE).fetchone()
        return row[0]

    def active_count(self) -> int:
        marks = ",".join("?" * len(ACTIVE))
        return self.db.execute(f"SELECT COUNT(*) FROM jobs WHERE state IN ({marks})", ACTIVE).fetchone()[0]

    # Events ----------------------------------------------------------------------------------
    def event(self, job_id: str, state: str, level: str, message: str):
        self._event(job_id, state, level, message)

    def _event(self, job_id, state, level, message):
        from .http import redact
        self.db.execute("INSERT INTO events(job_id, at, state, level, message) VALUES(?,?,?,?,?)", (job_id, self.now(), state, level, redact(message)))

    def events(self, job_id: str, limit=50):
        rows = self.db.execute("SELECT * FROM events WHERE job_id=? ORDER BY id DESC LIMIT ?", (job_id, limit)).fetchall()
        return [dict(r) for r in reversed(rows)]

    # Provider tasks --------------------------------------------------------------------------
    def task(self, job_id: str, kind: str):
        """The current task of this kind for the job. `abandoned` (never accepted) and `orphaned`
        (given up by a person, may still exist remotely) tasks are history, not current."""
        row = self.db.execute(
            "SELECT * FROM provider_tasks WHERE job_id=? AND kind=? AND status NOT IN ('abandoned','orphaned') ORDER BY id DESC LIMIT 1",
            (job_id, kind)).fetchone()
        return _task(row) if row else None

    def tasks(self, job_id: str):
        return [_task(r) for r in self.db.execute("SELECT * FROM provider_tasks WHERE job_id=? ORDER BY id", (job_id,)).fetchall()]

    def open_task(self, job_id, stage, provider, kind, request_sha256, summary, credits) -> dict:
        """Write-ahead record before a request leaves the machine."""
        now = self.now()
        cur = self.db.execute(
            "INSERT INTO provider_tasks(job_id, stage, provider, kind, request_sha256, request_summary, status, credits, submitted_at, updated_at)"
            " VALUES(?,?,?,?,?,?,?,?,?,?)",
            (job_id, stage, provider, kind, request_sha256, json.dumps(summary, ensure_ascii=False), "submitting", credits, now, now))
        return self.task_by_id(cur.lastrowid)

    def task_by_id(self, task_id: int):
        row = self.db.execute("SELECT * FROM provider_tasks WHERE id=?", (task_id,)).fetchone()
        return _task(row) if row else None

    def update_task(self, task_id: int, **fields):
        for key in ("response", "outputs"):
            if key in fields:
                fields[f"{key}_json"] = json.dumps(fields.pop(key), ensure_ascii=False)
        fields["updated_at"] = self.now()
        cols = ", ".join(f"{k}=?" for k in fields)
        self.db.execute(f"UPDATE provider_tasks SET {cols} WHERE id=?", (*fields.values(), task_id))

    def orphan_tasks(self, job_id: str, stage: str, statuses=("unknown", "submitting", "failed", "canceled")) -> int:
        """A person chose to resubmit: the old tasks stop being current but keep counting as spent."""
        marks = ",".join("?" * len(statuses))
        cur = self.db.execute(f"UPDATE provider_tasks SET status='orphaned', updated_at=? WHERE job_id=? AND stage=? AND status IN ({marks})",
                              (self.now(), job_id, stage, *statuses))
        return cur.rowcount

    def credits_reserved(self, job_id: str) -> int:
        """Credits this job has used or may have used. Only `abandoned` (refused or never sent) costs nothing."""
        row = self.db.execute("SELECT COALESCE(SUM(COALESCE(consumed_credits, credits)),0) FROM provider_tasks"
                              " WHERE job_id=? AND status<>'abandoned'", (job_id,)).fetchone()
        return int(row[0])

    def credits_since(self, since: float) -> int:
        row = self.db.execute("SELECT COALESCE(SUM(COALESCE(consumed_credits, credits)),0) FROM provider_tasks WHERE submitted_at>=? AND credits>0", (since,)).fetchone()
        return int(row[0])


def _job(row) -> dict:
    job = dict(row)
    job["brief"] = json.loads(job.pop("brief_json"))
    job["artifacts"] = json.loads(job.pop("artifacts_json") or "{}")
    needs = job.pop("needs_json")
    job["needs"] = json.loads(needs) if needs else []
    return job


def _task(row) -> dict:
    task = dict(row)
    for key in ("request_summary", "response_json", "outputs_json"):
        value = task.get(key)
        task[key.replace("_json", "")] = json.loads(value) if value else None
    task.pop("response_json", None)
    task.pop("outputs_json", None)
    return task
