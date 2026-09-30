// LadyLakePremium - MODEL worker, Unity Editor authoring entry point.
//
//   public static void LegacyGwent.LadyLakePremium.Editor.LadyLakeModelBuilder.Build()
//
// Produces Assets/Experiments/LadyLakePremium/Model/FigureRig.prefab from the
// reproducibly generated FBX (Model/Source/build_figure.py -> Model/Generated/
// FigureRig.fbx), wires the materials, the Animator (Intro + Loop, both a
// seamless 12 s cycle), the anatomical skinned rig, the Sword child with the
// Grip marker and the bone-parented GripSocket on the palm.
//
// Editor only. Unity 2019.4 compatible. It never touches production scripts,
// the old experiment, the catalog, or the import settings of the user-selected
// source textures: only the worker-local COPIES under Model/Generated/Textures
// are configured.
//
// The build is executed by root, not by the worker.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LegacyGwent.LadyLakePremium.Editor
{
    [Serializable]
    public class LLPManifestSword
    {
        public float[] grip_world;
        public float[] tip_world;
        public float[] pommel_world;
        public float[] guard_world;
        public string texture;
        public string local_origin;
    }

    [Serializable]
    public class LLPManifestDto
    {
        public string generator;
        public string blender;
        public int bone_count;
        public LLPManifestSword sword;
        public string[] previews;
        public string[] runtime_notes;
    }

    public static class LadyLakeModelBuilder
    {
        public const string ModelRoot = "Assets/Experiments/LadyLakePremium/Model";
        public const string Generated = ModelRoot + "/Generated";
        public const string TexDir = Generated + "/Textures";
        public const string MatDir = Generated + "/Materials";
        public const string ClipDir = Generated + "/Clips";

        public const string FbxPath = Generated + "/FigureRig.fbx";
        public const string PrefabPath = ModelRoot + "/FigureRig.prefab";
        public const string ControllerPath = Generated + "/FigureRig.controller";
        public const string ManifestPath = Generated + "/model-manifest.json";

        public const string FigureSkinTex = TexDir + "/FigureSkin.png";
        public const string GoldSkinTex = TexDir + "/GoldSkin.png";
        public const string HairTex = TexDir + "/Hair.png";
        public const string SwordTex = TexDir + "/Sword.png";

        // ------------------------------------------------------------------
        public static void Build()
        {
            Debug.Log("[LadyLakePremium][Model] Build() start");
            try
            {
                EnsureFolders();
                ConfigureCopiedTextures();
                ConfigureModelImporter();

                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
                if (fbx == null)
                    throw new InvalidOperationException("FBX not importable: " + FbxPath);

                var clips = new List<AnimationClip>(
                    AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>());
                if (clips.Count == 0)
                    throw new InvalidOperationException("FBX has no animation clip: " + FbxPath);
                AnimationClip source = PickSourceClip(clips);
                Debug.Log("[LadyLakePremium][Model] clip candidates: " +
                          string.Join(", ", clips.Select(c => c.name + "(" +
                              c.length.ToString("0.##") + "s," +
                              AnimationUtility.GetCurveBindings(c).Length + " curves)").ToArray()));
                Debug.Log("[LadyLakePremium][Model] source clip: " + source.name +
                          "  " + source.length.ToString("0.###") + "s  " +
                          AnimationUtility.GetCurveBindings(source).Length + " curves");

                Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Avatar>().FirstOrDefault();
                if (avatar == null)
                    Debug.LogWarning("[LadyLakePremium][Model] FBX produced no Avatar; " +
                                     "building a Generic avatar for the Animator");

                var mats = CreateMaterials();
                var introClip = MakeClip(source, ClipDir + "/Intro.anim", "Intro", false);
                var loopClip = MakeClip(source, ClipDir + "/Loop.anim", "Loop", true);
                var controller = MakeController(introClip != null ? introClip : source,
                                                loopClip != null ? loopClip : source);

                var root = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                try
                {
                    root.name = "FigureRig";
                    root.transform.localPosition = Vector3.zero;
                    root.transform.localRotation = Quaternion.identity;
                    root.transform.localScale = Vector3.one;

                    var animator = root.GetComponent<Animator>();
                    if (animator == null) animator = root.AddComponent<Animator>();
                    if (avatar == null) avatar = BuildGenericAvatar(root);
                    animator.avatar = avatar;
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                    ApplyMaterials(root, mats);
                    var report = new List<string>();
                    PlaceGripSocket(root, report);
                    EnsureGripMarker(root, report);

                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[LadyLakePremium][Model] prefab written: " + PrefabPath +
                              "  bones=" + CountBones(root) + "  avatar=" + (avatar != null));
                    foreach (var line in report) Debug.Log("[LadyLakePremium][Model] " + line);

                    WriteBuildReport(root, source, avatar, mats, controller, report);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[LadyLakePremium][Model] Build() FAILED: " + e);
                throw;
            }
        }

        // ------------------------------------------------------------------
        static void EnsureFolders()
        {
            Directory.CreateDirectory(ToAbsolute(ModelRoot));
            Directory.CreateDirectory(ToAbsolute(Generated));
            Directory.CreateDirectory(ToAbsolute(TexDir));
            Directory.CreateDirectory(ToAbsolute(MatDir));
            Directory.CreateDirectory(ToAbsolute(ClipDir));
            AssetDatabase.Refresh();
        }

        static string ToAbsolute(string assetPath)
        {
            // deliberately not a static field initialiser: Application.dataPath is
            // not legal to touch from a field initialiser in this project.
            string project = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(project, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        // ------------------------------------------------------------------
        static void ConfigureCopiedTextures()
        {
            SetTexture(FigureSkinTex, TextureWrapMode.Clamp, false, true, false);
            SetTexture(HairTex, TextureWrapMode.Clamp, false, true, false);
            SetTexture(SwordTex, TextureWrapMode.Clamp, false, true, true);
            SetTexture(GoldSkinTex, TextureWrapMode.Repeat, true, true, false);
            AssetDatabase.Refresh();
        }

        static void SetTexture(string path, TextureWrapMode wrap, bool repeat,
                               bool sRGB, bool alphaIsTransparency)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[LadyLakePremium][Model] missing copied texture: " + path);
                return;
            }
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = sRGB;
            importer.alphaIsTransparency = alphaIsTransparency;
            importer.mipmapEnabled = true;
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 4;
            importer.isReadable = true;          // builder needs CPU access for checks
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------
        static void ConfigureModelImporter()
        {
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException("not a model: " + FbxPath);

            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.optimizeGameObjects = false;     // bones must stay addressable
            importer.importMaterials = false;         // worker-authored materials win
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.weldVertices = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.resampleCurves = false;          // keep the baked 30 fps sword track
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------------
        static Material MakeStandard(string path, string name, string texPath, Color tint,
                                     bool cutout, TextureWrapMode wrap)
        {
            var shader=Shader.Find("DynamicCards/PortableCard");
            if(shader==null)throw new InvalidOperationException("Production painted-card shader missing");
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
            mat.shader=shader;mat.name=name;mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            mat.SetColor("_Color",tint);mat.SetColor("_TintColor",Color.white);
            mat.SetFloat("_VertexColorStrength",0f);mat.SetFloat("_Brightness",1f);
            mat.SetFloat("_ZWrite",1f);mat.SetFloat("_Cull",2f);
            mat.SetFloat("_SrcBlend",1f);mat.SetFloat("_DstBlend",0f);
            mat.SetFloat("_Cutoff",cutout?.15f:.001f);
            mat.renderQueue=cutout?2450:2000;
            EditorUtility.SetDirty(mat);return mat;
        }

        static Dictionary<string, Material> CreateMaterials()
        {
            var d = new Dictionary<string, Material>();
            d["FigureFront"] = MakeStandard(MatDir + "/FigureFront.mat", "FigureFront",
                FigureSkinTex, Color.white, false, TextureWrapMode.Clamp);
            d["GoldSkin"] = MakeStandard(MatDir + "/GoldSkin.mat", "GoldSkin",
                GoldSkinTex, new Color(1.06f, 0.98f, 0.86f, 1f), false, TextureWrapMode.Repeat);
            d["HairSheet"] = MakeStandard(MatDir + "/HairSheet.mat", "HairSheet",
                HairTex, Color.white, true, TextureWrapMode.Clamp);
            d["SwordBlade"] = MakeStandard(MatDir + "/SwordBlade.mat", "SwordBlade",
                SwordTex, Color.white, true, TextureWrapMode.Clamp);
            AssetDatabase.SaveAssets();
            return d;
        }

        static void ApplyMaterials(GameObject root, Dictionary<string, Material> mats)
        {
            var figure = new[] { mats["FigureFront"], mats["GoldSkin"] };
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name.ToLowerInvariant();
                if (n.Contains("body") || n.Contains("figure"))
                    r.sharedMaterials = figure;
                else if (n.Contains("hair"))
                    r.sharedMaterials = new[] { mats["HairSheet"] };
                else if (n.Contains("sword") || n.Contains("blade"))
                    r.sharedMaterials = new[] { mats["SwordBlade"] };
            }
        }

        // ------------------------------------------------------------------
        static readonly string[] SwordPaths = { "/SwordBone", "/Sword" };

        // The FBX carries several takes.  Unity also synthesises a
        // "__preview__<stack>" clip that has no runtime value; the rejected build
        // picked that one (it was simply the longest by list sort) and every
        // sampled pose came out identical.  Rank the real takes instead.
        static AnimationClip PickSourceClip(List<AnimationClip> clips)
        {
            var ranked = new List<AnimationClip>();
            foreach (var c in clips)
            {
                if (c == null) continue;
                if (c.name.Contains("__preview__")) continue;
                ranked.Add(c);
            }
            if (ranked.Count == 0) ranked = clips;

            AnimationClip best = null;
            int bestScore = int.MinValue;
            foreach (var c in ranked)
            {
                var paths = AnimationUtility.GetCurveBindings(c)
                    .Select(b => b.path).Distinct().ToArray();
                int bones = paths.Count(p => p.Contains("/Root") || p.Contains("/Hips"));
                int sword = paths.Count(p => SwordPaths.Any(d => p.EndsWith(d)) ||
                                             p.Contains("/Sword"));
                int score = (sword > 0 ? 100000 : 0) + (bones > 0 ? 50000 : 0) +
                            paths.Length + (int)(c.length * 10f);
                if (score > bestScore) { bestScore = score; best = c; }
            }
            return best ?? clips[0];
        }

        // A Generic rig needs an Avatar for the Animator to bind.  2019.4 has no
        // ModelImporter.avatarSetup, newer versions do, so ask by reflection and
        // otherwise fall back to building one directly.
        static Avatar BuildGenericAvatar(GameObject root)
        {
            try
            {
                var avatar = AvatarBuilder.BuildGenericAvatar(root, root.name);
                if (avatar != null)
                    Debug.Log("[LadyLakePremium][Model] built generic avatar " + avatar.name);
                return avatar;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LadyLakePremium][Model] generic avatar failed: " + e.Message);
                return null;
            }
        }

        // ------------------------------------------------------------------
        static AnimationClip MakeClip(AnimationClip src, string path, string name, bool loop)
        {
            try
            {
                if (src == null) return null;
                var clip = new AnimationClip();
                clip.name = name;
                clip.frameRate = src.frameRate > 0f ? src.frameRate : 30f;
                var settings = AnimationUtility.GetAnimationClipSettings(src);
                settings.loopTime = loop;
                settings.loopBlend = loop;
                settings.startTime = 0f;
                settings.stopTime = src.length;
                AnimationUtility.SetAnimationClipSettings(clip, settings);

                // Group the source curves per transform path.  The FBX clip is
                // serialised as *euler* curves (localEulerAnglesRaw); a clip built
                // from those via SetEditorCurve is stored with an empty
                // m_RotationCurves list, and the sampled pose then never changes -
                // that was the second half of "every captured time is identical".
                // Re-emit proper quaternion curves so the rotation actually drives.
                var byPath = new SortedDictionary<string, Dictionary<string, AnimationCurve>>();
                foreach (var b in AnimationUtility.GetCurveBindings(src))
                {
                    if (b.type != typeof(Transform)) continue;
                    var c = AnimationUtility.GetEditorCurve(src, b);
                    if (c == null) continue;
                    Dictionary<string, AnimationCurve> d;
                    if (!byPath.TryGetValue(b.path, out d))
                    {
                        d = new Dictionary<string, AnimationCurve>();
                        byPath[b.path] = d;
                    }
                    d[b.propertyName] = c;
                }

                int paths = 0, curves = 0, moving = 0;
                var missing = new List<string>();
                foreach (var kv in byPath)
                {
                    string p = kv.Key;
                    if (p.Length == 0) continue;      // root motion, not wanted
                    var d = kv.Value;
                    AnimationCurve ex, ey, ez;
                    d.TryGetValue("localEulerAnglesRaw.x", out ex);
                    d.TryGetValue("localEulerAnglesRaw.y", out ey);
                    d.TryGetValue("localEulerAnglesRaw.z", out ez);
                    AnimationCurve px, py, pz;
                    d.TryGetValue("m_LocalPosition.x", out px);
                    d.TryGetValue("m_LocalPosition.y", out py);
                    d.TryGetValue("m_LocalPosition.z", out pz);
                    AnimationCurve qx, qy, qz, qw;
                    d.TryGetValue("m_LocalRotation.x", out qx);
                    d.TryGetValue("m_LocalRotation.y", out qy);
                    d.TryGetValue("m_LocalRotation.z", out qz);
                    d.TryGetValue("m_LocalRotation.w", out qw);

                    var times = KeyTimes(ex, ey, ez, px, py, pz, qx, qy, qz, qw);
                    if (times.Count == 0) continue;
                    paths++;

                    var pos = new List<Vector3>(times.Count);
                    var rot = new List<Quaternion>(times.Count);
                    for (int i = 0; i < times.Count; i++)
                    {
                        float t = times[i];
                        pos.Add(new Vector3(px != null ? px.Evaluate(t) : 0f,
                                            py != null ? py.Evaluate(t) : 0f,
                                            pz != null ? pz.Evaluate(t) : 0f));
                        if (qw != null)
                            rot.Add(new Quaternion(qx.Evaluate(t), qy.Evaluate(t),
                                                   qz.Evaluate(t), qw.Evaluate(t)));
                        else
                            rot.Add(Quaternion.Euler(ex != null ? ex.Evaluate(t) : 0f,
                                                     ey != null ? ey.Evaluate(t) : 0f,
                                                     ez != null ? ez.Evaluate(t) : 0f));
                    }
                    // keep the quaternion track continuous across the seam
                    for (int i = 1; i < rot.Count; i++)
                        if (Quaternion.Dot(rot[i - 1], rot[i]) < 0f)
                            rot[i] = new Quaternion(-rot[i].x, -rot[i].y, -rot[i].z, -rot[i].w);

                    bool moves = false;
                    for (int i = 1; i < rot.Count; i++)
                        if (Quaternion.Angle(rot[0], rot[i]) > 0.05f) { moves = true; break; }
                    if (moves) moving++;
                    if (moves)
                    {
                        curves += SetCurve(clip, p, "m_LocalRotation.x", times, rot, 0);
                        curves += SetCurve(clip, p, "m_LocalRotation.y", times, rot, 1);
                        curves += SetCurve(clip, p, "m_LocalRotation.z", times, rot, 2);
                        curves += SetCurve(clip, p, "m_LocalRotation.w", times, rot, 3);
                    }
                    else
                    {
                        curves += SetCurve(clip, p, "m_LocalRotation.x", times, rot, 0, true);
                        curves += SetCurve(clip, p, "m_LocalRotation.y", times, rot, 1, true);
                        curves += SetCurve(clip, p, "m_LocalRotation.z", times, rot, 2, true);
                        curves += SetCurve(clip, p, "m_LocalRotation.w", times, rot, 3, true);
                    }
                    if (IsMoving(pos))
                        curves += SetCurve(clip, p, "m_LocalPosition.x", times, pos, 0) +
                                  SetCurve(clip, p, "m_LocalPosition.y", times, pos, 1) +
                                  SetCurve(clip, p, "m_LocalPosition.z", times, pos, 2);
                }

                if (curves == 0)
                {
                    Debug.LogWarning("[LadyLakePremium][Model] clip copy produced no curves for " + name);
                    return null;
                }
                var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (old != null) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(clip, path);
                Debug.Log("[LadyLakePremium][Model] " + name + ": " + curves + " curves over " +
                          paths + " transforms (" + moving + " rotating), " +
                          clip.length.ToString("0.###") + " s");
                if (moving == 0)
                    Debug.LogError("[LadyLakePremium][Model] " + name +
                                   " has NO rotating transform: the animation would be static");
                foreach (var m in missing) Debug.LogWarning("[LadyLakePremium][Model] " + m);
                return clip;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LadyLakePremium][Model] could not copy clip " + name +
                                 " (" + e.Message + "); the FBX clip will be used directly");
                return null;
            }
        }

        static List<float> KeyTimes(params AnimationCurve[] cs)
        {
            var times = new List<float>();
            foreach (var c in cs)
            {
                if (c == null) continue;
                foreach (var k in c.keys)
                    if (!times.Contains(k.time)) times.Add(k.time);
            }
            times.Sort();
            return times;
        }

        static bool IsMoving(List<Vector3> v)
        {
            for (int i = 1; i < v.Count; i++)
                if ((v[i] - v[0]).sqrMagnitude > 1e-8f) return true;
            return false;
        }

        static int SetCurve(AnimationClip clip, string path, string prop,
                            List<float> times, List<Quaternion> vals, int comp, bool constant = false)
        {
            var c = new AnimationCurve();
            if (constant || vals.Count < 2)
            {
                float v = Component(vals[0], comp);
                c.AddKey(times[0], v);
                c.AddKey(times[times.Count - 1], v);
            }
            else
            {
                for (int i = 0; i < times.Count; i++)
                    c.AddKey(times[i], Component(vals[i], comp));
            }
            Finish(c);
            AnimationUtility.SetEditorCurve(
                clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), prop), c);
            return 1;
        }

        static int SetCurve(AnimationClip clip, string path, string prop,
                            List<float> times, List<Vector3> vals, int comp)
        {
            var c = new AnimationCurve();
            if (!IsMoving(vals))
            {
                float v = Component(vals[0], comp);
                c.AddKey(times[0], v);
                c.AddKey(times[times.Count - 1], v);
            }
            else
            {
                for (int i = 0; i < times.Count; i++)
                    c.AddKey(times[i], Component(vals[i], comp));
            }
            Finish(c);
            AnimationUtility.SetEditorCurve(
                clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), prop), c);
            return 1;
        }

        static void Finish(AnimationCurve c)
        {
            for (int i = 0; i < c.length; i++)
            {
                var key = c[i];
                key.inTangent = 0f;
                key.outTangent = 0f;
                c.MoveKey(i, key);
            }
        }

        static float Component(Quaternion q, int i)
        {
            return i == 0 ? q.x : i == 1 ? q.y : i == 2 ? q.z : q.w;
        }

        static float Component(Vector3 v, int i)
        {
            return i == 0 ? v.x : i == 1 ? v.y : v.z;
        }

        static AnimatorController MakeController(AnimationClip intro, AnimationClip loop)
        {
            var old = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (old != null) AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm = controller.layers[0].stateMachine;

            var sIntro = sm.AddState("Intro", new Vector3(260f, 60f));
            sIntro.motion = intro;
            sIntro.writeDefaultValues = false;
            var sLoop = sm.AddState("Loop", new Vector3(260f, 180f));
            sLoop.motion = loop;
            sLoop.writeDefaultValues = false;
            sm.defaultState = sIntro;

            var t = sIntro.AddTransition(sLoop);
            t.hasExitTime = true;
            t.exitTime = 1f;
            t.duration = 0f;
            t.hasFixedDuration = true;
            t.orderedInterruption = false;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        // ------------------------------------------------------------------
        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        static int CountBones(GameObject root)
        {
            return root.GetComponentsInChildren<Transform>(true).Length;
        }

        static LLPManifestDto ReadManifest()
        {
            try
            {
                if (!File.Exists(ToAbsolute(ManifestPath))) return null;
                return JsonUtility.FromJson<LLPManifestDto>(File.ReadAllText(ToAbsolute(ManifestPath)));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LadyLakePremium][Model] manifest unreadable: " + e.Message);
                return null;
            }
        }

        static Vector3 FromArray(float[] a, Vector3 fallback)
        {
            if (a == null || a.Length < 3) return fallback;
            return new Vector3(a[0], a[1], a[2]);
        }

        // The palm socket lives in the BIND pose of the rig, which is exactly the
        // approved figure-v5 rest pose, so the manifest world grip maps straight in.
        static void PlaceGripSocket(GameObject root, List<string> report)
        {
            var hand = FindDeep(root.transform, "HandR");
            if (hand == null)
            {
                report.Add("WARNING: HandR bone not found; GripSocket not created");
                return;
            }
            var existing = FindDeep(root.transform, "GripSocket");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var dto = ReadManifest();
            Vector3 gripWorld = FromArray(dto != null && dto.sword != null ? dto.sword.grip_world : null,
                                          new Vector3(1.315f, -1.88f, -0.15f));

            var socket = new GameObject("GripSocket");
            socket.transform.SetParent(hand, false);
            socket.transform.localPosition = hand.InverseTransformPoint(gripWorld);
            socket.transform.localRotation = Quaternion.identity;
            socket.transform.localScale = Vector3.one;
            report.Add("GripSocket under HandR localPosition=" +
                       socket.transform.localPosition.ToString("F4") +
                       "  (world grip " + gripWorld.ToString("F4") + ")");
        }

        static void EnsureGripMarker(GameObject root, List<string> report)
        {
            var sword = FindDeep(root.transform, "Sword");
            if (sword == null)
            {
                report.Add("WARNING: Sword node not found");
                return;
            }
            var grip = FindDeep(sword, "Grip");
            if (grip == null)
            {
                var go = new GameObject("Grip");
                go.transform.SetParent(sword, false);
                grip = go.transform;
            }
            // the sword mesh origin IS the contract grip point
            grip.localPosition = Vector3.zero;
            grip.localRotation = Quaternion.identity;
            report.Add("Grip marker at Sword local origin (contract grip point)");
        }

        // ------------------------------------------------------------------
        static void WriteBuildReport(GameObject root, AnimationClip source, Avatar avatar,
                                     Dictionary<string, Material> mats, AnimatorController controller,
                                     List<string> extra)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"built_by\": \"LegacyGwent.LadyLakePremium.Editor.LadyLakeModelBuilder.Build()\",");
            sb.AppendLine("  \"unity\": \"" + Application.unityVersion + "\",");
            sb.AppendLine("  \"prefab\": \"" + PrefabPath + "\",");
            sb.AppendLine("  \"fbx\": \"" + FbxPath + "\",");
            sb.AppendLine("  \"source_clip\": \"" + (source != null ? source.name : "") + "\",");
            sb.AppendLine("  \"source_clip_seconds\": " + (source != null ? source.length : 0f).ToString("0.###") + ",");
            sb.AppendLine("  \"avatar\": " + (avatar != null ? "\"" + avatar.name + "\"" : "null") + ",");
            sb.AppendLine("  \"controller\": \"" + ControllerPath + "\",");
            sb.AppendLine("  \"clips\": [\"" + ClipDir + "/Intro.anim\", \"" + ClipDir + "/Loop.anim\"],");
            sb.AppendLine("  \"materials\": [" + string.Join(", ", mats.Values.Select(m => "\"" + AssetDatabase.GetAssetPath(m) + "\"").ToArray()) + "],");
            sb.AppendLine("  \"transform_count\": " + CountBones(root) + ",");
            sb.AppendLine("  \"notes\": [");
            for (int i = 0; i < extra.Count; i++)
                sb.AppendLine("    \"" + extra[i].Replace("\\", "/").Replace("\"", "'") + "\"" +
                              (i == extra.Count - 1 ? "" : ","));
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            File.WriteAllText(ToAbsolute(Generated + "/unity-build-report.json"), sb.ToString());
        }
    }
}
