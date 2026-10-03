using UnityEngine;

namespace CampanhaRio.Kayak
{
    /// <summary>
    /// Hull colour per kayak (bots, players' chosen colours). A property block on the hull renderer: the shared material
    /// is never copied, so painting costs nothing per frame.
    /// </summary>
    public static class KayakPaint
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public static void SetHullColor(KayakController kayak, Color color)
        {
            if (!kayak) return;
            Transform visual = kayak.transform.Find("Visual");
            Paint(visual, color);
        }

        /// <summary>Any kayak visual with a "Hull" child (the race kayak's Visual, or a camp kayak).</summary>
        public static void SetHullColor(Transform root, Color color)
        {
            if (!root) return;
            var visual = root.Find("Visual");
            Paint(visual ? visual : root, color);
        }

        static void Paint(Transform visual, Color color)
        {
            var hull = visual ? visual.Find("Hull") : null;
            var renderer = hull ? hull.GetComponent<Renderer>() : null;
            if (!renderer) return;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }
}
