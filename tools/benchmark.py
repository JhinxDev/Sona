"""One bounded local candidate pass over a caller-owned corpus; never opens a microphone."""
import argparse, ctypes, hashlib, json, re, subprocess, sys, time, wave
from pathlib import Path

class Memory(ctypes.Structure):
    _fields_ = [("cb", ctypes.c_ulong), ("faults", ctypes.c_ulong)] + [(x, ctypes.c_size_t) for x in ("peak_ws", "ws", "peak_paged", "paged", "peak_nonpaged", "nonpaged", "pagefile", "peak_pagefile", "private")]

def words(text):
    import re
    return re.findall(r"\w+", text.lower())

def wer(expected, actual):
    a, b = words(expected), words(actual)
    row = list(range(len(b) + 1))
    for i, x in enumerate(a, 1):
        new = [i]
        for j, y in enumerate(b, 1):
            new.append(min(new[-1]+1, row[j]+1, row[j-1]+(x != y)))
        row = new
    return row[-1] / max(1, len(a))

def read_result(path):
    result = json.loads(path.read_text(encoding='utf-8'))
    return ''.join(x['text'] for x in result['transcription']).strip(), result.get('result', {}).get('language')

def gpu_memory(monitor):
    if not monitor: return None
    result = subprocess.run([str(monitor), '--query-gpu=memory.used', '--format=csv,noheader,nounits', '-i', '0'], capture_output=True, text=True, timeout=3, creationflags=subprocess.CREATE_NO_WINDOW)
    if result.returncode: raise RuntimeError('GPU monitor failed')
    return int(result.stdout.strip())

def rescore_saved(args, cases, model):
    if args.output.resolve() == args.rescore.resolve(): raise ValueError('Retain the original evaluation evidence in a different output file')
    result = json.loads(args.rescore.read_text(encoding='utf-8'))
    if result['model_sha256'] != hashlib.sha256(model.read_bytes()).hexdigest(): raise ValueError('Saved evaluation model differs')
    expected = {case['Name']: case['Text'] for case in cases}
    for row in result['rows']:
        name = row['case']
        if name not in expected or not re.fullmatch(r'[A-Za-z0-9_-]+', name): raise ValueError('Unknown or unsafe saved corpus case')
        if row['audio_sha256'] != hashlib.sha256((args.corpus/(name+'.wav')).read_bytes()).hexdigest(): raise ValueError('Saved evaluation audio differs')
        row['actual'], row['detected_language'] = read_result(args.corpus/(name+'-result.json'))
        row['expected'] = expected[name]
        rate = wer(row['expected'], row['actual']); row['reference_words'] = len(words(row['expected'])); row['word_errors'] = round(rate * row['reference_words']); row['word_error_rate'] = round(rate,3)
    result['text_encoding'] = 'utf-8'; result['rescored_saved_outputs_only'] = True
    args.output.write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding='utf-8')
    print(json.dumps({'rescored':len(result['rows']),'output':str(args.output),'recognition_runs':0},ensure_ascii=False))

def main():
    sys.stdout.reconfigure(encoding='utf-8')
    parser=argparse.ArgumentParser(); parser.add_argument('--corpus', type=Path, required=True); parser.add_argument('--runtime', type=Path, required=True); parser.add_argument('--output', type=Path, required=True); parser.add_argument('--rescore',type=Path); parser.add_argument('--beam',type=int,default=1); parser.add_argument('--best-of',type=int,default=1); parser.add_argument('--cli',type=Path); parser.add_argument('--model',type=Path); parser.add_argument('--gpu',action='store_true'); parser.add_argument('--gpu-monitor',type=Path)
    args=parser.parse_args(); cases=json.loads((args.corpus/'cases.json').read_text(encoding='utf-8-sig')); rows=[]
    cli=args.cli or args.runtime/'whisper/Release/whisper-cli.exe'; model=args.model or args.runtime/'models/ggml-small-q5_1.bin'
    if not (1 <= args.beam <= 5 and 1 <= args.best_of <= 5): raise ValueError('Bounded search supports one through five candidates')
    if args.output.exists(): raise ValueError('Preserve existing evaluation output; choose a new file')
    if args.rescore: rescore_saved(args,cases,model); return
    if args.gpu and not args.gpu_monitor: raise ValueError('GPU comparisons require explicit device memory monitoring')
    for case in cases:
        if not re.fullmatch(r'[A-Za-z0-9_-]+',case['Name']): raise ValueError('Corpus names must be safe simple identifiers')
        audio=args.corpus/(case['Name']+'.wav'); result=args.corpus/(case['Name']+'-result')
        if result.with_suffix('.json').exists(): raise ValueError('Preserve existing recognition evidence; choose a fresh corpus output directory')
        command=[str(cli),'-m',str(model),'-f',str(audio),'-l',case.get('Language','auto'),'-t','6','-bs',str(args.beam),'-bo',str(args.best_of),'-nf','-sns','-oj','-of',str(result),'--prompt','Codex, Jhinx, GitHub, Nextcloud, PowerShell']
        if not args.gpu: command.append('-ng')
        gpu_before=gpu_memory(args.gpu_monitor); gpu_peak=gpu_before; next_gpu=0
        started=time.perf_counter(); peak=0
        with (args.corpus/(case['Name']+'.stdout.log')).open('w',encoding='utf-8') as out, (args.corpus/(case['Name']+'.stderr.log')).open('w',encoding='utf-8') as err:
            process=subprocess.Popen(command, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
            try:
                while process.poll() is None:
                    memory=Memory(); memory.cb=ctypes.sizeof(memory)
                    if ctypes.windll.psapi.GetProcessMemoryInfo(ctypes.c_void_p(int(process._handle)),ctypes.byref(memory),memory.cb): peak=max(peak,memory.peak_ws)
                    if time.perf_counter()-started>90: raise TimeoutError('ASR exceeded 90 seconds')
                    if args.gpu_monitor and time.perf_counter() >= next_gpu:
                        gpu_peak=max(gpu_peak,gpu_memory(args.gpu_monitor)); next_gpu=time.perf_counter()+.5
                    time.sleep(.05)
            finally:
                if process.poll() is None: process.kill(); process.wait(timeout=5)
        if process.returncode: raise RuntimeError(f"ASR failed for {case['Name']}: {process.returncode}")
        elapsed=time.perf_counter()-started; gpu_after=gpu_memory(args.gpu_monitor)
        backend_lines=[line for line in (args.corpus/(case['Name']+'.stderr.log')).read_text(encoding='utf-8',errors='replace').splitlines() if 'CUDA' in line or 'GPU' in line]
        raw, language=read_result(result.with_suffix('.json'))
        with wave.open(str(audio),'rb') as wav: seconds=wav.getnframes()/wav.getframerate()
        rate=wer(case['Text'],raw); reference_words=len(words(case['Text']))
        rows.append({'case':case['Name'],'expected':case['Text'],'actual':raw,'detected_language':language,'seconds':round(seconds,3),'elapsed':round(elapsed,3),'peak_worker_mib':round(peak/1048576,2),'device_used_mib_before':gpu_before,'device_used_mib_peak':gpu_peak,'device_used_mib_after':gpu_after,'backend_log':backend_lines,'word_error_rate':round(rate,3),'reference_words':reference_words,'word_errors':round(rate*reference_words),'audio_sha256':hashlib.sha256(audio.read_bytes()).hexdigest(),'worker_exited':process.poll() is not None})
    args.output.write_text(json.dumps({'model_sha256':hashlib.sha256(model.read_bytes()).hexdigest(),'cli_sha256':hashlib.sha256(cli.read_bytes()).hexdigest(),'gpu_requested':args.gpu,'gpu_measurement':'Device-wide samples every 0.5 seconds; includes other workloads, not per-process attribution' if args.gpu_monitor else None,'threads':6,'beam':args.beam,'best_of':args.best_of,'rows':rows},indent=2,ensure_ascii=False),encoding='utf-8')
    print(json.dumps(rows,ensure_ascii=False))
if __name__=='__main__': main()
