using CampanhaRio.Core;
using UnityEngine;

namespace CampanhaRio.Campaign
{
    /// <summary>
    /// Seu Alce (PLANO_CAMPANHA 2), simple graybox version: Grandma Nina's old friend who takes the van round to the end
    /// of every river. He waits at the village next to the van (the SeuAlce marker) and greets every arrival with a
    /// different joke (AgencyFlow counts the arrivals in the host's save and tells everyone which one). Local only: each
    /// machine builds its own stand-in where the marker is. The moose model comes in Phase 4.
    /// </summary>
    public class SeuAlce : MonoBehaviour
    {
        /// <summary>One per arrival, in order, then again (English keys; Loc has the Portuguese).</summary>
        public static readonly string[] Jokes =
        {
            "I took the shortcut. The shortcut took longer.",
            "The van and I are the same age. She aged better.",
            "I waved at a bear on the road. He waved back. I think.",
            "I parked on the first try. Don't ask about the other tries.",
            "Seu Alce never gets lost. The road does.",
            "I brought snacks. I ate the snacks. It was a long road.",
        };

        public static string JokeFor(int arrival) => Jokes[Mathf.Abs(arrival) % Jokes.Length];

        [Tooltip("The bubble shows when the local player is this close (m).")]
        public float talkDistance = 18f;

        GameObject body;
        WorldMarker spot;
        GUIStyle bubble;

        void Update()
        {
            if (!spot) spot = WorldMarker.Find(WorldMarker.Kind.SeuAlce);
            if (!spot) { if (body) Destroy(body); return; }
            if (!body) body = Build(spot.transform);
            var me = NetworkPlayer.Local;
            if (me) // he turns to the nearest friend
            {
                var to = me.transform.position - body.transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.1f) body.transform.rotation = Quaternion.Slerp(body.transform.rotation, Quaternion.LookRotation(to), 1f - Mathf.Exp(-3f * Time.deltaTime));
            }
        }

        void OnGUI()
        {
            var flow = AgencyFlow.Instance;
            var me = NetworkPlayer.Local;
            var cam = Camera.main;
            if (!body || !flow || !me || !cam || flow.Current != AgencyFlow.Phase.ReturnBoarding) return;
            if ((me.transform.position - body.transform.position).sqrMagnitude > talkDistance * talkDistance) return;
            var head = cam.WorldToScreenPoint(body.transform.position + Vector3.up * 3.2f);
            if (head.z < 0f) return;
            bubble ??= new GUIStyle(GUI.skin.box) { fontSize = Mathf.RoundToInt(18 * Screen.height / 1080f), wordWrap = true, alignment = TextAnchor.MiddleCenter };
            float w = Screen.width * 0.24f, h = Screen.height * 0.09f;
            GUI.Box(new Rect(head.x - w / 2f, Screen.height - head.y - h, w, h), "Seu Alce: " + Loc.T(JokeFor(flow.Arrivals - 1)), bubble);
        }

        static GameObject Build(Transform at)
        {
            var root = new GameObject("Seu Alce (graybox)");
            root.transform.SetPositionAndRotation(at.position, at.rotation);
            var fur = Mat(new Color(0.42f, 0.29f, 0.2f));
            var antler = Mat(new Color(0.85f, 0.78f, 0.62f));
            var cap = Mat(new Color(0.2f, 0.35f, 0.55f));
            Part(root, PrimitiveType.Capsule, new Vector3(0f, 1.1f, 0f), new Vector3(0.9f, 1.1f, 0.9f), Quaternion.identity, fur);
            Part(root, PrimitiveType.Capsule, new Vector3(0f, 2.3f, 0.25f), new Vector3(0.5f, 0.5f, 0.75f), Quaternion.Euler(90f, 0f, 0f), fur);
            Part(root, PrimitiveType.Cube, new Vector3(0f, 2.75f, 0.05f), new Vector3(1.9f, 0.12f, 0.5f), Quaternion.identity, antler);
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 2.62f, 0.05f), new Vector3(0.55f, 0.08f, 0.55f), Quaternion.identity, cap); // his old cap
            return root;
        }

        static Material Mat(Color c)
        {
            var m = new Material(Shader.Find("CampanhaRio/SoftToon") ?? Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            return m;
        }

        static void Part(GameObject root, PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            var p = GameObject.CreatePrimitive(type);
            DestroyImmediate(p.GetComponent<Collider>());
            p.transform.SetParent(root.transform, false);
            p.transform.SetLocalPositionAndRotation(pos, rot);
            p.transform.localScale = scale;
            p.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
