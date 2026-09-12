"""Check Tempest's foreground water and foam edge repair; --apply backs up changes.

Only the named Tempest materials, conversion manifest and 19 foreground vertex
alpha values are changed. Rebuild dynamic card packages after applying.
"""
from pathlib import Path
from datetime import datetime, timezone
import argparse, json, re, shutil

ROOT = Path(__file__).resolve().parents[2]
PREFIX = 'Assets/DynamicCards/Content/Latest/'
ALPHA = {915:251,916:0,917:0,921:0,925:0,929:222,930:0,938:195,
         939:0,946:154,947:0,952:130,953:0,957:114,958:0,968:107,
         969:0,977:105,978:0}

def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--project',type=Path,default=ROOT/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
    ap.add_argument('--apply',action='store_true');args=ap.parse_args();p=args.project
    changes={}
    def edit(rel,fn):
        raw=(p/rel).read_bytes();out=fn(raw.decode('utf-8-sig')).encode('utf-8')
        if out!=raw:changes[rel]=out
    def guid(shader):
        return re.search(r'(?m)^guid: ([0-9a-f]{32})', (p/('Assets/DynamicCards/Shaders/'+shader+'.shader.meta')).read_text())[1]
    def value(text,key,v):
        pattern=r'(- '+re.escape(key)+r': )[-.0-9]+'
        if re.search(pattern,text):return re.sub(pattern,lambda m:m[1]+str(v),text)
        ending='\r\n' if '\r\n' in text else '\n'
        return text.replace('    m_Floats:'+ending,'    m_Floats:'+ending+'    - '+key+': '+str(v)+ending)
    def material(text,shader,queue,old_guid,foam=False):
        g=guid(shader)
        pattern=r'(m_Shader: \{fileID: 4800000, guid: )([0-9a-f]{32})'
        current=re.search(pattern,text);assert current and current[2] in (old_guid,g)
        text=re.sub(pattern,lambda m:m[1]+g,text)
        text=re.sub(r'(m_CustomRenderQueue: )-?\d+',lambda m:m[1]+str(queue),text)
        text=text.replace('RenderType: TransparentCutout','RenderType: Transparent')
        for k,v in {'_SrcBlend':5,'_DstBlend':10,'_ZWrite':0,'_AlphaToMask':0,'_Mode':2}.items():text=value(text,k,v)
        if foam:text=value(text,'_EdgeFeather',.06)
        else:text=value(text,'_LayerEnabled',0)
        return text
    edit(PREFIX+'Shared/16550101_198218__16550300_Tempest-water_front.mat',lambda t:material(t,'TempestWaterSurface',3100,'91ff939ec94249046cad32d2ce5f4afb'))
    edit(PREFIX+'Shared/16550101_198230_SplashFlow.mat',lambda t:material(t,'TempestFoam',3101,'03f7d1d50ce124fe58d31ef03609fcf7',True))
    def mesh(text):
        m=re.search(r'(?s)(    m_FloatColors:\r?\n.*?      m_Data: )([0-9a-f]+)',text)
        assert m and 'm_NumItems: 4004' in m[1]
        data=bytearray.fromhex(m[2]);assert len(data)==4004
        for i,a in ALPHA.items():
            assert data[4*i+3] in (255,a),'Unexpected foreground vertex alpha: '+str(i)
            data[4*i+3]=a
        return text[:m.start(2)]+data.hex()+text[m.end(2):]
    edit(PREFIX+'Shared/16550101_198256_Main_Mesh.asset',mesh)
    def conversion(text):
        d=json.loads(text);seen=set()
        for m in d['materials']:
            name=m['originalName']
            if name=='[16550300]Tempest-water_front':
                m.update(portableShader='DynamicCards/TempestWaterSurface',queue=3100,renderType='Transparent');seen.add(name)
            elif name=='SplashFlow':
                m.update(portableShader='DynamicCards/TempestFoam',queue=3101,renderType='Transparent');seen.add(name)
        assert len(seen)==2
        out=json.dumps(d,ensure_ascii=False,indent=2)+'\n'
        return out.replace('\n','\r\n') if '\r\n' in text else out
    edit(PREFIX+'16550101/conversion.json',conversion)
    if not changes:
        print('PASS: Tempest edge repair is applied.');return
    if not args.apply:raise SystemExit('Repair pending for '+str(len(changes))+' files; rerun with --apply and rebuild packages.')
    backup=ROOT/'work/DynamicCards/TempestEdge-20260913/Before'/datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
    for rel,out in changes.items():
        target=backup/rel;target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(p/rel,target);(p/rel).write_bytes(out)
    print('Applied '+str(len(changes))+' files. Backup: '+str(backup))

if __name__=='__main__':main()
