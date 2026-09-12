"""Restore continuous moving sea beneath Tempest's foam after hiding water_front.

Default is read-only. Use --apply after repair_tempest_edges.py --apply, then
rebuild the dynamic card packages. Changes are backed up before writing.
"""
from pathlib import Path
from datetime import datetime, timezone
import argparse, json, re, shutil

ROOT = Path(__file__).resolve().parents[2]
PREFIX = 'Assets/DynamicCards/Content/Latest/'
# Selected sea vertices: local Y and UV0 V. X/Z, triangles, and colours stay intact.
LOWER_SEA = {721: (-6.19732332, 0.282561541), 722: (-7.27544832, 0.236857817), 723: (-6.798886, 0.2488704), 725: (-7.492245, 0.235196546), 726: (-8.675838, 0.1593411), 727: (-8.512753, 0.169906437), 728: (-11.3682213, 0.0286602769), 732: (-7.492245, 0.235196546), 733: (-6.960995, 0.2755364), 734: (-9.166073, 0.159398228), 735: (-11.605526, 0.0193832666), 736: (-13.4443932, -0.0541090444), 737: (-11.0410728, 0.09884721), 738: (-16.3467369, -0.165036544), 739: (-7.722714, 0.2520006), 740: (-6.960995, 0.2755364), 746: (-11.3809166, 0.08702507), 747: (-17.28619, -0.192965209), 750: (-8.915096, 0.1969441), 751: (-7.722714, 0.2520006), 761: (-9.115292, 0.173328757), 762: (-8.915096, 0.1969441), 763: (-12.3066978, 0.04092246), 764: (-16.9297447, -0.17723231), 765: (-8.561581, 0.194543421), 766: (-12.0859947, 0.04232633), 767: (-16.9297447, -0.17723231), 768: (-16.636776, -0.179065034), 769: (-11.8652916, 0.0474912226), 770: (-16.4854088, -0.181313962), 771: (-8.694393, 0.203378484), 772: (-11.8652916, 0.0474912226), 773: (-6.99419832, 0.277637333), 774: (-6.901425, 0.266025037), 783: (-7.347714, 0.250975043), 784: (-8.915096, 0.1969441), 794: (-6.56744051, 0.272726536), 797: (-6.19732332, 0.282561541), 798: (-7.65435457, 0.225653678), 799: (-7.27544832, 0.236857817), 801: (-7.347714, 0.250975043), 806: (-8.832088, 0.149931535), 807: (-7.65435457, 0.225653678), 808: (-7.27544832, 0.236857817), 809: (-8.94537, 0.143263251), 810: (-11.4102135, 0.0255607441), 811: (-11.3838463, 0.0277471729), 812: (-8.675838, 0.1593411), 813: (-6.798886, 0.2488704), 828: (-6.51372957, 0.287777066), 829: (-8.701229, 0.227694288), 830: (-8.575253, 0.2111531), 833: (-7.92681551, 0.233786955), 835: (-8.084042, 0.24346742)}

def packed(text, name):
    match = re.search(r'(?ms)^    '+name+r':\r?\n(.*?)(?=^    \w|\Z)',text)
    assert match, name
    body=match[1]
    def field(n):return re.search(r'(?m)^      '+n+r': ([^\r\n]*)',body)[1]
    count=int(field('m_NumItems')); bits=int(field('m_BitSize'))
    start=float(field('m_Start')); span=float(field('m_Range'))
    raw=bytes.fromhex(field('m_Data')); integer=int.from_bytes(raw,'little'); mask=(1<<bits)-1
    values=[start+((integer>>(bits*i))&mask)*span/mask for i in range(count)]
    return match, values, bits, start, span

def write_packed(text, name, values, bits=None, start=None, span=None):
    match, old, b, a, r=packed(text,name)
    bits=b if bits is None else bits;start=a if start is None else start;span=r if span is None else span
    mask=(1<<bits)-1; integer=0
    for i,value in enumerate(values):
        quant=round((value-start)*mask/span)
        assert 0<=quant<=mask,(name,i,value)
        integer|=quant<<(bits*i)
    data=integer.to_bytes((len(values)*bits+7)//8,'little').hex()
    body=match[1]
    for key,value in [('m_Data',data),('m_BitSize',bits),('m_Start',format(start,'.9g')),('m_Range',format(span,'.9g'))]:
        body=re.sub(r'(?m)(^      '+key+r': )[^\r\n]*',lambda m:m[1]+str(value),body)
    return text[:match.start(1)]+body+text[match.end(1):]

def bounds_block(points, indent):
    lo=[min(p[j] for p in points) for j in range(3)]
    hi=[max(p[j] for p in points) for j in range(3)]
    def vector(v):return '{'+', '.join(k+': '+format(x,'.9g') for k,x in zip('xyz',v))+'}'
    return indent+'m_Center: '+vector([(a+b)/2 for a,b in zip(lo,hi)])+'\n'+indent+'m_Extent: '+vector([(b-a)/2+.0001 for a,b in zip(lo,hi)])

def sea_mesh(text):
    _,vertices,_,_,_=packed(text,'m_Vertices');_,uv,_,_,_=packed(text,'m_UV')
    assert len(vertices)==3003 and len(uv)==8008
    for i,(y,v) in LOWER_SEA.items():vertices[i*3+1]=y;uv[i*2+1]=v
    text=write_packed(text,'m_Vertices',vertices)
    # Extra precision preserves the other atlas coordinates while allowing V < 0.
    text=write_packed(text,'m_UV',uv,bits=24,start=-1,span=2.1)
    _,vertices,_,_,_=packed(text,'m_Vertices');points=list(zip(*[iter(vertices)]*3))
    blocks=list(re.finditer(r'(?m)^    localAABB:\r?\n      m_Center: [^\r\n]*\r?\n      m_Extent: [^\r\n]*',text))
    assert len(blocks)==5
    m=blocks[2];s='    localAABB:\n'+bounds_block(points[695:880],'      ')
    if '\r\n' in m[0]:s=s.replace('\n','\r\n')
    text=text[:m.start()]+s+text[m.end():]
    m=re.search(r'(?m)^  m_LocalAABB:\r?\n    m_Center: [^\r\n]*\r?\n    m_Extent: [^\r\n]*',text)
    assert m
    s='  m_LocalAABB:\n'+bounds_block(points,'    ')
    if '\r\n' in m[0]:s=s.replace('\n','\r\n')
    return text[:m.start()]+s+text[m.end():]

def main():
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--project',type=Path,default=ROOT/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');ap.add_argument('--apply',action='store_true');args=ap.parse_args();p=args.project
    changes={}
    def edit(rel,fn):
        raw=(p/rel).read_bytes();out=fn(raw.decode('utf-8-sig')).encode('utf-8')
        if raw!=out:changes[rel]=out
    meta=(p/'Assets/DynamicCards/Shaders/TempestSeaCoverage.shader.meta').read_text()
    guid=re.search(r'(?m)^guid: ([0-9a-f]{32})',meta)[1]
    def material(t):
        old=re.search(r'm_Shader: \{fileID: 4800000, guid: ([0-9a-f]{32})',t)[1]
        assert old in ('91ff939ec94249046cad32d2ce5f4afb',guid)
        return t.replace('m_Shader: {fileID: 4800000, guid: '+old,'m_Shader: {fileID: 4800000, guid: '+guid)
    edit(PREFIX+'Shared/16550101_198216__16550300_Tempest-water.mat',material)
    edit(PREFIX+'Shared/16550101_198256_Main_Mesh.asset',sea_mesh)
    def conversion(t):
        d=json.loads(t);found=False
        for m in d['materials']:
            if m['originalName']=='[16550300]Tempest-water':m['portableShader']='DynamicCards/TempestSeaCoverage';found=True
        assert found
        s=json.dumps(d,ensure_ascii=False,indent=2)+'\n'
        return s.replace('\n','\r\n') if '\r\n' in t else s
    edit(PREFIX+'16550101/conversion.json',conversion)
    if not changes:print('PASS: Tempest sea coverage repair is applied.');return
    if not args.apply:raise SystemExit('Sea coverage pending for '+str(len(changes))+' files; rerun with --apply.')
    backup=ROOT/'work/DynamicCards/TempestWaterCoverage-20260913/Before'/datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
    for rel,out in changes.items():
        target=backup/rel;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p/rel,target);(p/rel).write_bytes(out)
    print('Applied '+str(len(changes))+' files. Backup: '+str(backup))

if __name__=='__main__':main()
