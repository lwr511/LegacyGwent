from pathlib import Path
import sys,json,time,shutil,os
r=Path(__file__).resolve().parent;sys.path.insert(0,str(r/'DiagnosticsDependencies'))
import psutil
p=psutil.Process(49340);birth=p.create_time()
assert p.name()=='Unity.exe' and str(r/'Probe').lower() in ' '.join(p.cmdline()).lower()
(r/'build_disk_guard_ready.json').write_text(json.dumps(dict(pid=os.getpid(),buildPid=p.pid)))
while p.is_running() and p.create_time()==birth:
 if shutil.disk_usage(r).free<3*1024**3:
  p.suspend();ids=[p.pid]
  for child in p.children(recursive=True):
   if child.name()=='UnityShaderCompiler.exe':child.suspend();ids.append(child.pid)
  (r/'paused_build_processes.json').write_text(json.dumps(ids))
  print('BUILD_DISK_GUARD_PAUSED',ids,flush=True);break
 time.sleep(.25)
else:print('BUILD_DISK_GUARD_COMPLETED',flush=True)
