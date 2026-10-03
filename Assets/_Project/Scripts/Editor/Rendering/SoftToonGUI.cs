using UnityEditor;
using UnityEngine;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// The SoftToon material inspector: a preset picker (Default, Rock/Cliff, Foliage, Character) on top of the normal
    /// property list. The preset values are the same as the Blender side (ArtSource/pipeline/common.py PRESETS).
    /// </summary>
    public class SoftToonGUI : ShaderGUI
    {
        public enum Preset { Default, Rock, Foliage, Character }

        public override void OnGUI(MaterialEditor editor, MaterialProperty[] props)
        {
            var mat = (Material)editor.target;
            var current = (Preset)Mathf.RoundToInt(mat.GetFloat("_Preset"));
            EditorGUI.BeginChangeCheck();
            var picked = (Preset)EditorGUILayout.EnumPopup("Preset", current);
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var o in editor.targets)
                {
                    Undo.RecordObject(o, "SoftToon preset");
                    Apply((Material)o, picked);
                }
            }
            EditorGUILayout.Space();
            base.OnGUI(editor, props);
        }

        /// <summary>Sets a preset's values (the rest of the material is kept).</summary>
        public static void Apply(Material mat, Preset preset)
        {
            mat.SetFloat("_Preset", (float)preset);
            mat.SetFloat("_Wrap", 0.5f); mat.SetFloat("_RampCenter", 0.45f); mat.SetFloat("_RampSoftness", 0.28f);
            mat.SetFloat("_RimStrength", 0.16f); mat.SetFloat("_TopTintAmount", 0f); mat.SetFloat("_Translucency", 0f);
            mat.SetFloat("_Cull", 2f);
            mat.DisableKeyword("_ALPHATEST_ON"); mat.SetFloat("_AlphaClip", 0f);
            mat.DisableKeyword("_WIND"); mat.SetFloat("_Wind", 0f);
            switch (preset)
            {
                case Preset.Rock:
                    mat.SetFloat("_Wrap", 0.4f); mat.SetFloat("_RampSoftness", 0.32f); mat.SetFloat("_TopTintAmount", 0.35f); mat.SetFloat("_RimStrength", 0.1f);
                    break;
                case Preset.Foliage:
                    mat.SetFloat("_Wrap", 0.7f); mat.SetFloat("_RampSoftness", 0.35f); mat.SetFloat("_Translucency", 0.45f); mat.SetFloat("_RimStrength", 0.12f);
                    mat.SetFloat("_Cull", 0f);
                    mat.EnableKeyword("_ALPHATEST_ON"); mat.SetFloat("_AlphaClip", 1f);
                    mat.EnableKeyword("_WIND"); mat.SetFloat("_Wind", 1f);
                    break;
                case Preset.Character:
                    mat.SetFloat("_Wrap", 0.55f); mat.SetFloat("_RampSoftness", 0.22f); mat.SetFloat("_RimStrength", 0.22f);
                    break;
            }
            EditorUtility.SetDirty(mat);
        }
    }
}
