# 原版 Seltkirk 闪卡资源追踪（只读 source-notes）

> 本文件是**只读分析记录**。分析期间未修改任何代码、模型、Unity 原资源或湖女资源，未调用 Unity 编辑器/控制文件。
> 分析对象：静态图 `art 20161800`，动态闪卡 `catalog id 20161801`。
> 目的：在重启"湖女制作"之前，先把一张**原版人物闪卡**的精确资源引用关系钉死，供主 Codex 亲自在 Unity 中渲染、查网格与贴图时对照。

---

## 0. 一句话结论

Seltkirk 闪卡是一张 **Legacy2017 转换来的 5 网格骨骼动画闪卡**：
`Card.prefab`（根 `Card` → 子 `20161801` → `Pivot`）内含 **5 个 SkinnedMeshRenderer + 7 个 MeshRenderer + 20 个 ParticleSystem + 3 个 Animator + 12 个 `DynamicCardAnimatedMaterialProperty`**，
外链 **42 个资产（29 材质 / 9 网格 / 3 控制器 / 1 运行时脚本）**，全部落在 `Assets/DynamicCards/Content/Old/Legacy2017/Shared/` 与同目录 `20161801/` 内，**无缺失引用**。
静态图 `20161800.png` 与动态卡**不是同一份贴图**：静态图是独立的 1024×1024 Addressable Sprite，动态卡贴图是另一份 1024×1024 `20161801_13952_20161801.png` 图集。

---

## 1. 身份与入口

### 1.1 静态画 `20161800`

| 项 | 值 |
|---|---|
| 路径 | `Assets/Addressables/Cards/20161800.png` |
| GUID | `c612aabdeb6ba074683463f6d90996cd` |
| 文件 | 366 227 B，2026-09-01 12:15:34 |
| 像素 | **1024 × 1024** |
| 导入 | `textureType: 8`（Sprite）、`spriteMode: 1`（Single）、mipmap 关、`alphaIsTransparency: 1`、`maxTextureSize: 2048`、`spritePixelsToUnits: 100` |
| Sprite ID（不是序列化 fileID） | `9a1f3ffdb4ab8fc4d811cd2afa6f4765`；场景引用的 fileID 为 `21300000` |
| Addressables | `Default Local Group`，address = **`20161800`**，GUID 同上 |

- 文件见 `Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset` L1896–1899。

### 1.2 微缩图 `20161800_slot`

| 项 | 值 |
|---|---|
| 路径 | `Assets/Addressables/Miniatures/20161800_slot.png` |
| GUID | `00f8dd79c13400442a16ffe59c57bde1` |
| 文件 | 44 398 B |
| 导入 | `textureType: 8`（Sprite）、Single |
| Addressables | `Miniatures` 组，address = **`20161800_slot`** |

### 1.3 catalog 条目（动态卡入口）

文件 `Assets/DynamicCards/Content/catalog.json` L28068–28124：

```json
{
  "id": "20161801",
  "artIds": ["20161800"],
  "prefab": "Assets/DynamicCards/Content/Old/Legacy2017/20161801/Card.prefab",
  "audio": "Assets/DynamicCards/Content/Old/Legacy2017/Audio/994972981.wav",
  "pivot": "20161801/Pivot",
  "fieldOfView": 25.0,
  "cameraDistance": -29.871030807495117,
  "nearClip": 5.0,
  "farClip": 130.0,
  "xStart": -6.0, "xEnd": 6.0, "yStart": -2.0, "yEnd": 2.0,
  "introDuration": 2.9666666984558105,
  "loopDuration": 9.0,
  "cutTime": -1, "topMargin": 0,
  "particleEvents": [], "uvMotions": [], "transformPairs": [],
  "viewMotions": [ 见 §8 ], "cameraParents": [], "jiggles": [],
  "wiggles": [], "materialValues": [], "initialTransforms": [],
  "sourceId": "20161801", "sourceVersion": "Legacy2017"
}
```

要点：
- **一个 art 对应一个 id**：`artIds = ["20161800"]`，运行时 `DynamicCardLibrary.Load(artId)` 用 artId 反查 entry（`DynamicCardLibrary.cs` L168、L290–292）。所以 UI 侧绑定 `20161800`，加载到的就是 `20161801` 的这张闪卡。
- `pivot = "20161801/Pivot"`：注意**多一层 `20161801` 包裹**。`DynamicCardView` 用 `model.transform.Find(entry.pivot)` 定位（L302），所以层级必须是 `Card/20161801/Pivot`。
- viewMotions 只作用于 `_1_Mesh`（槽 0、2）与 `HelmFireRefl`（槽 0）。

### 1.4 预制体

| 项 | 值 |
|---|---|
| 路径 | `Assets/DynamicCards/Content/Old/Legacy2017/20161801/Card.prefab` |
| GUID | `19f136ca9ff7ae44e90d92815b50d119` |
| 文件 | 1 437 851 B，2026-09-11 21:55:07 |
| YAML 行数 | 59 239 |
| Addressables | **未登记**（不在任何 `AssetGroups/*.asset` 中）。运行时由 `Library/DynamicCardsBundles/<target>/cards-legacy2017-*.bundle` 载入 |

`DynamicCardBundleBuilder.Build`（`Assets/DynamicCards/Editor/DynamicCardBundleBuilder.cs`）按 `catalog.prefab` + `catalog.audio` 取 `AssetDatabase.GetDependencies(..., true)`，把 `.controller` 单列为显式根（L94–104），因此 **prefab / wav / 全部 controller / 全部 anim clip / 材质 / 网格 / 贴图** 都进同一个 `cards-legacy2017-0xx.bundle`。

同目录还有一份转换记录 `conversion.json`（306 行，10 968 B），是"原始材质/动画名 → 现资产"的对照表，见 §4、§6。

---

## 2. `Card.prefab` 结构

### 2.1 文件与组件统计

| 类 | 数量 | 说明 |
|---|---|---|
| `!u!1` GameObject | 115 | |
| `!u!4` Transform | 115 | 1 个根（`Card`） |
| `!u!137` SkinnedMeshRenderer | **5** | 角色/头/盔甲反射/第二头/发射器 |
| `!u!23` MeshRenderer | **7** | 5 张共享 Quad + 背景 + 云 |
| `!u!33` MeshFilter | **7** | 与上表一一对应 |
| `!u!198` ParticleSystem | **20** | 主要位于 `Pivot/VFX`；`Souls_FX` 在右腕骨下 |
| `!u!199` ParticleSystemRenderer | **20** | |
| `!u!95` Animator | **3** | 根 / Pivot / 右腕 |
| `!u!114` MonoBehaviour | **12** | 全部是 `DynamicCardAnimatedMaterialProperty`（脚本 GUID `7ad2b996f6523fa43a7cbfe026f48fc7`） |
| 其它脚本 | 0 | 无 Camera/自定义相机脚本；`InfoHolderRoot` 仅空挂点 |

骨架：**60 根 `Seltkirk_Rig_*SHJnt`**，全部 5 个 SkinnedMeshRenderer 都绑定同一批 **60 bones**，`m_RootBone = Seltkirk_Rig_ROOTSHJnt`。

### 2.2 层级树（`Card` 根，缩进即父子）

```
Card                         ← 根；挂 Animator(Global_Source_2580136d.controller)
  20161801                   ← 空包裹层（catalog.pivot 的前缀）
    Pivot                    ← 挂 Animator(Source_279e8f9c.controller)
      _1_Mesh                ← 角色主体网格（SkinnedMeshRenderer，5 材质槽）
      _1_Head                ← 头（SkinnedMeshRenderer，FaceGood）
      _2_Head2               ← 第二头/替代头（SkinnedMeshRenderer，FaceBad）
      HelmFireRefl           ← 头盔火光反射（SkinnedMeshRenderer，HelmFireRefl）
      SeltkirkEmitter        ← 粒子发射体（SkinnedMeshRenderer，Empty）
      Seltkirk_Rig           ← 骨架根
        Seltkirk_Rig_SHJntGrp
          Seltkirk_Rig_ROOTSHJnt
            Seltkirk_Rig_Vandergrift_COG_AuxSHJnt …（Vandergrift 半身，可见于背景/前景）
            Seltkirk_Rig_Seltkirk_COG_AuxSHJnt …
              Seltkirk_Rig_Seltkirk_Spine1→2→3
                l/r_Seltkirk_Arm_Clavicle→Shoulder→Elbow→Wrist
                  （右腕）Seltkirk_Rig_r_Seltkirk_Arm_WristSHJnt
                    Souls_FX / SwordGlow / SwordBlik   ← 3 个 Quad 网格挂点
                Seltkirk_Rig_Seltkirk_Neck_AuxSHJnt
                  Seltkirk_Rig_Seltkirk_Head_AuxSHJnt
                    Nose / Brows_In / Brows_Out / Cheek / Jaw / Mouth / Blink（左右镜像）
                Seltkirk_Rig_Seltkirk_NeckChain_AuxSHJnt
              腿链 l/r：Hip→Knee→Ankle→Ball→Toe
              Belt1→Belt2
      VFX                    ← 20 个粒子系统 + 4 个广告牌 MeshRenderer 的父节点
        MoonGlow_FX, DustVeryFront_FX, BodySmoke_FX, SparksBokeh_FX,
        FireWallBack_FX, CloudsGood_2ndLayer_FX, DustSmallGrndRight_FX,
        Fire_FX, Fire2_FX, DustSmallGrndLeft_FX, FireVeryFront_FX,
        FireDecal(MR), SparksMid_FX, SoldierShadow(MR), VirnetteFire(MR),
        ShaftsGood_FX, SparksBack_FX, DustLeft_FX, BodyDark_FX,
        DustRight_FX, BackFlash_FX, LensFlareGood_FX, Clouds(MR)
      _2_background2         ← MeshRenderer（背景板 mesh，材质 SkyBad）
      Sky1FX                 ← **空 Transform 标记（无任何组件）**
      InfoHolderRoot
        CameraSettingsHolder / RotationLimitHolder   ← 仅空挂点，无组件
      Clouds                 ← MeshRenderer（网格 `20161801_380446_Sky1FX.asset`，材质 Clouds）
      __SourceClipClock      ← 空标记 Transform（无组件，纯转换标记）
      __SourceMaterial_<hash> ×12   ← 各挂 1 个 DynamicCardAnimatedMaterialProperty
```

> 12 个 `__SourceMaterial_*` 与 `__SourceClipClock` 均为转换残留命名，**任何 .cs 都不引用**（全工程 grep `__Source` 无命中）。它们只是动画曲线/材质动画的挂载点。

### 2.3 渲染器 → 网格 → 材质槽（顺序即 slot）

**SkinnedMeshRenderer（5）** — 全部 `m_Bones = 60`，`m_RootBone = Seltkirk_Rig_ROOTSHJnt`

| GameObject | mesh 资产 | 三角面 | 材质槽（slot 顺序） |
|---|---|---|---|
| `_1_Mesh` | `20161801_380436__1_Mesh.asset` | 1765 | `[Matcap, RecolorAlphaCut, Matcap, Tents, SkyGood]` |
| `_1_Head` | `20161801_380440__1_Head.asset` | 273 | `[FaceGood]` |
| `_2_Head2` | `20161801_380442__2_Head2.asset` | 259 | `[FaceBad]` |
| `HelmFireRefl` | `20161801_380450_HelmFireRefl.asset` | 76 | `[HelmFireRefl]` |
| `SeltkirkEmitter` | `20161801_380448_SeltkirkEmitter.asset` | 20 | `[Empty]` |

**MeshRenderer（7）**

| GameObject | mesh 资产 | 三角面 | 材质 |
|---|---|---|---|
| `SwordGlow` | `11210401_462_Quad.asset` | 2 | `SwordGlow` |
| `SwordBlik` | `11210401_462_Quad.asset` | 2 | `SwordBlik` |
| `SoldierShadow` | `11210401_462_Quad.asset` | 2 | `SoldierShadow` |
| `FireDecal` | `11210401_462_Quad.asset` | 2 | `FireDecal` |
| `VirnetteFire` | `11210401_462_Quad.asset` | 2 | `VignetteFire` |
| `_2_background2` | `20161801_380444__2_background2.asset` | 12 | `SkyBad` |
| `Clouds` | `20161801_380446_Sky1FX.asset` | 200 | `Clouds` |

**ParticleSystemRenderer（20）** — `renderMode: 0 = Billboard`，除标注外

| GameObject | renderMode | 材质 |
|---|---|---|
| `MoonGlow_FX` | 0 | `FX_MoonGlow` ×2 |
| `DustVeryFront_FX` | 0 | `FX_Dust` |
| `DustLeft_FX` | 0 | `FX_Dust` |
| `DustRight_FX` | 0 | `FX_Dust` |
| `BodySmoke_FX` | 0 | `FX_BodySmoke` |
| `BodyDark_FX` | 0 | `FX_BodyDark` |
| `SparksBokeh_FX` | 0 | `FX_SparksBokeh` |
| `FireWallBack_FX` | 0 | `FX_FirewallBack` |
| `CloudsGood_2ndLayer_FX` | 0 | `FX_Clouds` |
| `DustSmallGrndRight_FX` | 0 | `FX_Sand` |
| `DustSmallGrndLeft_FX` | 0 | `FX_Sand` |
| `Fire_FX` | 0 | `FX_Fire` |
| `Fire2_FX` | 0 | `FX_Fire` |
| `FireVeryFront_FX` | 0 | `FX_Fire` |
| `ShaftsGood_FX` | 0 | `FX_Shafts` |
| `BackFlash_FX` | 0 | `FX_Backflash` |
| `LensFlareGood_FX` | 0 | `FX_LensFlare` |
| `SparksMid_FX` | 1 (Mesh) | `FX_Sparks` |
| `SparksBack_FX` | 1 (Mesh) | `FX_Sparks` |
| `Souls_FX` | 1 (Mesh) | `FX_Souls` |

> 特殊情况：`BodyDark_FX` 的 ParticleSystem 形状模块 `m_Mesh` 指向 `20161801_380438_SeltkirkEmitter.asset`，`m_SkinnedMeshRenderer` 指向 `SeltkirkEmitter` 的渲染器。

---

## 3. 网格资产（`Assets/DynamicCards/Content/Old/Legacy2017/Shared/`）

| 资产 | GUID | 子网格 | 子网格顶点 | 三角面 | bone |
|---|---|---|---|---|---|
| `11210401_462_Quad.asset` | `fb3a6e4e16ad32f4aad5e3ed4b9470c4` | 1 | 4 | 2（1×1 单位四边形，extent 0.5） | 共享 |
| `20161801_380436__1_Mesh.asset` | `66c0b0f69918c2147bbd8fd4b12cdef5` | **5** | 257 / 903 / 286 / 26 / 14 | 1765（825+3357+987+90+36 indices） | 60 |
| `20161801_380438_SeltkirkEmitter.asset` | `640faa0be6d3f40449ea6a8921c01fef` | 1 | 36 | 20 | 粒子形状网格 |
| `20161801_380440__1_Head.asset` | `d454725db5784cf4aa8510530c1b4238` | 1 | 166 | 273 | 60 |
| `20161801_380442__2_Head2.asset` | `ed579d73d5f95854fb6e446233d0b2f8` | 1 | 150 | 259 | 60 |
| `20161801_380444__2_background2.asset` | `929abba6748950a40bca12ebf6323ff5` | 1 | 14 | 12 | — |
| `20161801_380446_Sky1FX.asset` | `b0e7cb56c1819c74f9b9e3b5d839e874` | 1 | 121 | 200 | — |
| `20161801_380448_SeltkirkEmitter.asset` | `20e83b4a3e556404f8564334bc63ef24` | 1 | 36 | 20 | 60 |
| `20161801_380450_HelmFireRefl.asset` | `82b90bf4596335c45b771f9fd2f63e3e` | 1 | 48 | 76 | 60 |

说明：
- `_1_Mesh` 的 5 个子网格按 slot 依次喂给 `[Matcap, RecolorAlphaCut, Matcap, Tents, SkyGood]`。
- 资产内的子网格 `vertexCount` 之和（1486）大于 Unity 运行时 `mesh.vertexCount`（1425），因为跨子网格共享顶点；以 Unity 运行时为准见 §12。
- Quad 被 5 个 MeshRenderer 复用，是**同一份网格资产**。

> 引擎侧已有一份 Unity 内实测（`Assets/Experiments/LadyLakePremium/Acceptance/source-audit.json`）与上表一致：`_1_Mesh` 1425 顶点 / 1765 三角 / 60 骨 / 354 多权重顶点；`_1_Head` 166/273；`_2_Head2` 150/259；`HelmFireRefl` 48/76；`SeltkirkEmitter` 36/20。

---

## 4. 材质（29 个，全部外链自 prefab）

路径前缀 `Assets/DynamicCards/Content/Old/Legacy2017/Shared/`；着色器路径前缀 `Assets/DynamicCards/Shaders/Legacy/`。
`originalName` 取自 `conversion.json`。

| 资产文件 | GUID | originalName | 着色器 | 队列 |
|---|---|---|---|---|
| `20161801_380366__…_Empty.mat` | `ca37e5c0cce5bfd4bbc4a2f0076adc0d` | `[20161801]Seltkirk_Empty` | `VFX_Common_AdditiveAlpha` | 3000 |
| `20161801_380368__…_FX_Backflash.mat` | `56711c2468e27554fac77e5ee58096c5` | `…FX_Backflash` | `VFX_Common_AdditiveAlpha` | 3018 |
| `20161801_380370__…_FX_BodyDark.mat` | `2034b6fbb32cb654db9935d2927a6d1d` | `…FX_BodyDark` | `VFX_Common_AlphaBlended_alphaPower` | 3010 |
| `20161801_380364__…_Clouds.mat` | `c63631e128474704d8a7aea1e49af85d` | `…Clouds` | `VFX_Common_AlphaBlended_moveInDirection_VertexColor_UVNoise` | 3010 |
| `20161801_380372__…_FX_BodySmoke.mat` | `57b30ca484165d744b816e687dac135d` | `…FX_BodySmoke` | `…_moveInDirection_VertexColor_UVNoise` | 3011 |
| `20161801_380374__…_FX_Clouds.mat` | `e20b75aae5b3c674c9f4903c4b519485` | `…FX_Clouds` | `VFX_Common_AlphaBlended_alphaPower` | 3009 |
| `20161801_380376__…_FX_Dust.mat` | `9fe165ddb46d664459e41addd5d44dc3` | `…FX_Dust` | `VFX_Common_AlphaBlended_alphaPower` | 3035 |
| `20161801_380378__…_FX_Fire.mat` | `01a04d96d3a3a8845ad4919e6426be4b` | `…FX_Fire` | `VFX_Common_AdditiveAlpha` | 3021 |
| `20161801_380380__…_FX_FirewallBack.mat` | `2ae2ab7b71475a4448c06878970e8e40` | `…FX_FirewallBack` | `…_moveInDirection_VertexColor_UVNoise` | 3020 |
| `20161801_380382__…_FX_LensFlare.mat` | `0063bc1c04cec084aad2e3305aa286af` | `…FX_LensFlare` | `VFX_Common_AdditiveAlpha` | 3045 |
| `20161801_380384__…_FX_MoonGlow.mat` | `785eb05b92144bf46be9f6844e06ab92` | `…FX_MoonGlow` | `VFX_Common_AdditiveAlpha` | 3011 |
| `20161801_380386__…_FX_Sand.mat` | `b76f3e6ba1a92de45a9c1e2516cd58bb` | `…FX_Sand` | `VFX_Common_AlphaBlended_alphaPower` | 3020 |
| `20161801_380388__…_FX_Shafts.mat` | `3e768aa577dfbfa439d25b14f4bd26c7` | `…FX_Shafts` | `VFX_Common_AdditiveAlpha` | 3011 |
| `20161801_380390__…_FX_Souls.mat` | `53393e50c51f55343941a68aa0c4c7e3` | `…FX_Souls` | `VFX_Common_Additive_moveInDirection_VertexColor_UVNoise` | 3020 |
| `20161801_380392__…_FX_Sparks.mat` | `67c7969466c4ff646a206588f74793d8` | `…FX_Sparks` | `VFX_Common_AdditiveAlpha` | 3040 |
| `20161801_380394__…_FX_SparksBokeh.mat` | `da97f9b6cd7ef2a42a8345380286d14d` | `…FX_SparksBokeh` | `Custom_morkvarg_dust` | 3100 |
| `20161801_380396__…_FaceBad.mat` | `555ea37c023d1c54888853a9e5e3b381` | `…FaceBad` | `Custom_Cards_CardCore_ImageLayerShaderDissolve` | 3011 |
| `20161801_380398__…_FaceGood.mat` | `ccfb6b2c9974bdd448faab35372e4d82` | `…FaceGood` | `Custom_Cards_CardCore_ImageLayerShaderDissolve` | 3010 |
| `20161801_380400__…_FireDecal.mat` | `f96efebb1187d324fa3b9d7ba5d0188f` | `…FireDecal` | `VFX_Common_Additive_moveInDirection_VertexColor_UVNoise` | 3010 |
| `20161801_380402__…_HelmFireRefl.mat` | `b3f034144d772a44d8b04644406a5283` | `…HelmFireRefl` | `VFX_Common_Additive_moveInDirection_VertexColor_UVNoise` | 3021 |
| `20161801_380404__…_Matcap.mat` | `afa38f6ae2fab1c4e996caeb7dacda1f` | `…Matcap` | `VFX_Special_MatCap_Metalic` | 3020 |
| `20161801_380406__…_RecolorAlphaCut.mat` | `dbd2a189a0e2e654787605e303c16229` | `…RecolorAlphaCut` | `VFX_Effects_ImageLayerShaderFakeLight_ZBuffer` | 3010 |
| `20161801_380408__…_SkyBad.mat` | `561b30275875f224a8efe18a530d07b9` | `…SkyBad` | `Custom_Cards_CardCore_ImageLayerShader_Zbuffer` | 3000 |
| `20161801_380410__…_SkyGood.mat` | `d1b1feaa10a2d6140b97a80f59a005d8` | `…SkyGood` | `Custom_Cards_CardCore_ImageLayerShader_ZbufferTint` | 3000 |
| `20161801_380412__…_SoldierShadow.mat` | `0ee25528be723b341b56192c9da11da5` | `…SoldierShadow` | `VFX_Common_AlphaBlended_alphaPower` | 3010 |
| `20161801_380414__…_SwordBlik.mat` | `a8408aa217cd2a34bafbb247c25d4c4a` | `…SwordBlik` | `VFX_Special_surface_glow` | 3020 |
| `20161801_380416__…_SwordGlow.mat` | `b038ddbc48a89aa49bf97c1243a5ceb6` | `…SwordGlow` | `VFX_Common_Additive_moveInDirection_VertexColor_UVNoise` | 3021 |
| `20161801_380418__…_Tents.mat` | `0446a2e5f853d3241b48baee4d6b5505` | `…Tents` | `VFX_Effects_ImageLayerShaderFakeLight` | 3011 |
| `20161801_380420__…_VignetteFire.mat` | `92a8cdfc85ec3b24192367cadadc7721` | `…VignetteFire` | `VFX_Common_Additive_moveInDirection_VertexColor_UVNoise` | 3100 |

**7 个材质使用图集 `_MainTex`**（= `conversion.json.textureAssignments`，已被 `grep` 核实）：
`FaceGood`、`Matcap`、`RecolorAlphaCut`、`FaceBad`、`Tents`、`SkyGood`、`SkyBad` → GUID `9488a46ee62b4494b94aa4c107e993a8`。

其它值得注意的材质参数（供对光/对色）：
- `Matcap`：`_Color = 0.502`、`_CutOut = 1`、`_LightRotation = 0`（被 viewMotion 驱动）。
- `RecolorAlphaCut`：`_TintMainTex = 0.498`、`_LightIntensity = 0.1`。
- `HelmFireRefl`：`_AlphaMult = 1.5`、`_AlphaOverride = 1`、`_AlphaPower = 2`、`_MainU/_MainV = 0`、`_TintColor = (1, 0.408, 0.272)`。
- `Empty`：`_MainTex = blackPixel`、`_AlphaPower = 1`、`_Brightness = 1`。

---

## 5. 贴图（27）与着色器（12）

### 5.1 贴图

| GUID | 资产 | 用途 |
|---|---|---|
| `9488a46ee62b4494b94aa4c107e993a8` | `20161801_13952_20161801.png`（**1024×1024，733 648 B，图集**） | 7 个材质的 `_MainTex` |
| `845f309bc6ecc174e9fecf1ea1d0a9f6` | `20161801_380430__20161801_Seltkirk_FXMap.png`（1024×1024，607 055 B） | `_LightMask` / `_Light` |
| `2ac1d48d49a9f3647a03ab4a83a3d36f` | `20022701_355668_Gradient_reflect.png` | Matcap 的 `_MatCap` |
| `165aa0b872fbb82458e1cc2eabcae101` | `11211301_35160_TilingNoise3D_small_uncompressed.png` | RecolorAlphaCut 的 `_Turbulence` |
| `12ac5be8463305748aeb85f0073a2f14` | `20161801_380360_SandDustTiling.png` | HelmFireRefl `_MainTex` |
| `cbebbb8b26f350745ae4a3da2dc0cca0` | `11211301_35156_Noise3D_Fire_Tiling.png` | HelmFireRefl `_NoiseTex` |
| `70737c9b961954f44a65f3a4bfdaa889` | `11211301_35166_blackPixel.png` | Empty `_MainTex` |
| `b6c64f9f9a33fcb44b0775e9bc0bdc59` | `20161801_380358_VFX_smokeSingleCloud.png` | 烟/云 |
| `e054d4c9447f02940ab176636271a1a5` | `20161801_380356_golem_rayLight_quad_PT.png` | 光柱/射线 |
| `cd402a79ba781024198906c2351bb624` | `20161801_380362_VFX_lineFG_gradient.png` | 线状渐变 |
| `e1288769f235f4e43a17e8ce1912d64b` | `20161801_380422_VFX_GhostSkullHead.png` | Souls 幽灵头 |
| `308276d74929184478dd385f0af74cbd` | `20161801_380424_SmokePuff_p1.png` | 烟团 |
| `bf4006d643339d841b0ee856a398a662` | `20161801_380426__HighLight_EnemyGrad.png` | 高光/渐变 |
| `cbb109ee1844cd14e8e4532c14370cad` | `20161801_380428_VFX_SmokeMagical01.png` | 魔法烟 |
| `163e088bfb4720e44a65d523e25e6012` | `20161801_380432_VFX_MaskVignette.png` | 暗角遮罩 |
| `36c00c773e047b34881a5b2592e337ed` | `20161801_380434_SmokeFresnel01.png` | 菲涅尔烟 |
| `66afd2a092bcb3c4689f19485fa33b71` | `11221201_48214_VFX_Sparks_dusty.png` | 火星 |
| `c60d35bd291b0f1459cdb66301e15a4f` | `11211201_33630_VFX_saprks_bokeh.png` | 散景火星 |
| `a93840b8ef178d84386ca3d143e1a5c2` | `12210801_82856_VFX_Sparks_metal_lowglow4.png` | 金属火星 |
| `a816ca2a79d53a842bcc3c8c7c29dd3b` | `11210701_23728_bigFire_15x10_Screen.png` | 火焰图集 |
| `76d864059b2727449bb78dc73e08dd38` | `11210501_19264_VFX_clouds.png` | 云 |
| `8bc592d0f517d634580f6b25b7a728f1` | `11220501_39440_clouds_4x4.png` | 云（4×4） |
| `8574eb1c83bf6e74ea2790b6b80be060` | `11211001_29382_NoiseCloud1_Darker.png` | 暗云噪声 |
| `a4f1c7d41bc0cff408467169d0e0a95f` | `11221401_51548_VFX_softGlowalpha.png` | 柔光 |
| `addc7b1c69532354fa7c03683e0edfe3` | `20030001_364496_Line01.png` | 线条 |
| `1f402289dc389984a8e15e2f8bbb91f3` | `20003401_292648_Flare00.png` | 镜头光晕 |
| `ceb979366d1d6c0469273ddb8dd0bc48` | `13231011_154736_GlowLenFlare.png` | 辉光光晕 |

图集导入设置（`20161801_13952_20161801.png.meta`）：`textureType: 0`（Default，**不是 Sprite**）、mipmap 开、wrap clamp、`maxTextureSize: 2048`（源 1024）、sRGB 开。

### 5.2 着色器（`Assets/DynamicCards/Shaders/Legacy/`）

| GUID | 文件 |
|---|---|
| `6bcf9ba871f784c92511f2ebea2cc65e` | `VFX_Common_AdditiveAlpha.shader` |
| `6f4d3fc86b63a77abd3da0d233720efd` | `VFX_Common_AlphaBlended_alphaPower.shader` |
| `32da831e96124f99a5be335beb3dbf9b` | `VFX_Common_AlphaBlended_moveInDirection_VertexColor_UVNoise.shader` |
| `5c805b95bcf8196ccbc52e1ff55c796e` | `VFX_Common_Additive_moveInDirection_VertexColor_UVNoise.shader` |
| `683dad7ff1855f925f50b0a80cefa14e` | `VFX_Special_MatCap_Metalic.shader` |
| `1f661c46f6e8142c123d6a071f5787e4` | `VFX_Special_surface_glow.shader` |
| `27f7c2363cb2d9f27d969630b993eaa8` | `VFX_Effects_ImageLayerShaderFakeLight.shader` |
| `42b719562d9e5a4591aa91be6d1b5eba` | `VFX_Effects_ImageLayerShaderFakeLight_ZBuffer.shader` |
| `8999db8b14b21811365199dd76834c2e` | `Custom_Cards_CardCore_ImageLayerShaderDissolve.shader` |
| `80087861b23b4b1f12ade3006d94763e` | `Custom_Cards_CardCore_ImageLayerShader_Zbuffer.shader` |
| `60dde7d66b24494a95e4eb1186c4a8c5` | `Custom_Cards_CardCore_ImageLayerShader_ZbufferTint.shader` |
| `8b75775dd493bee188b62f4fc51db4ae` | `Custom_morkvarg_dust.shader` |

---

## 6. 动画图

### 6.1 预制体里 3 个 Animator

| 挂载 GameObject | 控制器 | GUID | 层 |
|---|---|---|---|
| `Card`（根） | `Global_Source_2580136d.controller` | `a97710b53f6d3ce4d956219d94b02848` | `VFX` |
| `20161801/Pivot` | `Source_279e8f9c.controller` | `90a59046d98cfe44d8252fc127026670` | `Base Layer` + `ParameterOverlay` |
| `Seltkirk_Rig_r_Seltkirk_Arm_WristSHJnt`（全路径见上表） | `Source_ec65446a.controller` | `3fa7de2e0044447468c67c7321e33bfb` | `VFX` + `Base Layer` |

三者均 `m_Avatar: 0`、`m_ApplyRootMotion: 0`、`m_CullingMode: 0`、`m_UpdateMode: 2`（UnscaledTime）。

### 6.2 控制器 → clip（`Assets/DynamicCards/Content/Old/Legacy2017/20161801/`）

| 控制器 | 状态/clip | clip GUID | 长度 |
|---|---|---|---|
| `Global_Source_2580136d.controller` | `[20161801]Seltkirk_VFX_Intro` | `6809aceae61620f439b6f2f489ef11bc` | 2.967 s |
| | `[20161801]Seltkirk_VFX_Loop` | `f75d0ebfbd3951743a9c19a297459e1f` | 9.000 s |
| `Source_279e8f9c.controller` | `[20161801]Seltkirk_Ogo_Intro` | `b7bf3a60f0465924e9b3dc0705fd1cd9` | 2.967 s |
| | `[20161801]Seltkirk_Ogo_Loop` | `71eb3422877d4e84da7a074f390460fd` | 9.000 s |
| | `Intro` | `1053f8d1c10a7904ba1469b1bf45406f`（`Source_279e8f9c_Intro.anim`） | 2.967 s |
| | `Loop` | `40b9b35f79a1de34599061b6d8800b71`（`Source_279e8f9c_Loop.anim`） | 9.000 s |
| `Source_ec65446a.controller` | `[20161801]Seltkirk_VFX_Intro` | `b1e8e34c86f47b547b0a3c9045e551b6` | 2.967 s |
| | `[20161801]Seltkirk_VFX_Loop` | `493156305bb07f947ab04493e11f83e9` | 9.000 s |

`conversion.json.animations` 的原始映射（原始名 → 挂点）：
- `20161801/Pivot/VFX` → `[20161801]Seltkirk_VFX_Intro/Loop`
- `20161801/Pivot` → `[20161801]Seltkirk_Ogo_Intro/Loop`
- `20161801/Pivot/Seltkirk_Rig_r_Seltkirk_Arm_WristSHJnt` → `[20161801]Seltkirk_VFX_Intro/Loop`

### 6.3 未被引用的控制器与"Decoded"中间产物（重要）

同目录下另有 **3 个控制器**与 **6 个 `Decoded_*` clip**，它们**不被 prefab 引用**：

| 资产 | GUID | 状态 |
|---|---|---|
| `Global_VFX.controller` | `857fd42f5ff1df6469e19e3465746f82` | 无任何文件引用；内部引用 `Decoded_Card__…_VFX_Intro/Loop` |
| `Pivot.controller` | `6c198166da0d5194fbb0d0d172ea0ec9` | 无任何文件引用；内部引用 `Decoded_Pivot_*` |
| `Seltkirk_Rig_r_Seltkirk_Arm_WristSHJnt.controller` | `c895533d799a0a144b9b12b93084d312` | 无任何文件引用 |
| `Decoded_Card__20161801_Seltkirk_VFX_Intro.anim` | `a03aacc5dbae2ce46b51a96628173a49` | 仅被 `Global_VFX.controller` 引用 |
| `Decoded_Card__20161801_Seltkirk_VFX_Loop.anim` | `d6b46be453dbcd14587e33735faafd40` | 仅被 `Global_VFX.controller` 引用 |
| `Decoded_Pivot__…Ogo_Intro.anim` | `1f5ea86921e2174488cec87b0e494444` | 仅被 `Pivot.controller` 引用 |
| `Decoded_Pivot__…Ogo_Loop.anim` | `9061d6fffc8c77945bf050aeffa9f45d` | 仅被 `Pivot.controller` 引用 |
| `Decoded_Pivot_Intro.anim`（6.98 MB） | `8a434589522dd314e81a71b80d01e749` | 仅被 `Pivot.controller` 引用 |
| `Decoded_Pivot_Loop.anim`（14.66 MB） | `9632bbe87bdc5644e837824e24f5c1e1` | 仅被 `Pivot.controller` 引用 |

> `Decoded_Pivot_Intro/Loop.anim` 体积巨大（6.98 / 14.66 MB），是转换时的解码中间产物。运行时实际走 `Source_*` 控制器，**不要**把它们当成渲染结果的一部分。
> prefab 只通过 `AssetDatabase.GetDependencies` 打包依赖，因此这 3 个孤儿控制器与 6 个 Decoded clip **未必**进 bundle（取决于打包时依赖图是否把它们算进去——它们不在 prefab 依赖闭包内）。

---

## 7. 动态材质属性（12 个 MonoBehaviour）

脚本：`Assets/DynamicCards/Runtime/DynamicCardAnimatedMaterialProperty.cs`（GUID `7ad2b996f6523fa43a7cbfe026f48fc7`）。
`Type: 1 = Color, 2 = Float`；`Targets` 直接指向同 prefab 内的 Renderer，`Slot = 材质槽`。

| 挂载 GameObject | Type | Slot | 属性 | 初值 | 目标渲染器 |
|---|---|---|---|---|---|
| `__SourceMaterial_0d0c2687` | 1 | 0 | `_Color` | 0.502 灰 | `_1_Mesh` |
| `__SourceMaterial_364cf203` | 1 | 1 | `_TintColor` | 白 | `_1_Mesh` |
| `__SourceMaterial_41d220f3` | 1 | 2 | `_TintColor` | 白 | `_1_Mesh` |
| `__SourceMaterial_4f2921fa` | 1 | 2 | `_Color` | 0.502 灰 | `_1_Mesh` |
| `__SourceMaterial_59ba4ac6` | 1 | 1 | `_TintMainTex` | 0.498 灰 | `_1_Mesh` |
| `__SourceMaterial_ade9be6c` | 1 | 0 | `_TintColor` | 白 | `_1_Mesh` |
| `__SourceMaterial_aeef8513` | 1 | 4 | `_TintColor` | 白 | `_1_Mesh` |
| `__SourceMaterial_b8d48e6b` | 1 | 3 | `_TintMainTex` | 0.502 灰 | `_1_Mesh` |
| `__SourceMaterial_da776c9c` | 1 | 3 | `_TintColor` | 白 | `_1_Mesh` |
| `__SourceMaterial_77b776e8` | 2 | 0 | `_Progress` | 0 | `_1_Head` |
| `__SourceMaterial_ef11bd80` | 2 | 0 | `_Progress` | 1 | `_2_Head2` |
| `__SourceMaterial_5f0c226d` | 2 | 0 | `_AlphaMult` | 1.5 | `HelmFireRefl` |

> `DynamicCardView.Initialize` 会在 `Animator.Rebind/Update(0)` 后对所有 `DynamicCardAnimatedMaterialProperty` 调 `ApplyNow()`（`DynamicCardView.cs` L340–341），因此在**第一帧之前**这些初始颜色/透明度就已写入。

---

## 8. catalog `viewMotions` 与运行时施加

`catalog.json` 的 3 条 viewMotions：

| path | property | materialIndex | kind | axis | multiplier |
|---|---|---|---|---|---|
| `20161801/Pivot/_1_Mesh` | `_LightRotation` | 0 | 1 | 1（yaw） | 0.30 |
| `20161801/Pivot/_1_Mesh` | `_LightRotation` | 2 | 1 | 1（yaw） | 0.30 |
| `20161801/Pivot/HelmFireRefl` | `_MainU` | 0 | 1 | 1（yaw） | 0.001 |

运行时由 `DynamicCardSourceControllers`（`Assets/DynamicCards/Runtime/DynamicCardSourceControllers.cs`）在 `Initialize` 时按 `DynamicCardPaths.Find` 解析路径，校验目标 Renderer / 材质槽 / 材质确实有该属性；`kind == 1` 表示"初值 + 轴角度 × multiplier"，通过 MaterialPropertyBlock 写入（L37–43、L84–94）。

对位关系：
- `_1_Mesh` 槽 0、2 **都是 `Matcap`**，而 `Matcap` 材质确实有 `_LightRotation`，所以两条 motion 作用在同一材质的两个槽上 → 随 yaw 转动打光。
- `HelmFireRefl` 槽 0 材质确有 `_MainU`（初值 0）→ 随 yaw 极缓慢滚动火光反射 UV。

`kind == 0`（UV 平移）在本卡为空。

---

## 9. 音频

| 项 | 值 |
|---|---|
| 路径 | `Assets/DynamicCards/Content/Old/Legacy2017/Audio/994972981.wav` |
| 文件 | 2 572 844 B |
| 格式 | RIFF/WAVE，PCM，**48 000 Hz，双声道，16 bit** |
| data 长度 | 2 572 800 B → **≈13.40 s** |

由 `DynamicCardLibrary.Load(artId, withAudio, …)` 在 `preview` 时加载并循环播放（`DynamicCardView.cs` L334–335）。

---

## 10. 引用完整性

`Card.prefab` 内出现 **42 个唯一外部 GUID**，全部分类如下，**0 缺失**：

| 类别 | 数量 |
|---|---|
| 材质 `.mat` | 29 |
| 网格 `.asset` | 9 |
| 控制器 `.controller` | 3 |
| 运行时脚本 `.cs`（`DynamicCardAnimatedMaterialProperty.cs`） | 1 |
| **合计** | **42** |

prefab 内部 fileID → GameObject 名称映射（115 个）、Transform 父子关系、`m_Materials` 槽顺序均已逐条解析，无 `fileID: 0` 悬挂引用（`m_Avatar`、`m_Mesh` 形状辅助项除外）。

反向依赖：`Card.prefab` 自身 **不在任何 Addressables Group** 中，只通过 `catalog.json` 的 `prefab` 字段被 `DynamicCardLibrary` / `DynamicCardBundleBuilder` 使用。

---

## 11. 未引用 / 中间产物清单

| 资产 | 说明 |
|---|---|
| `Global_VFX.controller` | 孤儿控制器（§6.3） |
| `Pivot.controller` | 孤儿控制器（§6.3） |
| `Seltkirk_Rig_r_Seltkirk_Arm_WristSHJnt.controller` | 孤儿控制器（§6.3） |
| 6 个 `Decoded_*` clip | 仅被上述孤儿控制器引用，总计约 21.8 MB |
| `__SourceClipClock` | 空 Transform 标记，无组件 |
| `Sky1FX` GameObject | 空 Transform 标记，无组件（注意与同名网格资产 `20161801_380446_Sky1FX.asset` 区分，后者挂在 `Clouds` GameObject 上） |
| 12 个 `__SourceMaterial_<hash>` | 仅承载 §7 的动画属性组件，命名是转换哈希 |

运行时实际使用的动画资产是 `Global_Source_2580136d.*`、`Source_279e8f9c.*`、`Source_ec65446a.*` 三套。

---

## 12. 与既有 Unity 审计交叉核对

引擎内已生成过一份 Unity 实测（`Assets/Experiments/LadyLakePremium/Acceptance/source-audit.json`，Seltkirk 行 L3–95），可与本追踪互相印证：

| 维度 | 本追踪（资产侧） | Unity 审计（运行时侧） |
|---|---|---|
| SkinnedMeshRenderer | 5 | 5 |
| Animator | 3 | 3 |
| ParticleSystem | 20 | 20 |
| `_1_Mesh` | 5 子网格 / 1486 子网格顶点 / 1765 三角 / 60 骨 | 1425 顶点 / 1765 三角 / 60 骨 / 354 多权重 |
| `_1_Head` | 166 顶点 / 273 三角 | 166 / 273 |
| `_2_Head2` | 150 顶点 / 259 三角 | 150 / 259 |
| `HelmFireRefl` | 48 顶点 / 76 三角 | 48 / 76 |
| `SeltkirkEmitter` | 36 顶点 / 20 三角 | 36 / 20 |
| clips | 8（Intro 2.967 s / Loop 9.000 s） | 同样 8 条、同样时长 |

（`posedSize` 等位姿信息以 Unity 实测为准，本文件不复述。）

**静态图可见区域**：`20161800.png` 是 1024×1024，但真实立绘只占左上 497×713 像素。Unity UV 原点在左下，因而对应 `x ∈ [0, 497]`、`y ∈ [311, 1024]`，不能把这个 UV 坐标误读为图片底部。两条既有代码一致：
- `DynamicCardView.cs` L315–316：`anchorMin = (0, 1-713/1024)`、`anchorMax = (497/1024, 1)`
- `SeltkirkStudy.cs` L25：`uvRect = Rect(0, 1-713/1024, 497/1024, 713/1024)`

---

## 13. 给主 Codex 的检查要点

1. **入口**：闪卡由 `DynamicCardView.Bind(image, "20161800", …)` 驱动；编辑器内有效 bundle 与 `.editor-ready` 存在才尝试加载包。包不可用时，只有显式设置 `AllowEditorSourceLoading = true` 才允许读取 `AssetDatabase`，否则保留静态图。本研究场景仅在运行期间开启原资源读取，退出时恢复。
2. **找模型**：`model = Instantiate(prefab)`，`pivot = model.transform.Find("20161801/Pivot")`。层级多一层 `20161801`，直接找 `Pivot` 会失败。
3. **相机**：catalog 给 FOV 25、`cameraDistance -29.871`、near 5、far 130；`DynamicCardFraming.Apply` 强制 `aspect = 1`，并叠加 `AppearanceOffset = (0, 2, 0)`。
4. **多网格叠加**：`_1_Mesh` 的 5 个槽是 角色(Matcap)×2 + 重着色+AlphaCut + 帐篷 + 天空，几何是同一网格的不同子网格，**渲染顺序由材质队列决定**（3000/3010/3020/3011）。
5. **头部半透明**：`_1_Head`(FaceGood, queue 3010) 与 `_2_Head2`(FaceBad, queue 3011) 用 Dissolve 着色器 + `_Progress` 驱动，第一帧前已由 `ApplyNow()` 写入初值（`_1_Head` 0、`_2_Head2` 1）。若渲染出"错脸/双层脸"，先查这两个 `_Progress` 与队列差。
6. **骨骼**：5 个 SkinnedMeshRenderer 共用 60 骨，根为 `Seltkirk_Rig_ROOTSHJnt`。Vandergrift 半身骨链也在同一 rig 内（背景角色），不要误删。
7. **粒子**：20 个 ParticleSystem 分布在 VFX 层级与右腕的 `Souls_FX` 等节点。`DynamicCardView` 在加载时先 `Stop(clear)` 再按 `playOnAwake` 重播（L328–329）。
8. **孤儿资产**：如果打包体积异常，检查 3 个孤儿 controller 与 6 个 `Decoded_*` clip（≈21.8 MB）是否被误纳入依赖闭包。
9. **静态 vs 动态**：本卡静态图 `20161800.png` 与动态图集 `20161801_13952_20161801.png` 是两份独立 1024×1024 贴图；UI 立绘裁剪区固定 497×713。对照渲染时不要拿图集当静态底图。
