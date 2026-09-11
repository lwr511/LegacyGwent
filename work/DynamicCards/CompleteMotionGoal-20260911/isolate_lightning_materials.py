from pathlib import Path
W=Path('C:/UnityProjects/LegacyGwent/work/DynamicCards/CompleteMotionGoal-20260911');H=W/'OgoImportHarness';p=H/'Assets/DynamicCards/Runtime/SourceLightning/LightningAnimator.cs';s=p.read_text()
insert='''private readonly List<Material> cardMaterials = new List<Material>();
    private void CreateCardMaterialInstances()
    {
        var replacements = new Dictionary<Material, Material>();
        var renderers = transform.root.GetComponentsInChildren<Renderer>(true);
        foreach (var animation in MaterialAnimations)
        {
            var original = animation.WiggleMaterials;
            if (original == null) continue;
            Material clone;
            if (!replacements.TryGetValue(original, out clone))
            {
                clone = new Material(original); replacements.Add(original, clone); cardMaterials.Add(clone);
                foreach (var renderer in renderers)
                {
                    var materials = renderer.sharedMaterials; bool changed = false;
                    for (int i = 0; i < materials.Length; i++)
                        if (materials[i] == original) { materials[i] = clone; changed = true; }
                    if (changed) renderer.sharedMaterials = materials;
                }
            }
            animation.WiggleMaterials = clone;
        }
    }
    private void OnDestroy()
    {
        foreach (var material in cardMaterials)
            if (material != null) Destroy(material);
    }
    '''
s=s.replace('public void Awake()\n\t{',insert+'public void Awake()\n\t{\n        if (Application.isPlaying) CreateCardMaterialInstances();')
p.write_text(s,encoding='utf-8')
p=H/'Assets/DynamicCards/Runtime/SourceLightning/LightningTool.cs';s=p.read_text();s=s.replace('\t\t\tMesh item = new Mesh();\n\t\t\tm_CreatedMeshes.Add(item);','\t\t\tvar previousMesh = meshFilter.sharedMesh;\n\t\t\tif (previousMesh != null && m_CreatedMeshes.Remove(previousMesh)) Destroy(previousMesh);');s=s.replace('\t\t\tmeshRenderer.material = material;','\t\t\tm_CreatedMeshes.Add(meshFilter.sharedMesh);\n\t\t\tmeshRenderer.material = material;');p.write_text(s,encoding='utf-8')
