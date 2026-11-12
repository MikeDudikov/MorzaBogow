import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest
import struct
import zlib

MODULE = Path(__file__).resolve().parents[1] / 'verify.py'
spec = importlib.util.spec_from_file_location('studio_verify', MODULE)
v = importlib.util.module_from_spec(spec)
spec.loader.exec_module(v)


def xml(names=('A', 'B'), result='Passed'):
    cases = ''.join(f'<test-case fullname="{name}" result="{result}" asserts="1"/>' for name in names)
    return f'<test-run result="Passed" total="{len(names)}" passed="{len(names)}" failed="0" skipped="0" inconclusive="0">{cases}</test-run>'


class EvidenceTests(unittest.TestCase):
    def setUp(self):
        # Keep fixtures in work/, outside the existing quality-gate snapshot.
        parent = Path(os.environ.get('STUDIO_TEST_TMP', Path(__file__).resolve().parents[3] / 'work' / 'studio-quality-tests'))
        parent.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix='fixture-', dir=parent)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def write(self, name, data):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(data, encoding='utf-8')
        return {'path': name, 'sha256': v.sha(path)}

    def dump(self, name, data):
        self.write(name, json.dumps(data))
        return self.root / name

    def regression_fixture(self):
        runs = []
        for profile in ('EditMode', 'PlayMode'):
            data = self.write(f'run/{profile}.xml', xml())
            log = self.write(f'run/{profile}.log', 'Unity completed')
            runs.append({'profile': profile, 'exitCode': 0, 'logSha256': log['sha256'],
                         'results': {'sha256': data['sha256'], 'names': ['A', 'B'], 'total': 2, 'passed': 2}})
        baseline = self.dump('baseline.json', {'schemaVersion': 1, 'profiles': {'EditMode': ['A', 'B'], 'PlayMode': ['A', 'B']}})
        report = self.dump('run/report.json', {'schemaVersion': 1, 'status': 'PASS', 'taskId': 'T', 'sessionId': 'S',
                                             'snapshotSha256': '1234', 'runs': runs})
        return baseline, report

    def test_regression_checks_historical_artifacts_without_claiming_current_state(self):
        b, r = self.regression_fixture()
        result = v.regressions(self.root, b, r)
        self.assertEqual(result['scope'], 'HISTORICAL_ARTIFACTS_ONLY')

    def test_different_snapshot_is_blocked(self):
        b, r = self.regression_fixture()
        with self.assertRaisesRegex(v.Blocked, 'snapshot'):
            v.regressions(self.root, b, r, 'changed')

    def test_changed_xml_is_blocked(self):
        b, r = self.regression_fixture()
        self.write('run/EditMode.xml', xml(('A',)))
        with self.assertRaisesRegex(v.Blocked, 'changed'):
            v.regressions(self.root, b, r)

    def test_missing_baseline_test_is_blocked_even_with_consistent_xml_and_report(self):
        b, r = self.regression_fixture()
        policy = v.load(b)
        policy['profiles']['PlayMode'].append('PreviouslyAccepted')
        self.dump('baseline.json', policy)
        with self.assertRaisesRegex(v.Blocked, 'missing PlayMode'):
            v.regressions(self.root, b, r)

    def test_changed_log_is_blocked(self):
        b, r = self.regression_fixture()
        self.write('run/EditMode.log', 'new content')
        with self.assertRaisesRegex(v.Blocked, 'log was changed'):
            v.regressions(self.root, b, r)

    def test_skipped_case_is_blocked(self):
        p = self.write('tests.xml', xml(result='Skipped'))
        with self.assertRaises(v.Blocked):
            v.nunit(self.root / p['path'])

    def test_empty_test_run_is_blocked(self):
        self.write('tests.xml', xml(names=()))
        with self.assertRaises(v.Blocked):
            v.nunit(self.root / 'tests.xml')

    def test_duplicate_case_is_blocked(self):
        self.write('tests.xml', xml(names=('A', 'A')))
        with self.assertRaises(v.Blocked):
            v.nunit(self.root / 'tests.xml')

    def test_dtd_is_blocked(self):
        self.write('tests.xml', '<!DOCTYPE a [<!ENTITY b "xx">]>' + xml())
        with self.assertRaises(v.Blocked):
            v.nunit(self.root / 'tests.xml')

    def test_parent_traversal_is_blocked(self):
        with self.assertRaises(v.Blocked):
            v.inside(self.root, '../outside')

    def test_windows_absolute_or_ads_path_is_blocked(self):
        for name in ('C:/private/file', 'local.txt:stream'):
            with self.subTest(name=name), self.assertRaises(v.Blocked):
                v.inside(self.root, name)

    def test_duplicate_json_key_is_blocked(self):
        self.write('input.json', '{"x": 1, "x": 2}')
        with self.assertRaises(v.Blocked):
            v.load(self.root / 'input.json')

    def test_nonfinite_json_is_blocked(self):
        self.write('input.json', '{"x": NaN}')
        with self.assertRaises(v.Blocked):
            v.load(self.root / 'input.json')

    def test_review_cannot_be_by_author(self):
        with self.assertRaisesRegex(v.Blocked, 'author'):
            v.review({'reviewerId': 'coder'}, ['coder'], [])

    def test_review_must_cover_actual_artifacts(self):
        with self.assertRaisesRegex(v.Blocked, 'omits'):
            v.review({'reviewerId': 'reviewer', 'verdict': 'PASS', 'notes': 'Inspected', 'reviewedSha256': []},
                     ['coder'], ['ABCD'])

    def physics_fixture(self):
        reference = self.write('reference.csv', 'time_s,value,unit\n0,0,m\n1,1,m\n2,2,m\n')
        actual = self.write('actual.csv', 'time_s,value,unit\n0,0.01,m\n1,1.01,m\n2,2.01,m\n')
        tests = self.write('physics.xml', xml())
        contract = {'required': True, 'evidenceLevel': 'CALIBRATED',
                    'model': dict.fromkeys(('name', 'version', 'equations', 'units', 'parameters', 'validity', 'assumptions'), 'Specified in fixture'),
                    'sources': [{'url': 'https://example.org/paper', 'author': 'Author', 'locator': 'eq 1',
                                 'claim': 'Fixture', 'limitations': 'Fixture only', 'readInSession': 'S',
                                 'access': 'CONTENT_READ', 'kind': 'RESEARCH'}],
                    'numericalTests': ['A'], 'comparisons': {'height': {'reference': reference, 'referenceProducerId': 'lab',
                        'referenceType': 'CALIBRATION', 'provenance': 'fixture', 'unit': 'm', 'minimumSamples': 3,
                        'maxAbsError': 0.02, 'maxRmse': 0.02}}}
        evidence = {'nunit': tests, 'comparisons': {'height': actual}, 'review': {
            'reviewerId': 'reviewer', 'verdict': 'PASS', 'notes': 'Fixture reviewed', 'actualResultInspected': True,
            'reviewedSha256': [tests['sha256'], reference['sha256'], actual['sha256']]}}
        return contract, evidence

    def test_calibration_is_never_labelled_measured_validation(self):
        c, e = self.physics_fixture()
        result = v.physics_gate(self.root, c, e, ['coder'], 'S')
        self.assertEqual(result['evidenceLevel'], 'CALIBRATED')
        self.assertAlmostEqual(result['metrics']['height']['rmse'], .01)

    def test_calibration_cannot_support_claim_of_measured_validation(self):
        c, e = self.physics_fixture()
        c['evidenceLevel'] = 'MEASURED_VALIDATION'
        with self.assertRaisesRegex(v.Blocked, 'No measurement'):
            v.physics_gate(self.root, c, e, ['coder'], 'S')

    def test_missing_comparison_is_blocked(self):
        c, e = self.physics_fixture()
        e['comparisons'] = {}
        with self.assertRaises(v.Blocked):
            v.physics_gate(self.root, c, e, ['coder'], 'S')

    def test_error_above_preapproved_threshold_is_blocked(self):
        c, e = self.physics_fixture()
        c['comparisons']['height']['maxRmse'] = .001
        with self.assertRaisesRegex(v.Blocked, 'comparison failed'):
            v.physics_gate(self.root, c, e, ['coder'], 'S')

    def test_units_are_checked(self):
        c, e = self.physics_fixture()
        e['comparisons']['height'] = self.write('actual.csv', 'time_s,value,unit\n0,0.01,cm\n1,1.01,cm\n2,2.01,cm\n')
        with self.assertRaisesRegex(v.Blocked, 'Unit'):
            v.physics_gate(self.root, c, e, ['coder'], 'S')

    def test_nonfinite_samples_are_blocked(self):
        self.write('bad.csv', 'time_s,value,unit\n0,NaN,m\n1,1,m\n2,2,m\n')
        with self.assertRaises(v.Blocked):
            v.series(self.root / 'bad.csv', 'm')

    def test_unaligned_times_are_blocked(self):
        c, e = self.physics_fixture()
        e['comparisons']['height'] = self.write('actual.csv', 'time_s,value,unit\n0,0.01,m\n1,1.01,m\n3,2.01,m\n')
        with self.assertRaisesRegex(v.Blocked, 'Unaligned'):
            v.physics_gate(self.root, c, e, ['coder'], 'S')

    def test_metadata_only_source_cannot_validate_physics(self):
        c, e = self.physics_fixture()
        c['sources'][0]['access'] = 'ABSTRACT_ONLY'
        with self.assertRaises(v.Blocked):
            v.physics_gate(self.root, c, e, ['coder'], 'S')

    def test_n_a_needs_reason(self):
        with self.assertRaises(v.Blocked):
            v.art_gate(self.root, {'required': False}, {}, ['coder'])

    def png(self, name, colour=0, short=False):
        def chunk(tag, data):
            return struct.pack('>I', len(data)) + tag + data + struct.pack('>I', zlib.crc32(tag+data) & 0xffffffff)
        data = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', 1, 1, 8, 2, 0, 0, 0))
        data += chunk(b'IDAT', zlib.compress(b'\x00' if short else bytes([0, colour, 0, 0]))) + chunk(b'IEND', b'')
        path = self.root / name
        path.write_bytes(data)
        return {'path': name, 'sha256': v.sha(path)}

    def art_fixture(self):
        reference = self.png('ref.png')
        settings = {'scene': 'Fixture', 'seed': 7, 'camera': {'id': 'A'}, 'ticks': [0, 30, 60],
                    'weather': 'calm', 'lighting': 'neutral', 'exposure': 0, 'quality': 'Ultra', 'resolution': [1, 1]}
        contract = {'required': True, 'references': [reference], 'criteria': ['materials','silhouette','motion','readability'],
                    'minimumScore': 3, 'captureSettings': settings, 'motionRequired': True,
                    'minimumDurationSeconds': 2, 'frameTimesSeconds': [0, 1, 2]}
        hashes = [reference['sha256']]
        evidence = {}
        for kind in ('baseline', 'candidate'):
            frames = []
            for index in range(3):
                frame = self.png(f'{kind}-{index}.png', index*20)
                frame['timeSeconds'] = index
                frames.append(frame)
                hashes.append(frame['sha256'])
            path = self.root / (kind + '.mp4')
            path.write_bytes(b'\x00\x00\x00\x18ftypisom' + b'0'*64)
            recording = {'path': path.name, 'sha256': v.sha(path)}
            hashes.append(recording['sha256'])
            evidence[kind] = {'settings': copy.deepcopy(settings), 'frames': frames, 'recording': recording,
                              'recordingPlayedByReviewer': True}
        evidence['review'] = {'reviewerId': 'reviewer', 'verdict': 'PASS', 'notes': 'Fixture attestation, not real visual QA',
            'actualResultInspected': True, 'reviewedSha256': hashes, 'criticalDefects': [],
            'criteria': {key: {'score': 3, 'notes': 'Fixture score'} for key in contract['criteria']}}
        return contract, evidence

    def test_art_gate_validates_records_without_claiming_to_score_art(self):
        c, e = self.art_fixture()
        self.assertEqual(v.art_gate(self.root, c, e, ['coder'])['status'], 'PASS')

    def test_crc_correct_but_truncated_pixel_data_is_blocked(self):
        record = self.png('short.png', short=True)
        with self.assertRaisesRegex(v.Blocked, 'scanlines'):
            v.png(self.root / record['path'])

    def test_different_capture_settings_are_blocked(self):
        c, e = self.art_fixture()
        e['candidate']['settings']['seed'] = 8
        with self.assertRaisesRegex(v.Blocked, 'Unrepeatable'):
            v.art_gate(self.root, c, e, ['coder'])

    def test_different_frame_times_are_blocked(self):
        c, e = self.art_fixture()
        e['candidate']['frames'][-1]['timeSeconds'] = 3
        with self.assertRaisesRegex(v.Blocked, 'times differ'):
            v.art_gate(self.root, c, e, ['coder'])

    def test_absent_recording_is_blocked(self):
        c, e = self.art_fixture()
        del e['candidate']['recording']
        with self.assertRaises(KeyError):
            v.art_gate(self.root, c, e, ['coder'])

    def test_repeated_still_does_not_prove_motion(self):
        c, e = self.art_fixture()
        frames = e['candidate']['frames']
        for frame in frames[1:]:
            frame.update(path=frames[0]['path'], sha256=frames[0]['sha256'])
        with self.assertRaisesRegex(v.Blocked, 'still'):
            v.art_gate(self.root, c, e, ['coder'])

    def test_low_score_cannot_be_compensated_by_other_categories(self):
        c, e = self.art_fixture()
        e['review']['criteria']['motion']['score'] = 2
        with self.assertRaisesRegex(v.Blocked, 'motion'):
            v.art_gate(self.root, c, e, ['coder'])

    def test_reviewer_identity_with_trailing_space_is_blocked(self):
        with self.assertRaisesRegex(v.Blocked, 'identity'):
            v.review({'reviewerId': 'coder '}, ['coder'], [])

    def test_physics_sources_from_a_previous_session_are_blocked(self):
        c, e = self.physics_fixture()
        with self.assertRaisesRegex(v.Blocked, 'different session'):
            v.physics_gate(self.root, c, e, ['coder'], 'new-session')

    def test_zero_assertions_without_current_gate_proof_are_insufficient(self):
        c, e = self.physics_fixture()
        e['nunit'] = self.write('physics.xml', xml().replace('asserts="1"','asserts="0"'))
        with self.assertRaisesRegex(v.Blocked, 'assertions'):
            v.physics_gate(self.root, c, e, ['coder'], 'S')

    def test_unity_zero_counters_need_a_matching_complete_gate_report(self):
        baseline, report_path = self.regression_fixture()
        self.dump('qa/studio/regression-baseline.json',v.load(baseline))
        record = self.write('run/EditMode.xml',xml().replace('asserts="1"','asserts="0"'))
        report = v.load(report_path)
        report['runs'][0]['results']['sha256'] = record['sha256']
        self.dump('run/report.json',report)
        c, e = self.physics_fixture()
        e['nunit'] = record
        e['unityGateReport'] = {'path':'run/report.json','sha256':v.sha(report_path)}
        e['review']['reviewedSha256'] += [record['sha256'],v.sha(report_path)]
        result = v.physics_gate(self.root,c,e,['coder'],'S','1234')
        self.assertEqual(result['status'],'PASS')

    def acceptance_fixture(self, domains=('documentation',)):
        source = self.write('source.txt', 'source fixture')
        contract = {'schemaVersion': 1, 'taskId': 'T', 'sessionId': 'S', 'authors': ['coder'], 'domains': list(domains),
                    'behavior': {key: 'Defined' for key in ('playerGoal','given','when','then','edgeCases','preserve','requirements')},
                    'art': {'required': False, 'reason': 'Documentation-only task'},
                    'physics': {'required': False, 'reason': 'No physical behavior changed'}}
        contract['behavior']['allowedFiles'] = ['source.txt']
        path = self.dump('contract.json', contract)
        evidence = {'schemaVersion': 1, 'taskId': 'T', 'sessionId': 'S', 'snapshotSha256': 'A'*64,
                    'contractSha256': v.sha(path), 'sourceFiles': [source],
                    'scopeReview': {'reviewerId': 'reviewer', 'verdict': 'PASS', 'notes': 'Scope inspected',
                                    'actualResultInspected': True, 'reviewedSha256': [source['sha256'], v.sha(path)]}}
        return path, self.dump('evidence.json', evidence)

    def test_both_gates_can_be_n_a_for_an_independently_classified_docs_task(self):
        c, e = self.acceptance_fixture()
        result = v.acceptance(self.root, c, e, 'A'*64, 'S')
        self.assertEqual(result['gates']['art']['status'], 'N/A')
        self.assertEqual(result['gates']['physics']['status'], 'N/A')

    def test_unknown_domain_cannot_bypass_both_gates(self):
        c, e = self.acceptance_fixture(('phyiscal_animation',))
        with self.assertRaisesRegex(v.Blocked, 'Unknown change domain'):
            v.acceptance(self.root, c, e, 'A'*64, 'S')

    def test_weather_cannot_bypass_physics(self):
        c, e = self.acceptance_fixture(('weather',))
        with self.assertRaisesRegex(v.Blocked, 'Physical domain'):
            v.acceptance(self.root, c, e, 'A'*64, 'S')

    def test_changed_source_is_blocked(self):
        c, e = self.acceptance_fixture()
        self.write('source.txt', 'changed')
        with self.assertRaisesRegex(v.Blocked, 'Hash mismatch'):
            v.acceptance(self.root, c, e, 'A'*64, 'S')

    def test_contract_changed_after_review_is_blocked(self):
        c, e = self.acceptance_fixture()
        contract = v.load(c)
        contract['then'] = 'new criterion'
        self.dump('contract.json', contract)
        with self.assertRaisesRegex(v.Blocked, 'Contract changed'):
            v.acceptance(self.root, c, e, 'A'*64, 'S')


if __name__ == '__main__':
    unittest.main()
