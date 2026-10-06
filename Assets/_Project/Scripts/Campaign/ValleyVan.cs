using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CampanhaRio.Campaign
{
    /// <summary>
    /// The agency's van in the vertical slice (graybox): Seu Alce drives it, the group rides in it. It lives in Core (it
    /// crosses segments) and the HOST moves it: <see cref="Drive"/> follows a list of points at a calm speed, settling on
    /// the ground under it (the road, the terrain), and the NetworkTransform shows it everywhere. Riding players sit on
    /// <see cref="Seat"/>s (NetworkPlayer follows its seat). The drivable van (VanController) comes back with the road art.
    /// </summary>
    public class ValleyVan : NetworkBehaviour
    {
        public static ValleyVan Instance { get; private set; }

        public float speed = 11f;
        [Tooltip("Slower in the last metres before a stop.")]
        public float arriveSlowdown = 12f;
        public Vector3[] seats =
        {
            new Vector3(-0.5f, 1.3f, 0.6f), new Vector3(0.5f, 1.3f, 0.6f),
            new Vector3(-0.5f, 1.3f, -0.6f), new Vector3(0.5f, 1.3f, -0.6f),
        };

        public bool Driving { get; private set; }

        void Awake() => Instance = this;
        public override void OnDestroy() { if (Instance == this) Instance = null; base.OnDestroy(); }

        public Vector3 Seat(int index) => transform.TransformPoint(seats[Mathf.Abs(index) % seats.Length]);

        /// <summary>Host: put the van here at once (Seu Alce took it there while nobody looked).</summary>
        public void Park(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(Grounded(position), rotation);
        }

        /// <summary>Host: drive through these points (ground height is found on the way), then call done.</summary>
        public void Drive(List<Vector3> points, Action done)
        {
            StopAllCoroutines();
            StartCoroutine(DriveRoutine(points, done));
        }

        IEnumerator DriveRoutine(List<Vector3> points, Action done)
        {
            Driving = true;
            for (int i = 0; i < points.Count; i++)
            {
                bool last = i == points.Count - 1;
                while (true)
                {
                    Vector3 flat = points[i] - transform.position;
                    flat.y = 0f;
                    float dist = flat.magnitude;
                    if (dist < (last ? 0.3f : 2.5f)) break;
                    float v = speed * (1f + AgencyUpgrades.Value(AgencyUpgrades.Effect.VanSpeed)) * (last ? Mathf.Clamp(dist / arriveSlowdown, 0.2f, 1f) : 1f);
                    var dir = flat / dist;
                    var rot = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-3f * Time.deltaTime));
                    var pos = transform.position + dir * Mathf.Min(v * Time.deltaTime, dist);
                    transform.SetPositionAndRotation(Grounded(pos), rot);
                    yield return null;
                }
            }
            Driving = false;
            done?.Invoke();
        }

        /// <summary>The point on the ground under p (the van's base).</summary>
        Vector3 Grounded(Vector3 p)
        {
            var from = new Vector3(p.x, 400f, p.z); // from high above: the points' own heights are only hints
            var hits = Physics.RaycastAll(from, Vector3.down, 600f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 ground = p;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform) || h.rigidbody) continue; // not itself, not a kayak
                if (h.distance < best) { best = h.distance; ground = h.point; }
            }
            return ground;
        }

        public static List<Vector3> Points(params Vector3[] p) => new List<Vector3>(p);
    }
}
