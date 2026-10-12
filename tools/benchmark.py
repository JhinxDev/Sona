"""One frozen CPU candidate pass over a caller-owned corpus; never opens a microphone."""
import argparse, ctypes, hashlib, json, subprocess, time, wave
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

def main():
    parser=argparse.ArgumentParser(); parser.add_argument('--corpus', type=Path, required=True); parser.add_argument('--runtime', type=Path, required=True); parser.add_argument('--output', type=Path, required=True)
    args=parser.parse_args(); cases=json.loads((args.corpus/'cases.json').read_text(encoding='utf-8-sig')); rows=[]
    cli=args.runtime/'whisper/Release/whisper-cli.exe'; model=args.runtime/'models/ggml-small-q5_1.bin'
    for case in cases:
        audio=args.corpus/(case['Name']+'.wav'); result=args.corpus/(case['Name']+'-result')
        command=[str(cli),'-m',str(model),'-f',str(audio),'-l',case.get('Language','auto'),'-t','6','-bs','1','-bo','1','-nf','-sns','-ng','-oj','-of',str(result),'--prompt','Codex, Jhinx, GitHub, Nextcloud, PowerShell']
        started=time.perf_counter(); peak=0
        with (args.corpus/(case['Name']+'.stdout.log')).open('w',encoding='utf-8') as out, (args.corpus/(case['Name']+'.stderr.log')).open('w',encoding='utf-8') as err:
            process=subprocess.Popen(command, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
            try:
                while process.poll() is None:
                    memory=Memory(); memory.cb=ctypes.sizeof(memory)
                    if ctypes.windll.psapi.GetProcessMemoryInfo(ctypes.c_void_p(int(process._handle)),ctypes.byref(memory),memory.cb): peak=max(peak,memory.peak_ws)
                    if time.perf_counter()-started>90: raise TimeoutError('ASR exceeded 90 seconds')
                    time.sleep(.05)
            finally:
                if process.poll() is None: process.kill(); process.wait(timeout=5)
        if process.returncode: raise RuntimeError(f"ASR failed for {case['Name']}: {process.returncode}")
        raw=''.join(x['text'] for x in json.loads(result.with_suffix('.json').read_text())['transcription']).strip()
        with wave.open(str(audio),'rb') as wav: seconds=wav.getnframes()/wav.getframerate()
        rows.append({'case':case['Name'],'expected':case['Text'],'actual':raw,'seconds':round(seconds,3),'elapsed':round(time.perf_counter()-started,3),'peak_worker_mib':round(peak/1048576,2),'word_error_rate':round(wer(case['Text'],raw),3),'audio_sha256':hashlib.sha256(audio.read_bytes()).hexdigest(),'worker_exited':process.poll() is not None})
    args.output.write_text(json.dumps({'model_sha256':hashlib.sha256(model.read_bytes()).hexdigest(),'threads':6,'beam':1,'rows':rows},indent=2,ensure_ascii=False),encoding='utf-8')
    print(json.dumps(rows,ensure_ascii=False))
if __name__=='__main__': main()
