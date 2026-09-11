import sys,json,struct,re
from pathlib import Path
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
from UnityPy.export.ShaderConverter import ShaderProgram
from UnityPy.helpers import CompressionHelper
from UnityPy.streams import EndianBinaryReader
root=Path(__file__).resolve().parent/'ShaderPrograms';root.mkdir(exist_ok=True)
env=UnityPy.load(r'C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes')
for obj in env.objects:
    if obj.type.name!='Shader':continue
    shader=obj.read();name=shader.m_ParsedForm.m_Name
    if '--all' not in sys.argv and not any(n in name for n in ['unicorn_nightmare_cloud','/ABTop','CardCore/ImageLayerShader']):continue
    folder=root/re.sub('[^A-Za-z0-9_-]','_',name);folder.mkdir(exist_ok=True)
    tree=obj.read_typetree();(folder/'metadata.json').write_text(json.dumps(tree['m_ParsedForm'],indent=2))
    count=0
    for i,platform in enumerate(shader.platforms):
        def first(a):return a[i][0] if isinstance(a[i],list) else a[i]
        off=first(shader.offsets);length=first(shader.compressedLengths);size=first(shader.decompressedLengths)
        data=CompressionHelper.decompress_lz4(bytes(shader.compressedBlob)[off:off+length],size)
        program=ShaderProgram(EndianBinaryReader(data,endian='<'),obj.version)
        for j,sub in enumerate(program.m_SubPrograms):
            code=bytes(sub.m_ProgramCode);start=code.find(b'DXBC')
            if start<0:continue
            length=struct.unpack_from('<I',code,start+24)[0];code=code[start:start+length]
            (folder/f'{platform}_{j}_{sub.m_ProgramType.name}.dxbc').write_bytes(code);count+=1
    print(name,count,flush=True)
