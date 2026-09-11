from pathlib import Path
W=Path(__file__).resolve().parent
code=(W/'audit_source_renderer_properties.py').read_text(encoding='utf-8-sig')
code=code.replace("selected=[r for r in rows", "if source!='Latest':continue\n selected=[r for r in rows")
code=code.replace('env=UnityPy.load(*files);',"files.extend(str(Path(base)/'StreamingAssets/bundledassets/dependencies'/v) for v in ['shaders','shaderlibrary'])\n env=UnityPy.load(*files);")
code=code.replace("W/'source-renderer-property-proof.json'","W/'latest-renderer-property-proof.json'")
exec(compile(code,__file__,'exec'),{'__file__':__file__})
