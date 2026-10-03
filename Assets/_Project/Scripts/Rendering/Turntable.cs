using UnityEngine;

namespace CampanhaRio.Rendering
{
    /// <summary>Spins the lookdev subject slowly while playing (captures stop it at its rest angle).</summary>
    public class Turntable : MonoBehaviour
    {
        [Tooltip("Degrees per second.")]
        public float speed = 20f;
        public bool spinning = true;

        void Update()
        {
            if (spinning) transform.Rotate(0f, speed * Time.deltaTime, 0f, Space.World);
        }

        public void ResetAngle() => transform.rotation = Quaternion.identity;
    }
}
