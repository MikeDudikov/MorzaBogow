import os
import socket
import subprocess
import unittest

from asset_pipeline.core import service
from asset_pipeline.core.errors import NeedsConfiguration, Retryable
from asset_pipeline.core.stages import PIPELINE
from asset_pipeline.core.worker import Wait, Worker, resume
from asset_pipeline.tests.support import Sandbox

BRIEF = {"id": "ship_cutter_queue", "category": "ship", "shipClass": "Cutter", "description": "A small fast boat.", "generation": {"provider": "blockout"}}


def passing(ctx):
    return None


class WorkerTests(unittest.TestCase):
    def setUp(self):
        self.box = Sandbox()
        self.store = self.box.store
        self.clock = self.box.clock
        self.job = service.create_jobs(self.box.config, self.store, BRIEF)[0]["id"]

    def tearDown(self):
        self.box.close()

    def worker(self, handlers, owner=None):
        table = {stage: passing for stage in PIPELINE if stage != "queued"}
        table.update(handlers)
        return Worker(self.box.config, self.store, providers={}, handlers=table, owner=owner, now=self.clock, sleep=self.clock.sleep)

    def state(self):
        return self.store.get(self.job)

    def test_a_job_walks_every_stage_to_ready(self):
        self.worker({}).run(until_idle=True, handle_signals=False)
        self.assertEqual(self.state()["state"], "ready")
        seen = [e["state"] for e in self.store.events(self.job, 100)]
        for stage in PIPELINE[1:]:
            self.assertIn(stage, seen)

    def test_waiting_comes_back_later_without_using_attempts(self):
        calls = []

        def slow(ctx):
            calls.append(self.clock())
            if len(calls) == 1:
                raise Wait(30, "czekam")

        worker = self.worker({"generating": slow})
        worker.run(max_steps=2, handle_signals=False)
        job = self.state()
        self.assertEqual((job["state"], job["attempt"]), ("generating", 0))
        self.assertAlmostEqual(job["next_run_at"], self.clock() + 30)
        worker.run(until_idle=True, handle_signals=False)
        self.assertEqual(self.state()["state"], "ready")
        self.assertGreaterEqual(calls[1] - calls[0], 30)

    def test_retries_back_off_and_stop_after_the_limit(self):
        def broken(ctx):
            raise Retryable("chwilowy błąd")

        worker = self.worker({"generating": broken})
        delays = []
        for _ in range(10):
            before = self.clock()
            worker.run(max_steps=1, handle_signals=False)
            job = self.state()
            if job["state"] != "generating":
                break
            if job["attempt"]:
                delays.append(job["next_run_at"] - before)
            self.clock.t = max(self.clock(), job["next_run_at"])
        job = self.state()
        self.assertEqual(job["state"], "failed")
        self.assertEqual(job["blocked_stage"], "generating")
        self.assertEqual(len(delays), self.box.config.limits["maxAttemptsPerStage"])
        self.assertTrue(all(b > a for a, b in zip(delays, delays[1:])), delays)

    def test_retry_after_from_the_server_is_honoured(self):
        def limited(ctx):
            raise Retryable("429", retry_after=77)

        self.worker({"generating": limited}).run(max_steps=2, handle_signals=False)
        self.assertAlmostEqual(self.state()["next_run_at"], self.clock() + 77)

    def test_missing_configuration_waits_for_resume(self):
        state = {"configured": False}

        def needs_blender(ctx):
            if not state["configured"]:
                raise NeedsConfiguration("Brak Blendera", ["BLENDER_PATH"])

        worker = self.worker({"preparing_geometry": needs_blender})
        worker.run(until_idle=True, handle_signals=False)
        job = self.state()
        self.assertEqual((job["state"], job["blocked_stage"], job["needs"]), ("needs_configuration", "preparing_geometry", ["BLENDER_PATH"]))
        state["configured"] = True
        resume(self.store, self.job, now=self.clock)
        self.assertEqual(self.state()["state"], "preparing_geometry")
        worker.run(until_idle=True, handle_signals=False)
        self.assertEqual(self.state()["state"], "ready")
        self.assertEqual(self.state()["needs"], [])

    def test_a_live_lease_is_respected_and_a_dead_one_taken_over(self):
        live = f"{socket.gethostname()}:{os.getpid()}:live"
        self.assertIsNotNone(self.store.claim(live, 600))
        self.assertIsNone(self.store.claim("other:1:x", 600), "Another host's worker must wait for the lease.")
        finished = subprocess.Popen(["true"])
        finished.wait()
        self.store.update(self.job, lease_owner=f"{socket.gethostname()}:{finished.pid}:dead", lease_until=self.clock() + 600)
        taken = self.store.claim("me:2:y", 600)
        self.assertIsNotNone(taken, "A lease of a process that no longer exists is taken over.")
        self.assertTrue(any("Przejęto" in e["message"] for e in self.store.events(self.job)))

    def test_cancel_stops_the_job_at_the_next_step(self):
        service.cancel(self.store, self.state())
        self.worker({}).run(until_idle=True, handle_signals=False)
        job = self.state()
        self.assertEqual(job["state"], "failed")
        self.assertIn("Anulowano", job["detail"])

    def test_an_unexpected_error_fails_the_job_and_keeps_the_trace(self):
        def bug(ctx):
            return 1 / 0

        self.worker({"texturing": bug}).run(until_idle=True, handle_signals=False)
        job = self.state()
        self.assertEqual((job["state"], job["blocked_stage"]), ("failed", "texturing"))
        self.assertTrue(any("ZeroDivisionError" in e["message"] and e["level"] == "error" for e in self.store.events(self.job, 50)))


if __name__ == "__main__":
    unittest.main()
