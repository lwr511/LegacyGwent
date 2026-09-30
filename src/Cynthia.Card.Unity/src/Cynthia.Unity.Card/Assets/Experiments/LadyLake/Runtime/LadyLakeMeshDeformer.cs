using UnityEngine;

namespace LegacyGwent.LadyLakeLab
{
    /// <summary>
    /// 网格变形器基类。场景里保存的是烘焙好的「静止姿态」网格资产（Editor 里可直接检查），
    /// 运行时（或 Editor 取样时）才 Instantiate 一份可写副本做局部变形，
    /// 避免修改资产、也避免出现「非 Play 就什么都没有」的情况。
    ///
    /// 同时把渲染器设置成透明排序安全的状态：ZWrite Off、双面、关闭阴影/探针/遮挡剔除，
    /// 并把 bounds 预放大，防止局部动作把顶点移出包围盒被视锥剔除而闪掉。
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public abstract class LadyLakeMeshDeformer : MonoBehaviour
    {
        [Tooltip("场景中引用的静止姿态网格资产（由 Builder 生成并保存）。")]
        public Mesh bakedMesh;

        protected Mesh runtimeMesh;
        protected MeshFilter meshFilter;

        private bool _prepared;

        /// <summary>可写副本（变形目标）。未准备好时为 null。</summary>
        public Mesh RuntimeMesh
        {
            get { return runtimeMesh; }
        }

        public Mesh SourceMesh
        {
            get
            {
                if (bakedMesh != null) return bakedMesh;
                if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
                return meshFilter != null ? meshFilter.sharedMesh : null;
            }
        }

        protected virtual void Awake()
        {
            EnsureRuntimeMesh();
        }

        protected virtual void OnDestroy()
        {
            ReleaseRuntimeMesh();
        }

        /// <summary>准备可写网格副本。可重复调用。</summary>
        public bool EnsureRuntimeMesh()
        {
            if (_prepared && runtimeMesh != null) return true;

            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            Mesh source = SourceMesh;
            if (source == null)
            {
                _prepared = false;
                return false;
            }

            runtimeMesh = Instantiate(source);
            runtimeMesh.name = source.name + "_Runtime";
            runtimeMesh.MarkDynamic();

            Bounds b = source.bounds;
            // 局部动作（伸手/摆动/气泡）可能把顶点推离静止包围盒，这里一次性放宽，
            // 避免逐帧重算 bounds，也避免被视锥剔除导致渲染背面消失或闪烁。
            Vector3 pad = GetBoundsPadding();
            b.Expand(new Vector3(pad.x * 2f, pad.y * 2f, pad.z * 2f));
            runtimeMesh.bounds = b;

            if (meshFilter != null) meshFilter.sharedMesh = runtimeMesh;
            _prepared = true;
            return true;
        }

        /// <summary>释放可写副本，并把 MeshFilter 还原到烘焙资产（Editor 取样后调用）。</summary>
        public void ReleaseRuntimeMesh()
        {
            if (meshFilter != null && bakedMesh != null && meshFilter.sharedMesh == runtimeMesh)
            {
                meshFilter.sharedMesh = bakedMesh;
            }

            if (runtimeMesh != null)
            {
                if (Application.isPlaying) Destroy(runtimeMesh);
                else DestroyImmediate(runtimeMesh);
                runtimeMesh = null;
            }

            _prepared = false;
        }

        /// <summary>包围盒外扩量（默认给人物/摆动留足空间）。</summary>
        protected virtual Vector3 GetBoundsPadding()
        {
            return new Vector3(1.2f, 1.2f, 0.6f);
        }

        public abstract void ApplyPose(LadyLakePose pose);

        /// <summary>场景内渲染器统一设置（Builder 与运行时都会调用，保证幂等）。</summary>
        public static void ConfigureRenderer(Renderer renderer)
        {
            if (renderer == null) return;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }
    }
}
