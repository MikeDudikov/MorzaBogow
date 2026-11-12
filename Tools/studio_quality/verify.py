"""Read-only evidence checks. No Unity runs, network, hook installation or artistic scoring.

Local review records are attestations, not authenticated signatures. See
docs/RYGOR_PRODUKCJI_I_ODBIORU.md for the required independent review.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
from pathlib import Path, PureWindowsPath
import re
import stat
import struct
import sys
import xml.etree.ElementTree as ET
import zlib


class Blocked(ValueError):
    pass


def require(condition, reason):
    if not condition:
        raise Blocked(reason)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def pairs(items):
    result = {}
    for key, value in items:
        require(key not in result, f"Duplicate JSON key: {key}")
        result[key] = value
    return result


def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=pairs,
                      parse_constant=lambda x: (_ for _ in ()).throw(Blocked(f"Non-finite JSON: {x}")))


def inside(root, value):
    require(isinstance(value, str) and value.strip(), "Empty artifact path")
    require(not PureWindowsPath(value).drive and ":" not in value, "Drive/ADS path forbidden")
    relative = Path(value.replace("\\", "/"))
    require(not relative.is_absolute() and ".." not in relative.parts, "Path escapes project")
    current = root.resolve()
    for part in relative.parts:
        current = current / part
        info = current.lstat()
        require(not stat.S_ISLNK(info.st_mode) and
                not (getattr(info, "st_file_attributes", 0) & 0x400), "Reparse point forbidden")
    require(current.resolve().is_relative_to(root.resolve()) and current.is_file(), "Not a project file")
    return current


def artifact(root, record):
    require(isinstance(record, dict), "Artifact must be an object")
    path = inside(root, record["path"])
    require(sha(path) == record["sha256"].upper(), f"Hash mismatch: {record['path']}")
    require(path.stat().st_size > 0, f"Empty artifact: {record['path']}")
    return path


def texts(value, field):
    require(isinstance(value, list) and value and
            all(isinstance(x, str) and x.strip() for x in value), f"Missing {field}")
    require(len(value) == len(set(value)), f"Duplicate {field}")
    return value


def nunit(path, assertion_tests=()):
    data = path.read_bytes()
    require(b"<!DOCTYPE" not in data.upper() and b"<!ENTITY" not in data.upper(), "DTD forbidden")
    node = ET.fromstring(data)
    require(node.tag == "test-run" and node.get("result") == "Passed", "NUnit run did not pass")
    cases = list(node.iter("test-case"))
    names = [x.get("fullname") for x in cases]
    require(cases and all(names) and len(names) == len(set(names)), "Empty/duplicate NUnit cases")
    require(all(x.get("result") == "Passed" for x in cases), "Non-passing NUnit case")
    require(all(x.get("result") == "Passed" for x in node.iter("test-suite")), "Non-passing suite")
    for key in ("failed", "skipped", "inconclusive"):
        require(int(node.attrib[key]) == 0, f"NUnit {key} != 0")
    for key in ("total", "passed"):
        require(int(node.attrib[key]) == len(cases), f"Inconsistent NUnit {key}")
    for case in cases:
        if case.get('fullname') in assertion_tests:
            require(int(case.get('asserts', '0')) > 0, "Numerical evidence lacks counted assertions")
    return set(names)


def regressions(root, baseline, report_path, expected_snapshot=None):
    policy = load(baseline)
    require(policy["schemaVersion"] == 1, "Unknown regression schema")
    report = load(report_path)
    require(report["schemaVersion"] == 1 and report["status"] == "PASS", "Original gate has no PASS")
    require(report.get("taskId") and report.get("sessionId") and report.get("snapshotSha256"),
            "Missing original gate provenance")
    if expected_snapshot:
        require(report["snapshotSha256"].upper() == expected_snapshot.upper(), "Different snapshot")
    runs = {run["profile"]: run for run in report["runs"]}
    require(len(runs) == len(report["runs"]), "Duplicate profiles")
    summaries = {}
    for profile in ("EditMode", "PlayMode"):
        run = runs[profile]
        require(run["exitCode"] == 0, "Runner failed")
        xml = inside(root, (report_path.parent / f"{profile}.xml").relative_to(root).as_posix())
        log = inside(root, (report_path.parent / f"{profile}.log").relative_to(root).as_posix())
        require(sha(xml) == run["results"]["sha256"].upper(), "NUnit artifact was changed")
        require(log.stat().st_size > 0 and sha(log) == run["logSha256"].upper(), "Runner log was changed")
        names = nunit(xml)
        require(set(run["results"]["names"]) == names and
                run["results"]["total"] == run["results"]["passed"] == len(names), "Report/XML disagree")
        required = set(texts(policy["profiles"][profile], "regression names"))
        missing = sorted(required - names)
        require(not missing, f"Unapproved missing {profile} tests: {missing}")
        summaries[profile] = {"passed": len(names), "baseline": len(required), "added": len(names-required)}
    return {"status": "PASS", "scope": "CURRENT_SNAPSHOT" if expected_snapshot else "HISTORICAL_ARTIFACTS_ONLY",
            "taskId": report["taskId"], "sessionId": report["sessionId"],
            "snapshotSha256": report["snapshotSha256"], "profiles": summaries}


def identity(value):
    require(isinstance(value, str) and re.fullmatch(r"[A-Za-z0-9_./:@-]{1,160}", value), "Invalid identity")
    return value


def review(record, authors, required_hashes):
    reviewer = identity(record["reviewerId"])
    require(reviewer not in [identity(a) for a in authors], "Reviewer is an author")
    require(record["verdict"] == "PASS" and record.get("notes", "").strip(), "Review not accepted")
    require(set(required_hashes) <= set(x.upper() for x in record["reviewedSha256"]), "Review omits artifacts")
    require(record.get("actualResultInspected") is True, "Actual result was not inspected")


def png(path):
    data = path.read_bytes()
    require(len(data) >= 33 and data[:8] == b"\x89PNG\r\n\x1a\n" and data[12:16] == b"IHDR",
            "Capture must be a PNG with IHDR")
    width, height = struct.unpack(">II", data[16:24])
    require(width > 0 and height > 0, "Invalid capture dimensions")
    require(data[8:12] == struct.pack('>I', 13) and data[24] == 8 and data[25] in (2, 6)
            and data[26:29] == b'\x00\x00\x00', "Only RGB/RGBA8 non-interlaced PNG supported")
    require(width * height <= 67108864, "Capture exceeds validation limit")
    offset, compressed, ended = 8, bytearray(), False
    while offset < len(data):
        require(offset + 12 <= len(data), "Truncated PNG chunk")
        length = struct.unpack(">I", data[offset:offset+4])[0]
        tag = data[offset+4:offset+8]
        end = offset + 12 + length
        require(end <= len(data), "Truncated PNG payload")
        payload = data[offset+8:offset+8+length]
        crc = struct.unpack(">I", data[offset+8+length:end])[0]
        require(zlib.crc32(tag + payload) & 0xffffffff == crc, "Invalid PNG CRC")
        if tag == b"IDAT":
            compressed.extend(payload)
        if tag == b"IEND":
            require(length == 0 and end == len(data), "Invalid PNG end")
            ended = True
        offset = end
    require(ended and compressed, "PNG lacks image/end chunks")
    decoder = zlib.decompressobj()
    stride = 1 + width * (3 if data[25] == 2 else 4)
    expected = stride * height
    require(expected <= 268435456, "PNG decoded size exceeds limit")
    decoded = decoder.decompress(bytes(compressed), expected + 1)
    require(decoded and decoder.eof and not decoder.unconsumed_tail and not decoder.unused_data,
            "Invalid or oversized PNG image data")
    require(len(decoded) == expected and all(decoded[y*stride] <= 4 for y in range(height)),
            "Invalid PNG scanlines")
    # Integrity check only; the independent reviewer must inspect actual pixels/motion.
    return [width, height]


def video(path):
    with path.open('rb') as stream:
        header = stream.read(32)
    require(path.stat().st_size > 32 and
            (header[4:8] == b'ftyp' or header[:4] == b'\x1aE\xdf\xa3'),
            "Motion capture needs an MP4/WebM artifact")
    # Container identification is not decoding or duration validation; review must play it.


def art_gate(root, contract, evidence, authors):
    if not contract["required"]:
        require(contract.get("reason", "").strip(), "Art N/A needs a reason")
        return {"status": "N/A", "reason": contract["reason"]}
    refs = contract["references"]
    require(refs, "No agreed artistic references")
    hashes = [sha(artifact(root, r)) for r in refs]
    criteria = texts(contract["criteria"], "art criteria")
    require({"materials", "silhouette", "motion", "readability"} <= set(criteria), "Incomplete art rubric")
    minimum = contract["minimumScore"]
    require(type(minimum) is int and 3 <= minimum <= 4, "Art threshold must be 3 or 4")
    settings = contract["captureSettings"]
    for key in ("scene", "seed", "camera", "ticks", "weather", "lighting", "exposure", "quality", "resolution"):
        require(key in settings and settings[key] is not None, f"Capture lacks {key}")
    require(type(contract['motionRequired']) is bool, "motionRequired must be boolean")
    frame_times = []
    for kind in ("baseline", "candidate"):
        capture = evidence[kind]
        require(capture["settings"] == settings, f"Unrepeatable {kind} capture settings")
        frames = capture["frames"]
        require(len(frames) >= (3 if contract["motionRequired"] else 1), "Missing motion frames")
        times, frame_hashes = [], []
        for frame in frames:
            path = artifact(root, frame)
            require(png(path) == settings["resolution"], "Capture resolution mismatch")
            time = float(frame["timeSeconds"])
            require(math.isfinite(time) and time >= 0, "Invalid frame time")
            times.append(time)
            frame_hashes.append(sha(path))
        require(all(a < b for a, b in zip(times, times[1:])), "Frame times must increase")
        frame_times.append(times)
        if contract["motionRequired"]:
            require(len(set(frame_hashes)) >= 2, "Motion is a repeated still image")
            require(times[-1] - times[0] >= contract["minimumDurationSeconds"] > 0, "Motion interval too short")
            recording = artifact(root, capture["recording"])
            video(recording)
            require(capture.get("recordingPlayedByReviewer") is True, "Recording was not played")
            hashes.append(sha(recording))
        hashes += frame_hashes
    require(frame_times[0] == frame_times[1] and frame_times[0] == contract['frameTimesSeconds'],
            "Baseline/candidate times differ from the contract")
    review(evidence["review"], authors, hashes)
    require(evidence["review"]["criticalDefects"] == [], "Critical artistic defect")
    for key in criteria:
        score = evidence["review"]["criteria"][key]
        require(type(score["score"]) is int and minimum <= score["score"] <= 4 and score["notes"].strip(),
                f"Art criterion failed: {key}")
    return {"status": "PASS", "meaning": "Independent review recorded; script does not judge beauty"}


def series(path, unit):
    with path.open(encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream)
        require(reader.fieldnames == ["time_s", "value", "unit"], "CSV columns: time_s,value,unit")
        rows = list(reader)
    require(len(rows) >= 3, "At least three samples required")
    values = []
    for row in rows:
        require(row["unit"] == unit, "Unit mismatch")
        t, v = float(row["time_s"]), float(row["value"])
        require(math.isfinite(t) and math.isfinite(v), "Non-finite sample")
        values.append((t, v))
    require(all(a[0] < b[0] for a, b in zip(values, values[1:])), "Sample times must increase")
    return values


def physics_gate(root, contract, evidence, authors, session_id=None, snapshot_sha=None):
    if not contract["required"]:
        require(contract.get("reason", "").strip(), "Physics N/A needs a reason")
        return {"status": "N/A", "reason": contract["reason"]}
    level = contract["evidenceLevel"]
    require(level in ("MEASURED_VALIDATION", "CALIBRATED", "RECONSTRUCTION"), "Unknown physics evidence level")
    for field in ("name", "version", "equations", "units", "parameters", "validity", "assumptions"):
        require(contract["model"].get(field), f"Model lacks {field}")
    sources = contract["sources"]
    require(sources, "No model sources")
    for source in sources:
        for key in ("url", "author", "locator", "claim", "limitations", "readInSession"):
            require(source.get(key), f"Source lacks {key}")
        require(source["access"] == "CONTENT_READ" and source["kind"] in
                ("RESEARCH", "STANDARD", "DATASET", "CRITICAL_EDITION"), "Insufficient source access/type")
        require(session_id and source['readInSession'] == session_id, "Source read in a different session")
    tests = texts(contract["numericalTests"], "numerical tests")
    test_path = artifact(root, evidence["nunit"])
    require(set(tests) <= nunit(test_path), "Missing numerical test")
    hashes = [sha(test_path)]
    cases = ET.fromstring(test_path.read_bytes()).iter('test-case')
    zero_asserts = any(case.get('fullname') in tests and int(case.get('asserts', '0')) < 1 for case in cases)
    if zero_asserts:
        # Unity may report zero counted assertions. Its existing gate checks assertion source instead.
        # Accept that proof only from a complete, pinned gate report for this exact snapshot/session/XML.
        require(snapshot_sha, 'Numerical evidence lacks counted assertions or current Unity gate proof')
        proof_path = artifact(root, evidence['unityGateReport'])
        proof = regressions(root, inside(root, 'qa/studio/regression-baseline.json'), proof_path, snapshot_sha)
        require(proof['sessionId'] == session_id, 'Assertion proof belongs to a different session')
        full_report = load(proof_path)
        require(any(run['results']['sha256'].upper() == sha(test_path) for run in full_report['runs']),
                'Numerical XML does not belong to the verified gate')
        hashes.append(sha(proof_path))
    measurements = evidence["comparisons"]
    require(set(measurements) == set(contract["comparisons"]) and measurements, "Missing/extra comparisons")
    metrics = {}
    for key, spec in contract["comparisons"].items():
        record = measurements[key]
        reference = artifact(root, spec["reference"])
        actual = artifact(root, record)
        require(reference != actual, "Reference and actual must be separate artifacts")
        require(spec["provenance"].strip() and identity(spec["referenceProducerId"]) not in authors,
                "No independent reference provenance")
        if level == "MEASURED_VALIDATION":
            require(spec["referenceType"] == "MEASUREMENT", "No measurement: use calibration/reconstruction")
        else:
            require(spec["referenceType"] in ("MEASUREMENT", "ANALYTICAL", "CALIBRATION"), "Unknown reference type")
        ref, act = series(reference, spec["unit"]), series(actual, spec["unit"])
        require(len(ref) == len(act) and [x[0] for x in ref] == [x[0] for x in act], "Unaligned sample times")
        require(len(ref) >= spec["minimumSamples"] >= 3, "Insufficient samples")
        errors = [abs(a[1]-b[1]) for a, b in zip(ref, act)]
        rmse = math.sqrt(math.fsum(x*x for x in errors) / len(errors))
        maximum = max(errors)
        for limit in (spec["maxAbsError"], spec["maxRmse"]):
            require(type(limit) in (int, float) and math.isfinite(limit) and limit >= 0, "Invalid tolerance")
        require(math.isfinite(rmse) and math.isfinite(maximum), "Numerical overflow")
        require(maximum <= spec["maxAbsError"] and rmse <= spec["maxRmse"], f"Physics comparison failed: {key}")
        metrics[key] = {"samples": len(ref), "maxAbsError": maximum, "rmse": rmse, "unit": spec["unit"]}
        hashes += [sha(reference), sha(actual)]
    review(evidence["review"], authors, hashes)
    return {"status": "PASS", "evidenceLevel": level, "metrics": metrics,
            "meaning": "Accepted within the declared model/range; no universal scientific certificate"}


def acceptance(root, contract_path, evidence_path, expected_snapshot, expected_session=None):
    contract, evidence = load(contract_path), load(evidence_path)
    require(not contract.get("template") and not evidence.get("template") and contract["schemaVersion"] == evidence["schemaVersion"] == 1,
            "Template or unknown schema")
    require(evidence["contractSha256"].upper() == sha(contract_path), "Contract changed after review/capture")
    require(contract["taskId"] == evidence["taskId"] and contract["sessionId"] == evidence["sessionId"],
            "Different task/session")
    require(expected_session and contract["sessionId"] == expected_session, "Missing/different active session")
    require(evidence["snapshotSha256"].upper() == expected_snapshot.upper(), "Evidence from another snapshot")
    authors = [identity(a) for a in texts(contract["authors"], "authors")]
    for key in ("playerGoal", "given", "when", "then", "edgeCases", "allowedFiles", "preserve", "requirements"):
        require(contract["behavior"].get(key), f"Behavior contract lacks {key}")
    require(evidence["sourceFiles"], "Missing source file hashes")
    allowed = texts(contract["behavior"]["allowedFiles"], "exact allowed files")
    require(set(allowed) == {f["path"] for f in evidence["sourceFiles"]}, "Source coverage differs from allowed files")
    source_hashes = [sha(artifact(root, f)) for f in evidence["sourceFiles"]]
    review(evidence["scopeReview"], authors, source_hashes + [sha(contract_path)])
    domains = texts(contract["domains"], "change domains")
    physical = {"sea", "wind", "weather", "destruction", "crew", "construction", "extraction",
                "physical_animation", "birds", "vegetation"}
    visible = physical | {"materials", "models", "vfx", "animation", "lighting"}
    known = visible | {"gameplay", "save", "economy", "ai", "commands", "ui_behavior", "documentation", "tooling"}
    require(set(domains) <= known, "Unknown change domain: classify it before acceptance")
    require(not (set(domains) & physical) or contract["physics"]["required"] is True,
            "Physical domain cannot bypass physics gate")
    require(not (set(domains) & visible)
            or contract["art"]["required"] is True, "Visible domain cannot bypass artistic gate")
    require(not (set(domains) & (physical | {'animation', 'vfx'}))
            or contract['art'].get('motionRequired') is True, "Animated domain requires motion recordings")
    results = {}
    for key, function in (("art", art_gate), ("physics", physics_gate)):
        require(type(contract[key]["required"]) is bool, "Gate applicability must be boolean")
        try:
            if key == 'physics':
                results[key] = function(root, contract[key], evidence.get(key, {}), authors, contract['sessionId'], expected_snapshot)
            else:
                results[key] = function(root, contract[key], evidence.get(key, {}), authors)
        except (Blocked, KeyError, TypeError, ValueError, OSError, ET.ParseError, zlib.error, OverflowError, AttributeError, IndexError) as exc:
            results[key] = {"status": "BLOCKED", "reason": str(exc)}
    return {"status": "PASS" if all(r["status"] in ("PASS", "N/A") for r in results.values()) else "BLOCKED",
            "taskId": contract["taskId"], "snapshotSha256": expected_snapshot, "gates": results,
            "limits": "Evidence checks only. Not technical acceptance, a playtest, or authenticated authorship."}


def main():
    for stream in (sys.stdin, sys.stdout, sys.stderr):
        if hasattr(stream, 'reconfigure'):
            stream.reconfigure(encoding='utf-8', errors='strict')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    sub = parser.add_subparsers(dest="action", required=True)
    reg = sub.add_parser("regression")
    reg.add_argument("--baseline", required=True)
    reg.add_argument("--report", required=True)
    reg.add_argument("--expected-snapshot")
    accept = sub.add_parser("acceptance")
    accept.add_argument("--contract", required=True)
    accept.add_argument("--evidence", required=True)
    accept.add_argument("--expected-snapshot", required=True)
    accept.add_argument("--expected-session", required=True)
    args = parser.parse_args()
    try:
        if args.action == "regression":
            result = regressions(args.root, inside(args.root, args.baseline), inside(args.root, args.report),
                                 args.expected_snapshot)
        else:
            result = acceptance(args.root, inside(args.root, args.contract), inside(args.root, args.evidence),
                                args.expected_snapshot, args.expected_session)
        print(json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False))
        return 0 if result["status"] == "PASS" else 2
    except (Blocked, KeyError, TypeError, ValueError, OSError, ET.ParseError, OverflowError, zlib.error, AttributeError, IndexError) as exc:
        print(json.dumps({"status": "BLOCKED", "reason": str(exc)}, ensure_ascii=False))
        return 2


if __name__ == "__main__":
    sys.exit(main())
