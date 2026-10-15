"""Opt-in local synthetic cleanup evaluation; no downloads or live Sona access.

Use Sona.CleanupEvaluation to prepare cases and replay outputs through Sona.Core.
The parent admits a hidden worker to a kill-on-close Windows job before resuming
it, caps each process at 8 GiB, and proves zero remaining owned processes.
"""
from __future__ import annotations
import argparse, collections, ctypes as c, hashlib, json, os, re, secrets, socket
import statistics, subprocess, sys, threading, time, urllib.request
from ctypes import wintypes as w
from pathlib import Path

def sha256(path):
    with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()

def plain_absolute(path):
    path=Path(path)
    if not path.is_absolute():raise ValueError('Use exact absolute local paths.')
    for part in (path,*path.parents):
        if part.exists() and (getattr(part.stat(follow_symlinks=False),'st_file_attributes',0)&0x400 or part.is_symlink()):
            raise ValueError('Linked evaluation paths are refused.')
    return path

def validate_inputs(args):
    for field in ('model','runtime_dir','runtime_manifest','prepared_cases','output'):
        setattr(args,field,plain_absolute(getattr(args,field)))
    if args.output.exists() or args.output.with_suffix('.job.json').exists():raise ValueError('Preserve existing evaluation receipts.')
    if not args.output.parent.is_dir():raise ValueError('Create the output directory explicitly first.')
    if not re.fullmatch(r'[0-9a-fA-F]{64}',args.model_sha256) or not args.model.is_file() or not 1<=args.model.stat().st_size<=8*1024**3 or sha256(args.model)!=args.model_sha256.lower():
        raise ValueError('The supplied model digest or bounded size differs.')
    if args.runtime_manifest.stat().st_size>256*1024:raise ValueError('Oversized runtime integrity manifest.')
    manifest=json.loads(args.runtime_manifest.read_text(encoding='utf-8-sig'))
    files=manifest.get('Files',[])
    if not 1<=len(files)<=512:raise ValueError('Invalid runtime integrity manifest.')
    seen=set()
    for entry in files:
        name=entry.get('Path','')
        if not name or Path(name).is_absolute() or ':' in name or '..' in Path(name).parts or name.lower() in seen:raise ValueError('Unsafe runtime integrity path.')
        seen.add(name.lower());file=plain_absolute(args.runtime_dir/name)
        if not file.is_file() or file.stat().st_size!=entry.get('Bytes') or sha256(file)!=str(entry.get('Sha256','')).lower():raise ValueError('The supplied runtime failed integrity verification.')
    if 'llama-server.exe' not in seen:raise ValueError('The runtime integrity manifest lacks llama-server.exe.')
    actual={str(file.relative_to(args.runtime_dir)).lower() for file in args.runtime_dir.rglob('*') if file.is_file() and file!=args.runtime_manifest}
    if actual!=seen:raise ValueError('The runtime integrity file set differs.')
    if args.prepared_cases.stat().st_size>1024*1024:raise ValueError('Oversized synthetic fixture.')
    fixture=json.loads(args.prepared_cases.read_text(encoding='utf-8-sig'));cases=fixture.get('cases',[])
    terms=fixture.get('terms',[])
    if not isinstance(terms,list) or len(terms)>256 or any(not isinstance(term,str) or not 1<=len(term)<=120 for term in terms):raise ValueError('Invalid synthetic fixture protected terms.')
    if not 1<=len(cases)<=64 or len({case.get('name') for case in cases})!=len(cases):raise ValueError('Invalid synthetic fixture case count.')
    for case in cases:
        if not isinstance(case.get('name'),str) or not 1<=len(case['name'])<=80 or not isinstance(case.get('input'),str) or not 1<=len(case['input'])<=4000 or not isinstance(case.get('prompt'),str) or not 1<=len(case['prompt'])<=24000 or not isinstance(case.get('protectedText'),str):raise ValueError('Invalid bounded synthetic fixture.')
    if not 1<=args.repeats<=3 or not 30<=args.deadline_seconds<=600:raise ValueError('Use one to three repeats and a 30-600 second deadline.')
    if args.prompt_format=='chatml':
        if not args.system_prompt:raise ValueError('ChatML needs an explicit system prompt file.')
        args.system_prompt=plain_absolute(args.system_prompt)
        if not args.system_prompt.is_file() or not 1<=args.system_prompt.stat().st_size<=16000:raise ValueError('Invalid bounded system prompt file.')
    return fixture,manifest

class Basic(c.Structure):
    _fields_=[('process_time',c.c_int64),('job_time',c.c_int64),('flags',w.DWORD),('min_ws',c.c_size_t),('max_ws',c.c_size_t),('active_limit',w.DWORD),('affinity',c.c_size_t),('priority',w.DWORD),('scheduling',w.DWORD)]
class Extended(c.Structure):
    _fields_=[('basic',Basic),('io',c.c_uint64*6),('process_memory',c.c_size_t),('job_memory',c.c_size_t),('peak_process_memory',c.c_size_t),('peak_job_memory',c.c_size_t)]
class Startup(c.Structure):
    _fields_=[('cb',w.DWORD),('reserved',w.LPWSTR),('desktop',w.LPWSTR),('title',w.LPWSTR),('x',w.DWORD),('y',w.DWORD),('x_size',w.DWORD),('y_size',w.DWORD),('x_chars',w.DWORD),('y_chars',w.DWORD),('fill',w.DWORD),('flags',w.DWORD),('show',w.WORD),('reserved_size',w.WORD),('reserved_buffer',c.c_void_p),('stdin',w.HANDLE),('stdout',w.HANDLE),('stderr',w.HANDLE)]
class ProcessInfo(c.Structure):
    _fields_=[('process',w.HANDLE),('thread',w.HANDLE),('pid',w.DWORD),('thread_id',w.DWORD)]
class Accounting(c.Structure):
    _fields_=[('times',c.c_int64*4),('page_faults',w.DWORD),('total',w.DWORD),('active',w.DWORD),('terminated',w.DWORD)]
class Memory(c.Structure):
    _fields_=[('cb',w.DWORD),('PageFaultCount',w.DWORD)]+[(n,c.c_size_t) for n in ('PeakWorkingSetSize','WorkingSetSize','QuotaPeakPagedPoolUsage','QuotaPagedPoolUsage','QuotaPeakNonPagedPoolUsage','QuotaNonPagedPoolUsage','PagefileUsage','PeakPagefileUsage','PrivateUsage')]

def run_bounded(argv,*,timeout_seconds,cwd=None):
    if os.name!='nt' or c.sizeof(c.c_void_p)!=8:raise OSError('Windows x64 is required.')
    if not 0.05<=timeout_seconds<=600:raise ValueError('A finite deadline is required.')
    if not Path(argv[0]).is_absolute():raise ValueError('Use an exact absolute executable path.')
    executable=plain_absolute(Path(argv[0]).resolve())
    if not executable.is_file() or 'windowsapps' in {p.lower() for p in executable.parts}:raise ValueError('Use an existing native executable.')
    if (c.sizeof(Basic),c.sizeof(Extended),c.sizeof(Startup),c.sizeof(ProcessInfo),c.sizeof(Accounting))!=(64,144,104,24,48):raise RuntimeError('Unexpected Windows structure layout.')
    kernel=c.WinDLL('kernel32',use_last_error=True)
    signatures={
      'CreateJobObjectW':([c.c_void_p,w.LPCWSTR],w.HANDLE),'SetInformationJobObject':([w.HANDLE,c.c_int,c.c_void_p,w.DWORD],w.BOOL),
      'AssignProcessToJobObject':([w.HANDLE,w.HANDLE],w.BOOL),'CreateProcessW':([w.LPCWSTR,w.LPWSTR,c.c_void_p,c.c_void_p,w.BOOL,w.DWORD,c.c_void_p,w.LPCWSTR,c.POINTER(Startup),c.POINTER(ProcessInfo)],w.BOOL),
      'ResumeThread':([w.HANDLE],w.DWORD),'WaitForSingleObject':([w.HANDLE,w.DWORD],w.DWORD),'GetExitCodeProcess':([w.HANDLE,c.POINTER(w.DWORD)],w.BOOL),
      'TerminateProcess':([w.HANDLE,w.UINT],w.BOOL),'TerminateJobObject':([w.HANDLE,w.UINT],w.BOOL),'QueryInformationJobObject':([w.HANDLE,c.c_int,c.c_void_p,w.DWORD,c.POINTER(w.DWORD)],w.BOOL),'CloseHandle':([w.HANDLE],w.BOOL)}
    for name,(params,result) in signatures.items():getattr(kernel,name).argtypes=params;getattr(kernel,name).restype=result
    def check(value):
        if not value:raise c.WinError(c.get_last_error())
    job=kernel.CreateJobObjectW(None,None);check(job);pi=ProcessInfo();assigned=False;started=time.monotonic()
    try:
        limits=Extended();limits.basic.flags=0x2000|0x100;limits.process_memory=8*1024**3
        check(kernel.SetInformationJobObject(job,9,c.byref(limits),c.sizeof(limits)))
        si=Startup(cb=c.sizeof(Startup),flags=1,show=0)
        check(kernel.CreateProcessW(str(executable),c.create_unicode_buffer(subprocess.list2cmdline(argv)),None,None,False,0x08000004,None,cwd,c.byref(si),c.byref(pi)))
        check(kernel.AssignProcessToJobObject(job,pi.process));assigned=True
        if kernel.ResumeThread(pi.thread)==0xFFFFFFFF:raise c.WinError(c.get_last_error())
        waited=kernel.WaitForSingleObject(pi.process,max(1,int(timeout_seconds*1000)))
        if waited not in (0,258):raise c.WinError(c.get_last_error())
        timedout=waited==258;code=w.DWORD()
        if not timedout:check(kernel.GetExitCodeProcess(pi.process,c.byref(code)))
        check(kernel.TerminateJobObject(job,124));accounting=Accounting();deadline=time.monotonic()+5
        while True:
            check(kernel.QueryInformationJobObject(job,1,c.byref(accounting),c.sizeof(accounting),None))
            if accounting.active==0:break
            if time.monotonic()>deadline:raise RuntimeError('Owned job cleanup did not settle.')
            time.sleep(.02)
        if kernel.WaitForSingleObject(pi.process,3000)!=0:raise RuntimeError('Owned worker termination was not confirmed.')
        return dict(pid=pi.pid,exitCode=124 if timedout else code.value,timedOut=timedout,elapsedSeconds=round(time.monotonic()-started,3),remainingJobProcesses=accounting.active,totalJobProcesses=accounting.total)
    finally:
        if pi.process and not assigned:kernel.TerminateProcess(pi.process,125)
        kernel.CloseHandle(job)
        for handle in (pi.thread,pi.process):
            if handle:kernel.CloseHandle(handle)

def benchmark(args,fixture,manifest):
    opener=urllib.request.build_opener(urllib.request.ProxyHandler({}));key=secrets.token_hex(32)
    with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
    base=f'http://127.0.0.1:{port}'
    def request(route,payload=None,timeout=20):
        body=None if payload is None else json.dumps(payload,ensure_ascii=False).encode('utf-8')
        req=urllib.request.Request(base+route,data=body,headers={'Authorization':'Bearer '+key,'Content-Type':'application/json'})
        with opener.open(req,timeout=timeout) as response:
            raw=response.read(1024*1024+1)
            if len(raw)>1024*1024:raise ValueError('Response limit exceeded.')
            return json.loads(raw)
    proof=[]
    def drain(stream):
        for line in iter(stream.readline,''):
            if re.search(r'(CUDA\d+.*model buffer size|offloaded \d+/\d+ layers)',line):
                if len(proof)<32:proof.append(line.strip())
    process=None;threads=[];rows=[];startup=None;peak=0;failure=None
    papi=c.WinDLL('psapi',use_last_error=True);papi.GetProcessMemoryInfo.argtypes=[w.HANDLE,c.POINTER(Memory),w.DWORD]
    system=args.system_prompt.read_text(encoding='utf-8-sig') if args.system_prompt else None
    started=time.perf_counter()
    try:
        process=subprocess.Popen([str(args.runtime_dir/'llama-server.exe'),'-m',str(args.model),'-c','4096','-np','1','-t','6','-tb','6','-ngl','99' if args.backend=='GPU' else '0','--host','127.0.0.1','--port',str(port),'--api-key',key,'--log-verbosity','4'],cwd=args.runtime_dir,stdin=subprocess.DEVNULL,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,encoding='utf-8',errors='replace',creationflags=subprocess.CREATE_NO_WINDOW)
        for stream in (process.stdout,process.stderr):
            thread=threading.Thread(target=drain,args=(stream,),daemon=True);thread.start();threads.append(thread)
        deadline=time.monotonic()+45
        while True:
            if process.poll() is not None:raise RuntimeError('Worker exited before readiness.')
            try:
                if request('/health',timeout=1).get('status')=='ok':break
            except (OSError,ValueError):
                if time.monotonic()>deadline:raise TimeoutError('Readiness deadline exceeded.')
                time.sleep(.1)
        startup=round((time.perf_counter()-started)*1000,2)
        if args.backend=='GPU' and not any(re.search(r'offloaded [1-9]\d*/\d+ layers',line) for line in proof):raise RuntimeError('GPU offload was not confirmed.')
        for case in fixture['cases']:
            prompt=case['prompt'] if system is None else '<|im_start|>system\n'+system+'<|im_end|>\n<|im_start|>user\n<dictation>\n'+case['protectedText']+'\n</dictation><|im_end|>\n<|im_start|>assistant\n<think>\n\n</think>\n\n'
            count=len(request('/tokenize',{'content':prompt,'add_special':True})['tokens'])
            if count>2500:raise ValueError('Production prompt token bound exceeded.')
            durations=[];outputs=[];timings=[];truncated=[]
            for _ in range(args.repeats):
                start=time.perf_counter();payload=dict(prompt=prompt,temperature=0,n_predict=1536,cache_prompt=False,seed=1,stream=False)
                if system is not None:payload['stop']=['<|im_end|>','<|im_start|>','<|endoftext|>']
                result=request('/completion',payload);text=result.get('content','').strip()
                if not text or len(text)>6000:raise ValueError('Output text bound exceeded.')
                durations.append(round((time.perf_counter()-start)*1000,2));outputs.append(text);timings.append(result.get('timings'));truncated.append(bool(result.get('truncated')) or result.get('stopped_limit',False) or result.get('stop_type')=='limit')
                memory=Memory();memory.cb=c.sizeof(memory)
                if papi.GetProcessMemoryInfo(w.HANDLE(int(process._handle)),c.byref(memory),c.sizeof(memory)):peak=max(peak,memory.PeakWorkingSetSize)
            rows.append(dict(case=case['name'],input=case['input'],promptTokens=count,outputs=outputs,deterministic=len(set(outputs))==1,milliseconds=durations,medianMs=round(statistics.median(durations),2),timings=timings,truncated=any(truncated)))
    except Exception as error:failure=type(error).__name__+': '+str(error)
    finally:
        if process and process.poll() is None:
            try:process.terminate();process.wait(timeout=5)
            except (OSError,subprocess.TimeoutExpired):failure=failure or 'Worker termination could not be confirmed.'
        for thread in threads:thread.join(timeout=2)
        worker_exited=process is None or process.poll() is not None
        if process:
            for stream in (process.stdout,process.stderr):
                if worker_exited:stream.close()
        receipt=dict(model=args.model_id,revision=args.model_revision,sha256=args.model_sha256.lower(),modelBytes=args.model.stat().st_size,runtime=manifest.get('Runtime','unknown'),runtimeManifestSha256=sha256(args.runtime_manifest),serverSha256=sha256(args.runtime_dir/'llama-server.exe'),preparedCasesSha256=sha256(args.prepared_cases),inputOrigin=fixture.get('inputOrigin','caller-supplied'),runnerSha256=sha256(Path(__file__)),systemPromptSha256=sha256(args.system_prompt) if args.system_prompt else None,terms=fixture['terms'],backend=args.backend,threads=6,context=4096,greedy=True,cachePrompt=False,repeats=args.repeats,startupReadyMs=startup,peakWorkingSetMiB=round(peak/1024**2,2),cudaProof=proof,samples=rows,failure=failure,workerExited=worker_exited,microphoneOpened=False,liveAppChanged=False)
        with args.output.open('x',encoding='utf-8') as output:json.dump(receipt,output,ensure_ascii=False,indent=2)
    return 1 if failure else 0

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('model','runtime-dir','runtime-manifest','prepared-cases','output'):parser.add_argument('--'+name,type=Path,required=True)
    for name in ('model-sha256','model-id','model-revision'):parser.add_argument('--'+name,required=True)
    parser.add_argument('--backend',choices=['GPU','CPU'],required=True)
    parser.add_argument('--prompt-format',choices=['plain','chatml'],required=True)
    parser.add_argument('--system-prompt',type=Path);parser.add_argument('--repeats',type=int,default=3)
    parser.add_argument('--deadline-seconds',type=int,default=240)
    parser.add_argument('--internal-worker',action='store_true',help=argparse.SUPPRESS)
    args=parser.parse_args();fixture,manifest=validate_inputs(args)
    if args.internal_worker:return benchmark(args,fixture,manifest)
    result=run_bounded([sys.executable,str(Path(__file__).resolve()),'--internal-worker',*sys.argv[1:]],timeout_seconds=args.deadline_seconds,cwd=str(args.output.parent))
    with args.output.with_suffix('.job.json').open('x',encoding='utf-8') as output:json.dump(result,output,indent=2)
    print(json.dumps(result));return result['exitCode']

if __name__=='__main__':
    try:raise SystemExit(main())
    except (ValueError,OSError,KeyError,TypeError) as error:
        print('Evaluation refused: '+str(error),file=sys.stderr);raise SystemExit(2)
