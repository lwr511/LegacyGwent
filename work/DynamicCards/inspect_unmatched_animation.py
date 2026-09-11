exec(open(__file__.replace('inspect_unmatched_animation.py','inspect_legacy_animation.py')).read().split('for o in env.objects:')[0])
for o in env.objects:
    if '12221001' not in o.assets_file.name or o.type.name!='Animator':continue
    d=o.read();print('ANIMATOR',d.m_GameObject.deref().read().m_Name,'avatar',d.m_Avatar.path_id)
    if d.m_Avatar.path_id:
        t=d.m_Avatar.deref().read_typetree();print('TOS',len(t.get('m_TOS',[])),t.get('m_TOS',[])[:5])
