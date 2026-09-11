from pathlib import Path
import json,sys,re,struct
sys.stdout.reconfigure(encoding='utf-8')
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
from UnityPy.export.ShaderConverter import ShaderProgram
from UnityPy.helpers import CompressionHelper
from UnityPy.streams import EndianBinaryReader
W=Path(__file__).resolve().parent;native='--native' in sys.argv;source=Path('C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data' if native else 'E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data');out=W/('NativePostFxShaderPrograms' if native else 'PostFxShaderPrograms');out.mkdir(exist_ok=True);rows=[]
files=[source/'resources.assets',source/'globalgamemanagers.assets',*source.glob('sharedassets*.assets')]
for file in files:
    env=UnityPy.load(str(file))
    for obj in env.objects:
        if obj.type.name!='Shader':continue
        shader=obj.read();name=shader.m_ParsedForm.m_Name
        if not any(word in name.lower() for word in ['pollen','glitch','bloom','candle']):continue
        print(file.name,name,flush=True);folder=out/re.sub('[^A-Za-z0-9_-]','_',name);folder.mkdir(exist_ok=True)
        (folder/'metadata.json').write_text(json.dumps(obj.read_typetree()['m_ParsedForm'],indent=2));count=0
        for i,platform in enumerate(shader.platforms):
            def first(a):return a[i][0] if isinstance(a[i],list) else a[i]
            off=first(shader.offsets);length=first(shader.compressedLengths);size=first(shader.decompressedLengths)
            data=CompressionHelper.decompress_lz4(bytes(shader.compressedBlob)[off:off+length],size);program=ShaderProgram(EndianBinaryReader(data,endian='<'),obj.version)
            for j,sub in enumerate(program.m_SubPrograms):
                code=bytes(sub.m_ProgramCode);start=code.find(b'DXBC')
                if start<0:continue
                length=struct.unpack_from('<I',code,start+24)[0];(folder/f'{platform}_{j}_{sub.m_ProgramType.name}.dxbc').write_bytes(code[start:start+length]);count+=1
        rows.append({'file':str(file),'name':name,'programs':count,'folder':str(folder)})
(W/('native-postfx-original-shader-inventory.json' if native else 'postfx-original-shader-inventory.json')).write_text(json.dumps(rows,indent=2));print('MATCHES',len(rows),flush=True)
