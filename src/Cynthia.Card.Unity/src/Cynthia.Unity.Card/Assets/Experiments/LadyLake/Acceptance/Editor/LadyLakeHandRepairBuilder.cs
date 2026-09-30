using System;
using UnityEditor;
using UnityEngine;

namespace LegacyGwent.LadyLakeLab.Acceptance
{
    public static class LadyLakeHandRepairBuilder
    {
        public static LadyLakeFigureRig[] Attach(LadyLakeLabRoot root, Material figureMaterial)
        {
            const string path = "Assets/Experiments/LadyLake/Textures/hands-repaired.png";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) throw new InvalidOperationException("Missing independently repaired hand sprites: " + path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            // Source atlas rectangles are measured on the 1774 x 887 hand-only sheet.
            // Destinations use the original card's 497 x 710 coordinates, from top left.
            var upper = Create(root, "RaisedHand_Repaired", tex, figureMaterial,
                new Rect(275, 10, 485, 843), new Rect(155.5f, 25f, 82f, 125f),
                new Vector2(180, 139), 0, false);
            var lower = Create(root, "GrippingHand_Repaired", tex, figureMaterial,
                new Rect(990, 220, 535, 530), new Rect(319.5f, 477.1f, 43f, 43f),
                new Vector2(350, 510), 20, true);
            return new[] { upper, lower };
        }

        private static LadyLakeFigureRig Create(LadyLakeLabRoot root, string name, Texture2D texture,
            Material sourceMaterial, Rect atlasPixels, Rect cardPixels, Vector2 pivot, float clockwise, bool gripping)
        {
            int nx = 25, ny = 33;
            var vertices = new Vector3[nx * ny];
            var uv = new Vector2[vertices.Length];
            var uv2 = new Vector2[vertices.Length];
            var profiles = new Vector2[vertices.Length];
            var weights = new Color32[vertices.Length];
            var triangles = new int[(nx - 1) * (ny - 1) * 6];
            float angle = clockwise * Mathf.Deg2Rad, c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            for (int y = 0; y < ny; y++)
            for (int x = 0; x < nx; x++)
            {
                int i = y * nx + x; float u = x / (float)(nx - 1), v = y / (float)(ny - 1);
                Vector2 point = new Vector2(cardPixels.x + u * cardPixels.width, cardPixels.y + v * cardPixels.height);
                if (!gripping && v > .68f)
                {
                    float taper = Mathf.SmoothStep(0, 1, (v - .68f) / .32f);
                    float wristAxis = Mathf.Lerp(.34f, .22f, taper);
                    point.x = cardPixels.x + (wristAxis + (u - wristAxis) * Mathf.Lerp(1f, .72f, taper)) * cardPixels.width;
                }
                Vector2 delta = point - pivot;
                point = pivot + new Vector2(delta.x * c - delta.y * s, delta.x * s + delta.y * c);
                Vector2 plane = LadyLakeLabConfig.AnchorToWorld(point);
                float z = gripping ? -.44f : -.18f;
                float k = LadyLakeLabConfig.PerspectiveCompensation(z);
                vertices[i] = new Vector3(plane.x * k, plane.y * k, z);
                uv[i] = new Vector2((atlasPixels.x + u * atlasPixels.width) / texture.width,
                    1f - (atlasPixels.y + v * atlasPixels.height) / texture.height);
                weights[i] = gripping ? new Color32(0, 0, 255, 0) : new Color32(255, 0, 0, 0);
                // UV3 is zero: repaired fingers remain undistorted and the wrist follows the same bone.
                profiles[i] = Vector2.zero;
            }
            int t = 0;
            for (int y = 0; y < ny - 1; y++)
            for (int x = 0; x < nx - 1; x++)
            {
                int a = y * nx + x;
                triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = a + nx;
                triangles[t++] = a + 1; triangles[t++] = a + nx + 1; triangles[t++] = a + nx;
            }
            var mesh = new Mesh { name = name + "_Relief" };
            mesh.vertices = vertices; mesh.uv = uv; mesh.uv2 = uv2; mesh.uv3 = profiles;
            mesh.colors32 = weights; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string meshPath = LadyLakeLabConfig.GeneratedMeshFolder + "/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
            else AssetDatabase.CreateAsset(mesh, meshPath);

            string materialPath = LadyLakeLabConfig.GeneratedMaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(sourceMaterial); AssetDatabase.CreateAsset(material, materialPath); }
            else material.CopyPropertiesFromMaterial(sourceMaterial);
            material.name = name; material.mainTexture = texture;
            material.renderQueue = gripping ? 3040 : 3055;
            material.SetVector("_WristFadePlane", gripping ? new Vector4(1.008f, 1f, -1380f, 85f) : new Vector4(.545f, -1f, 593f, 140f));
            material.SetVector("_Wobble", Vector4.zero);
            if (material.HasProperty("_MaskHands")) material.SetFloat("_MaskHands", 0);
            EditorUtility.SetDirty(material);

            var go = new GameObject(name);
            go.transform.SetParent(root.layerFigure.parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            LadyLakeMeshDeformer.ConfigureRenderer(renderer);
            var rig = go.AddComponent<LadyLakeFigureRig>(); rig.bakedMesh = mesh;
            rig.shoulder = LadyLakeLabConfig.AnchorToWorld(LadyLakeLabConfig.ShoulderAnchor);
            rig.elbow = LadyLakeLabConfig.AnchorToWorld(LadyLakeLabConfig.ElbowAnchor);
            rig.wrist = LadyLakeLabConfig.AnchorToWorld(LadyLakeLabConfig.SwordHandAnchor);
            rig.CacheBuffers();
            return rig;
        }
    }
}
