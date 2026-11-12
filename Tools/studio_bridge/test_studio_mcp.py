import base64
from concurrent.futures import ThreadPoolExecutor
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('studio_mcp', Path(__file__).with_name('studio_mcp.py'))
s = importlib.util.module_from_spec(spec)
spec.loader.exec_module(s)


class BridgeTests(unittest.TestCase):
    def setUp(self):
        parent = s.ROOT / 'work/studio-bridge-tests'
        parent.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=parent)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.box = s.Mailbox(self.root)

    def test_store_is_not_delivery_and_real_ack_changes_status(self):
        message = self.box.send('codex','claude','HELLO','Read-only handshake','T',message_id='m1')
        self.assertFalse(self.box.receive('codex')['outgoing'][0]['acknowledged'])
        self.assertEqual(self.box.receive('claude')['pending'][0]['id'], message['id'])
        self.box.send('claude','codex','ACK','Received','T',reply_to=message['id'])
        self.assertTrue(self.box.receive('codex')['outgoing'][0]['acknowledged'])
        self.assertEqual(self.box.receive('claude')['pending'], [])

    def test_duplicate_id_is_idempotent(self):
        first = self.box.send('codex','claude','STATUS','Same','T',message_id='same')
        second = self.box.send('codex','claude','STATUS','Same','T',message_id='same')
        self.assertEqual(first,second)
        self.assertEqual(len(self.box.messages()),1)

    def test_conflicting_duplicate_is_preserved(self):
        self.box.send('codex','claude','STATUS','First','T',message_id='same')
        with self.assertRaises(ValueError):
            self.box.send('codex','claude','STATUS','Different','T',message_id='same')
        self.assertEqual(self.box.messages()[0]['text'],'First')

    def test_concurrent_publish_never_overwrites(self):
        self.box.prepare()
        with ThreadPoolExecutor(max_workers=4) as pool:
            results = list(pool.map(lambda _: self.box.send('codex','claude','STATUS','Same','T',message_id='concurrent'), range(8)))
        self.assertTrue(all(row == results[0] for row in results))
        self.assertEqual(len(self.box.messages()),1)

    def test_sender_cannot_ack_own_message(self):
        message = self.box.send('codex','claude','HELLO','Hi','T')
        with self.assertRaises(ValueError):
            self.box.send('codex','claude','ACK','Forged','T',reply_to=message['id'])

    def test_ack_needs_original_and_same_task(self):
        message = self.box.send('codex','claude','HELLO','Hi','T')
        for reply, task in (('absent','T'),(message['id'],'Other')):
            with self.subTest(reply=reply,task=task), self.assertRaises(ValueError):
                self.box.send('claude','codex','ACK','Read',task,reply_to=reply)

    def test_ack_of_ack_is_rejected(self):
        original = self.box.send('codex','claude','HELLO','Hi','T')
        ack = self.box.send('claude','codex','ACK','Read','T',reply_to=original['id'])
        with self.assertRaises(ValueError):
            self.box.send('codex','claude','ACK','No loop','T',reply_to=ack['id'])

    def test_path_traversal_in_id_is_rejected(self):
        with self.assertRaises(ValueError):
            self.box.send('codex','claude','STATUS','Hi','T',message_id='../outside')

    def test_corrupt_record_is_preserved_and_blocks(self):
        self.box.prepare()
        path = self.box.folder / 'bad.json'
        path.write_text('{broken',encoding='utf-8')
        with self.assertRaises(ValueError):
            self.box.receive('claude')
        self.assertEqual(path.read_text(),'{broken')

    def test_mcp_initialize_lists_only_scoped_tools(self):
        result = s.handle(self.root,'claude',{'jsonrpc':'2.0','id':1,'method':'initialize'})
        self.assertEqual(result['result']['serverInfo']['name'],'morza-bogow-studio')
        tools = s.handle(self.root,'claude',{'id':2,'method':'tools/list'})['result']['tools']
        self.assertEqual(len(tools),5)
        self.assertFalse(any('execute' in row['name'] for row in tools))

    def test_tool_cannot_select_another_sender_or_unknown_argument(self):
        result = s.handle(self.root,'claude',{'id':1,'method':'tools/call','params':{
            'name':'studio_send','arguments':{'kind':'HELLO','text':'Hi','taskId':'T','sender':'codex'}}})
        self.assertTrue(result['result']['isError'])

    def test_invalid_stored_ack_does_not_create_a_delivery_confirmation(self):
        original = self.box.send('codex','claude','HELLO','Hi','T')
        ack = self.box.send('claude','codex','ACK','Read','T',reply_to=original['id'])
        path = self.box.folder / (ack['id']+'.json')
        ack['taskId'] = 'Other'
        path.write_text(json.dumps(ack),encoding='utf-8')
        with self.assertRaisesRegex(ValueError,'stored ACK'):
            self.box.receive('codex')

    def test_duplicate_keys_in_a_stored_record_are_blocked(self):
        original = self.box.send('codex','claude','HELLO','Hi','T')
        path = self.box.folder / (original['id']+'.json')
        text = path.read_text(encoding='utf-8')
        path.write_text(text[:-1]+',"kind":"ACK"}',encoding='utf-8')
        with self.assertRaisesRegex(ValueError,'Duplicate'):
            self.box.receive('claude')

    def test_private_or_unapproved_host_is_blocked(self):
        for url in ('http://openaccess-api.clevelandart.org/','https://127.0.0.1/',
                    'https://openaccess-api.clevelandart.org.evil.test/','https://user:pw@openaccess-api.clevelandart.org/'):
            with self.subTest(url=url), self.assertRaises(ValueError):
                s.allowed_url(url)

    def test_museum_preview_is_an_image_block_and_stays_unreviewed(self):
        row = {'id':1,'title':'Fixture','share_license_status':'CC0','images':{'web':{
            'url':'https://openaccess-cdn.clevelandart.org/fixture.jpg'}}}
        with patch.object(s,'museum_get',return_value=row), patch.object(s,'fetch',return_value=(b'fixture-image','image/jpeg')):
            result = s.call(self.root,'claude','references_image',{'objectId':1})
        metadata = json.loads(result[0]['text'])
        self.assertEqual(metadata['status'],'IMAGE_PROVIDED_NOT_REVIEWED')
        self.assertEqual(result[1]['type'],'image')
        self.assertEqual(base64.b64decode(result[1]['data']),b'fixture-image')

    def test_copyrighted_object_does_not_fetch_an_image(self):
        with patch.object(s,'museum_get',return_value={'id':1,'share_license_status':'Copyrighted'}), patch.object(s,'fetch') as fetch:
            with self.assertRaises(ValueError):
                s.call(self.root,'claude','references_image',{'objectId':1})
            fetch.assert_not_called()

    def test_stdio_round_trip_is_utf8_even_with_a_legacy_windows_encoding(self):
        text = 'Żeglarze: Ἑλλάς — 🌊'
        request = {'jsonrpc':'2.0','id':1,'method':'tools/call','params':{'name':'studio_send','arguments':{
            'kind':'HELLO','text':text,'taskId':'T'}}}
        env = dict(os.environ, PYTHONIOENCODING='cp1250')
        process = subprocess.run([sys.executable,'-B',str(Path(s.__file__)),'--root',str(self.root),
                                  '--endpoint','claude','mcp'],input=(json.dumps(request,ensure_ascii=False)+'\n').encode('utf-8'),
                                 capture_output=True,env=env,timeout=15)
        self.assertEqual(process.returncode,0,process.stderr.decode('utf-8',errors='replace'))
        response = json.loads(process.stdout.decode('utf-8'))
        self.assertFalse(response['result']['isError'])
        self.assertEqual(self.box.receive('codex')['pending'][0]['text'],text)


if __name__ == '__main__':
    unittest.main()
