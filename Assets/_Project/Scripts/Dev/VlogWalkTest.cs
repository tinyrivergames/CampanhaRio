using System.Collections;
using System.IO;
using CampanhaRio.Campaign;
using UnityEngine;

namespace CampanhaRio.Dev
{
    /// <summary>
    /// -cc-script vlogwalk [-cc-savedir dir]: the dev vlog's clip. The player walks the agency's trails (from the van,
    /// past the junction, along the right trail into the woods) with the camera following behind, and every frame is
    /// saved (frames/f_0000.jpg, at a fixed 30 fps whatever the machine does, without the HUD): ffmpeg turns them into a
    /// video. Logs "[Test] vlogwalk ..." and quits.
    /// </summary>
    public class VlogWalkTest : MonoBehaviour
    {
        const int Fps = 30;
        const float Speed = 4.2f;

        static readonly Vector3[] Route =
        {
            new Vector3(2f, 0f, -309f), new Vector3(0f, 0f, -318f), new Vector3(9f, 0f, -321.5f),
            new Vector3(20f, 0f, -318f), new Vector3(34f, 0f, -326f), new Vector3(46f, 0f, -323f),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (TestSwitches.Script != "vlogwalk") return;
            DontDestroyOnLoad(new GameObject("Vlog Walk").AddComponent<VlogWalkTest>().gameObject);
        }

        IEnumerator Start()
        {
            while (!AgencyFlow.Instance || !AgencyFlow.Instance.IsSpawned || AgencyFlow.Instance.Current != AgencyFlow.Phase.Hub || !NetworkPlayer.Local) yield return null;
            var player = NetworkPlayer.Local;
            var view = FindAnyObjectByType<CoreView>();
            var cam = Camera.main;
            player.transform.position = Route[0] + Vector3.up * 40f; // (it falls onto the ground)
            if (view) view.CurrentYaw = Yaw(Route[1] - Route[0]);
            yield return new WaitForSeconds(4f); // the grass cells around

            string dir = Path.Combine(SaveSystem.Root, "frames");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            var rt = new RenderTexture(Screen.width, Screen.height, 24);
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            cam.targetTexture = rt; // (renders without the HUD)
            Time.captureFramerate = Fps;

            int frame = 0, leg = 1;
            float hold = 1.2f; // a still moment first, and at the end
            while (true)
            {
                Vector3 here = player.transform.position;
                if (hold > 0f) { hold -= Time.deltaTime; NetworkPlayer.ScriptedMove = Vector3.zero; }
                else if (leg < Route.Length)
                {
                    Vector3 to = Route[leg] - here; to.y = 0f;
                    if (to.magnitude < 1.2f) { leg++; if (leg == Route.Length) hold = 1.5f; }
                    NetworkPlayer.ScriptedMove = to.normalized * Speed;
                }
                else break;
                if (view && NetworkPlayer.ScriptedMove.sqrMagnitude > 0f) // the camera swings in behind, slowly
                    view.CurrentYaw = Mathf.LerpAngle(view.CurrentYaw, Yaw(NetworkPlayer.ScriptedMove), 1f - Mathf.Exp(-1.6f * Time.deltaTime));

                yield return new WaitForEndOfFrame();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(dir, $"f_{frame++:0000}.jpg"), tex.EncodeToJPG(92));
            }
            NetworkPlayer.ScriptedMove = Vector3.zero;
            Time.captureFramerate = 0;
            cam.targetTexture = null;
            Debug.Log($"[Test] vlogwalk: {frame} frames ({frame / (float)Fps:0.0} s) in {dir}");
            Application.Quit();
        }

        static float Yaw(Vector3 d) => Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }
}
