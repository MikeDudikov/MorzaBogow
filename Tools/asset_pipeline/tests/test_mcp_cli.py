import contextlib
import io
import json
import os
import unittest

from asset_pipeline.cli.main import main
from asset_pipeline.mcp.server import TOOLS, Server, serve
from asset_pipeline.tests.support import Sandbox

GOOD = {"id": "ship_cutter_mcp", "category": "ship", "shipClass": "Cutter", "description": "A small fast boat with one square sail."}


class McpTests(unittest.TestCase):
    def setUp(self):
        self.box = Sandbox()
        self.server = Server(self.box.config)

    def tearDown(self):
        self.server.store.close()
        self.box.close()

    def call(self, name, arguments):
        reply = self.server.handle({"jsonrpc": "2.0", "id": 5, "method": "tools/call", "params": {"name": name, "arguments": arguments}})
        result = reply["result"]
        return result["isError"], result["content"][0]["text"]

    def test_handshake_lists_tools_and_none_of_them_spends_money(self):
        reply = self.server.handle({"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {"protocolVersion": "2025-06-18"}})
        self.assertEqual(reply["result"]["protocolVersion"], "2025-06-18")
        self.assertIn("tools", reply["result"]["capabilities"])
        names = [t["name"] for t in self.server.handle({"jsonrpc": "2.0", "id": 2, "method": "tools/list"})["result"]["tools"]]
        self.assertIn("assets_create_job", names)
        self.assertFalse(any("approve" in n for n in names), "Credits are approved by a person in a terminal only.")
        self.assertEqual(len(names), len(TOOLS))

    def test_create_job_reports_brief_problems_or_the_cost(self):
        failed, text = self.call("assets_create_job", {"brief": {"id": "Bad", "category": "ship"}})
        self.assertFalse(failed)
        self.assertIn("problems", json.loads(text))
        failed, text = self.call("assets_create_job", {"brief": GOOD})
        jobs = json.loads(text)["jobs"]
        self.assertEqual(jobs[0]["estimated_credits"], 30)
        self.assertTrue(jobs[0]["approval_needed"])
        failed, text = self.call("assets_job_status", {"job": jobs[0]["id"][:20]})
        self.assertFalse(failed)
        self.assertEqual(json.loads(text)["state"], "queued")

    def test_unknown_jobs_are_tool_errors_not_crashes(self):
        failed, text = self.call("assets_job_status", {"job": "nothing"})
        self.assertTrue(failed)

    def test_serve_reads_and_writes_one_json_message_per_line(self):
        requests = "\n".join(json.dumps(m) for m in (
            {"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {}},
            {"jsonrpc": "2.0", "method": "notifications/initialized"},
            {"jsonrpc": "2.0", "id": 2, "method": "ping"},
            {"jsonrpc": "2.0", "id": 3, "method": "nope"},
        )) + "\n"
        out = io.StringIO()
        serve(self.box.config, stdin=io.StringIO(requests), stdout=out)
        replies = [json.loads(line) for line in out.getvalue().splitlines()]
        self.assertEqual([r["id"] for r in replies], [1, 2, 3])
        self.assertEqual(replies[2]["error"]["code"], -32601)


class CliTests(unittest.TestCase):
    def setUp(self):
        self.box = Sandbox()
        os.environ["AP_ROOT"] = str(self.box.dir)
        self.brief = self.box.dir / "brief.json"
        self.brief.write_text(json.dumps(dict(GOOD, generation={"provider": "blockout"})), encoding="utf-8")

    def tearDown(self):
        os.environ.pop("AP_ROOT", None)
        self.box.close()

    def run_cli(self, *args):
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = main(list(args))
        return code, out.getvalue(), err.getvalue()

    def test_create_status_and_approve(self):
        code, out, _ = self.run_cli("create", str(self.brief))
        self.assertEqual(code, 0)
        job = out.split()[0]
        self.assertIn("szacunek 0 kr.", out)
        code, out, _ = self.run_cli("status", job)
        self.assertIn("[queued]", out)
        code, out, _ = self.run_cli("approve", job, "--credits", "15")
        self.assertIn("15 kr.", out)
        code, out, _ = self.run_cli("status", job, "--json")
        self.assertEqual(json.loads(out)["credits"]["approved"], 15)

    def test_a_bad_brief_lists_its_problems(self):
        self.brief.write_text(json.dumps({"id": "x", "category": "ship"}), encoding="utf-8")
        code, _, err = self.run_cli("create", str(self.brief))
        self.assertEqual(code, 2)
        self.assertIn("shipClass", err)

    def test_a_stage_needs_the_results_of_earlier_stages(self):
        _, out, _ = self.run_cli("create", str(self.brief))
        code, _, err = self.run_cli("import", out.split()[0], "--no-run")
        self.assertEqual(code, 2)
        self.assertIn("wymaga", err)


if __name__ == "__main__":
    unittest.main()
