from pathlib import Path
W=Path(__file__).resolve().parent
code=(W/'verify_ogo_source_properties.py').read_text(encoding='utf-8-sig')
code=code.replace("results.append({'source':source", "uniforms=sorted({n for sub in shader['m_ParsedForm']['m_SubShaders'] for p in sub['m_Passes'] for n,i in p.get('m_NameIndices',[])})\n            results.append({'originalHasUniform':proxy['name'] in uniforms,'originalUniformNames':uniforms,'source':source")
code=code.replace("W/'ogo-original-property-proof.json'","W/'ogo-original-uniform-proof.json'")
exec(compile(code,__file__,'exec'),{'__file__':__file__})
