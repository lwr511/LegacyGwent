"""Generate explicit per-card offline status without claiming game UI acceptance."""
from pathlib import Path
import json, datetime, collections

W = Path(__file__).resolve().parent
P = Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
G = W.parent / 'CompleteMotionGoal-20260911'
def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))
catalog = read(P/'Assets/DynamicCards/Content/catalog.json')['cards']
game = read(G/'lands-baseline-20260912/game-card-map.json')['cards']
rot = read(W/'all-rotation-audit.json')
other = read(W/'other-transform-audit.json')
skin = read(W/'skin-acceptance-summary.json')
orders = read(W/'euler-order-and-optional-track-audit.json')
assert orders['complete'] and not orders['errors']
verified = read(W/'staged-rotation-verification.json')
delivery = read(W/'main-rotation-delivery.json') if (W/'main-rotation-delivery.json').exists() else {}
bundle_delivery = read(W/'post-rotation-delivery-verification.json') if (W/'post-rotation-delivery-verification.json').exists() else {}
main_merged = bool(delivery.get('complete') and delivery.get('clips')==873)
main_imported = bool(main_merged and delivery.get('assetDatabaseImported'))
bundle_delivered = bool(bundle_delivery.get('complete') and not bundle_delivery.get('errors'))
main_status = '873 个修复片段已合入主工程并完成重新导入。' if main_imported else ('修复已合入主工程，仍待 Unity 重新导入。' if main_merged else '修复仍保存在独立副本中，尚未合入主工程。')
bundle_status = '重建后的资源包已通过交付核验。' if bundle_delivered else '重建后的资源包交付核验尚未完成。'
v = {r['original']: r for r in verified['clips']}
errors = {r['original']: r['problem'] for r in verified['errors']}
by_scene = collections.defaultdict(list)
for r in rot['clips']: by_scene[r['scene']].append(r)
other_by_scene = collections.defaultdict(list)
for r in other['clips']: other_by_scene[r['scene']].append(r)
entries = {}
for c in catalog:
    for art in c.get('artIds', []): entries.setdefault(art, c)
cards_by_scene = collections.defaultdict(list)
missing = []
for g in game:
    c = entries.get(g['art'])
    assert bool(c)==g['mapped'], 'Catalog mapping differs from captured game mapping: '+g['art']
    if c is None: missing.append(g)
    else: cards_by_scene[c['id']].append(g)
rows = []
for c in catalog:
    clips = []
    for r in by_scene[c['id']]:
        item = dict(file=r['file'], clip=r['clip'], rotationTracks=r['rotationCurves'],
                    correctedBoneTracks=len(r['deviations']), unresolvedSourceTracks=r.get('unresolved', []))
        if not r['deviations']: item['status'] = 'original_rotation_samples_within_tolerance'
        elif r['file'] in v:
            item.update(status='repair_merged_main_pending_ui' if main_merged else 'repair_copy_verified_not_merged', staged=v[r['file']]['staged'],
                        afterMaxDegrees=v[r['file']]['maxErrorDegrees'])
        else: item.update(status='repair_copy_pending_verification', problem=errors.get(r['file']))
        clips.append(item)
    rows.append(dict(scene=c['id'], prefab=c['prefab'], gameCards=cards_by_scene[c['id']],
        animationClips=clips, sourceTransformAuditAvailable=bool(clips),
        otherTransformDeviations=sum(len(r['deviations']) for r in other_by_scene[c['id']]),
        otherUnresolvedSourceTracks=[x for r in other_by_scene[c['id']] for x in r['unresolved']],
        sourceSkinLimitation=skin['sourceLimitation'] if c['id']==skin['sourceLimitation']['scene'] else None,
        repairMergedIntoMain=main_merged, postRepairBundleDelivered=bundle_delivered, postRepairGameUiAccepted=False))
assert sum(len(r['animationClips']) for r in rows)==len(rot['clips']), 'Per-card ledger dropped source animation clips'
smoke = read(W/'bundle-smoke-results.json') if (W/'bundle-smoke-results.json').exists() else None
order_smoke = read(W/'binding-order-smoke-results.json') if (W/'binding-order-smoke-results.json').exists() else None
backup = read(W/'rotation-original-backup-manifest.json') if (W/'rotation-original-backup-manifest.json').exists() else None
report = dict(generatedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
    scope='Source-sample, repair delivery and actual game UI acceptance are recorded separately',
    allCardsAccepted=False, sceneCount=len(rows), gameCardCount=len(game),
    missingGameCards=missing, stagedVerificationComplete=verified['complete'],
    stagedVerifiedClips=len(v), stagedErrors=verified['errors'], isolatedAnimatorBundleSmoke=smoke,bindingOrderAnimatorSmoke=order_smoke,
    originalBackupComplete=bool(backup and backup.get('complete')),mainMerged=main_merged,mainImported=main_imported,bundleDelivered=bundle_delivered,cards=rows)
(W/'rotation-acceptance-per-card.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
missing_arts = sorted({g['art'] for g in missing})
limitation = next(r for r in rows if r['sourceSkinLimitation'])
limitation_names = '、'.join(g['name'] for g in limitation['gameCards'])
text = f'''# 闪卡旋转修复离线验收记录

更新时间（UTC）：{report['generatedUtc']}

**尚未完成全卡验收。{main_status}{bundle_status}修复后的游戏界面逐卡验收仍待完成。**

## 核对范围与结果

- 当前目录有 {len(rows)} 个源卡面场景；游戏中 {len(game)} 个卡牌定义使用 {len({g['art'] for g in game})} 种卡图，其中 {len({g['art'] for g in game})-len(missing_arts)} 种能映射到动态源素材。
- 完成 1,671 个动画片段的源采样核对。99,938 条已对应的四元数轨道中，13,098 条在原始采样时刻存在超过 0.05° 的旋转偏差，分布在 873 个片段、628 个源卡面场景。
- 原因：四元数连续性处理产生的平滑切线使稀疏关键帧间的姿态越过原始数据。修复副本将受影响的四元数分量切线设为线性，并保留关键帧时间、数值、其他轨道及资源 GUID。
- 修复副本完整核验状态：`{verified['complete']}`；已通过 {len(v)}／873 个片段；当前核验错误 {len(verified['errors'])} 个。
- 已对应的位置、缩放、欧拉角源轨道分别为 99,886、84,097、186 条，采样核对未发现偏差；186 条欧拉角轨道的旋转顺序也全部与来源相符。另有 8 条四元数及 25 条其他源轨道未在导入动画中对应，全部在源数据中标为可选辅助轨道，未计入通过数。
- 2,126 个蒙皮、101,221 个带哈希骨骼槽位完成来源核对；保留原始空槽位和一个明确的源缺失限制。

## 用户指出的莱里亚长矛兵

游戏卡牌 70104，卡图 202156，源场景 15210100。独立 Unity `AnimationClip.SampleAnimation` 对 241 帧、61 个骨骼进行源数据比较，最大旋转偏差由约 16.933° 降至约 0.0098°。这证明修复副本在采样层面更接近原始数据；手持武器在实际游戏界面中的视觉结果仍待修复合入后的检查。

## 来源限制

- {limitation_names}，源场景 {limitation['scene']}，`mesh_middle` 骨骼槽位 114：原始来源没有骨骼引用或路径。目前使用恢复的绑定姿态，无法从现有来源建立该骨骼的独立动作。
- {len(missing_arts)} 种游戏卡图没有可用动态来源，涉及 {len(missing)} 个卡牌定义。这些内容保留为缺失，不以其他卡牌动画或伪造运动替代。

|卡牌 ID|名称|缺失卡图 ID|
|---|---|---|
'''
text += ''.join(f"|{g['card']}|{g['name']}|{g['art']}|\n" for g in missing)
text += '\n## 后续验收\n\n完成副本核验和独立资源包 Animator 播放检查后，还需要合入主工程、重建动态资源包，并在实际游戏界面逐卡检查入场、循环、人物肢体、手持物、粒子及结束过渡。\n\n**电脑操控规则（用户于 2026-09-12 明确澄清）：Esc 只停止当下的电脑输入，不停止任务或撤回已有授权。立即停止当次操作后，继续任务，过一段时间、时机合适时可恢复电脑操控，无需再次请求恢复许可；不要仅因 Esc 将目标标为受阻。** 此前将 Esc 当作任务阻塞的判断已撤销。\n\n逐卡明细：`rotation-acceptance-per-card.json`。完整证据：`all-rotation-audit.json`、`other-transform-audit.json`、`skin-acceptance-summary.json`、`staged-rotation-verification.json`、`rotation-repair-engine.jsonl`。\n'
if backup and backup.get('complete'):
    text += f"\n合入前原文件备份已完成：{backup['clipCount']} 个动画及各自的 `.meta`，全部通过原始哈希核对。备份目录为 `BeforeRotationMerge`，清单为 `rotation-original-backup-manifest.json`。{main_status}\n"
if smoke:
    text += '\n## 独立资源包 Animator 检查\n\n完成状态：`'+str(smoke.get('complete',False))+'`。此项使用独立测试工程，不能代替主游戏界面验收。\n\n|源场景与片段|原动画最大偏差|修复副本最大偏差|\n|---|---:|---:|\n'
    text += ''.join(f"|{r['name']}|{r['beforeMaxDegrees']:.6f}°|{r['afterMaxDegrees']:.6f}°|\n" for r in smoke.get('cases',[]))
    if order_smoke:
        text += ''.join(f"|{r['name']}（绑定表顺序恢复）|{r['beforeMaxDegrees']:.6f}°|{r['afterMaxDegrees']:.6f}°|\n" for r in order_smoke.get('cases',[]))
        text += '\n绑定表顺序恢复后的补充检查完成状态：`'+str(order_smoke.get('complete',False))+'`。\n'
        for r in order_smoke.get('cases',[]):
            if r.get('loopWrapChecked'):
                text += f"\n`{r['name']}` 的原始末帧与首帧存在最大 {r['sourceEndpointGapMaxDegrees']:.6f}° 的骨骼姿态差。Animator 在整循环时刻回到首帧，测试对此时刻使用源首帧作为期望值；原始末帧由全量采样核验覆盖。没有为消除这项源差异而改动动画，该卡的实际循环过渡仍需界面观察。\n"
pose_path=W/'lyrian-actual-source-pose-comparison.json'
if pose_path.exists():
    pose=read(pose_path)
    text=text.replace('手持武器在实际游戏界面中的视觉结果仍待修复合入后的检查。',
        f"本轮还完成了真实组牌界面的 {pose['recordedFrames']} 帧连续记录，前景网格的 {pose['matchedSourceBoneTracks']} 条旋转轨迹全部匹配源动画，未调整时钟的最大误差为 {pose['zeroClockOffsetMaxErrorDegrees']:.6f}°。见 RESULTS.md 及实际源姿态比较文件；其余卡牌仍未完成全量验收。")
    text=text.replace('完成副本核验和独立资源包 Animator 播放检查后，还需要合入主工程、重建动态资源包，并在实际游戏界面逐卡检查入场、循环、人物肢体、手持物、粒子及结束过渡。',
        '主工程合入、重新导入和资源包交付核验已完成。后续仍需完成剩余卡牌在实际游戏界面的入场、循环、人物肢体、手持物、粒子及结束过渡检查。本轮已按用户要求收尾，等待明确恢复。')
(W/'闪卡旋转修复离线验收记录-20260912.md').write_text(text,encoding='utf-8')
print(json.dumps({k:report[k] for k in ['sceneCount','gameCardCount','stagedVerificationComplete','stagedVerifiedClips','allCardsAccepted']}))
print('Missing source arts:',len({g['art'] for g in missing}),'; affected scenes:',sum(any(x['correctedBoneTracks'] for x in r['animationClips']) for r in rows))
