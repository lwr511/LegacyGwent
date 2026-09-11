import sys
from pathlib import Path
root=Path(__file__).resolve().parent
sys.argv.append('--all')
if '--generate' not in sys.argv:
    text=(root/'extract_shader_programs.py').read_text().replace("/'ShaderPrograms'","/'LatestShaderPrograms'")
    text=text.replace("UnityPy.load(r'C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes')","UnityPy.load(r'C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets/dependencies/shaders',r'C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets/dependencies/shaderlibrary')")
else:
    text=(root/'generate_native_shaders.py').read_text().replace("'NativeShaders'","'LatestShaders'").replace("'ShaderPrograms'","'LatestShaderPrograms'")
    text=text.replace('DynamicCards/Native/','DynamicCards/Latest/').replace('native_shader_manifest.json','latest_shader_manifest.json')
if '--scenes' in sys.argv and '--generate' not in sys.argv:
    sourceLine=next(l for l in text.splitlines() if l.startswith('env=UnityPy.load('))
    text=text.replace(sourceLine,"def source_objects():\n    base=Path(r'C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets/cardassets/scenes')\n    for p in base.iterdir():\n        if p.name.isdigit():yield from UnityPy.load(str(p)).objects")
    text=text.replace('for obj in env.objects:', 'for obj in source_objects():')
    text=text.replace('for obj in source_objects():',"seen={json.loads(p.read_text())['m_Name'] for p in root.glob('*/metadata.json')}\nfor obj in source_objects():")
    text=text.replace('shader=obj.read();name=shader.m_ParsedForm.m_Name','shader=obj.read();name=shader.m_ParsedForm.m_Name\n    if name in seen:continue\n    seen.add(name)')
if '--generate' not in sys.argv:
    text=text.replace('from UnityPy.export.ShaderConverter import ShaderProgram','from UnityPy.export.ShaderConverter import ShaderProgram, ShaderSubProgram')
    start=text.index('        def first(a):');end=text.index('            code=bytes(sub.m_ProgramCode);',start)
    text=text[:start]+"""        offsets=shader.offsets[i];lengths=shader.compressedLengths[i];sizes=shader.decompressedLengths[i]
        if not isinstance(offsets,list):offsets=[offsets];lengths=[lengths];sizes=[sizes]
        blobs=[CompressionHelper.decompress_lz4(bytes(shader.compressedBlob)[off:off+length],size) for off,length,size in zip(offsets,lengths,sizes)]
        countPrograms=struct.unpack_from('<I',blobs[0],0)[0]
        for j in range(countPrograms):
            offset,length,segment=struct.unpack_from('<III',blobs[0],4+j*12)
            reader=EndianBinaryReader(blobs[segment],endian='<');reader.Position=offset
            (folder/(str(platform)+'_'+str(j)+'.blob')).write_bytes(blobs[segment][offset:offset+length])
            if length==0 or struct.unpack_from('<I',blobs[segment],offset+4)[0] not in [15,16,17,18]:continue
            try:sub=ShaderSubProgram(reader)
            except Exception:
                print('SHADER_PROGRAM_ERROR',name,platform,j,offset,length,segment,blobs[segment][offset:offset+40].hex(),flush=True)
                raise
            (folder/(str(platform)+'_'+str(j)+'.keywords.json')).write_text(json.dumps(sub.m_Keywords))
"""+text[end:]
text=text.replace('if start<0:continue' ,'if start<0 or start+28>len(code):continue')
exec(compile(text,__file__,'exec'))
