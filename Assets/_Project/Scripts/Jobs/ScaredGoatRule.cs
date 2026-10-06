using CampanhaRio.Kayak;
using CampanhaRio.River;
using UnityEngine;

namespace CampanhaRio.Jobs
{
    /// <summary>
    /// The scared goat (PLANO_CAMPANHA 3.2): a passenger on the carrier's rear deck. His FEAR fills with speed, jumps, hits
    /// and capsizes, and calms down when the boat goes gently. When it is full he panics and jumps into the river: he
    /// drifts with the current until any kayak of the group paddles up to him (he then rides with that one). Each panic
    /// costs happiness (the condition: the passenger star), and the group can't finish without him aboard.
    /// The goat is a graybox stand-in (white capsule, head, horns) until Phase 4.
    /// </summary>
    public class ScaredGoatRule : CarriedRule
    {
        [Header("Fear")]
        [Tooltip("Speed (m/s) he is fine with; above it fear grows with the extra speed.")]
        public float calmSpeed = 4.5f;
        public float fearPerExtraSpeed = 0.06f;
        [Tooltip("Fear lost per second when the boat goes gently.")]
        public float calmDown = 0.1f;
        [Tooltip("Fear from a landing, per second in the air.")]
        public float jumpFear = 0.5f;
        public float scrapeFear = 0.12f, spinOutFear = 0.3f;
        [Header("Panic")]
        [Tooltip("Happiness lost each time he jumps out.")]
        public float panicCost = 0.25f;
        [Tooltip("Happiness lost per second while he is very scared (fear above 0.7).")]
        public float scaredCost = 0.015f;
        [Tooltip("A kayak this close picks him up (m).")]
        public float pickupRadius = 2.4f;

        public float Fear { get; private set; }
        public bool InWater => goatInWater;
        public int Panics { get; private set; }
        public Vector3 GoatPosition => goat ? goat.transform.position : Vector3.zero;

        public override string Label => "Goat";
        public override string State =>
            goatInWater ? "in the water! Fetch him!" : Fear < 0.3f ? "calm" : Fear < 0.6f ? "nervous" : Fear < 0.85f ? "scared" : "about to jump!";
        public override float Meter => Fear;
        public override string MeterLabel => "Fear";
        public override bool CanFinish => !goatInWater;

        GameObject goat;
        bool goatInWater;
        RiverPath river;
        int hint = -1;

        public override void OnAttemptBegin()
        {
            base.OnAttemptBegin();
            Fear = 0f;
            Panics = 0;
            goatInWater = false;
            if (!river) river = FindAnyObjectByType<RiverPath>();
            Seat(Carrier);
        }

        public override void Tick(float dt)
        {
            if (goatInWater) { Drift(dt); return; }
            if (!Carrier) return;
            float speed = Carrier.Speed;
            Fear = Mathf.Clamp01(Fear + (speed > calmSpeed ? (speed - calmSpeed) * fearPerExtraSpeed : -calmDown) * dt);
            if (Fear > 0.7f) Condition = Mathf.Clamp01(Condition - scaredCost * dt);
            if (Fear >= 1f) Panic("too fast");
        }

        protected override void Capsized() { if (!goatInWater) Panic("capsized"); }

        protected override void Landed(float airTime)
        {
            if (!goatInWater && airTime > 0.25f) Scare(airTime * jumpFear, $"jump ({airTime:0.0} s)");
        }

        protected override void Hit(ImpactLevel level, float intensity, Vector3 point, Vector3 normal)
        {
            if (goatInWater) return;
            float k = level == ImpactLevel.SpinOut ? spinOutFear : level == ImpactLevel.Scrape ? scrapeFear : 0f;
            if (k > 0f) Scare(k * Mathf.Clamp01(intensity + 0.3f), $"{level} hit");
        }

        void Scare(float amount, string why)
        {
            Fear = Mathf.Clamp01(Fear + amount);
            Debug.Log($"[Job] goat: {why}, fear {Fear:0.00}");
            if (Fear >= 1f) Panic(why);
        }

        void Panic(string why)
        {
            Panics++;
            Condition = Mathf.Clamp01(Condition - panicCost);
            goatInWater = true;
            var from = Carrier ? Carrier.transform : null;
            Vector3 at = from ? from.position + from.right * 3.4f - from.forward * 1.2f : goat.transform.position;
            SetCarrier(null);
            goat.transform.SetParent(null, true);
            goat.transform.SetPositionAndRotation(at, Quaternion.LookRotation(from ? from.forward : Vector3.forward));
            Debug.Log($"[Job] goat: PANIC ({why}) -> jumped into the river (panic {Panics}, happiness {Condition:0.00})");
        }

        void Drift(float dt)
        {
            var p = goat.transform.position;
            if (river)
            {
                p += river.GetCurrent(p, ref hint) * dt;
                p.y = river.GetWaterHeight(p, ref hint) + 0.15f + Mathf.Sin(Time.time * 3f) * 0.05f; // bobbing
            }
            goat.transform.position = p;
            foreach (var k in KayakRegistry.All)
            {
                if ((k.transform.position - p).sqrMagnitude > pickupRadius * pickupRadius) continue;
                goatInWater = false;
                Fear = 0.4f; // wet and still a bit shaken
                SetCarrier(k);
                Seat(k);
                Debug.Log($"[Job] goat: picked up by {k.name}");
                break;
            }
        }

        /// <summary>The goat on this kayak's rear deck (the cargo X).</summary>
        void Seat(KayakController kayak)
        {
            if (!goat) goat = BuildGoat();
            if (!kayak) return;
            goat.transform.SetParent(kayak.transform, false);
            goat.transform.localPosition = new Vector3(0f, 0.32f, -0.95f);
            goat.transform.localRotation = Quaternion.identity;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (goat) Destroy(goat);
        }

        static GameObject BuildGoat()
        {
            var root = new GameObject("Goat (graybox)");
            var white = new Material(Shader.Find("CampanhaRio/SoftToon") ?? Shader.Find("Universal Render Pipeline/Lit"));
            white.SetColor("_BaseColor", new Color(0.93f, 0.92f, 0.88f));
            var horn = new Material(white);
            horn.SetColor("_BaseColor", new Color(0.35f, 0.28f, 0.22f));
            Part(root, PrimitiveType.Capsule, new Vector3(0f, 0.12f, 0f), new Vector3(0.32f, 0.26f, 0.5f), Quaternion.Euler(90f, 0f, 0f), white);
            Part(root, PrimitiveType.Sphere, new Vector3(0f, 0.36f, 0.28f), new Vector3(0.2f, 0.22f, 0.24f), Quaternion.identity, white);
            Part(root, PrimitiveType.Capsule, new Vector3(0.06f, 0.5f, 0.24f), new Vector3(0.04f, 0.07f, 0.04f), Quaternion.Euler(-25f, 0f, 15f), horn);
            Part(root, PrimitiveType.Capsule, new Vector3(-0.06f, 0.5f, 0.24f), new Vector3(0.04f, 0.07f, 0.04f), Quaternion.Euler(-25f, 0f, -15f), horn);
            return root;
        }

        static void Part(GameObject root, PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            var p = GameObject.CreatePrimitive(type);
            DestroyImmediate(p.GetComponent<Collider>()); // never part of the kayak's physics (not even for a frame)
            p.transform.SetParent(root.transform, false);
            p.transform.SetLocalPositionAndRotation(pos, rot);
            p.transform.localScale = scale;
            p.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
