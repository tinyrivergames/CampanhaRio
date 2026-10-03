using UnityEngine;

namespace CampanhaRio.Kayak.Paddler
{
    /// <summary>
    /// Spring-based secondary motion for parts without bones (the hat brim and the scarf): a damped spring
    /// driven by the head's acceleration (turns, strokes, landings) offsets the masked vertices in the
    /// PainterlyLit shader (_WOBBLE, vertex color R = hat, G = scarf). A property block keeps the material asset untouched.
    /// </summary>
    [DefaultExecutionOrder(100)] // after the rig has posed the head
    public class PaddlerSecondaryMotion : MonoBehaviour
    {
        static readonly int WobbleOffsetId = Shader.PropertyToID("_WobbleOffset");

        public PaddlerRig rig;
        [Tooltip("Renderers with the wobble material (the character mesh).")]
        public Renderer[] renderers;

        [Tooltip("Spring stiffness (1/s²). Higher = quicker, smaller wobble.")]
        public float stiffness = 70f;
        [Tooltip("Spring damping (1/s). Higher = settles faster.")]
        public float damping = 5f;
        [Tooltip("How much head acceleration pushes the spring (m per m/s²).")]
        public float response = 0.9f;
        [Tooltip("Maximum offset of a fully masked vertex (m).")]
        public float maxOffset = 0.06f;
        [Tooltip("Constant droop/sway from gravity and wind (m).")]
        public Vector3 restOffset = new Vector3(0f, -0.003f, 0f);

        /// <summary>Extra offset set by the animation controller (the hat bobbing while capsized).</summary>
        [System.NonSerialized] public Vector3 extraOffset;

        Vector3 lastPosition, lastVelocity, offset, velocity;
        bool hasLast;
        MaterialPropertyBlock block;

        void LateUpdate()
        {
            if (!rig || !rig.Head) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 p = rig.Head.position;
            if (!hasLast) { lastPosition = p; lastVelocity = Vector3.zero; hasLast = true; }
            Vector3 v = (p - lastPosition) / dt;
            Vector3 accel = Vector3.ClampMagnitude((v - lastVelocity) / dt, 40f);
            lastPosition = p; lastVelocity = v;

            // The brim lags behind the head: pushed opposite to its acceleration
            Vector3 force = -stiffness * offset - damping * velocity - accel * response;
            velocity += force * dt;
            offset = Vector3.ClampMagnitude(offset + velocity * dt, maxOffset);

            block ??= new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(block);
                block.SetVector(WobbleOffsetId, offset + restOffset + extraOffset);
                r.SetPropertyBlock(block);
            }
        }
    }
}
