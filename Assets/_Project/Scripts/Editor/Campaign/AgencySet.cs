using CampanhaRio.Campaign;
using CampanhaRio.World;
using UnityEditor;
using UnityEngine;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// The agency's set, composed after the developer's reference picture (Docs/Reference/agencia_referencia.png): seen
    /// from the van's trail, a wide earth trail runs straight into the woods; the cabin (Grandma Nina's agency: a wooden
    /// cabin with a porch, a gabled roof, a sign and a lit inside) stands on the right, facing the trail, with the map board
    /// in front of it; a trail sign, a fence and big grey rocks on the left; lantern posts; pebbles on the trail. Graybox
    /// wood and stone until the props are made in Blender. Called by AgencyDressing (after the terrain exists).
    /// </summary>
    public static class AgencySet
    {
        public static readonly Vector3 CabinPos = new Vector3(-12f, 0f, -331f);
        public const float CabinYaw = 30f;   // the porch faces the trails' junction
        public static readonly Vector3 Junction = new Vector3(0f, 0f, -318f);
        public static readonly Vector3 RefEye = new Vector3(4f, 2.2f, -303f), RefAt = new Vector3(0f, 1.0f, -338f); // (y above the ground)

        static Terrain terrain;
        static Transform set;
        static Material wood, woodDark, woodLight, roof, glow, stone;

        static float G(float x, float z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;

        public static void Build(Transform root, Terrain t, GroundPaths paths)
        {
            terrain = t;
            foreach (string old in new[] { "Galpao", "QuadroDePedidos", "QuadroDeMelhorias", "Set" })
            {
                var o = root.Find(old);
                if (o) Object.DestroyImmediate(o.gameObject);
            }
            foreach (var m in root.GetComponentsInChildren<WorldMarker>(true))
                if (m.kind == WorldMarker.Kind.Entry || m.kind == WorldMarker.Kind.JobBoard || m.kind == WorldMarker.Kind.UpgradeBoard)
                    Object.DestroyImmediate(m.gameObject);
            set = new GameObject("Set").transform;
            set.SetParent(root, false);
            wood = GrayboxMaterials.Get("CabinWood", new Color(0.52f, 0.32f, 0.19f));
            woodDark = GrayboxMaterials.Get("CabinWoodDark", new Color(0.34f, 0.2f, 0.11f));
            woodLight = GrayboxMaterials.Get("CabinWoodLight", new Color(0.66f, 0.45f, 0.28f));
            roof = GrayboxMaterials.Get("CabinRoof", new Color(0.45f, 0.17f, 0.12f));
            glow = Glow("M_Glow_Lantern", new Color(1f, 0.85f, 0.5f));
            stone = GrayboxMaterials.Get("PebbleGrey", new Color(0.5f, 0.5f, 0.52f));

            Cabin(root);
            MapBoard(root);
            SignPost(new Vector3(6.6f, 0f, -327f), 205f);
            Fence(new Vector3(8.5f, 0, -333f), new Vector3(22f, 0, -338f));
            Fence(new Vector3(-6.5f, 0, -341f), new Vector3(-3.5f, 0, -350f));
            Fence(new Vector3(-27f, 0, -320f), new Vector3(-36f, 0, -315f));
            foreach (var l in new[] { new Vector3(-3.6f, 0, -309f), new Vector3(3.8f, 0, -346f), new Vector3(-7.5f, 0, -323.5f), new Vector3(7.5f, 0, -319f) }) Lantern(l);
            BigRocks();
            Pebbles(paths);
            var entry = Marker(root, WorldMarker.Kind.Entry, new Vector3(2f, 0f, -309f), 180f, 1f);
            var segment = root.GetComponent<Campaign.Segment>();
            if (segment) segment.entry = entry; // (the old entry was removed with the old yard: players spawned in the void)
        }

        /// <summary>A self-lit material (unlit): the lanterns' glow and the warm inside (SoftToon only takes the sun).</summary>
        static Material Glow(string name, Color c)
        {
            string path = $"Assets/_Project/Art/Materials/Graybox/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------------------------------------------------------------- pieces
        static Transform Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material m, Quaternion? rot = null)
        {
            var b = KayakPrefabBuilder.Primitive(PrimitiveType.Cube, name, parent, localPos, size, m);
            if (rot.HasValue) b.localRotation = rot.Value;
            return b;
        }

        static Transform Solid(Transform parent, string name, Vector3 localPos, Vector3 size, Material m)
        {
            var b = Box(parent, name, localPos, size, m);
            b.gameObject.AddComponent<BoxCollider>(); // walls and floors stop the walk
            return b;
        }

        static Light PointLight(Transform parent, Vector3 localPos, float range, float intensity)
        {
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.localPosition = localPos;
            l.type = LightType.Point;
            l.color = new Color(1f, 0.74f, 0.42f);
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        /// <summary>The cabin: local +z is the front (the porch). 12 m wide, 8 m deep, a 3 m porch, on a 0.6 m floor.</summary>
        static void Cabin(Transform root)
        {
            var c = new GameObject("Cabana").transform;
            c.SetParent(set, false);
            float y = G(CabinPos.x, CabinPos.z);
            c.SetPositionAndRotation(new Vector3(CabinPos.x, y, CabinPos.z), Quaternion.Euler(0f, CabinYaw, 0f));
            const float W = 12f, D = 8f, P = 3f, F = 0.6f, H = 3.3f;
            // Floor and porch deck (planks), a stone base under it, steps
            Solid(c, "Base", new Vector3(0, F * 0.5f - 0.3f, -D / 2 + P / 2), new Vector3(W + 0.4f, F + 0.6f, D + P + 0.2f), stone);
            for (int i = 0; i < 12; i++)
                Box(c, "Plank", new Vector3(-W / 2 + 0.5f + i, F + 0.03f, -D / 2 + P / 2), new Vector3(0.96f, 0.06f, D + P), i % 2 == 0 ? woodLight : wood);
            for (int s = 0; s < 3; s++)
                Solid(c, "Step", new Vector3(0, F - 0.2f * (s + 1) + 0.1f, P + 0.35f + s * 0.35f), new Vector3(3.2f, 0.2f, 0.4f), woodLight);
            // Walls: back, sides (log courses), the front open as a shop counter
            for (int k = 0; k < 6; k++)
            {
                float yy = F + 0.28f + k * 0.55f;
                var m = k % 2 == 0 ? wood : woodDark;
                Solid(c, "BackLog", new Vector3(0, yy, -D), new Vector3(W, 0.55f, 0.3f), m);
                Solid(c, "SideLogL", new Vector3(-W / 2, yy, -D / 2), new Vector3(0.3f, 0.55f, D), m);
                Solid(c, "SideLogR", new Vector3(W / 2, yy, -D / 2), new Vector3(0.3f, 0.55f, D), m);
                Solid(c, "FrontL", new Vector3(-W / 2 + 1.4f, yy, 0), new Vector3(2.8f, 0.55f, 0.3f), m);
                Solid(c, "FrontR", new Vector3(W / 2 - 1.4f, yy, 0), new Vector3(2.8f, 0.55f, 0.3f), m);
            }
            Box(c, "Lintel", new Vector3(0, F + H - 0.3f, 0), new Vector3(W - 5.6f, 0.6f, 0.32f), woodDark);
            Solid(c, "Counter", new Vector3(0, F + 0.55f, -0.8f), new Vector3(W - 5.8f, 1.1f, 0.7f), woodLight);
            Box(c, "CounterTop", new Vector3(0, F + 1.13f, -0.8f), new Vector3(W - 5.6f, 0.08f, 0.85f), woodDark);
            // Inside: the map on the back wall, shelves, a lamp (the warm lit inside of the reference)
            Box(c, "WallMap", new Vector3(1.5f, F + 1.9f, -D + 0.17f), new Vector3(3.2f, 1.9f, 0.04f), GrayboxMaterials.Get("MapGreen", new Color(0.55f, 0.75f, 0.45f)));
            Box(c, "WallMapRiver", new Vector3(1.5f, F + 1.9f, -D + 0.2f), new Vector3(0.25f, 1.7f, 0.03f), GrayboxMaterials.Get("MapBlue", new Color(0.32f, 0.58f, 0.85f)), Quaternion.Euler(0, 0, 25f));
            Box(c, "Shelf", new Vector3(-3.5f, F + 1.6f, -D + 0.4f), new Vector3(2.5f, 0.08f, 0.5f), woodLight);
            Box(c, "Crate", new Vector3(-3.8f, F + 0.4f, -2.5f), new Vector3(0.8f, 0.8f, 0.8f), woodLight);
            Box(c, "Suitcase", new Vector3(3.6f, F + 0.35f, -1.6f), new Vector3(0.7f, 0.7f, 0.35f), GrayboxMaterials.Get("Suitcase", new Color(0.2f, 0.42f, 0.5f)));
            PointLight(c, new Vector3(0, F + 2.2f, -2.5f), 11f, 4.5f); // the warm lit inside
            PointLight(c, new Vector3(-3.5f, F + 2f, -5.5f), 7f, 2.5f);
            PointLight(c, new Vector3(3.5f, F + 2f, -5.5f), 7f, 2.5f);
            Box(c, "InsideBackLining", new Vector3(0, F + H / 2f, -D + 0.16f), new Vector3(W - 0.4f, H, 0.02f), Glow("M_Glow_CabinInside", new Color(0.86f, 0.58f, 0.32f)));
            Box(c, "InsideLamp", new Vector3(-1.5f, F + 2.6f, -3f), new Vector3(0.35f, 0.4f, 0.35f), glow);
            Box(c, "InsideLamp", new Vector3(2.5f, F + 2.6f, -4.5f), new Vector3(0.35f, 0.4f, 0.35f), glow);
            // Porch posts and hanging lanterns
            foreach (float x in new[] { -W / 2 + 0.2f, -1.9f, 1.9f, W / 2 - 0.2f })
                Solid(c, "PorchPost", new Vector3(x, F + H / 2 + 0.3f, P - 0.2f), new Vector3(0.28f, H + 0.6f, 0.28f), woodDark);
            Box(c, "PorchBeam", new Vector3(0, F + H + 0.55f, P - 0.2f), new Vector3(W + 0.6f, 0.3f, 0.32f), woodDark);
            foreach (float x in new[] { -2.6f, 2.6f })
            {
                Box(c, "PorchLantern", new Vector3(x, F + H - 0.15f, P - 0.45f), new Vector3(0.28f, 0.38f, 0.28f), glow);
                PointLight(c, new Vector3(x, F + H - 0.4f, P - 0.3f), 6f, 1.4f);
            }
            // Railings on the porch, either side of the steps
            foreach (float s in new[] { -1f, 1f })
            {
                Box(c, "Rail", new Vector3(s * (W / 4 + 1.1f), F + 0.9f, P - 0.2f), new Vector3(W / 2 - 2.3f, 0.1f, 0.1f), woodLight);
                Box(c, "Rail", new Vector3(s * (W / 4 + 1.1f), F + 0.5f, P - 0.2f), new Vector3(W / 2 - 2.3f, 0.1f, 0.1f), woodLight);
            }
            // The gabled roof, the gable on the FRONT (the reference): the ridge runs front to back, two slabs down to the sides
            const float rise = 3.0f, overhang = 0.8f;
            float roofFront = P + 0.7f, roofBack = -D - 0.7f, depth = roofFront - roofBack, midZ = (roofFront + roofBack) / 2f;
            float half = W / 2f + overhang, wallTop = F + H + 0.6f, ridgeY = wallTop + rise;
            float angle = Mathf.Atan2(rise, half) * Mathf.Rad2Deg, len = Mathf.Sqrt(rise * rise + half * half) + 0.2f;
            Box(c, "RoofL", new Vector3(-half / 2f, ridgeY - rise / 2f, midZ), new Vector3(len, 0.3f, depth), roof, Quaternion.Euler(0, 0, angle));
            Box(c, "RoofR", new Vector3(half / 2f, ridgeY - rise / 2f, midZ), new Vector3(len, 0.3f, depth), roof, Quaternion.Euler(0, 0, -angle));
            for (int k = 1; k < 5; k++) // plank lines on the roof
            {
                float t = k / 5f;
                foreach (float s in new[] { -1f, 1f })
                    Box(c, "RoofPlank", new Vector3(s * half * t, ridgeY - rise * t + 0.17f, midZ), new Vector3(0.12f, 0.08f, depth), woodDark, Quaternion.Euler(0, 0, -s * angle));
            }
            Box(c, "Ridge", new Vector3(0, ridgeY + 0.1f, midZ), new Vector3(0.45f, 0.3f, depth + 0.1f), woodDark);
            foreach (float z in new[] { 0f, -D })
            {
                var gable = new GameObject("Gable").transform;
                gable.SetParent(c, false);
                gable.localPosition = new Vector3(0, wallTop, z);
                gable.localScale = new Vector3(W / Mathf.Sqrt(2f), rise * 2f / Mathf.Sqrt(2f), 1f);
                Box(gable, "GableFill", Vector3.zero, new Vector3(1f, 1f, 0.28f), wood, Quaternion.Euler(0, 0, 45f));
            }
            Box(c, "GableBeam", new Vector3(0, wallTop, 0.05f), new Vector3(W + 0.2f, 0.3f, 0.36f), woodDark);
            // The agency's sign in the front gable (a globe and a plane, painted graybox)
            var signY = wallTop + rise * 0.38f;
            Box(c, "AgencySign", new Vector3(0, signY, 0.22f), new Vector3(3.4f, 1.1f, 0.12f), woodLight).name = "AgencySign";
            Box(c, "SignGlobe", new Vector3(-0.75f, signY, 0.3f), new Vector3(0.7f, 0.7f, 0.04f), GrayboxMaterials.Get("Paper", new Color(0.93f, 0.9f, 0.8f)));
            Box(c, "SignPlane", new Vector3(0.8f, signY, 0.3f), new Vector3(0.95f, 0.2f, 0.04f), GrayboxMaterials.Get("Paper", new Color(0.93f, 0.9f, 0.8f)), Quaternion.Euler(0, 0, 20f));
            // The upgrades board on the porch's side
            var up = Box(c, "QuadroDeMelhorias", new Vector3(W / 2 + 1.2f, F + 1.1f, P - 1f), new Vector3(1.4f, 1.1f, 0.1f), GrayboxMaterials.Get("VanBody", new Color(0.36f, 0.55f, 0.62f)), Quaternion.Euler(0, 90f, 0));
            Marker(root, WorldMarker.Kind.UpgradeBoard, c.TransformPoint(new Vector3(W / 2 + 2.2f, 0, P - 1f)), 0f);
            _ = up;
        }

        /// <summary>The orders board as the reference's map board: two posts, a roof plank, the valley map with a red pin.</summary>
        static void MapBoard(Transform root)
        {
            var b = new GameObject("QuadroDePedidos").transform;
            b.SetParent(set, false);
            var at = new Vector3(-3.4f, 0f, -333.5f);
            b.SetPositionAndRotation(new Vector3(at.x, G(at.x, at.z), at.z), Quaternion.Euler(0f, 12f, 0f));
            b.localScale = Vector3.one * 1.35f;
            Solid(b, "PostL", new Vector3(-1.4f, 1.1f, 0), new Vector3(0.2f, 2.2f, 0.2f), wood);
            Solid(b, "PostR", new Vector3(1.4f, 1.1f, 0), new Vector3(0.2f, 2.2f, 0.2f), wood);
            Box(b, "Frame", new Vector3(0, 1.45f, 0), new Vector3(2.9f, 1.5f, 0.12f), woodDark);
            Box(b, "Map", new Vector3(0, 1.45f, 0.07f), new Vector3(2.6f, 1.25f, 0.02f), GrayboxMaterials.Get("MapGreen", new Color(0.55f, 0.75f, 0.45f)));
            Box(b, "MapRiver", new Vector3(-0.1f, 1.45f, 0.09f), new Vector3(0.2f, 1.2f, 0.02f), GrayboxMaterials.Get("MapBlue", new Color(0.32f, 0.58f, 0.85f)), Quaternion.Euler(0, 0, 30f));
            Box(b, "Pin", new Vector3(0.6f, 1.6f, 0.11f), new Vector3(0.14f, 0.2f, 0.04f), GrayboxMaterials.Get("Pin", new Color(0.85f, 0.2f, 0.18f)));
            Box(b, "Top", new Vector3(0, 2.3f, 0), new Vector3(3.2f, 0.14f, 0.4f), woodLight);
            Marker(root, WorldMarker.Kind.JobBoard, b.TransformPoint(new Vector3(0, 0, 1.6f)), 0f);
        }

        static void SignPost(Vector3 at, float yaw)
        {
            var s = new GameObject("TrailSign").transform;
            s.SetParent(set, false);
            s.SetPositionAndRotation(new Vector3(at.x, G(at.x, at.z), at.z), Quaternion.Euler(0f, yaw, 0f));
            Solid(s, "Post", new Vector3(0, 1.25f, 0), new Vector3(0.2f, 2.5f, 0.2f), woodDark);
            for (int k = 0; k < 3; k++)
            {
                float dir = k == 1 ? -1f : 1f;
                Box(s, "Board", new Vector3(dir * 0.5f, 2.05f - k * 0.45f, 0.12f), new Vector3(1.0f, 0.34f, 0.07f), wood);
                Box(s, "Tip", new Vector3(dir * 1.05f, 2.05f - k * 0.45f, 0.12f), new Vector3(0.24f, 0.24f, 0.07f), wood, Quaternion.Euler(0, 0, 45f));
                Box(s, "Icon", new Vector3(dir * 0.45f, 2.05f - k * 0.45f, 0.165f), new Vector3(0.22f, 0.18f, 0.02f), GrayboxMaterials.Get("Paper", new Color(0.93f, 0.9f, 0.8f)), Quaternion.Euler(0, 0, 45f));
            }
        }

        static void Fence(Vector3 a, Vector3 b)
        {
            var dir = b - a; dir.y = 0f;
            int n = Mathf.Max(1, Mathf.RoundToInt(dir.magnitude / 2.2f));
            var rot = Quaternion.LookRotation(dir);
            for (int k = 0; k <= n; k++)
            {
                var p = Vector3.Lerp(a, b, k / (float)n); p.y = G(p.x, p.z);
                var post = Box(set, "FencePost", p + Vector3.up * 0.6f, new Vector3(0.18f, 1.2f, 0.18f), woodDark);
                post.rotation = rot;
                if (k == n) continue;
                var q = Vector3.Lerp(a, b, (k + 0.5f) / n); q.y = G(q.x, q.z);
                float len = dir.magnitude / n + 0.1f;
                Box(set, "FenceRail", q + Vector3.up * 0.95f, new Vector3(0.1f, 0.12f, len), wood).rotation = rot;
                Box(set, "FenceRail", q + Vector3.up * 0.5f, new Vector3(0.1f, 0.12f, len), wood).rotation = rot;
            }
        }

        static void Lantern(Vector3 at)
        {
            float y = G(at.x, at.z);
            var l = new GameObject("LanternPost").transform;
            l.SetParent(set, false);
            l.position = new Vector3(at.x, y, at.z);
            Solid(l, "Post", new Vector3(0, 1.4f, 0), new Vector3(0.2f, 2.8f, 0.2f), woodDark);
            Box(l, "Arm", new Vector3(0.4f, 2.65f, 0), new Vector3(0.85f, 0.14f, 0.14f), woodDark);
            Box(l, "Cap", new Vector3(0.72f, 2.38f, 0), new Vector3(0.36f, 0.08f, 0.36f), woodDark);
            Box(l, "Lantern", new Vector3(0.72f, 2.14f, 0), new Vector3(0.28f, 0.38f, 0.28f), glow);
            PointLight(l, new Vector3(0.72f, 2.0f, 0.1f), 6.5f, 1.5f);
        }

        /// <summary>The reference's big grey rocks: a cluster on the left of the trail and a few along the way.</summary>
        static void BigRocks()
        {
            var a = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/Rock/PedraCinzaA.fbx");
            var b = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/Rock/PedraCinzaB.fbx");
            var spots = new (Vector3 p, float s, bool big, float yaw)[]
            {
                (new Vector3(6.2f, 0, -314f), 1.35f, true, 20f), (new Vector3(13f, 0, -320f), 1.0f, true, 140f), (new Vector3(10.5f, 0, -327.5f), 0.7f, false, 70f),
                (new Vector3(15.5f, 0, -329f), 0.9f, true, 300f), (new Vector3(5.5f, 0, -315f), 0.45f, false, 10f), (new Vector3(-4.5f, 0, -307f), 0.55f, false, 200f),
                (new Vector3(5f, 0, -341f), 1.1f, true, 80f), (new Vector3(-6f, 0, -349f), 0.8f, false, 30f), (new Vector3(9f, 0, -352f), 1.3f, true, 160f),
                (new Vector3(4.6f, 0, -322.5f), 0.75f, false, 40f), (new Vector3(-3.6f, 0, -320.5f), 0.55f, false, 190f), (new Vector3(5.8f, 0, -335f), 0.85f, false, 120f),
                (new Vector3(-0.2f, 0, -326.5f), 0.45f, false, 60f), (new Vector3(11.5f, 0, -341f), 1.25f, true, 210f), (new Vector3(16f, 0, -325f), 0.7f, false, 15f),
                (new Vector3(-6f, 0, -315.5f), 0.5f, false, 100f), (new Vector3(3.5f, 0, -312f), 0.4f, false, 300f),
                (new Vector3(-9f, 0, -316f), 0.6f, false, 250f), (new Vector3(18f, 0, -314f), 0.8f, false, 330f), (new Vector3(-2.5f, 0, -357f), 0.9f, true, 15f),
            };
            foreach (var (p, s, big, yaw) in spots)
            {
                var r = (GameObject)PrefabUtility.InstantiatePrefab(big ? a : b, set);
                float size = (big ? s : Mathf.Max(0.7f, s * 1.5f)) * 1.6f; // (well above the knee-high grass, as in the reference)
                r.transform.SetPositionAndRotation(new Vector3(p.x, G(p.x, p.z) - 0.08f * size, p.z), Quaternion.Euler(0f, yaw, 0f));
                r.transform.localScale = Vector3.one * size;
            }
        }

        /// <summary>Small stones lying on the trails (the reference's pebbles), no colliders.</summary>
        static void Pebbles(GroundPaths paths)
        {
            var root = new GameObject("Pebbles").transform;
            root.SetParent(set, false);
            var pebble = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/Rock/PedraCinzaB.fbx");
            var rng = new System.Random(19);
            float R() => (float)rng.NextDouble();
            int n = 0;
            foreach (var path in paths.paths)
                for (int i = 0; i + 1 < path.points.Length; i++)
                {
                    var a = path.points[i]; var b = path.points[i + 1];
                    int count = Mathf.RoundToInt(Vector3.Distance(a, b) * 0.9f);
                    var side = Vector3.Cross(Vector3.up, (b - a).normalized);
                    for (int k = 0; k < count; k++)
                    {
                        var p = Vector3.Lerp(a, b, R()) + side * (R() - 0.5f) * path.width * 0.9f;
                        float s = Mathf.Lerp(0.13f, 0.3f, R() * R());
                        var peb = (GameObject)PrefabUtility.InstantiatePrefab(pebble, root);
                        peb.transform.SetPositionAndRotation(new Vector3(p.x, G(p.x, p.z) - s * 0.15f, p.z), Quaternion.Euler(0f, R() * 360f, 0f));
                        peb.transform.localScale = new Vector3(s, s * 0.7f, s);
                        foreach (var col in peb.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
                        foreach (var lg in peb.GetComponentsInChildren<LODGroup>()) { lg.ForceLOD(0); lg.enabled = false; } // (too small to switch)
                        n++;
                    }
                }
            Debug.Log($"[Campanha] Agency set: {n} pebbles");
        }

        static Transform Marker(Transform root, WorldMarker.Kind kind, Vector3 at, float yaw, float lift = 0f)
        {
            var go = new GameObject($"Marker_{kind}");
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(new Vector3(at.x, G(at.x, at.z) + lift, at.z), Quaternion.Euler(0f, yaw, 0f));
            go.AddComponent<WorldMarker>().kind = kind;
            return go.transform;
        }
    }
}
