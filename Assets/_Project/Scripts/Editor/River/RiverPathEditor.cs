using CampanhaRio.River;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// Scene-view handles for the river widths: drag the blue (left) / orange (right) spheres
    /// sideways to change the bank distance at each width point. Knots are edited with the
    /// regular Spline tools (select the River and use the Spline tool in the toolbar).
    /// </summary>
    [CustomEditor(typeof(RiverPath))]
    public class RiverPathEditor : UnityEditor.Editor
    {
        static readonly Color LeftColor = new Color(0.3f, 0.6f, 1f);
        static readonly Color RightColor = new Color(1f, 0.6f, 0.2f);

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var river = (RiverPath)target;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Knots: select the River and use the Spline edit tool.\n" +
                "Widths: drag the blue (left) / orange (right) spheres in the Scene view, or edit the lists above " +
                "(Index = meters along the river).", MessageType.Info);

            if (GUILayout.Button("Add Width Points At Every Knot"))
            {
                Undo.RecordObject(river, "Add river width points");
                AddPointsAtKnots(river);
                river.MarkChanged();
                EditorUtility.SetDirty(river);
            }
            EditorGUILayout.LabelField("Length", $"{river.Length:0.0} m");
        }

        void OnSceneGUI()
        {
            var river = (RiverPath)target;
            if (!river.Container || river.Container.Spline == null || river.Container.Spline.Count < 2) return;
            DrawWidthHandles(river, river.leftWidth, -1f, LeftColor);
            DrawWidthHandles(river, river.rightWidth, 1f, RightColor);
        }

        static void DrawWidthHandles(RiverPath river, SplineData<float> data, float side, Color color)
        {
            var spline = river.Container.Spline;
            for (int i = 0; i < data.Count; i++)
            {
                var point = data[i];
                float t = spline.ConvertIndexUnit(point.Index, data.PathIndexUnit, PathIndexUnit.Normalized);
                Vector3 center = (Vector3)river.Container.EvaluatePosition(t);
                Vector3 tangent = river.Container.EvaluateTangent(t);
                tangent.y = 0f;
                tangent.Normalize();
                Vector3 outward = new Vector3(tangent.z, 0f, -tangent.x) * side;
                Vector3 handlePos = center + outward * point.Value;

                Handles.color = color;
                Handles.DrawDottedLine(center, handlePos, 4f);
                float size = HandleUtility.GetHandleSize(handlePos) * 0.15f;
                Handles.Label(handlePos + Vector3.up * size * 2f, $"{point.Value:0.0} m");

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.Slider(handlePos, outward, size, Handles.SphereHandleCap, 0f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(river, "Change river width");
                    float width = Mathf.Max(1f, Vector3.Dot(moved - center, outward));
                    data.SetDataPointNoSort(i, new DataPoint<float>(point.Index, width));
                    river.MarkChanged();
                    EditorUtility.SetDirty(river);
                }
            }
        }

        /// <summary>Adds a left and right width point at each knot, using the width the river has there now.</summary>
        public static void AddPointsAtKnots(RiverPath river)
        {
            var spline = river.Container.Spline;
            for (int k = 0; k < spline.Count; k++)
            {
                float distance = spline.ConvertIndexUnit(k, PathIndexUnit.Knot, PathIndexUnit.Distance);
                var sample = river.GetPointAtDistance(river.GetKnotDistance(k));
                AddOrReplace(river.leftWidth, distance, sample.leftWidth);
                AddOrReplace(river.rightWidth, distance, sample.rightWidth);
            }
        }

        public static void AddOrReplace(SplineData<float> data, float distance, float value)
        {
            data.PathIndexUnit = PathIndexUnit.Distance;
            for (int i = 0; i < data.Count; i++)
            {
                if (Mathf.Abs(data[i].Index - distance) > 0.5f) continue;
                data.SetDataPoint(i, new DataPoint<float>(distance, value));
                return;
            }
            data.Add(new DataPoint<float>(distance, value));
        }
    }
}
