from pathlib import Path
import json
w=Path(__file__).resolve().parent
load=lambda n:json.loads((w/n).read_text())
full=load('final-all-results.json');long=load('final-long-results.json');static=load('final-static-fallback.json');edge=load('final-edge-results.json')
for data,count in [(full,2007),(long,63)]:
 assert data['complete'] and len(data['rows'])==count
 assert all(r['pixels']>0 and r['resumePixels']>0 and r['resumed'] and r['unsupportedShaders']==0 and r['unassignedVisible']==0 for r in data['rows'])
skin=load('final-packed-skin-audit.json');assert skin['complete'] and skin['checked']==2144 and not skin['issues']
assert len({r['art'] for r in full['rows']})==669
assert static['complete'] and len(static['rows'])==63 and all(r['visible'] and r['staticSpritePreserved'] and r['noDynamicSurface'] and r['noModel'] for r in static['rows'])
assert edge['complete'] and len(edge['rows'])==42 and edge['unchangedDefaultEntries']==737
assert all(r['maxClearComponentHeight']<=1 and r['clearBorderPixels']==0 and (r['pose']==0 or r['motion']>0) for r in edge['rows'])
assert (w/'final-clean-compile.txt').read_text().startswith('OK')
assert 'editorPlaying=False' in (w/'final-clean-compile.txt').read_text()
assert (w/'final-ui-result.txt').read_text().startswith('OK')
assert (w/'final-framing-ui-result.json').exists()
assert load('final-framing-ui-result.json')['passed']
source=load('final-source-manifest-update.json');delivery=load('final-delivery-verification.json');runtime=load('final-runtime-source-hashes.json')
assert source['allMatch'] and delivery['allSha256Match'] and runtime['allMatch']
p=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
assert not any((p/'Assets/Editor'/n).exists() for n in ['LocalMotionInspection.cs','LocalSourceAnimationImporter.cs','LocalSkinIntegrityRepair.cs'])
s=load('final-summary.json');s.update(status='complete',fullPlaybackRows=2007,fullPlaybackArts=669,staticFallbackRows=63,framingRegressionRows=42,unchangedFramingEntries=737,payloadBytes=delivery['bytes'],runtimeScripts=runtime,sourceManifestDelta=source,delivery=delivery,mainCleanCompilePassed=True,temporaryEditorScriptsRemoved=True,usageResetsUsed=0)
s['sourceFilesVerified']=dict(files=source['files'],bytes=source['currentBytes'],allSha256Match=True,method='Full baseline SHA256 plus independently verified catalog-only delta');
(w/'final-summary.json').write_text(json.dumps(s,indent=2))
f=w/'final-verification.md';text=f.read_text(encoding='utf-8');text=text.replace('当前状态：全量播放回归已完成；末轮取景修正的正式包复测与临时脚本清理正在收尾。','当前状态：修复、全量回归、末轮正式包复测及主 Unity 清理后的编译检查均已完成。')
text=text.replace('1,741,039,087 字节（末轮缩略图目录更新后复核）',format(delivery['bytes'],',')+' 字节')
text=text.replace('| 实际游戏收藏界面与真实缩略图 Prefab |','| 末轮取景修正的正式包复测 | 两图号 × 三尺寸 × 七状态 = 42 项通过；737 个默认取景矩阵不变；实际 UI 三尺寸继续播放 | final-edge-results.json、final-framing-ui-result.json |\n| 实际游戏收藏界面与真实缩略图 Prefab |')
f.write_text(text,encoding='utf-8')
print('PASS: all delivery gates complete; bytes',delivery['bytes'])
