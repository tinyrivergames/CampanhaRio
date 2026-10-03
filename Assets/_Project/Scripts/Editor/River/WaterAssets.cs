using UnityEditor;
using UnityEngine;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// The river water material (Art/Materials/M_RiverWater.mat) on the ported RiverWater shader, with the procedural
    /// normal and foam textures. The shader's own defaults are the turquoise palette; the restyle comes in Phase 3.
    /// </summary>
    public static class WaterAssets
    {
        public const string MaterialPath = "Assets/_Project/Art/Materials/M_RiverWater.mat";

        public static Material WaterMaterial()
        {
            if (!AssetDatabase.LoadAssetAtPath<Texture2D>(WaterTextureGenerator.NormalPath)) WaterTextureGenerator.GenerateAll();
            var shader = Shader.Find("CampanhaRio/RiverWater");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, MaterialPath); }
            mat.shader = shader;
            mat.SetTexture("_NormalTex", AssetDatabase.LoadAssetAtPath<Texture2D>(WaterTextureGenerator.NormalPath));
            mat.SetTexture("_FoamTex", AssetDatabase.LoadAssetAtPath<Texture2D>(WaterTextureGenerator.FoamPath));
            mat.SetFloat("_Caustics", 1f);
            mat.EnableKeyword("_CAUSTICS");
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
