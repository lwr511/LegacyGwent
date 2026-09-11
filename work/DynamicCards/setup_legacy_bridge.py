import json,shutil
from pathlib import Path
root=Path(__file__).resolve().parent
dest=root/'LegacyBridge2019'
for folder in ['Assets/Editor','Packages','ProjectSettings']:(dest/folder).mkdir(parents=True,exist_ok=True)
for path in ['ProjectSettings/ProjectVersion.txt','Packages/manifest.json']:shutil.copy2(root/'Probe'/path,dest/path)
text=(root/'PortableCardExporter.cs').read_text()
text=text.replace(r'C:\SteamLibrary\steamapps\common\Thronebreaker The Witcher Tales\Thronebreaker_Data\StreamingAssets\bundledassets\cardassets',r'E:\Hbackup\FileRecv\Gwent\Gwent\Gwent_Data\StreamingAssets\AssetBundles\cardassets')
text=text.replace('paths=bundle.GetAllScenePaths().OrderBy(p=>p).ToArray();','var wanted=File.ReadAllLines(Application.dataPath+"/../../legacy_needed.txt"); paths=bundle.GetAllScenePaths().Where(p=>wanted.Contains(Path.GetFileNameWithoutExtension(p))).OrderBy(p=>p).ToArray();')
# Clone shaders in the same Unity version as the destination; validate the saved assets before using them.
text=text.replace('else if(d is Mesh || d is Material)','else if(d is Mesh || d is Material || d is Shader)')
text=text.replace('(d is Material?".mat":".asset")','(d is Material?".mat":".asset")')
text=text.replace('converted.shader=Shader.Find("Unlit/Texture");','')
text=text.replace('AnimationUtility.SetEditorCurves(output,bindings.ToArray(),outputCurves.ToArray());','for(int b=0;b<bindings.Count;b++)AnimationUtility.SetEditorCurve(output,bindings[b],outputCurves[b]);')
# Do not overwrite arbitrary particle textures with the atlas. Source scripts will supply exact assignments later.
text=text.replace('material.name.StartsWith("[") || ','').replace('originalMaterial.name.StartsWith("[") || ','')
(dest/'Assets/Editor/PortableCardExporter.cs').write_text(text)
matches=json.loads((root/'second_source_matches.json').read_text())
(root/'legacy_needed.txt').write_text('\n'.join(c['id'] for c in matches if c['newArtIds']))
print('first',next(c['id'] for c in matches if c['newArtIds']))
