import ctypes
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]


class InstallerTests(unittest.TestCase):
    def setUp(self):
        parent = ROOT/'work/studio-bridge-installer-tests'
        parent.mkdir(parents=True,exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=parent)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        script = self.root/'Tools/studio_bridge/studio_mcp.py'
        script.parent.mkdir(parents=True)
        script.write_text('# isolated installer fixture\n',encoding='utf-8')
        self.original = {'mcpServers':{'existing':{'type':'stdio','command':'fixture',
            'args':['2020-01-02T03:04:05.1234567+05:30'], 'env':{'TEXT':'2020-01-02T03:04:05Z'}}},
            'custom':{'value':'2020-01-02T03:04:05.1234567-07:00'}}
        self.path = self.root/'.mcp.json'
        self.path.write_text(json.dumps(self.original),encoding='utf-8')

    def run_installer(self, apply=False):
        command = [shutil.which('pwsh'),'-NoLogo','-NoProfile','-NonInteractive','-File',str(ROOT/'Wdroz-MCPStudia.ps1'),
                   '-Python',sys.executable,'-ProjectRoot',str(self.root)]
        if apply:
            command.append('-Apply')
        return subprocess.run(command,capture_output=True,encoding='utf-8',timeout=20)

    def test_default_is_read_only(self):
        before = self.path.read_bytes()
        result = self.run_installer()
        self.assertEqual(result.returncode,0,result.stderr)
        self.assertEqual(json.loads(result.stdout)['status'],'READY_TO_INSTALL')
        self.assertEqual(before,self.path.read_bytes())
        self.assertFalse((self.root/'qa').exists())

    def test_apply_preserves_existing_strings_and_is_idempotent(self):
        result = self.run_installer(True)
        self.assertEqual(result.returncode,0,result.stderr+result.stdout)
        value = json.loads(self.path.read_text(encoding='utf-8'))
        self.assertEqual(value['mcpServers']['existing'],self.original['mcpServers']['existing'])
        self.assertEqual(value['custom'],self.original['custom'])
        self.assertEqual(value['mcpServers']['morza-bogow-studio']['type'],'stdio')
        backups = list((self.root/'work/studio-bridge/config-backups').glob('*/mcp.json'))
        self.assertEqual(len(backups),1)
        self.assertEqual(json.loads(backups[0].read_text()),self.original)
        before = self.path.read_bytes()
        second = self.run_installer(True)
        self.assertEqual(second.returncode,0,second.stderr+second.stdout)
        self.assertEqual(json.loads(second.stdout)['status'],'ALREADY_CONFIGURED_RUNTIME_UNVERIFIED')
        self.assertEqual(before,self.path.read_bytes())

    def test_wrong_existing_transport_is_a_conflict(self):
        result = self.run_installer()
        entry = json.loads(result.stdout)['entry']
        entry['type'] = 'http'
        self.original['mcpServers']['morza-bogow-studio'] = entry
        self.path.write_text(json.dumps(self.original),encoding='utf-8')
        before = self.path.read_bytes()
        result = self.run_installer(True)
        self.assertEqual(result.returncode,2,result.stderr+result.stdout)
        self.assertEqual(before,self.path.read_bytes())

    @unittest.skipUnless(os.name == 'nt','Windows exclusive FileShare fixture')
    def test_active_gate_lock_prevents_installation(self):
        path = self.root/'qa/reports/.gate.lock'
        path.parent.mkdir(parents=True)
        kernel = ctypes.WinDLL('kernel32',use_last_error=True)
        kernel.CreateFileW.argtypes = [ctypes.c_wchar_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_void_p,
                                       ctypes.c_uint32,ctypes.c_uint32,ctypes.c_void_p]
        kernel.CreateFileW.restype = ctypes.c_void_p
        kernel.CloseHandle.argtypes = [ctypes.c_void_p]
        handle = kernel.CreateFileW(str(path),0xC0000000,0,None,4,0x80,None)
        self.assertNotEqual(handle,ctypes.c_void_p(-1).value)
        before = self.path.read_bytes()
        try:
            result = self.run_installer(True)
            self.assertEqual(result.returncode,2,result.stderr+result.stdout)
            self.assertEqual(before,self.path.read_bytes())
        finally:
            kernel.CloseHandle(handle)


if __name__ == '__main__':
    unittest.main()
