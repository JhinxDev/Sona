"""Trust-boundary checks for the opt-in synthetic cleanup evaluator."""
import hashlib, importlib.util, json, os, sys, tempfile, time, types, unittest
from pathlib import Path
sys.dont_write_bytecode=True

RUNNER=Path(__file__).with_name('cleanup_evaluation.py')
MODULE=None
if RUNNER.is_file():
    SPEC=importlib.util.spec_from_file_location('cleanup_evaluation',RUNNER)
    MODULE=importlib.util.module_from_spec(SPEC); SPEC.loader.exec_module(MODULE)

class EvaluationBoundaryTests(unittest.TestCase):
    def setUp(self):
        self.assertIsNotNone(MODULE,'The bounded cleanup evaluator must exist')
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.cleanup_temp)
        self.root=Path(self.temp.name)
        self.model=self.root/'model.gguf';self.model.write_bytes(b'synthetic model')
        self.runtime=self.root/'runtime';self.runtime.mkdir()
        self.server=self.runtime/'llama-server.exe';self.server.write_bytes(b'synthetic server')
        self.manifest=self.runtime/'integrity.json'
        self.manifest.write_text(json.dumps({'Runtime':'synthetic','Files':[{'Path':'llama-server.exe','Bytes':self.server.stat().st_size,'Sha256':hashlib.sha256(self.server.read_bytes()).hexdigest()}]}))
        self.prepared=self.root/'prepared.json'
        self.prepared.write_text(json.dumps({'terms':['Sona'],'cases':[{'name':'minimal','input':'um keep Sona','prompt':'app: Codex\ntone: formal\ntext: um keep [TERM_0]\nout:\n','protectedText':'um keep [TERM_0]'}]}))
        self.args=types.SimpleNamespace(model=self.model,model_sha256=hashlib.sha256(self.model.read_bytes()).hexdigest(),runtime_dir=self.runtime,runtime_manifest=self.manifest,prepared_cases=self.prepared,output=self.root/'result.json',prompt_format='plain',system_prompt=None,repeats=3,deadline_seconds=240,backend='GPU',model_id='fixture',model_revision='synthetic')
    def cleanup_temp(self):
        # Windows may release a terminated process's working-directory handle a
        # moment after its job accounting reaches zero. Retry this exact fixture.
        for attempt in range(21):
            try:self.temp.cleanup();return
            except PermissionError:
                if attempt==20:raise
                time.sleep(.05)
    def test_rejects_model_digest_mismatch(self):
        self.args.model_sha256='0'*64
        with self.assertRaisesRegex(ValueError,'model.*digest'):MODULE.validate_inputs(self.args)
    def test_rejects_changed_runtime_file(self):
        self.server.write_bytes(b'changed server')
        with self.assertRaisesRegex(ValueError,'runtime.*integrity'):MODULE.validate_inputs(self.args)
    def test_refuses_relative_model_paths(self):
        self.args.model=Path('model.gguf')
        with self.assertRaisesRegex(ValueError,'absolute'):MODULE.validate_inputs(self.args)
    def test_preserves_existing_result(self):
        self.args.output.write_text('earlier result')
        with self.assertRaisesRegex(ValueError,'existing'):MODULE.validate_inputs(self.args)
        self.assertEqual(self.args.output.read_text(),'earlier result')
    def test_refuses_unbounded_or_owner_sized_fixture(self):
        prepared=json.loads(self.prepared.read_text());prepared['cases'][0]['input']='x'*4001;self.prepared.write_text(json.dumps(prepared))
        with self.assertRaisesRegex(ValueError,'fixture'):MODULE.validate_inputs(self.args)
    def test_refuses_missing_chat_prompt(self):
        self.args.prompt_format='chatml'
        with self.assertRaisesRegex(ValueError,'system prompt'):MODULE.validate_inputs(self.args)
    def test_rejects_manifest_traversal(self):
        self.manifest.write_text(json.dumps({'Runtime':'synthetic','Files':[{'Path':'../outside.exe','Bytes':1,'Sha256':'0'*64}]}))
        with self.assertRaisesRegex(ValueError,'runtime.*path'):MODULE.validate_inputs(self.args)
    def test_rejects_unlisted_runtime_payload(self):
        (self.runtime/'extra.dll').write_bytes(b'extra')
        with self.assertRaisesRegex(ValueError,'file set'):MODULE.validate_inputs(self.args)
    @unittest.skipUnless(os.name=='nt','Windows job acceptance')
    def test_deadline_kills_only_owned_worker_and_descendant(self):
        marker=self.root/'child.pid'
        child=self.root/'child.py'
        child.write_text('import pathlib, subprocess, sys, time\np=subprocess.Popen([sys.executable,"-c","import time; time.sleep(30)"])\npathlib.Path(sys.argv[1]).write_text(str(p.pid))\ntime.sleep(30)\n')
        result=MODULE.run_bounded([sys.executable,str(child),str(marker)],timeout_seconds=1,cwd=str(self.root))
        self.assertTrue(result['timedOut']);self.assertEqual(result['remainingJobProcesses'],0)
        self.assertGreaterEqual(result['totalJobProcesses'],2)

if __name__=='__main__':unittest.main()
