using System.Collections.Generic;

namespace LegacyGwent.LadyLakeLab
{
    // ------------------------------------------------------------------
    // validation.json 的数据模型（JsonUtility 序列化，字段必须是 public 且 [Serializable]）
    // ------------------------------------------------------------------

    [System.Serializable]
    public class LadyLakeValidationImage
    {
        public string role = "";
        public string path = "";
        public string fileName = "";
        public int width;
        public int height;
        public bool hasAlpha;
        public bool readable;
        public string pixelBounds = "";
        public string note = "";
    }

    [System.Serializable]
    public class LadyLakeValidationNode
    {
        public string path = "";
        public string components = "";
        public int meshVertices;
        public int meshTriangles;
        public int subMeshCount;
        public string materials = "";
        public int renderQueue = -1;
        public string boundsCenter = "";
        public string boundsSize = "";
        public bool meshReadable;
        public string note = "";
    }

    [System.Serializable]
    public class LadyLakeValidationShader
    {
        public string shaderName = "";
        public bool found;
        public bool supported;
        public string usedBy = "";
        public int renderQueue = -1;
    }

    [System.Serializable]
    public class LadyLakeValidationTimeline
    {
        public float duration;
        public float pause;
        public float totalDuration;
        public int sampleCount;
        public float upperArmLengthPx;
        public float foreArmLengthPx;
        public string phaseBoundaries = "";
    }

    [System.Serializable]
    public class LadyLakeValidationGrip
    {
        public float heldWindowStart;
        public float heldWindowEnd;
        public float maxGripDistanceWhileHeld = -1f;
        public float maxGripDistanceWhileFree = -1f;
        public float maxHandSlipWhileHeld = -1f;
        public float maxHandDistanceWhileHeld = -1f;
        public int handVertexCount;
        /// <summary>握住相位「手部顶点云质心」与「剑握点」的距离（真实几何对齐量）。</summary>
        public float palmAlignment = -1f;
        public float palmAlignmentWorst = -1f;
        public string palmNote = "";
        public string samples = "";
        public string note = "";
    }

    [System.Serializable]
    public class LadyLakeValidationSeam
    {
        public float positionDelta;
        public float rotationDeltaDeg;
        public float wristDelta;
        public float gripDelta;
        public float maxVertexDelta;
        public float velocityDelta;
        public string note = "";
    }

    [System.Serializable]
    public class LadyLakeValidationCapture
    {
        public float seconds;
        public string phase = "";
        public string path = "";
        public int width;
        public int height;
        public float nonEmptyRatio;
        public float meanLuma;
        public string note = "";
    }

    [System.Serializable]
    public class LadyLakeValidationReport
    {
        public string tool = "LegacyGwent.LadyLakeLab";
        public string generatedAtUtc = "";
        public string unityVersion = "";
        public string scenePath = "";
        public string sceneGuid = "";
        public string textureFolder = "";
        public string layoutOverridePath = "";
        public bool buildSucceeded;
        public string buildError = "";

        /// <summary>四张分层图各自的注册变换（图像 -> 画面）。</summary>
        public List<string> registrations = new List<string>();
        /// <summary>标定辅助：锚点经注册后落在画面哪里，便于人工微调 layout.json。</summary>
        public List<string> calibration = new List<string>();

        public List<LadyLakeValidationImage> images = new List<LadyLakeValidationImage>();
        public List<LadyLakeValidationShader> shaders = new List<LadyLakeValidationShader>();
        public List<LadyLakeValidationNode> hierarchy = new List<LadyLakeValidationNode>();
        public LadyLakeValidationTimeline timeline = new LadyLakeValidationTimeline();
        public LadyLakeValidationGrip grip = new LadyLakeValidationGrip();
        public LadyLakeValidationSeam seam = new LadyLakeValidationSeam();
        public List<LadyLakeValidationCapture> captures = new List<LadyLakeValidationCapture>();

        public List<string> missingImages = new List<string>();
        public List<string> nullReferences = new List<string>();
        public List<string> warnings = new List<string>();
        public List<string> errors = new List<string>();
        public List<string> verified = new List<string>();
        public List<string> notVerified = new List<string>();
        public List<string> reusedProjectAssets = new List<string>();
    }
}
