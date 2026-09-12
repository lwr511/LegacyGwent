"""Compile this round's editor tools against the installed Unity 2019 assemblies.

This does not launch Unity, control its UI, or replace the project's loaded assemblies.
"""
from pathlib import Path
import subprocess, json, hashlib, datetime, sys
sys.stdout.reconfigure(encoding='utf-8')

W=Path(__file__).resolve().parent
P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
U=Path('C:/Program Files/Unity/Editor/Data')
out=W/'CompileCheck'; out.mkdir(exist_ok=True)
sources=[P/'Assets/Editor'/name for name in ['CompleteMotionGameUiAudit.cs','LandsknechtMotionProbe.cs','RotationRepairDelivery.cs']]
sources += [W.parent/'SourceAnimationImporter.cs',W/'BatchRotationRepair.cs']
profile=U/'MonoBleedingEdge/lib/mono/4.7.1-api'
refs=[profile/name for name in ['mscorlib.dll','System.dll','System.Core.dll']]
refs += list((profile/'Facades').glob('*.dll'))
refs += [U/'Managed/UnityEditor.dll',U/'Managed/UnityEngine.dll']
refs += list((U/'Managed/UnityEngine').glob('*.dll'))
refs += [P/'Library/ScriptAssemblies'/name for name in ['Assembly-CSharp.dll','Assembly-CSharp-Editor.dll','UnityEngine.UI.dll']]
refs += [P/'Assets/Assemblies/Cynthia.Card.Common.dll']
assert all(p.is_file() for p in sources+refs)
args=['/nologo','/target:library','/nostdlib+','/langversion:7.3','/nowarn:0436,0649',
      '/define:UNITY_EDITOR,UNITY_STANDALONE_WIN,UNITY_2019_4,UNITY_2019_4_OR_NEWER',
      '/out:"'+str(out/'RoundEditorCompile.dll')+'"']
args += ['/reference:"'+str(p)+'"' for p in dict.fromkeys(refs)]
args += ['"'+str(p)+'"' for p in sources]
rsp=out/'compile.rsp'; rsp.write_text('\n'.join(args),encoding='utf-8')
run=subprocess.run([str(U/'Tools/Roslyn/csc.exe'),'@'+str(rsp)],capture_output=True,text=True,encoding='utf-8',errors='replace')
(out/'compiler.log').write_text(run.stdout+run.stderr,encoding='utf-8')
report=dict(complete=run.returncode==0,exitCode=run.returncode,checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
    method='Unity-bundled Roslyn compilation against Unity 2019.4 and current project assemblies; no Unity UI input',
    files=[dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in sources])
(W/'closeout-compile-verification.json').write_text(json.dumps(report,indent=2))
print(run.stdout+run.stderr)
print('Compile result:',run.returncode)
raise SystemExit(run.returncode)
