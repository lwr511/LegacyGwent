using System;
using Assets.Script.DynamicCards;
using Cynthia.Card;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LegacyGwent.LadyLakeLab.Presentation
{
    /// <summary>
    /// LadyLake 实验的**卡牌展示适配层**：把实验场景的 2.5D 浮雕渲染进真实卡牌收藏的卡框里，
    /// 并复用收藏的自动摆动 / 鼠标拖动转向 / 视差 / 松手回正。
    ///
    /// 复用关系（逐条对应真实收藏，未自行发明参数）：
    ///   * 牌框资产：真实收藏卡牌 prefab <c>Assets/Resources/Prefab/Cards/ArtCard.prefab</c>
    ///     （Resources 路径 <c>Prefab/Cards/ArtCard</c>）。金色边框 sprite、中立金色阵营图标、
    ///     力量徽章、名称/规则文本框全部来自该 prefab 及其序列化 sprite，不使用自绘金边。
    ///   * 整卡转向与透视：生产组件 <see cref="DynamicCardPresentation"/> +
    ///     <see cref="DynamicCardPerspectiveMesh"/>（收藏里由 ArtCard.cs 第 66-75 行创建/挂接）。
    ///     入场 55° 摆动、MaxPitch / MaxYaw、55° 相机投影除法都来自它。
    ///   * 摆动/拖动驱动：仓库生产代码 <c>Assets/DynamicCards/Runtime/DynamicCardView.cs</c> 的
    ///     LateUpdate 与 OnBeginDrag/OnDrag/OnEndDrag。这里按行照搬同一组常量与公式（见下方常量注释）。
    ///     之所以不直接复用 DynamicCardView：它只会为 catalog 里有 <c>DynamicCardEntry</c> 的
    ///     动态卡加载 3D prefab（<c>DynamicCardLibrary</c>），而 DIY 立绘 <c>c10000000</c>（70006）
    ///     没有任何动态卡 catalog 条目，因此这里精确提取它的驱动，而不是另造一套手感。
    ///   * 卡面区域：与 DynamicCardView.cs 第 313-319 行完全相同的锚点矩形
    ///     <c>(0, 1-713/1024) .. (497/1024, 1)</c> 与 <c>DynamicCardSurface</c> 着色器。
    ///
    /// 场景来源：实验场景自带的 <c>Camera</c>（LadyLakeLabRoot.targetCamera）被接管为
    /// RenderTexture 输出（不再画全屏画面），卡面里的活动画面就是这台相机的实时渲染。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LadyLakeCardStage : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // 生产参数（全部照抄 DynamicCardView.cs，禁止改数值）
        // ------------------------------------------------------------------
        /// <summary>DynamicCardView.cs:13 —— 拖动/摆动归一化水平极限。</summary>
        public const float HorizontalLimit = .7f;
        /// <summary>DynamicCardView.cs:14 —— 拖动/摆动归一化垂直极限。</summary>
        public const float VerticalLimit = 1f / 3f;
        /// <summary>DynamicCardView.cs:488 —— 拖动灵敏度的参考高度。</summary>
        private const float ReferenceHeight = 900f;
        /// <summary>DynamicCardView.cs:489 —— 水平拖动除数。</summary>
        private const float HorizontalTravel = 150f;
        /// <summary>DynamicCardView.cs:489 —— 垂直拖动除数。</summary>
        private const float VerticalTravel = 120f;
        /// <summary>DynamicCardView.cs:400 —— 拖动中的跟随系数。</summary>
        private const float DragLerp = 18f;
        /// <summary>DynamicCardView.cs:400 —— 空闲/回正的跟随系数。</summary>
        private const float IdleLerp = 12f;
        /// <summary>DynamicCardView.cs:398 —— 空闲摆动水平角速度。</summary>
        private const float SwayHorizontalRate = .47f;
        /// <summary>DynamicCardView.cs:398 —— 空闲摆动垂直角速度。</summary>
        private const float SwayVerticalRate = .73f;
        /// <summary>DynamicCardView.cs:396 —— 入场后多久开始空闲摆动（秒）。</summary>
        private const float IdleBlendStart = 1f;
        /// <summary>DynamicCardView.cs:396 —— 松手后多久恢复空闲摆动（秒）。</summary>
        private const float ReleaseBlendStart = 2f;

        /// <summary>浮雕转角范围：DynamicCardCatalog.cs:43 的 DynamicCardEntry 默认值。</summary>
        public const float ArtPitchStart = -6f, ArtPitchEnd = 6f, ArtYawStart = -2f, ArtYawEnd = 2f;

        // ------------------------------------------------------------------
        // 卡面比例：真实收藏卡图窗口宽高 = 497 : 713，与 ArtCard.prefab 的
        // CardImg(560x560) 上 (497/1024, 713/1024) 的锚点矩形一致。
        // ------------------------------------------------------------------
        public const int RenderHeight = 1024;
        public static readonly int RenderWidth = Mathf.RoundToInt(RenderHeight * 497f / 713f);

        /// <summary>DIY 湖中仙女（GwentMap 70006）：Gold / Neutral，力量 25，立绘 c10000000。</summary>
        public const string CardId = "70006";
        /// <summary>真实收藏卡牌 prefab（Resources 路径）。</summary>
        public const string CollectionCardPrefab = "Prefab/Cards/ArtCard";
        /// <summary>卡框/活动面所在的 UI 层（ArtCard.prefab 全部在 layer 5）。</summary>
        public const int UiLayer = 5;

        // 展示层级命名（Editor 的 Attach 与运行时兜底共用）。
        public const string PresentationObjectName = "CardPresentation";
        public const string ArtPivotName = "ArtPivot";
        public const string PresentationCameraName = "PresentationCamera";
        public const string BackdropName = "Backdrop";
        public const string CardHostName = "CardHost";
        public const string CardInstanceName = "ArtCard";
        public const string EventSystemName = "EventSystem";
        public const float PlaneDistance = 5f;
        public static readonly Vector2 ReferenceResolution = new Vector2(1600f, 900f);
        public static readonly Color BackdropColor = new Color(.012f, .035f, .043f, 1f);

        [Header("接线（Attach 时写入，可手工替换）")]
        public LadyLakeLabRoot root;
        public Camera sourceCamera;
        [Tooltip("浮雕视差用：相机绕画面中心的父节点。")]
        public Transform artPivot;
        [Tooltip("真实收藏卡牌 prefab 实例。")]
        public ArtCard card;
        [Tooltip("卡面容器（撑满 Canvas，用来换算卡牌屏幕占比）。")]
        public RectTransform cardHost;
        [Tooltip("卡片占参考画布高度的比例。")]
        [Range(.2f, 1f)]
        public float cardHeightFraction = .72f;

        [Header("诊断（只读）")]
        [Tooltip("关闭后卡面保持静态首帧，用于和自动摆动对照。")]
        public bool animate = true;

        private RawImage surface;
        private Material surfaceMaterial;
        private RenderTexture texture;
        private Image cardArt;
        private Transform visualPivot;
        private bool ready;
        private bool contentFitPending;
        private float appliedScale = -1f;

        // 与 DynamicCardView.cs:45-47 同名同义
        private bool dragging;
        private Vector2 dragOrigin, dragStart, target, current;
        private float releasedAt = -10f;
        private float age;

        public bool IsReady { get { return ready; } }
        public bool IsDragging { get { return dragging; } }
        public Vector2 Current { get { return current; } }
        public Vector2 Target { get { return target; } }
        public RenderTexture RenderTarget { get { return texture; } }
        public RawImage Surface { get { return surface; } }
        public ArtCard CollectionCard { get { return card; } }
        public Camera ArtCamera { get { return sourceCamera; } }

        /// <summary>作用在浮雕上的 pitch（度）。DynamicCardView.cs:401。</summary>
        public float ArtPitch { get { return Mathf.Lerp(ArtPitchStart, ArtPitchEnd, (current.y + 1) * .5f); } }
        /// <summary>作用在浮雕上的 yaw（度）。DynamicCardView.cs:402。</summary>
        public float ArtYaw { get { return Mathf.Lerp(ArtYawStart, ArtYawEnd, (current.x + 1) * .5f); } }

        /// <summary>整卡（含金框/徽章）当前欧拉角 —— DynamicCardPresentation 的视觉枢轴。</summary>
        public Vector3 FrameEuler
        {
            get { return visualPivot != null ? visualPivot.localEulerAngles : Vector3.zero; }
        }

        // ==================================================================
        // 生命周期
        // ==================================================================
        private void Awake()
        {
            // 场景里已经挂好的层级：序列化引用在 Awake 前就绪，可以直接装配。
            // 运行时兜底安装（LadyLakeCardStage.Install）会先 AddComponent 再写字段，
            // 那一刻字段还是空的，所以这里不能盲建；由 Install 在接线后显式调用 Build()。
            if (IsWired()) Build();
        }

        private void Start()
        {
            if (!ready && IsWired()) Build();
        }

        /// <summary>必要的序列化接线是否齐全。</summary>
        public bool IsWired()
        {
            return root != null && sourceCamera != null && card != null && cardHost != null && artPivot != null;
        }

        private void OnEnable()
        {
            // Re-arm the entrance/idle driver if something disabled the presentation.
            if (ready && animate) RestartSway();
        }

        private void OnDestroy()
        {
            if (sourceCamera != null) sourceCamera.targetTexture = null;
            if (texture != null) { texture.Release(); Destroy(texture); texture = null; }
            if (surfaceMaterial != null) { Destroy(surfaceMaterial); surfaceMaterial = null; }
        }

        // ==================================================================
        // 构建
        // ==================================================================
        /// <summary>
        /// 运行时装配。可在 Play 中再次调用（幂等）。Editor 里由 Attach 走静态的
        /// <see cref="ApplyCollectionFrame"/>，不会创建 RenderTexture。
        /// </summary>
        public void Build()
        {
            if (ready) return;
            if (root == null) root = FindObjectOfType<LadyLakeLabRoot>();
            if (sourceCamera == null)
            {
                if (root != null) sourceCamera = root.targetCamera;
                if (sourceCamera == null) sourceCamera = Camera.main;
            }
            if (card == null) card = GetComponentInChildren<ArtCard>(true);
            if (card == null)
            {
                Debug.LogError("[LadyLakeCardStage] 找不到真实收藏卡牌实例 ArtCard；请先运行 " +
                    "LadyLakeCardPresentationBuilder.Attach(root)。");
                return;
            }
            cardArt = card.CardImg;
            if (cardArt == null)
            {
                Debug.LogError("[LadyLakeCardStage] ArtCard.CardImg 未接线，无法挂接活动卡面。");
                return;
            }

            ApplyCollectionFrame(card, CardId);
            ConfigurePresentation();
            CreateArtSurface();
            AttachCamera();
            RestartSway();

            ready = true;
            contentFitPending = true;
        }

        /// <summary>
        /// 按真实收藏的取用方式装配金色中立牌面。
        /// 对应生产代码：<c>ArtCard.SetCard()</c>（Assets/Script/Card/ArtCard.cs:64-181）与
        /// <c>CardShowInfo.SetCard()</c>（Assets/Script/Card/NewCard/CardShowInfo.cs:163-281）里的
        /// 边框 / 阵营 / 力量 / 文本分支。这里直接引用 prefab 上序列化好的原版 sprite，
        /// 并按 <see cref="GwentMap.CardMap"/> 里 70006 的真实数据填数值与文本。
        /// 不调用 ArtCard.SetCard() 是因为它经由 DynamicCardView.Bind 依赖动态卡 catalog，
        /// 并且 CardContent.SetCard() 会解析 <c>DependencyResolver</c>（实验场景没有登录容器）。
        /// </summary>
        public static void ApplyCollectionFrame(ArtCard target, string cardId)
        {
            if (target == null) return;

            // 卡面命中只从牌框开始：CardImg 不参与射线（ArtCard.cs:69-70）。
            if (target.CardImg != null) target.CardImg.raycastTarget = false;
            if (target.CardBorder != null) target.CardBorder.raycastTarget = true;

            // GwentCard 是 struct（Cynthia.Card.Common/GwentGame/GwentCard.cs），用 hasInfo 表示命中。
            GwentCard info = default(GwentCard);
            bool hasInfo = false;
            try
            {
                var map = GwentMap.CardMap;
                if (map != null && !string.IsNullOrEmpty(cardId)) hasInfo = map.TryGetValue(cardId, out info);
            }
            catch (System.Exception error)
            {
                // 实验场景不启动服务器；卡牌数据是客户端静态表。即使它不可用，牌框 / 阵营
                // 仍然按真实收藏资产装配，只退化为 prefab 自带数值。
                Debug.LogWarning("[LadyLakeCardStage] GwentMap 不可用，退化为 prefab 默认数值：" + error.Message);
            }

            // Gold / Leader -> GoldBorder（ArtCard.cs:117-118、CardShowInfo.cs:223-224）。
            bool gold = !hasInfo || info.Group == Group.Gold || info.Group == Group.Leader;
            if (target.CardBorder != null && target.GoldBorder != null && gold)
                target.CardBorder.sprite = target.GoldBorder;

            // Neutral Gold 阵营图标（ArtCard.cs:135-136）。
            if (target.FactionIcon != null && target.NeutralGoldIcon != null)
                target.FactionIcon.sprite = target.NeutralGoldIcon;

            if (hasInfo)
            {
                bool special = info.CardType == CardType.Special;
                if (target.StrengthShow != null) target.StrengthShow.SetActive(!special);
                if (target.Strength != null) target.Strength.text = info.Strength.ToString();
                if (target.ArmorShow != null) target.ArmorShow.SetActive(false);
                if (target.CountdownShow != null) target.CountdownShow.SetActive(false);
                ApplyCollectionContext(target.Content, info, cardId);
            }
        }

        /// <summary>
        /// 名称 / 规则文本框 / 阵营底纹。对应 CardContent.SetCard()
        /// （Assets/Script/Card/CardContent.cs:60-92）里不依赖本地化容器的部分：
        /// 中性底纹 + 真实名称 + 真实规则文本，并按同一公式刷新文本框高度。
        /// </summary>
        public static void ApplyCollectionContext(CardContent content, GwentCard info, string cardId)
        {
            if (content == null) return;
            // The collection rules panel sits outside the card; this standalone
            // experiment presents only the complete framed card.
            content.gameObject.SetActive(false);
            if (content.Head != null && content.NeutralHead != null) content.Head.sprite = content.NeutralHead;
            if (content.Bottom != null && content.NeutralContent != null) content.Bottom.sprite = content.NeutralContent;
            if (content.CardNameText != null)
                content.CardNameText.text = !string.IsNullOrEmpty(info.Name) ? info.Name : cardId;
            if (content.CardInfoText != null)
                content.CardInfoText.text = info.Info ?? string.Empty;
            // 标签行需要本地化服务；实验场景无容器，留空而不是显示原始 key。
            if (content.TagsText != null) content.TagsText.text = string.Empty;
            FitContentHeight(content);
        }

        /// <summary>CardContent.cs:91 —— 文本框高度 = 文本首选高度 + 115。</summary>
        public static void FitContentHeight(CardContent content)
        {
            if (content == null || content.Content == null || content.CardInfoText == null) return;
            content.Content.sizeDelta = new Vector2(
                content.Content.sizeDelta.x, content.CardInfoText.preferredHeight + 115f);
        }

        /// <summary>
        /// 添加入场/转向组件并按 ArtCard.cs:66-68 的方式配置。
        /// </summary>
        private void ConfigurePresentation()
        {
            var presentation = card.GetComponent<DynamicCardPresentation>();
            if (presentation == null) presentation = card.gameObject.AddComponent<DynamicCardPresentation>();
            if (card.CardBorder != null)
                presentation.Configure(card.CardBorder.rectTransform,
                    card.Content != null ? card.Content.transform : (Transform)null);
            // 旋转的是 DynamicCardVisualPivot（DynamicCardPresentation.cs:38-45），不是卡框本身。
            visualPivot = card.CardBorder != null ? card.CardBorder.transform.parent : null;
        }

        /// <summary>
        /// 与 DynamicCardView.cs:304-319 完全相同的活动卡面：CardImg 的子 RawImage +
        /// DynamicCardSurface 着色器 + (0, 1-713/1024)..(497/1024, 1) 锚点。
        /// 不同点只有一个：uvRect 不是 ArtRegion（那是方形源相机的取景矩形），
        /// 而是把本实验相机渲染的整幅 RT 裁到「497x710 原画有效画面」的矩形，换算见 <see cref="FrameCrop"/>。
        /// </summary>
        private void CreateArtSurface()
        {
            var overlay = new GameObject("LadyLakeDynamicArt", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            overlay.layer = UiLayer;
            overlay.transform.SetParent(cardArt.transform, false);
            surface = overlay.GetComponent<RawImage>();
            var shader = Resources.Load<Shader>("DynamicCardSurface");
            if (shader != null)
            {
                surfaceMaterial = new Material(shader);
                surfaceMaterial.hideFlags = HideFlags.HideAndDontSave;
                surface.material = surfaceMaterial;
            }
            surface.raycastTarget = false;

            var rect = surface.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f - 713f / 1024f);
            rect.anchorMax = new Vector2(497f / 1024f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            surface.uvRect = FrameCrop();

            var presentation = card.GetComponent<DynamicCardPresentation>();
            if (presentation != null)
            {
                // Bind 之前 Configure 已负责把 CardImg 移进视觉枢轴；这里只登记新加的卡面，
                // 让 DynamicCardPerspectiveMesh 也对它做 55° 投影除法（同 DynamicCardView.cs:312）。
                presentation.RegisterGraphic(surface);
            }
            surface.color = Color.white;
        }

        /// <summary>
        /// 把 RT 里对应「497x710 有效画面」的子矩形算出来。
        /// 实验相机是透视相机（FOV 35、距离 12.4、画面高 7.10 世界单位，
        /// 见 LadyLakeLabConfig.CameraDistance / CameraFieldOfView / FrameHeightUnits），
        /// 在 12.4 处的垂直半高 = tan(FOV/2) * 距离，于是有效画面的边距比例可以直接推出。
        /// 这样保留原画构图与透视，不缩放/不拉伸卡面。
        /// </summary>
        public static Rect FrameCrop()
        {
            float halfHeight = Mathf.Tan(LadyLakeLabConfig.CameraFieldOfView * .5f * Mathf.Deg2Rad)
                * LadyLakeLabConfig.CameraDistance;
            float halfWidth = halfHeight * ((float)RenderWidth / RenderHeight);
            float marginY = (halfHeight - LadyLakeLabConfig.FrameHeightUnits * .5f) / (2f * halfHeight);
            float marginX = (halfWidth - LadyLakeLabConfig.FrameWidthUnits * .5f) / (2f * halfWidth);
            marginX = Mathf.Clamp(marginX, 0f, .49f);
            marginY = Mathf.Clamp(marginY, 0f, .49f);
            return new Rect(marginX, marginY, 1f - 2f * marginX, 1f - 2f * marginY);
        }

        /// <summary>
        /// 接管实验相机：渲染到卡面 RT，并把卡框所在的 UI 层剔除，避免 UI 被画进卡面。
        /// 相机保持 enabled，所以在编辑器/验收脚本里 <c>Camera.main</c> 仍然有效。
        /// </summary>
        private void AttachCamera()
        {
            if (sourceCamera == null) return;
            if (texture == null || !texture.IsCreated())
            {
                texture = new RenderTexture(RenderWidth, RenderHeight, 24, RenderTextureFormat.ARGB32);
                texture.name = "LadyLake card art " + RenderWidth + "x" + RenderHeight;
                texture.Create();
            }
            sourceCamera.targetTexture = texture;
            if (surface != null) surface.texture = texture;
            sourceCamera.aspect = (float)RenderWidth / RenderHeight;
            sourceCamera.cullingMask &= ~(1 << UiLayer);
            if (artPivot != null) artPivot.localRotation = Quaternion.identity;
        }

        // ==================================================================
        // 层级安装：Editor 的 Attach 与运行时兜底共用同一套配置
        // ==================================================================
        /// <summary>
        /// 在实验场景里建立 / 刷新卡牌展示层级，并接线 <see cref="LadyLakeCardStage"/>。
        /// 幂等：已存在的对象会被复用。
        ///
        /// Editor 侧传入 PrefabUtility 的工厂以便保留 prefab 链接；运行时兜底传 null，
        /// 直接用 <see cref="UnityEngine.Object.Instantiate(Object,Transform)"/> 创建卡牌实例。
        /// 只创建层级与序列化引用，不创建 RenderTexture（那是 Play 时 Build() 的事）。
        /// </summary>
        public static LadyLakeCardStage Install(LadyLakeLabRoot root, Func<Transform, ArtCard> cardFactory = null)
        {
            if (root == null) return null;
            Camera sceneCamera = root.targetCamera != null ? root.targetCamera : Camera.main;
            if (sceneCamera == null)
            {
                Debug.LogError("[LadyLakeCardStage] 找不到实验相机，无法安装展示层。");
                return null;
            }

            // 1) 浮雕视差枢轴：与实验根同原点，所以相机 localPosition 不变。
            Transform pivot = FindOrCreate(root.transform, ArtPivotName, false);
            pivot.localPosition = Vector3.zero;
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;
            if (sceneCamera.transform.parent != pivot)
            {
                Vector3 localPosition = sceneCamera.transform.localPosition;
                Quaternion localRotation = sceneCamera.transform.localRotation;
                sceneCamera.transform.SetParent(pivot, false);
                sceneCamera.transform.localPosition = localPosition;
                sceneCamera.transform.localRotation = localRotation;
            }

            // 2) 展示相机：只画 UI 层。
            Transform cameraTransform = FindOrCreate(root.transform, PresentationCameraName, false);
            Camera presentationCamera = cameraTransform.GetComponent<Camera>();
            if (presentationCamera == null) presentationCamera = cameraTransform.gameObject.AddComponent<Camera>();
            ConfigurePresentationCamera(presentationCamera);
            cameraTransform.localPosition = new Vector3(0f, 0f, -10f);
            cameraTransform.localRotation = Quaternion.identity;
            cameraTransform.localScale = Vector3.one;

            // 3) Canvas（Screen Space - Camera，与游戏内 UI 一致，可被相机渲染截图）。
            RectTransform canvasRect = FindOrCreateRect(root.transform, PresentationObjectName);
            GameObject canvasGo = canvasRect.gameObject;
            SetLayer(canvasGo, UiLayer);
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            if (canvas == null) canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = presentationCamera;
            canvas.planeDistance = PlaneDistance;
            canvas.pixelPerfect = false;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            if (canvasGo.GetComponent<GraphicRaycaster>() == null) canvasGo.AddComponent<GraphicRaycaster>();

            // 4) 背景：只有底色，没有任何说明文字。
            RectTransform backdrop = FindOrCreateRect(canvasGo.transform, BackdropName);
            Stretch(backdrop);
            SetLayer(backdrop.gameObject, UiLayer);
            Image backdropImage = backdrop.GetComponent<Image>();
            if (backdropImage == null) backdropImage = backdrop.gameObject.AddComponent<Image>();
            backdropImage.sprite = null;
            backdropImage.color = BackdropColor;
            backdropImage.raycastTarget = false;

            // 5) 卡牌容器 + 真实收藏卡牌实例。
            RectTransform cardHost = FindOrCreateRect(canvasGo.transform, CardHostName);
            Stretch(cardHost);
            SetLayer(cardHost.gameObject, UiLayer);
            ArtCard card = cardHost.GetComponentInChildren<ArtCard>(true);
            if (card == null)
            {
                GameObject prefab = Resources.Load<GameObject>(CollectionCardPrefab);
                if (prefab == null)
                {
                    Debug.LogError("[LadyLakeCardStage] 找不到真实收藏卡牌 prefab：Resources/" + CollectionCardPrefab);
                    return null;
                }
                card = cardFactory != null ? cardFactory(cardHost) : null;
                if (card == null)
                {
                    var instance = Instantiate(prefab, cardHost);
                    instance.name = CardInstanceName;
                    card = instance.GetComponent<ArtCard>();
                }
            }
            if (card == null)
            {
                Debug.LogError("[LadyLakeCardStage] 卡牌 prefab 上没有 ArtCard 组件。");
                return null;
            }
            SetLayer(card.gameObject, UiLayer);
            RectTransform cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(.5f, .5f);
            cardRect.pivot = new Vector2(.5f, .5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.localRotation = Quaternion.identity;
            cardRect.localScale = Vector3.one;

            // 真实收藏取用方式装配金色中立牌面（同 ArtCard.SetCard 的分支）。
            ApplyCollectionFrame(card, CardId);

            // 6) 运行时驱动组件。
            if (card.GetComponent<DynamicCardPresentation>() == null)
                card.gameObject.AddComponent<DynamicCardPresentation>();

            LadyLakeCardStage stage = canvasGo.GetComponent<LadyLakeCardStage>();
            if (stage == null) stage = canvasGo.AddComponent<LadyLakeCardStage>();
            stage.root = root;
            stage.sourceCamera = sceneCamera;
            stage.artPivot = pivot;
            stage.card = card;
            stage.cardHost = cardHost;

            if (card.CardBorder != null)
            {
                var handle = card.CardBorder.GetComponent<LadyLakeCardDragHandle>();
                if (handle == null) handle = card.CardBorder.gameObject.AddComponent<LadyLakeCardDragHandle>();
                handle.View = stage;
            }

            // 7) 实验相机不把 UI 层画进卡面。
            sceneCamera.cullingMask &= ~(1 << UiLayer);

            // 8) EventSystem：鼠标拖动必须有。
            EnsureEventSystem(root.gameObject.scene);

            // 接线完成后才能装配：RenderTexture 只能在 Play 期间创建，
            // 否则会被序列化成一个非持久对象引用。
            if (Application.isPlaying) stage.Build();
            return stage;
        }

        private static void ConfigurePresentationCamera(Camera camera)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackdropColor;
            camera.orthographic = false;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 100f;
            camera.cullingMask = 1 << UiLayer;
            camera.depth = 1f;
            camera.allowHDR = false;
            camera.useOcclusionCulling = false;
            camera.targetTexture = null;
        }

        private static void EnsureEventSystem(Scene scene)
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject(EventSystemName, typeof(EventSystem), typeof(StandaloneInputModule));
            if (scene.IsValid() && scene.isLoaded) SceneManager.MoveGameObjectToScene(go, scene);
        }

        private static Transform FindOrCreate(Transform parent, string name, bool withRectTransform)
        {
            Transform child = parent.Find(name);
            if (child != null) return child;
            var go = withRectTransform
                ? new GameObject(name, typeof(RectTransform))
                : new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static RectTransform FindOrCreateRect(Transform parent, string name)
        {
            Transform child = FindOrCreate(parent, name, true);
            var rect = child as RectTransform;
            if (rect == null)
            {
                Debug.LogError("[LadyLakeCardStage] " + name + " 已存在但不是 RectTransform，无法作为 UI 容器。");
            }
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetLayer(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayer(child.gameObject, layer);
        }

        /// <summary>
        /// 开始/重启入场摆动。DynamicCardPresentation 的 55° 入场曲线由 Reveal(true) 触发；
        /// 空闲正弦摆动由本组件的 LateUpdate 驱动。
        /// </summary>
        public void RestartSway()
        {
            var presentation = card != null ? card.GetComponent<DynamicCardPresentation>() : null;
            if (presentation == null) return;
            presentation.Begin();
            presentation.Reveal(true);
            age = 0f;
            releasedAt = -10f;
            target = current = Vector2.zero;
        }

        // ==================================================================
        // 驱动：照搬 DynamicCardView.cs:377-429 的 LateUpdate
        // ==================================================================
        private void LateUpdate()
        {
            if (!ready) return;
            var presentation = card != null ? card.GetComponent<DynamicCardPresentation>() : null;

            if (animate)
            {
                age += Time.unscaledDeltaTime;                              // DynamicCardView.cs:392

                if (!dragging)                                              // DynamicCardView.cs:393-399
                {
                    float idleTime = Mathf.Min(age - IdleBlendStart,
                        Time.unscaledTime - releasedAt - ReleaseBlendStart);
                    float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(idleTime));
                    target = idleTime > 0f
                        ? new Vector2(Mathf.Sin(idleTime * SwayHorizontalRate) * HorizontalLimit,
                                      Mathf.Sin(idleTime * SwayVerticalRate) * VerticalLimit) * blend
                        : Vector2.zero;
                }

                current = Vector2.Lerp(current, target,
                    1f - Mathf.Exp(-(dragging ? DragLerp : IdleLerp) * Time.unscaledDeltaTime)); // :400
            }
            else
            {
                // 静态对照模式：角度归零，但仍然 Tick 让 55° 入场曲线收敛到平面。
                current = target = Vector2.zero;
            }

            if (presentation != null)
            {
                // 整卡（金框 / 徽章 / 卡面）转向 + 55° 入场投影，DynamicCardView.cs:411-412。
                presentation.Tick(current);
            }

            // 浮雕视差：DynamicCardView.cs:401-407 把 catalog 的 xStart..xEnd / yStart..yEnd
            // 映射成绕卡心的 pitch / yaw。实验里模型是整幅浮雕，所以把相机绕画面中心反向转同样角度，
            // 相机与画面的相对转角与生产完全一致（反向是因为转的是相机而不是模型）。
            if (artPivot != null)
                artPivot.localRotation = Quaternion.Euler(-ArtPitch, -ArtYaw, 0f);

            FitCardToHost();

            if (contentFitPending)
            {
                contentFitPending = false;
                FitContentHeight(card != null ? card.Content : null);
            }
        }

        /// <summary>卡片占 Canvas 高度的比例（Attach 时建立 cardHost，运行时跟随分辨率）。</summary>
        private void FitCardToHost()
        {
            if (card == null || cardHost == null) return;
            float cardHeight = card.transform is RectTransform ? ((RectTransform)card.transform).rect.height : 0f;
            if (cardHeight <= 0f) return;
            float scale = Mathf.Max(.01f, cardHost.rect.height * cardHeightFraction) / cardHeight;
            if (Mathf.Abs(scale - appliedScale) < .0005f) return;
            appliedScale = scale;
            card.transform.localScale = new Vector3(scale, scale, 1f);
        }

        // ==================================================================
        // 拖动：照搬 DynamicCardView.cs:477-493
        // ==================================================================
        public void OnBeginDrag(PointerEventData data)
        {
            if (data == null || data.button != PointerEventData.InputButton.Left) return;
            BeginDragAt(data.position);
        }

        public void OnDrag(PointerEventData data)
        {
            if (data == null) return;
            DragTo(data.position);
        }

        public void OnEndDrag(PointerEventData data)
        {
            ReturnToCenter();
        }

        /// <summary>公开输入接口：按屏幕坐标开始拖动（等价于 DynamicCardView.OnBeginDrag）。</summary>
        public void BeginDragAt(Vector2 screenPosition)
        {
            if (!ready || dragging) return;
            dragging = true;
            dragOrigin = screenPosition;
            dragStart = current;
            var presentation = card != null ? card.GetComponent<DynamicCardPresentation>() : null;
            if (presentation != null) presentation.InterruptEntrance();     // DynamicCardView.cs:480
        }

        /// <summary>公开输入接口：按屏幕坐标拖动（等价于 DynamicCardView.OnDrag）。</summary>
        public void DragTo(Vector2 screenPosition)
        {
            if (!dragging) return;
            var delta = screenPosition - dragOrigin;
            // DynamicCardView.cs:486-490：屏幕相对位移，窗口大小不同手感一致。
            float referenceScale = Mathf.Max(1f, Screen.height) / ReferenceHeight;
            target = dragStart + new Vector2(-delta.x / (HorizontalTravel * referenceScale),
                                             delta.y / (VerticalTravel * referenceScale));
            target = new Vector2(Mathf.Clamp(target.x, -HorizontalLimit, HorizontalLimit),
                                 Mathf.Clamp(target.y, -VerticalLimit, VerticalLimit));
        }

        /// <summary>公开输入接口：松手回正（等价于 DynamicCardView.OnEndDrag）。</summary>
        public void EndDrag()
        {
            ReturnToCenter();
        }

        internal void ReturnToCenter()
        {
            if (dragging) releasedAt = Time.unscaledTime;                   // DynamicCardView.cs:493
            dragging = false;
            target = Vector2.zero;
        }

        // ==================================================================
        // 采样 / 诊断（供 PlayMode 验收与截图脚本使用）
        // ==================================================================
        public LadyLakeCardSnapshot Sample()
        {
            var snapshot = new LadyLakeCardSnapshot();
            snapshot.ready = ready;
            snapshot.dragging = dragging;
            snapshot.current = current;
            snapshot.target = target;
            snapshot.artPitch = ArtPitch;
            snapshot.artYaw = ArtYaw;
            snapshot.frameEuler = FrameEuler;
            snapshot.artPivotEuler = artPivot != null ? artPivot.localEulerAngles : Vector3.zero;
            snapshot.cardId = CardId;
            snapshot.renderWidth = RenderWidth;
            snapshot.renderHeight = RenderHeight;
            snapshot.uvRect = surface != null ? surface.uvRect : new Rect(0, 0, 1, 1);
            snapshot.goldFrame = card != null && card.CardBorder != null &&
                card.GoldBorder != null && card.CardBorder.sprite == card.GoldBorder;
            snapshot.neutralFaction = card != null && card.FactionIcon != null &&
                card.NeutralGoldIcon != null && card.FactionIcon.sprite == card.NeutralGoldIcon;
            snapshot.cardName = card != null && card.Content != null && card.Content.CardNameText != null
                ? card.Content.CardNameText.text : null;
            snapshot.strength = card != null && card.Strength != null ? card.Strength.text : null;
            snapshot.hasRenderTarget = texture != null && texture.IsCreated();
            snapshot.presentationAnimating = card != null && card.GetComponent<DynamicCardPresentation>() != null &&
                visualPivot != null && visualPivot.localRotation != Quaternion.identity;
            snapshot.cardHeightFraction = cardHeightFraction;
            return snapshot;
        }

        /// <summary>一行 JSON，便于写进验收报告。</summary>
        public string Describe()
        {
            return JsonUtility.ToJson(Sample());
        }
    }

    /// <summary>可序列化的展示状态快照（LadyLakeCardStage.Sample）。</summary>
    [System.Serializable]
    public struct LadyLakeCardSnapshot
    {
        public bool ready;
        public bool dragging;
        public Vector2 current;
        public Vector2 target;
        public float artPitch;
        public float artYaw;
        public Vector3 frameEuler;
        public Vector3 artPivotEuler;
        public string cardId;
        public string cardName;
        public string strength;
        public int renderWidth;
        public int renderHeight;
        public Rect uvRect;
        public bool goldFrame;
        public bool neutralFaction;
        public bool hasRenderTarget;
        public bool presentationAnimating;
        public float cardHeightFraction;
    }
}
