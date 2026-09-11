"""Recompile source DXBC arithmetic with named Unity uniforms from serialized shader metadata."""
import json,re,subprocess,hashlib,sys,struct
from pathlib import Path
root=Path(__file__).resolve().parent
decompiler=next((root/'HLSLDecompiler').rglob('cmd_Decompiler.exe'))
output=root/'LatestVariants';output.mkdir(exist_ok=True)
builtin={'_Time','_SinTime','_CosTime','unity_DeltaTime','_WorldSpaceCameraPos','_ProjectionParams','_ScreenParams','_ZBufferParams','unity_OrthoParams','_WorldSpaceLightPos0','_LightColor0','glstate_lightmodel_ambient'}
blend={0:'Zero',1:'One',2:'DstColor',3:'SrcColor',4:'OneMinusDstColor',5:'SrcAlpha',6:'OneMinusSrcColor',7:'DstAlpha',8:'OneMinusDstAlpha',9:'SrcAlphaSaturate',10:'OneMinusSrcAlpha'}

def fix_packed_registers(text,bytecode):
    signature=re.search(r'void main\s*\((.*?)\)\s*\{',text,re.S)
    arguments={semantic.upper():(name,bool(out)) for out,typ,name,semantic in re.findall(r'\b(out\s+)?(float\d?|uint\d?|int\d?)\s+(\w+)\s*:\s*(\w+)',signature[1])}
    start=signature.end();body=text[start:];init=[];finish=[]
    count=struct.unpack_from('<I',bytecode,28)[0]
    for offset in struct.unpack_from('<'+'I'*count,bytecode,32):
        tag=bytecode[offset:offset+4]
        if tag not in [b'ISGN',b'OSGN']:continue
        data=bytecode[offset+8:];size=struct.unpack_from('<I',data)[0];groups={}
        for i in range(size):
            nameoff,index,system,component,register=struct.unpack_from('<5I',data,8+i*24)
            name=data[nameoff:data.index(b'\0',nameoff)].decode();mask=data[8+i*24+20]
            argument=arguments.get((name+str(index)).upper())
            if argument:groups.setdefault(register,[]).append((argument[0],mask))
        for register,parts in groups.items():
            if len(parts)<2:continue
            local='dc_pack_'+tag.decode()+'_'+str(register);components=['0']*4
            for name,mask in parts:
                bits=[b for b in range(4) if mask&(1<<b)]
                for j,bit in enumerate(bits):components[bit]=name+('.'+'xyzw'[j] if len(bits)>1 else '')
                def packed(match):
                    swizzle=match.group(1)
                    if swizzle is None:return local+'.'+''.join('xyzw'[b] for b in bits)
                    return local+'.'+''.join('xyzw'[bits['xyzw'.index(c)]] if 'xyzw'.index(c)<len(bits) else c for c in swizzle)
                body=re.sub(r'\b'+name+r'\b(?:\.([xyzw]+))?',packed,body)
                if tag==b'OSGN':finish.append(name+'='+local+'.'+''.join('xyzw'[b] for b in bits)+';')
            init.append('float4 '+local+'=float4('+(','.join(components) if tag==b'ISGN' else '0,0,0,0')+');')
    if finish:body=body.replace('return;','\n'.join(finish)+'\nreturn;')
    return text[:start]+'\n'+'\n'.join(init)+'\n'+body

def stage_code(folder,passinfo,stage,declarations):
    program=passinfo['progVertex' if stage=='v' else 'progFragment']
    candidates=[s for group in program.get('m_PlayerSubPrograms',[]) for s in group if s['m_GpuProgramType'] in ([15,16] if stage=='v' else [17,18])]
    if not candidates:candidates=[s for s in program.get('m_SubPrograms',[]) if s['m_GpuProgramType'] in ([15,16] if stage=='v' else [17,18])]
    def difference(candidate):
        keyfile=folder/('4_'+str(candidate['m_BlobIndex'])+'.keywords.json')
        actual=set(json.loads(keyfile.read_text())) if keyfile.exists() else set()
        return len(actual.symmetric_difference(keywords)),len(actual-keywords)
    candidate=min(candidates,key=difference)
    files=list(folder.glob('4_%d_*.dxbc'%candidate['m_BlobIndex']))
    if not files:raise ValueError('DXBC missing '+str(candidate['m_BlobIndex']))
    path=files[0];hlsl=path.with_suffix('.hlsl')
    if not hlsl.exists():subprocess.run([str(decompiler),'-D',str(path)],stdout=subprocess.DEVNULL,check=True)
    text=hlsl.read_text(errors='replace')
    text=fix_packed_registers(text,path.read_bytes())
    text=re.sub(r'^//.*$','',text,flags=re.M)
    text=re.sub(r'^Texture\w+<float4> (IniParams|StereoParams)[^\n]+\n','',text,flags=re.M)
    text=text.replace('#define cmp -','')
    params=program.get('m_CommonParameters',candidate)
    for group,entries in enumerate(program.get('m_PlayerSubPrograms',[])):
        for index,entry in enumerate(entries):
            if entry is candidate:
                blob=program['m_ParameterBlobIndices'][group][index]
                from shader_blob_parameters import parameters
                specific=parameters((folder/('4_'+str(blob)+'.blob')).read_bytes(),passinfo)
                import copy
                params=copy.deepcopy(program.get('m_CommonParameters',{}))
                for key,items in specific.items():
                    target=params.setdefault(key,[])
                    for item in items:
                        previous=next((x for x in target if x['m_NameIndex']==item['m_NameIndex']),None)
                        if key=='m_ConstantBuffers' and previous is not None:
                            for field in ['m_VectorParams','m_MatrixParams']:
                                existing={x['m_NameIndex'] for x in previous.get(field,[])}
                                previous.setdefault(field,[]).extend(x for x in item.get(field,[]) if x['m_NameIndex'] not in existing)
                        elif previous is None:target.append(item)
    names={index:name for name,index in passinfo['m_NameIndices']}
    for item in params.get('m_TextureParams',[]):
        name=names[item['m_NameIndex']];slot=item['m_Index'];sampler=item['m_SamplerIndex']
        text=re.sub(r'\bt%d\b'%slot,name,text)
        if sampler>=0:text=re.sub(r'\bs%d_s\b'%sampler,'sampler'+name,text)
    # Built-in legacy particles keep their texture binding in a variant parameter blob.
    # Their minimal (non-soft-particle) variant has exactly one image: _MainTex.
    unmapped=re.findall(r'Texture\w+(?:<[^>]+>)?\s+(t\d+)\s*:',text)
    if unmapped:
        properties_info=json.loads((folder/'metadata.json').read_text())['m_PropInfo']['m_Props']
        textures=[p['m_Name'] for p in properties_info if p['m_Type']==4]
        if unmapped==['t0'] and textures==['_MainTex']:
            text=re.sub(r'\bt0\b','_MainTex',text);text=re.sub(r'\bs0_s\b','sampler_MainTex',text)
        else:raise ValueError('Unmapped native textures: '+str(unmapped)+' properties '+str(textures))
    for match in list(re.finditer(r'^(?:Texture\w+(?:<[^>]+>)?|Sampler\w+)\s+[^;]+;',text,re.M)):
        declaration=re.sub(r'\s*:\s*register\([^)]*\)','',match[0]);declarations.add(declaration)
    text=re.sub(r'^(?:Texture\w+(?:<[^>]+>)?|Sampler\w+)\s+[^;]+;','',text,flags=re.M)
    arrays={int(m[0]):int(m[1]) for m in re.findall(r'cbuffer cb(\d+)[^{]*\{\s*float4 cb\d+\[(\d+)\];\s*\}',text)}
    text=re.sub(r'cbuffer cb\d+[^{]*\{[^}]+\}','',text)
    init=[]
    for slot,count in arrays.items():
        init.append('float4 dc_%s_cb%d[%d];'%(stage,slot,count))
        values=[['0','0','0','0'] for _ in range(count)]
        binding=next((b for b in params.get('m_ConstantBufferBindings',[]) if b['m_Index']==slot),None)
        buffer=next((b for b in params.get('m_ConstantBuffers',[]) if binding and b['m_NameIndex']==binding['m_NameIndex']),None)
        if buffer:
            for item in buffer.get('m_VectorParams',[]):
                name=names[item['m_NameIndex']];dim=item['m_Dim'];array=item.get('m_ArraySize',0)
                typ=('int' if item.get('m_Type',0)==1 else 'float')+(str(dim) if dim>1 else '')
                if not(name.startswith('unity_') or name in builtin):declarations.add(typ+' '+name+('['+str(array)+']' if array else '')+';')
                for a in range(max(1,array)):
                    for component in range(dim):
                        offset=item['m_Index']//4+a*4+component
                        if offset//4>=count:continue
                        value=name+('['+str(a)+']' if array else '')+('.'+'xyzw'[component] if dim>1 else '')
                        values[offset//4][offset%4]=value
            for item in buffer.get('m_MatrixParams',[]):
                name=names[item['m_NameIndex']];rows=item['m_RowCount'];array=item.get('m_ArraySize',0)
                if not(name.startswith('unity_') or name in builtin):declarations.add('float4x4 '+name+('['+str(array)+']' if array else '')+';')
                for a in range(max(1,array)):
                    for column in range(rows):
                        index=item['m_Index']//16+a*rows+column
                        if index>=count:continue
                        for row in range(4):values[index][row]=name+('['+str(a)+']' if array else '')+'[%d][%d]'%(row,column)
        for index,components in enumerate(values):init.append('dc_%s_cb%d[%d]=float4(%s);'%(stage,slot,index,','.join(components)))
        text=re.sub(r'\bcb%d\b'%slot,'dc_%s_cb%d'%(stage,slot),text)
    text=re.sub(r'\bmain\b','dc_'+stage,text)
    pos=text.index('{',text.index('void dc_'+stage))+1
    text=text[:pos]+'\n'+'\n'.join(init)+'\n'+text[pos:]
    return text

def properties(info):
    out=[]
    for item in info['m_PropInfo']['m_Props']:
        name=item['m_Name'];typ=item['m_Type'];value=[item['m_DefValue[%d]'%i] for i in range(4)]
        if typ in [0,1]:kind='Color' if typ==0 else 'Vector';default='('+','.join(map(str,value))+')'
        elif typ==4:
            kind={2:'2D',3:'3D',4:'Cube',5:'2DArray',6:'CubeArray'}.get(item['m_DefTexture']['m_TexDim'],'2D')
            default='"'+(item['m_DefTexture']['m_DefaultName'] or 'white')+'" {}'
        else:kind='Float';default=str(value[0])
        out.append(name+'("'+name+'",'+kind+')='+default)
    return '\n'.join(out)

def state_value(item,enum=None):
    name=item.get('name','')
    return '['+name+']' if name and name!='<noninit>' else (enum[int(item['val'])] if enum else str(int(item['val'])))

manifest=[]
jobs={}
for metadata in (root/'LatestBridge2022/Assets/PortableCards').glob('*/conversion.json'):
    for material in json.loads(metadata.read_text())['materials']:
        if not material['shader'].startswith('ShaderLibrary/'):continue
        body=(root/'LatestBridge2022'/material['asset']).read_text()
        keys=set()
        for field in ['m_ValidKeywords','m_InvalidKeywords']:
            block=re.search(r'  '+field+r':(.*?)(?=\n  [^ -]|\Z)',body,re.S)
            if block:keys.update(re.findall(r'^  - (.+)$',block[1],re.M))
        jobs[(material['shader'],tuple(sorted(keys)))]=None
for source,keywordTuple in jobs:
    keywords=set(keywordTuple);variant=hashlib.md5((' '.join(keywordTuple)).encode()).hexdigest()[:10]
    folder=root/'LatestShaderPrograms'/re.sub('[^A-Za-z0-9_-]','_',source)
    filename=folder.name+'__'+variant
    if not(folder/'metadata.json').exists():continue

    info=json.loads((folder/'metadata.json').read_text())
    if info['m_Name']=='Standard':
        name='DynamicCards/LatestVariant/Standard';path=output/'Standard.shader';guid=hashlib.md5(name.encode()).hexdigest()
        path.write_text('Shader "'+name+'" {\nProperties {\n'+properties(info)+'\n}\nSubShader { Tags { "RenderType"="Opaque" } UsePass "Standard/FORWARD" UsePass "Standard/FORWARD_DELTA" } Fallback "Standard"\n}',encoding='utf8')
        Path(str(path)+'.meta').write_text('fileFormatVersion: 2\nguid: '+guid+'\nShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n  nonModifiableTextures: []\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
        manifest.append(dict(source=info['m_Name'],name=name,file=path.name,guid=guid));print('OK Standard (Unity built-in passes)');continue
    try:
        sub=info['m_SubShaders'][0]
        p=next(p for p in sub['m_Passes'] if p['m_Type']==0 and p['m_State']['m_Name'].lower() not in ['shadowcaster','meta'])
        declarations=set();v=stage_code(folder,p,'v',declarations);f=stage_code(folder,p,'f',declarations)
        state=p['m_State'];b=state['rtBlend0'];tags=dict(sub['m_Tags']['tags'])
        grabs='\n'.join('GrabPass { '+('"'+item['m_TextureName']+'"' if item.get('m_TextureName') else '')+' }' for item in sub['m_Passes'] if item['m_Type']==2)
        state_lines=['Cull '+state_value(state['culling'],{0:'Off',1:'Front',2:'Back'}),'ZWrite '+state_value(state['zWrite'],{0:'Off',1:'On'}),'ZTest '+state_value(state['zTest'],{0:'Off',1:'Never',2:'Less',3:'Equal',4:'LEqual',5:'Greater',6:'NotEqual',7:'GEqual',8:'Always'}),'Blend '+state_value(b['srcBlend'],blend)+' '+state_value(b['destBlend'],blend)]
        name='DynamicCards/LatestVariant/'+filename
        text='Shader "'+name+'" {\nProperties {\n'+properties(info)+'\n}\nSubShader {\nTags {'+' '.join('"'+k+'"="'+val+'"' for k,val in tags.items())+'}\nPass {\n'+'\n'.join(state_lines)+'\nCGPROGRAM\n#pragma target 3.5\n#pragma vertex dc_v\n#pragma fragment dc_f\n#include "UnityCG.cginc"\n#include "Lighting.cginc"\n#define cmp -\n'+'\n'.join(sorted(declarations))+'\n'+v+'\n'+f+'\nENDCG\n}\n}\nFallback "DynamicCards/PortableCard"\n}\n'
        if grabs:text=text.replace('\nPass {','\n'+grabs+'\nPass {',1)
        path=output/(filename+'.shader');path.write_text(text,encoding='utf8')
        guid=hashlib.md5(name.encode()).hexdigest();Path(str(path)+'.meta').write_text('fileFormatVersion: 2\nguid: '+guid+'\nShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n  nonModifiableTextures: []\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
        manifest.append(dict(source=info['m_Name'],keywords=sorted(keywords),name=name,file=path.name,guid=guid));print('OK',info['m_Name'],sorted(keywords),flush=True)
    except Exception as e:print('FAIL',info['m_Name'],type(e).__name__,str(e))
(root/'latest_variant_manifest.json').write_text(json.dumps(manifest,indent=2))
