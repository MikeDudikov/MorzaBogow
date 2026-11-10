"""Paid tasks across interruptions: each create is sent once; a lost answer is reconciled with the
provider's task list; only an explicit `resume --resubmit` sends again. Runs against a local
Meshy stand-in (tests only)."""
import socket
import subprocess
import unittest

from asset_pipeline.core import handlers, http, service
from asset_pipeline.core.errors import NeedsReview
from asset_pipeline.core.worker import Worker, resume
from asset_pipeline.providers.meshy import Meshy
from asset_pipeline.tests.support import Clock, MockMeshy, Sandbox

KEY = "msy_test_only_not_a_real_key"
BRIEF = {"id": "ship_ram_ai", "category": "ship", "shipClass": "Ram", "description": "A lean warship with a bronze beak and a painted eye."}


def stop_here(ctx):
    raise NeedsReview("koniec testu generowania")


class MeshyRecoveryTests(unittest.TestCase):
    def setUp(self):
        self.clock = Clock()
        self.mock = MockMeshy(self.clock)
        self.box = Sandbox(overrides={"providers": {"meshy": {"baseUrl": self.mock.url}}}, environ={"MESHY_API_KEY": KEY})
        self.box.clock = self.clock
        self.box.store.now = self.clock
        self.store = self.box.store

    def tearDown(self):
        self.mock.close()
        self.box.close()

    def create(self, approve=30, brief=BRIEF):
        return service.create_jobs(self.box.config, self.store, brief, approve_credits=approve)[0]["id"]

    def worker(self, meshy=None, owner=None):
        table = {"generating": handlers.generating, "preparing_geometry": stop_here}
        return Worker(self.box.config, self.store, providers={"meshy": meshy or Meshy(self.box.config)}, handlers=table,
                      owner=owner, now=self.clock, sleep=self.clock.sleep)

    def run_all(self, worker, job):
        worker.run(job_id=job, until_idle=True, handle_signals=False, max_seconds=5000)
        return self.store.get(job)

    def creates(self, kind):
        return [body for path, body in self.mock.posts if body.get("mode") == kind or path.endswith(kind)]

    def test_text_flow_sends_each_paid_task_once_and_downloads_the_model(self):
        job = self.run_all(self.worker(), self.create())
        self.assertEqual(job["blocked_stage"], "preparing_geometry", job["detail"])
        self.assertEqual(len(self.creates("preview")), 1)
        refine = self.creates("refine")
        self.assertEqual(len(refine), 1)
        self.assertEqual(refine[0]["preview_task_id"], "task-1")
        model = job["artifacts"]["model"]
        self.assertTrue(model["complete"])
        self.assertTrue(model["unity"].endswith(".fbx"))
        self.assertEqual(set(model["maps"]), {"base_color", "normal", "roughness", "metallic"})
        self.assertEqual(self.mock.authorizations, {"Bearer " + KEY})
        events = " ".join(e["message"] for e in self.store.events(job["id"], 200))
        self.assertNotIn(KEY, events)
        self.assertNotIn("X-Amz-Signature=secret", events)
        self.assertNotIn(KEY.encode(), self.box.config.database.read_bytes(), "The key never reaches the job database.")

    def test_a_worker_killed_after_sending_finds_the_task_instead_of_sending_again(self):
        job = self.create()
        meshy = Meshy(self.box.config)
        real_create = meshy.create

        def create_then_die(kind, payload):
            real_create(kind, payload)
            raise KeyboardInterrupt  # the process dies before it can record the task id

        meshy.create = create_then_die
        with self.assertRaises(KeyboardInterrupt):
            self.worker(meshy).run(job_id=job, until_idle=True, handle_signals=False)
        dead = subprocess.Popen(["true"])
        dead.wait()
        self.store.update(job, lease_owner=f"{socket.gethostname()}:{dead.pid}:gone", lease_until=self.clock() + 1800)
        task = self.store.task(job, "text_to_3d_preview")
        self.assertEqual((task["status"], task["remote_id"]), ("submitting", None))

        result = self.run_all(self.worker(), job)
        self.assertEqual(len(self.creates("preview")), 1, "The preview was not sent a second time.")
        self.assertTrue(result["artifacts"]["model"]["complete"])
        self.assertTrue(any("odnaleziono" in e["message"] for e in self.store.events(job, 200)))

    def test_a_lost_answer_is_reconciled_not_resent(self):
        job = self.create()
        real_request = http.request
        state = {"first": True}

        def flaky(method, url, **kwargs):
            response = real_request(method, url, **kwargs)
            if method == "POST" and state["first"]:
                state["first"] = False
                raise http.TransportError("timed out", sent=True)
            return response

        meshy = Meshy(self.box.config)
        meshy.http = type("T", (), {"request": staticmethod(flaky), "download": staticmethod(http.download)})
        result = self.run_all(self.worker(meshy), job)
        self.assertEqual(len(self.creates("preview")), 1)
        self.assertEqual(len(self.creates("refine")), 1)
        self.assertTrue(result["artifacts"]["model"]["complete"])

    def test_an_unknown_submission_waits_for_a_person_and_only_resubmit_sends_again(self):
        job = self.create(approve=60)
        self.mock.hide_from_list = True
        real_request = http.request
        state = {"first": True}

        def lost(method, url, **kwargs):
            response = real_request(method, url, **kwargs)
            if method == "POST" and state["first"]:
                state["first"] = False
                raise http.TransportError("connection reset", sent=True)
            return response

        meshy = Meshy(self.box.config)
        meshy.http = type("T", (), {"request": staticmethod(lost), "download": staticmethod(http.download)})
        result = self.run_all(self.worker(meshy), job)
        self.assertEqual((result["state"], result["blocked_stage"]), ("needs_review", "generating"))
        self.assertIn("resubmit", result["detail"])

        resume(self.store, job, now=self.clock)
        result = self.run_all(self.worker(), job)
        self.assertEqual(result["state"], "needs_review", "Resume alone checks the list again and does not resend.")
        self.assertEqual(len(self.creates("preview")), 1)

        self.mock.hide_from_list = False
        resume(self.store, job, resubmit=True, now=self.clock)
        result = self.run_all(self.worker(), job)
        self.assertEqual(len(self.creates("preview")), 2, "Only --resubmit sends the paid task again.")
        self.assertTrue(result["artifacts"]["model"]["complete"])

    def test_paid_steps_wait_for_a_person_to_approve_the_cost(self):
        job = self.create(approve=0)
        result = self.run_all(self.worker(), job)
        self.assertEqual(result["state"], "needs_review")
        self.assertIn("ap approve", result["detail"])
        self.assertEqual(self.mock.posts, [])
        service.approve(self.store, result, 30)
        resume(self.store, job, now=self.clock)
        result = self.run_all(self.worker(), job)
        self.assertTrue(result["artifacts"]["model"]["complete"])
        self.assertEqual(len(self.mock.posts), 2)

    def test_rate_limit_waits_as_long_as_the_server_says(self):
        job = self.create()
        self.mock.fail_next_post = (429, {"message": "Too Many Requests"}, {"Retry-After": "42"})
        self.worker().run(job_id=job, max_steps=2, handle_signals=False)
        state = self.store.get(job)
        self.assertEqual((state["state"], state["attempt"]), ("generating", 1))
        self.assertAlmostEqual(state["next_run_at"], self.clock() + 42)
        result = self.run_all(self.worker(), job)
        self.assertTrue(result["artifacts"]["model"]["complete"])
        self.assertEqual(len(self.creates("preview")), 1)

    def test_no_credits_or_no_key_is_a_configuration_problem(self):
        job = self.create()
        self.mock.fail_next_post = (402, {"message": "Insufficient credits"}, {})
        result = self.run_all(self.worker(), job)
        self.assertEqual(result["state"], "needs_configuration")
        self.assertIn("Meshy: kredyty lub plan", result["needs"])

        box = Sandbox(overrides={"providers": {"meshy": {"baseUrl": self.mock.url}}}, environ={})
        try:
            other = service.create_jobs(box.config, box.store, BRIEF, approve_credits=30)[0]["id"]
            Worker(box.config, box.store, providers={"meshy": Meshy(box.config)}, handlers={"generating": handlers.generating},
                   now=box.clock, sleep=box.clock.sleep).run(until_idle=True, handle_signals=False)
            self.assertEqual(box.store.get(other)["needs"], ["MESHY_API_KEY"])
        finally:
            box.close()


if __name__ == "__main__":
    unittest.main()
