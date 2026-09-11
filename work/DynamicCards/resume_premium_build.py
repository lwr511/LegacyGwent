from pathlib import Path
import json,sys,shutil
r=Path(__file__).resolve().parent
sys.path.insert(0,str(r/'DiagnosticsDependencies'))
import psutil
assert shutil.disk_usage(r).free>=8*1024**3,'Keep output space reserved before resuming.'
guard=json.loads((r/'build_disk_guard_ready.json').read_text())
assert psutil.pid_exists(guard['pid']),'Start the disk-space guard before resuming.'
ids=json.loads((r/'paused_build_processes.json').read_text())
for pid in ids:
 if not psutil.pid_exists(pid):continue
 p=psutil.Process(pid)
 assert p.name() in ['Unity.exe','UnityShaderCompiler.exe']
 if pid==49340:assert str(r/'Probe').lower() in p.cmdline()[p.cmdline().index('-projectPath')+1].lower()
 else:assert p.ppid()==49340
 p.resume()
(r/'paused_build_processes.json').write_text('[]')
print('PREMIUM_BUILD_RESUMED',ids)
