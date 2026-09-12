from pathlib import Path
import json,re
W=Path(__file__).resolve().parent
a=json.loads((W/'skin-path-hash-audit.json').read_text());b=json.loads((W/'skin-source-resolution.json').read_text());c=json.loads((W/'skin-direct-resolution.json').read_text());source=json.loads((W/'source-direct-skins.json').read_text())
extra=[]
for row in c['remaining']:
    if row['problem']!='bone-count':continue
    candidates=[s for s in source if s['id']==row['scene'] and s['skin']==row['skin']];assert len(candidates)==1;s=candidates[0]
    assert len(s['hashes'])==row['hashes'] and len(s['bones'])==row['bones']
    blocks={i:(int(t),v) for t,i,v in re.findall(r'--- !u!(\d+) &(-?\d+)\n(.*?)(?=--- !u!|\Z)',Path(row['file']).read_text(encoding='utf-8-sig'),re.S)}
    def ref(v,key):return re.search(r'  '+key+r': \{fileID: (-?\d+)',v)[1]
    names={i:re.search(r'^  m_Name: (.*)',v,re.M)[1].strip('"') for i,(ty,v) in blocks.items() if ty==1}
    skin=next(v for i,(ty,v) in blocks.items() if ty==137 and names[ref(v,'m_GameObject')]==row['skin'])
    boneids=re.findall(r'fileID: (-?\d+)',skin.split('  m_Bones:',1)[1].split('  m_BlendShapeWeights:',1)[0])
    matches=[]
    for index in range(row['hashes'],len(boneids)):
        ident=boneids[index];actual='' if ident=='0' else names[ref(blocks[ident][1],'m_GameObject')];expected=s['bones'][index].split('/')[-1]
        assert actual==expected,(row['scene'],row['skin'],index,actual,expected)
        matches.append(index)
    extra.append(dict(scene=row['scene'],skin=row['skin'],sourceBoneCount=len(s['bones']),sourceHashCount=len(s['hashes']),extraSlotsMatchedBySourcePointerOrder=matches))
fire=json.loads((W/'fire-bomb-source-slot.json').read_text())
expected={r['bones'][i] for r in fire for i,h in enumerate(r['hashes']) if h==4176565552 and i<len(r['bones']) and r['bones'][i]}
assert len(expected)==1
fireRow=next(r for r in c['remaining'] if r['scene']=='12130100');assert fireRow['path'].endswith(next(iter(expected)))
fallback=next(r for r in c['remaining'] if r['scene']=='12230611')
count=a['totals']['exactPathHashMatches']+b['totals']['sourceBoneNameMatches']+c['totals']['directSourceBoneMatches']+c['totals']['originalNullSlots']+1+1
assert count==a['totals']['slots']
report=dict(complete=True,scenes=a['totals']['scenes'],skins=a['totals']['skins'],hashedSlots=count,exactImportedPathHashes=a['totals']['exactPathHashMatches'],originalAvatarBoneMatches=b['totals']['sourceBoneNameMatches'],originalDirectBoneSlotMatches=c['totals']['directSourceBoneMatches'],originalNullSlots=c['totals']['originalNullSlots'],restoredMissingReferenceUsingOtherSourceMesh=dict(scene='12130100',skin=fireRow['skin'],sourceBone=next(iter(expected))),extraSourceSlots=extra,sourceLimitation=dict(scene='12230611',skin='mesh_middle',slot=114,hash=1819506819,currentFallback=fallback['path'],limitation='Original source bone is null and no source path exists; authored bind pose is restored under its parent, independent motion cannot be established from this source'),scope='Source slot identity and null-slot provenance; not a whole-card visual acceptance')
(W/'skin-acceptance-summary.json').write_text(json.dumps(report,indent=2))
print('ACCOUNTED',count,'hashed slots;',sum(len(r['extraSlotsMatchedBySourcePointerOrder']) for r in extra),'extra source slots; one documented source limitation')
