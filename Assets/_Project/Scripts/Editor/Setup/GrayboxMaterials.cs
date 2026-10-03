using System.IO;
using UnityEditor;
using UnityEngine;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// Flat-coloured materials for graybox objects (Art/Materials/Graybox/M_Gray_&lt;name&gt;.mat). They use the SoftToon
    /// shader when it exists, so the graybox already lives in the game's light; otherwise URP Lit.
    /// </summary>
    public static class GrayboxMaterials
    {
        public const string Dir = "Assets/_Project/Art/Materials/Graybox";

        public static Material Get(string name, Color color)
        {
            string path = $"{Dir}/M_Gray_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("CampanhaRio/SoftToon") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (!mat)
            {
                Directory.CreateDirectory(Dir);
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.shader != shader) mat.shader = shader;
            mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
