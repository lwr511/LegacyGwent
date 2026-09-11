from pathlib import Path
import json,re
r=Path(__file__).resolve().parent
main=r.parents[2]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
def read(name):return json.loads((r/name).read_text(encoding='utf-8-sig'))
build=read('build-result.json');delivery=read('delivery-result.json');page=read('page-optimized.json');database=read('page-database.json');baseline=read('page-bundle-base.json')
assert build['passed'] and delivery['installed'] and read('bundle-cache-test.json')['passed'] and read('cache-test.json')['passed'] and read('runtime-result.json')['passed']
before=7011978254;after=build['bytes'];reduction=(before-after)/before*100
source=sum(p.stat().st_size for p in (main/'Assets/DynamicCards/Content').rglob('*') if p.is_file())
report=f'''\n\n## 2026-09-07：Windows 包体积与小卡加载修复

保留全部 1,279 个新版场景及唯一映射，发布包由 {before:,} 字节（6.53 GiB）降到 {after:,} 字节（{after/1024**3:.2f} GiB），减少 {reduction:.1f}%。43 个发布文件均经过读取和场景名单验证。

只合并 PNG 内容及导入设置完全一致的重复贴图：10,024 份减至 5,075 份，材质引用和转换记录同步更新。Windows 常规贴图改用 DXT5 Crunch、质量 80，保留原分辨率上限、mipmap、色彩空间及 alpha 设置，极小常量贴图保持原格式。剩余源 PNG 像素未改写；运行包压缩是有损的。8 张大贴图抽样的最大平均 RGBA 误差为 0.009865，尺寸不变；这不是全量卡牌的视觉验收。动画曲线、模型和粒子参数未为压体积而删减。

编辑器缓存现在正确处理 `.meta` 自身的刷新及新包已不包含的旧文件删除；真实内容或导入设置改动仍使缓存失效。修复了错误失效后退回同步 AssetDatabase 读取的退化。无可用包时会输出明确警告。逐张加载、显示顺序、预览优先及离屏释放策略保持现有行为。

分包复用同时改为依赖文件及 `.meta` 的 SHA256 指纹；按文件大小和修改时间缓存已计算指纹，依赖变更时强制重建该分包，未变更则复用。回归实际验证了未变更包保持不动，以及依赖贴图的导入设置改动会重建并进入最终包。

同一台机器、Unity 2019.4.1f1 隔离 PlayMode、同样 20 张中立金卡的页面：

| 加载路径 | 全部动态首帧就绪 | P95 帧耗时 | 最大帧耗时 |
| --- | ---: | ---: | ---: |
| 缓存失效，直接读取编辑器素材 | {database['totalMs']/1000:.2f} 秒 | {database['p95FrameMs']:.1f} ms | {database['maxFrameMs']:.1f} ms |
| 原有效异步包 | {baseline['totalMs']/1000:.2f} 秒 | {baseline['p95FrameMs']:.1f} ms | {baseline['maxFrameMs']:.1f} ms |
| 本次压缩后的有效异步包 | {page['totalMs']/1000:.2f} 秒 | {page['p95FrameMs']:.1f} ms | {page['maxFrameMs']:.1f} ms |

每次使用新 Unity 进程，未控制操作系统磁盘缓存；结果不代表所有设备或正式收藏 UI 的保证。新包另外通过杰洛特、伊格尼、金龙的动态画面变化及缺失资源静态回退检查。缓存误失效、依赖变更重建、19,643 个场景/材质/控制器引用检查均通过。详细本地证据在 `work/DynamicCards/Optimization`：`build-result.json`、`page-optimized.json`、`cache-test.json`、`bundle-cache-test.json`、`runtime-result.json`、`final-guid-audit.json`、`delivery-result.json`。

当前源 Content 约 {source/1024**3:.2f} GiB；这是编辑用模型、动画和源贴图，不能与压缩后的发布包混为一个口径。源码提交仍不包含被忽略的 Content 素材及 Library 资源包；完整素材及其 `.meta` 需要单独备份。本机正式缓存已替换为本次验证的版本。首次编辑器刷新导入设置可能产生一次性导入开销。
'''
path=main/'Assets/DynamicCards/README.md'
text=path.read_text(encoding='utf8')
text=text.replace('新版唯一场景包的重建结果为','纹理优化前，新版唯一场景包的重建结果为')
text=text.replace('当前包的构建结果和缺失卡图清单位于 `work/DynamicCards/LatestOnly`。','新版映射及缺失卡图记录位于 `work/DynamicCards/LatestOnly`，最新压缩包结果位于 `work/DynamicCards/Optimization`。')
text=text.replace('approximately 44.3 GiB after consolidation',f'approximately {source/1024**3:.2f} GiB after texture consolidation')
heading='\n\n## 2026-09-07：Windows 包体积与小卡加载修复'
if heading in text:text=text.split(heading)[0]
path.write_text(text.rstrip()+report,encoding='utf8')
summary={'beforeBytes':before,'afterBytes':after,'reductionPercent':reduction,'sourceBytes':source,'pageBeforeMs':database['totalMs'],'pageAfterMs':page['totalMs']}
(r/'summary.json').write_text(json.dumps(summary,indent=2))
print(json.dumps(summary),flush=True)
