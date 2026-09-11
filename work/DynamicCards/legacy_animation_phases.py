exec(open(__file__.replace('legacy_animation_phases.py','inspect_legacy_animation.py')).read().split('for o in env.objects:')[0])
document=json.loads((root/'LegacyAnimationData/animations.json').read_text());phases={}
for obj in env.objects:
    if obj.type.name!='AnimatorController':continue
    import re
    match=re.search(r'\d{8}',obj.assets_file.name)
    if not match:continue
    data=obj.read_typetree();names=dict(data['m_TOS'])
    for machine in data['m_Controller']['m_StateMachineArray']:
        for state in machine['data']['m_StateConstantArray']:
            state=state['data'];name=names.get(state['m_NameID'],'')
            phase='Intro' if 'intro' in name.lower() else 'Loop' if 'loop' in name.lower() else None
            if phase is None:continue
            for tree in state.get('m_BlendTreeConstantArray',[]):
                for node in tree['data']['m_NodeArray']:
                    i=node['data']['m_ClipID']
                    if i>=len(data['m_AnimationClips']):continue
                    ref=data['m_AnimationClips'][i]
                    phases[(match[0],str(ref['m_PathID']))]=phase
count=0
for record in document['animators']:
    for clip in record['clips']:
        phase=phases.get((record['id'],Path(clip['file']).stem.split('_')[-1]))
        if phase and phase.lower() not in clip['name'].lower():
            clip['sourceName']=clip['name'];clip['name']=phase;count+=1
(root/'LegacyAnimationData/animations.json').write_text(json.dumps(document,indent=2))
print('SOURCE_STATE_PHASES_RESTORED',count)
