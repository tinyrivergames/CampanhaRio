using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// Imports what ArtSource/pipeline/export.py writes into Art/Models/&lt;Family&gt;/ (see Docs/ART_PIPELINE.md):
    ///   - FBX settings: meters, normals from the file (the soft normals made in Blender), no animation, no cameras/lights;
    ///   - a LODGroup from the _LOD0/_LOD1/_LOD2 suffixes (40% / 15% / 2% of the screen, dither cross-fade);
    ///   - _COL becomes a convex MeshCollider (no renderer);
    ///   - the material: Art/Materials/&lt;Family&gt;/M_&lt;Asset&gt;.mat on the SoftToon shader, built from &lt;Asset&gt;.softtoon.json
    ///     (the same parameters the Blender preview used), and remapped onto the FBX.
    /// </summary>
    public class ArtImportPostprocessor : AssetPostprocessor
    {
        const string ModelsDir = "Assets/_Project/Art/Models/";
        const string MaterialsDir = "Assets/_Project/Art/Materials/";
        static readonly float[] LodHeights = { 0.4f, 0.15f, 0.02f };

        static bool IsPipelineModel(string path) => path.StartsWith(ModelsDir) && path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessModel()
        {
            if (!IsPipelineModel(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.addCollider = false;
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!IsPipelineModel(assetPath)) return;
            string asset = Path.GetFileNameWithoutExtension(assetPath);

            // _COL: a convex collider, never drawn
            foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => t.name.EndsWith("_COL")).ToArray())
            {
                var mf = t.GetComponent<MeshFilter>();
                if (mf && mf.sharedMesh)
                {
                    var col = root.AddComponent<MeshCollider>();
                    col.sharedMesh = mf.sharedMesh;
                    col.convex = true;
                }
                Object.DestroyImmediate(t.gameObject);
            }

            // _LOD0/1/2: one LODGroup on the root
            var lods = Enumerable.Range(0, 3)
                .Select(i => root.GetComponentsInChildren<Renderer>(true).Where(r => r.name.EndsWith("_LOD" + i)).ToArray())
                .Where(r => r.Length > 0).ToArray();
            if (lods.Length > 0)
            {
                var group = root.GetComponent<LODGroup>(); // Unity may already have made one from the names
                if (!group) group = root.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.CrossFade;
                group.animateCrossFading = true;
                var heights = SidecarLodHeights() ?? LodHeights; // per asset when the generator set them
                group.SetLODs(lods.Select((r, i) => new LOD(heights[Mathf.Min(i, heights.Length - 1)], r)).ToArray());
                group.RecalculateBounds();
            }
            Debug.Log($"[Campanha] Imported {asset}: {lods.Length} LOD(s), collider {(root.GetComponent<MeshCollider>() ? "yes" : "no")}.");
        }

        float[] SidecarLodHeights()
        {
            string path = Path.ChangeExtension(assetPath, ".softtoon.json");
            if (!File.Exists(path)) return null;
            var h = JsonUtility.FromJson<Sidecar>(File.ReadAllText(path)).lodHeights;
            return h != null && h.Length > 0 ? h : null;
        }

        /// <summary>Rebuilds every pipeline material from its sidecar (after a change to how materials are built).
        /// Menu: CampanhaRio > Art > Reimport Materials. Batch: CampanhaRio.Editor.ArtImportPostprocessor.ReimportMaterials</summary>
        [MenuItem("CampanhaRio/Art/Reimport Materials")]
        public static void ReimportMaterials()
        {
            foreach (string guid in AssetDatabase.FindAssets("", new[] { ModelsDir.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".softtoon.json")) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Campanha] Pipeline materials rebuilt from their sidecars.");
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
            {
                if (!path.StartsWith(ModelsDir) || !path.EndsWith(".softtoon.json")) continue;
                string fbx = path.Replace(".softtoon.json", ".fbx");
                var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
                if (!importer) continue;
                var data = JsonUtility.FromJson<Sidecar>(File.ReadAllText(path));
                var entries = data.materialList != null && data.materialList.Length > 0 ? data.materialList : new[] { data.material };
                if (string.IsNullOrEmpty(entries[0].name)) entries[0].name = "M_" + data.asset;
                bool changed = false;
                foreach (var entry in entries)
                {
                    var mat = BuildMaterial(data.family, entry);
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), entry.name);
                    if (importer.GetExternalObjectMap().TryGetValue(id, out var existing) && existing == mat) continue;
                    importer.AddRemap(id, mat);
                    changed = true;
                }
                if (changed) importer.SaveAndReimport();
            }
        }

        /// <summary>Baked textures (impostors, background cards): sRGB colour with an alpha that is clipped.</summary>
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Project/Art/Textures/") || assetPath.Contains("/Water/")) return;
            var t = (TextureImporter)assetImporter;
            t.sRGBTexture = true;
            t.alphaIsTransparency = true;
            t.wrapMode = TextureWrapMode.Clamp;
            t.mipmapEnabled = true;
            t.mipMapsPreserveCoverage = true; // alpha-clipped edges don't thin out with distance
            t.alphaTestReferenceValue = 0.5f;
        }

        /// <summary>Art/Materials/&lt;Family&gt;/&lt;name&gt;.mat on the SoftToon shader, with the Blender preview's parameters.</summary>
        static Material BuildMaterial(string family, Params p)
        {
            string dir = MaterialsDir + family;
            Directory.CreateDirectory(dir);
            string matPath = $"{dir}/{p.name}.mat";
            var shader = Shader.Find("CampanhaRio/SoftToon");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
            mat.shader = shader;
            if (System.Enum.TryParse(p.preset, out SoftToonGUI.Preset preset)) SoftToonGUI.Apply(mat, preset);
            mat.SetColor("_BaseColor", Hex(p._BaseColor));
            mat.SetFloat("_Wrap", p._Wrap);
            mat.SetFloat("_RampCenter", p._RampCenter);
            mat.SetFloat("_RampSoftness", p._RampSoftness);
            mat.SetFloat("_ReceiveShadows", p._ReceiveShadows);
            mat.SetFloat("_RimStrength", p._RimStrength);
            mat.SetFloat("_RimPower", p._RimPower);
            mat.SetFloat("_UseVertexColor", p._UseVertexColor);
            mat.SetColor("_GradientBottom", Hex(p._GradientBottom));
            mat.SetColor("_GradientTop", Hex(p._GradientTop));
            if (p._GradientHeights != null && p._GradientHeights.Length == 2) mat.SetVector("_GradientHeights", new Vector4(p._GradientHeights[0], p._GradientHeights[1]));
            mat.SetColor("_TopTint", Hex(p._TopTint));
            mat.SetFloat("_TopTintAmount", p._TopTintAmount);
            mat.SetFloat("_TopTintSharpness", p._TopTintSharpness);
            mat.SetFloat("_Translucency", p._Translucency);
            mat.SetFloat("_StencilRef", p._StencilRef);
            mat.SetFloat("_StencilWriteMask", p._StencilWriteMask);
            mat.SetFloat("_StencilComp", p._StencilWriteMask > 0f ? (float)UnityEngine.Rendering.CompareFunction.Always : 0f); // stencil off for everything but the boats (D3D12: see TECH_DECISIONS)

            // A texture with alpha is clipped; a mesh without one never pays for alpha testing
            var tex = string.IsNullOrEmpty(p._BaseMap) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(p._BaseMap);
            mat.SetTexture("_BaseMap", tex);
            SetKeyword(mat, "_ALPHATEST_ON", "_AlphaClip", tex != null);
            mat.SetFloat("_Cutoff", 0.5f);
            // Wind only where the vertex alpha carries its weight (a mesh without colours would sway from the ground up)
            SetKeyword(mat, "_WIND", "_Wind", mat.IsKeywordEnabled("_WIND") && p._UseVertexColor > 0.5f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssetIfDirty(mat);
            return mat;
        }

        static void SetKeyword(Material mat, string keyword, string property, bool on)
        {
            if (on) mat.EnableKeyword(keyword); else mat.DisableKeyword(keyword);
            mat.SetFloat(property, on ? 1f : 0f);
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;

        [System.Serializable]
        class Sidecar { public string asset, family, source; public Params material; public Params[] materialList; public float[] lodHeights; }

        [System.Serializable]
        class Params
        {
            public string name, preset, _BaseColor, _GradientBottom, _GradientTop, _TopTint, _BaseMap;
            public float _Wrap, _RampCenter, _RampSoftness, _ReceiveShadows, _RimStrength, _RimPower, _UseVertexColor;
            public float _TopTintAmount, _TopTintSharpness, _Translucency, _StencilRef, _StencilWriteMask;
            public float[] _GradientHeights;
        }
    }
}
