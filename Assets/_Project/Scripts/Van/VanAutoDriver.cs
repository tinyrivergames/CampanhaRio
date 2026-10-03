using UnityEngine;

namespace CampanhaRio.Van
{
    /// <summary>
    /// "A van sobe sozinha" (Stage 11): drives the van along the road at a calm speed, up to the camp or down to the
    /// take-out, and parks at the stop. Pure pursuit on the road's line: steer toward a point ahead (further at speed), slow
    /// for curves and for people near the road, brake to a stop at the end. The scripted test driver uses it too.
    /// </summary>
    public class VanAutoDriver
    {
        public enum Goal { None, Camp, TakeOut }

        public float cruise = 15f, curveSpeed = 7.5f, loopSpeed = 5f;
        public Goal goal;
        /// <summary>Arrived at the goal's stop (and stopped).</summary>
        public bool Arrived { get; private set; }

        readonly VanController van;
        float progress = -1f;

        public VanAutoDriver(VanController van) { this.van = van; }

        public void Start(Goal g) { goal = g; Arrived = false; progress = -1f; }
        public void Stop() { goal = Goal.None; }

        /// <summary>The controls toward the goal this frame (or a gentle stop with no goal).</summary>
        public VanControls Drive(float slowFactor = 1f)
        {
            var road = VanRoad.Instance;
            if (!road || goal == Goal.None || Arrived) return new VanControls { throttle = van.ForwardSpeed > 0.3f ? -0.6f : 0f, handbrake = van.Speed < 0.5f };
            Vector3 pos = van.transform.position;
            // Where we are on the circuit: near where we were (so it never jumps onto the other lane), else the lane we face
            float offset;
            float d = progress >= 0f ? road.NearestAround(pos, progress, 6f, 25f, out offset) : road.Nearest(pos, van.transform.forward, out offset);
            if (offset > 12f) d = road.Nearest(pos, van.transform.forward, out offset); // lost (a reset): look again
            progress = d;
            float remaining = road.Wrap((goal == Goal.Camp ? road.UpLength : 0f) - d);
            if (remaining > road.Length - 3f) remaining = 0f; // just past the stop

            float speed = van.ForwardSpeed;
            float look = 6f + Mathf.Abs(speed) * 0.8f;
            Vector3 target = road.PointAt(d + look);
            Vector3 local = van.transform.InverseTransformPoint(target);
            float steer = Mathf.Clamp(Mathf.Atan2(local.x, Mathf.Max(local.z, 0.5f)) * Mathf.Rad2Deg / 28f, -1f, 1f);
            // Slow down in time for the curves ahead: for each one, the speed it allows, braking back from it
            float want = cruise;
            for (float k = 5f; k <= 45f; k += 5f)
            {
                float turn = Vector3.Angle(road.DirectionAt(d + k - 5f), road.DirectionAt(d + k + 5f)); // over 10 m
                float allowed = turn > 50f ? loopSpeed : Mathf.Lerp(cruise, curveSpeed, Mathf.InverseLerp(6f, 30f, turn));
                want = Mathf.Min(want, Mathf.Sqrt(allowed * allowed + 2f * 3f * Mathf.Max(0f, k - 8f)));
            }
            want *= slowFactor;
            // Stop at the stop: slow down over the last 25 m
            want = Mathf.Min(want, Mathf.Sqrt(2f * 2.2f * Mathf.Max(0f, remaining - 0.8f)));
            if (offset > road.halfWidth + 3f) want = Mathf.Min(want, 4f); // off the road (after a bump): get back slowly
            float throttle = Mathf.Clamp((want - speed) * 0.45f, -1f, 1f);
            if (remaining < 1.2f && Mathf.Abs(speed) < 0.4f) { Arrived = true; return new VanControls { handbrake = true }; }
            return new VanControls { throttle = throttle, steer = steer, handbrake = remaining < 1.2f };
        }
    }
}
