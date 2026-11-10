"""Worker: takes due jobs from the queue and runs one stage step at a time.

A step is idempotent. Remote work goes through `StageContext.remote`, which records every request
before it is sent and afterwards only polls, reconciles or downloads, so a stopped worker never
repeats a paid submission. The worker turns stage outcomes into job states, retries with
exponential backoff or the server's Retry-After, and keeps a lease so two workers do not run the
same job.
"""
import hashlib
import json
import random
import signal
import time
import traceback

from . import handlers as stage_handlers
from .errors import Ambiguous, Fatal, NeedsConfiguration, NeedsReview, PipelineError, Retryable
from .http import TransportError, redact
from .stages import LABEL, next_stage
from .store import TASK_OPEN, owner_id

LEASE_SECONDS = 1800
RECONCILE_GRACE = 180  # seconds a provider's task list may lag behind a create request


class Wait(Exception):
    """Not an error: come back to this job after `seconds` (polling a remote task)."""

    def __init__(self, seconds: float, detail: str):
        super().__init__(detail)
        self.seconds = seconds
        self.detail = detail


def request_digest(payload) -> str:
    return hashlib.sha256(json.dumps(payload, sort_keys=True, default=str).encode("utf-8")).hexdigest()


class StageContext:
    """What a stage handler sees: the job, its workspace, its artifacts and the remote-task helper."""

    def __init__(self, worker, job: dict):
        self.worker = worker
        self.config = worker.config
        self.store = worker.store
        self.providers = worker.providers
        self.job = job
        self.brief = job["brief"]
        self.stage = job["state"]
        self.artifacts = dict(job["artifacts"])
        self.next_state = None
        self.dir = self.config.job_dir(job["id"])
        self.dir.mkdir(parents=True, exist_ok=True)

    def log(self, message: str, level="info"):
        self.store.event(self.job["id"], self.stage, level, message)

    def now(self) -> float:
        return self.worker.now()

    def keep_alive(self):
        self.store.extend(self.job["id"], self.worker.owner, LEASE_SECONDS)

    # Credits ---------------------------------------------------------------------------------
    def reserve(self, credits: int, what: str):
        if credits <= 0:
            return
        job = self.store.get(self.job["id"])
        used = self.store.credits_reserved(job["id"])
        limits = self.config.limits
        cmd = f"ap approve {job['id']} --credits {max(job['estimated_credits'], used + credits)}"
        if used + credits > job["approved_credits"]:
            raise NeedsReview(f"{what}: koszt ok. {credits} kr. (razem {used + credits}, szacunek zadania {job['estimated_credits']}). "
                              f"Zatwierdzono {job['approved_credits']} kr. Po zgodzie: {cmd}")
        if used + credits > limits["maxCreditsPerJob"]:
            raise NeedsReview(f"{what}: przekroczony limit {limits['maxCreditsPerJob']} kr. na zadanie (config limits.maxCreditsPerJob).")
        day = self.store.credits_since(self.now() - 86400)
        if day + credits > limits["maxCreditsPerDay"]:
            raise NeedsReview(f"{what}: przekroczony limit {limits['maxCreditsPerDay']} kr. na dobę ({day} użyte).")

    # Remote tasks ----------------------------------------------------------------------------
    def poll_interval(self, task) -> float:
        polling = self.config.polling
        elapsed = self.now() - (task.get("submitted_at") or self.now())
        return max(polling["initialSeconds"], min(polling["maxSeconds"], elapsed / 6))

    def remote(self, provider, kind: str, payload: dict, summary: dict, credits: int = 0):
        """Create (once), then poll a remote task until it succeeds. Returns the task row when done.

        Raises Wait while the task runs. A request that may already exist remotely is reconciled
        with the provider's own task list and never re-sent automatically.
        """
        job_id = self.job["id"]
        task = self.store.task(job_id, kind)
        if task is None:
            self.reserve(credits, f"{provider.name} {kind}")
            task = self.store.open_task(job_id, self.stage, provider.name, kind, request_digest(payload), summary, credits)
            try:
                remote_id = provider.create(kind, payload)
            except TransportError as error:
                if not error.sent:
                    self.store.update_task(task["id"], status="abandoned", error=str(error))
                    raise Retryable(f"{provider.name}: nie wysłano ({error}). Ponowię.") from None
                self.store.update_task(task["id"], status="unknown", error=str(error))
                raise Wait(30, f"{provider.name}: brak odpowiedzi na utworzenie {kind}; sprawdzę listę zadań zamiast wysyłać ponownie.") from None
            except Ambiguous as error:
                self.store.update_task(task["id"], status="unknown", error=error.message)
                raise Wait(30, f"{provider.name}: {error.message} Sprawdzę listę zadań.") from None
            except PipelineError as error:
                # The provider answered with a refusal: nothing was created.
                self.store.update_task(task["id"], status="abandoned", error=error.message)
                raise
            self.store.update_task(task["id"], status="submitted", remote_id=remote_id)
            self.log(f"{provider.name}: utworzono {kind} ({remote_id}).")
            task = self.store.task_by_id(task["id"])
            raise Wait(self.config.polling["initialSeconds"], f"{provider.name}: {kind} wysłane, czekam.")

        if task["status"] in ("submitting", "unknown"):
            task = self._reconcile(provider, task)

        if task["status"] in ("submitted", "running"):
            try:
                status = provider.status(kind, task["remote_id"])
            except TransportError as error:
                raise Retryable(f"{provider.name}: odpytywanie {kind}: {error}") from None
            self.store.update_task(task["id"], status="running" if status.state == "running" else status.state,
                                   progress=status.progress, response=status.raw, consumed_credits=status.consumed_credits,
                                   error=status.error)
            task = self.store.task_by_id(task["id"])
            if status.state == "running":
                raise Wait(self.poll_interval(task), f"{provider.name}: {kind} {status.progress or 0}%")
            if status.state in ("failed", "canceled"):
                if provider.paid:
                    raise NeedsReview(f"{provider.name}: zadanie {kind} ({task['remote_id']}) zakończone: {status.state} {status.error or ''}. "
                                      f"Nowa próba kosztuje: ap resume {job_id} --resubmit")
                self.store.update_task(task["id"], status="abandoned")
                raise Retryable(f"{provider.name}: {kind} {status.state}: {status.error or ''}")
        if task["status"] in ("succeeded", "downloaded"):
            return task
        if task["status"] in ("failed", "canceled"):
            raise NeedsReview(f"{provider.name}: zadanie {kind} ({task['remote_id']}) zakończone: {task['status']} {task.get('error') or ''}. "
                              f"Nowa próba (koszt): ap resume {job_id} --resubmit")
        raise Fatal(f"Nieoczekiwany stan zadania {kind}: {task['status']}")

    def _reconcile(self, provider, task):
        """A create whose answer was lost: find it remotely or stop for a person; never resend a paid one."""
        try:
            matches = provider.find(task["kind"], task["request_summary"] or {}, task["submitted_at"] or 0)
        except TransportError as error:
            raise Retryable(f"{provider.name}: lista zadań niedostępna ({error})") from None
        if len(matches) == 1:
            self.store.update_task(task["id"], status="submitted", remote_id=matches[0], error=None)
            self.log(f"{provider.name}: odnaleziono wysłane wcześniej zadanie {task['kind']} ({matches[0]}); bez ponownego wysyłania.", "warn")
            return self.store.task_by_id(task["id"])
        if len(matches) > 1:
            raise NeedsReview(f"{provider.name}: kilka zadań pasuje do {task['kind']}: {', '.join(matches[:5])}. Wybierz w panelu dostawcy.")
        if not provider.paid:
            self.store.update_task(task["id"], status="abandoned")
            raise Retryable(f"{provider.name}: zadanie {task['kind']} nie dotarło; wyślę ponownie (bez kosztu).")
        if self.now() - (task["submitted_at"] or 0) < RECONCILE_GRACE:
            self.store.update_task(task["id"], status="unknown")
            raise Wait(30, f"{provider.name}: jeszcze nie widać zadania {task['kind']} na liście; czekam.")
        self.store.update_task(task["id"], status="unknown")
        raise NeedsReview(f"{provider.name}: nie wiadomo, czy zadanie {task['kind']} zostało przyjęte (brak na liście po {RECONCILE_GRACE} s). "
                          f"Sprawdź panel dostawcy; ponowne wysłanie (koszt): ap resume {self.job['id']} --resubmit")


class Worker:
    def __init__(self, config, store, providers=None, handlers=None, owner=None, now=time.time, sleep=time.sleep, out=None):
        from ..providers import registry
        self.config = config
        self.store = store
        self.providers = providers if providers is not None else registry(config)
        self.handlers = handlers if handlers is not None else stage_handlers.HANDLERS
        self.owner = owner or owner_id()
        self.now = now
        self.sleep = sleep
        self.out = out or (lambda text: None)
        self.stopping = False

    def backoff(self, job_id: str, attempt: int) -> float:
        polling = self.config.polling
        base = min(polling["maxSeconds"], polling["initialSeconds"] * (2 ** max(0, attempt - 1)))
        jitter = random.Random(f"{job_id}:{attempt}").uniform(0.8, 1.2)
        return base * jitter

    def step(self, job: dict) -> str:
        store, job_id, state = self.store, job["id"], job["state"]
        if job["cancel_requested"]:
            store.transition(job_id, "failed", "Anulowano na żądanie.", "warn", blocked_stage=state)
            return "failed"
        if state == "queued":
            store.transition(job_id, "generating", f"Etap: {LABEL['generating']}", attempt=0, next_run_at=self.now())
            return "generating"
        handler = self.handlers[state]
        ctx = StageContext(self, job)
        try:
            detail = handler(ctx) or f"Zakończono: {LABEL[state]}"
            target = ctx.next_state or next_stage(state)
            store.transition(job_id, target, detail, artifacts=ctx.artifacts, attempt=0, next_run_at=self.now(), needs=None, blocked_stage=None)
            self.out(f"{job_id}: {state} -> {target}: {detail}")
            return target
        except Wait as wait:
            if wait.detail != job["detail"]:
                store.event(job_id, state, "info", wait.detail)
            store.update(job_id, detail=wait.detail, artifacts=ctx.artifacts, next_run_at=self.now() + wait.seconds)
            return state
        except Retryable as error:
            attempt = job["attempt"] + 1
            limit = self.config.limits["maxAttemptsPerStage"]
            if attempt > limit:
                store.transition(job_id, "failed", f"{error.message} (wyczerpano {limit} prób)", "error", artifacts=ctx.artifacts, blocked_stage=state, attempt=attempt)
                return "failed"
            delay = error.retry_after if error.retry_after is not None else self.backoff(job_id, attempt)
            store.event(job_id, state, "warn", f"Próba {attempt}/{limit} za {delay:.0f} s: {error.message}")
            store.update(job_id, detail=error.message, artifacts=ctx.artifacts, attempt=attempt, next_run_at=self.now() + delay)
            return state
        except NeedsConfiguration as error:
            store.transition(job_id, "needs_configuration", error.message, "warn", artifacts=ctx.artifacts, blocked_stage=state, needs=error.items)
            self.out(f"{job_id}: brak konfiguracji: {error.message}")
            return "needs_configuration"
        except NeedsReview as error:
            store.transition(job_id, "needs_review", error.message, "warn", artifacts=ctx.artifacts, blocked_stage=state)
            self.out(f"{job_id}: do przeglądu: {error.message}")
            return "needs_review"
        except (Fatal, PipelineError) as error:
            store.transition(job_id, "failed", error.message, "error", artifacts=ctx.artifacts, blocked_stage=state)
            self.out(f"{job_id}: błąd: {error.message}")
            return "failed"
        except Exception as error:  # a bug or an unexpected tool error: keep the evidence, stop the job
            store.event(job_id, state, "error", redact(traceback.format_exc(limit=8)))
            store.transition(job_id, "failed", f"Błąd wewnętrzny: {error.__class__.__name__}: {redact(error)}", "error",
                             artifacts=ctx.artifacts, blocked_stage=state)
            self.out(f"{job_id}: błąd wewnętrzny: {error}")
            return "failed"

    def _install_signals(self):
        def stop(signum, frame):
            self.stopping = True
            self.out("Zatrzymuję po bieżącym kroku…")
        previous = {}
        for sig in (signal.SIGINT, signal.SIGTERM):
            try:
                previous[sig] = signal.signal(sig, stop)
            except (ValueError, OSError):
                pass
        return previous

    def run(self, job_id=None, until_idle=False, max_seconds=None, max_steps=None, handle_signals=True) -> int:
        """Process due jobs. Returns the number of steps taken."""
        previous = self._install_signals() if handle_signals else {}
        steps = 0
        deadline = self.now() + max_seconds if max_seconds else None
        try:
            while not self.stopping:
                job = self.store.claim(self.owner, LEASE_SECONDS, job_id=job_id)
                if job is None:
                    if not until_idle:
                        break
                    due = self._due(job_id)
                    if due is None:
                        break
                    wait = max(0.2, due - self.now())
                    if deadline and self.now() + wait > deadline:
                        break
                    self.sleep(min(wait, 15))
                    continue
                try:
                    self.step(job)
                finally:
                    self.store.release(job["id"], self.owner)
                steps += 1
                if max_steps and steps >= max_steps:
                    break
                if deadline and self.now() > deadline:
                    break
        finally:
            for sig, handler in previous.items():
                signal.signal(sig, handler)
        return steps

    def _due(self, job_id):
        if job_id:
            job = self.store.get(job_id)
            from .stages import ACTIVE
            if not job or job["state"] not in ACTIVE:
                return None
            return job["next_run_at"]
        return self.store.next_due()


def resume(store, job_id: str, resubmit=False, now=time.time) -> dict:
    """Send a blocked job back to the stage where it stopped. `resubmit` drops unknown or failed
    remote tasks of that stage, so the next step creates new ones (and may cost credits again)."""
    job = store.get(job_id)
    if job is None:
        raise KeyError(job_id)
    stage = job["blocked_stage"] or job["state"]
    if job["state"] == "ready" and not job["blocked_stage"]:
        return job
    dropped = store.orphan_tasks(job_id, stage) if resubmit else 0
    if not resubmit:
        stuck = [t for t in store.tasks(job_id) if t["stage"] == stage and t["status"] in TASK_OPEN]
        for task in stuck:
            if task["status"] == "unknown":
                # Check the provider's list once more right away; still missing means review again, not a resend.
                store.update_task(task["id"], submitted_at=now() - RECONCILE_GRACE)
    store.transition(job_id, stage, f"Wznowiono od etapu: {LABEL.get(stage, stage)}" + (f" (porzucono {dropped} zadań zdalnych)" if dropped else ""),
                     attempt=0, next_run_at=now(), needs=None, blocked_stage=None, cancel_requested=0)
    return store.get(job_id)
