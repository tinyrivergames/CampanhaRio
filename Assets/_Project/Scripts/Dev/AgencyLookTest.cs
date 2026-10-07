using System.Collections;
using System.IO;
using CampanhaRio.Campaign;
using UnityEngine;

namespace CampanhaRio.Dev
{
    /// <summary>
    /// -cc-script agencylook [-cc-savedir dir]: pictures of the agency (the player's view, from above, down a trail),
    /// with the grass and frame numbers. Logs "[Test] agencylook ..." and quits.
    /// </summary>
    public class AgencyLookTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (TestSwitches.Script != "agencylook") return;
            DontDestroyOnLoad(new GameObject("Agency Look").AddComponent<AgencyLookTest>().gameObject);
        }

        IEnumerator Start()
        {
            while (!AgencyFlow.Instance || !AgencyFlow.Instance.IsSpawned || AgencyFlow.Instance.Current != AgencyFlow.Phase.Hub || !NetworkPlayer.Local) yield return null;
            yield return new WaitForSeconds(4f); // the grass cells around
            Shot("agencia_jogador");
            yield return new WaitForSeconds(0.5f);
            var cam = Camera.main;
            AgencyFlow.CameraOverride = true;
            foreach (var mb in cam.GetComponents<MonoBehaviour>()) mb.enabled = false;
            foreach (var (name, eye0, at0) in new[]
            {
                ("agencia_cima", new Vector3(30f, 70f, -270f), new Vector3(-12f, 37f, -335f)),
                ("agencia_trilha", new Vector3(-48f, 0f, -318f), new Vector3(-66f, 0f, -342f)),
                ("agencia_mata", new Vector3(26f, 0f, -340f), new Vector3(48f, 0f, -352f)),
            })
            {
                Vector3 eye = eye0, at = at0;
                if (eye.y == 0f) { eye.y = Ground(eye) + 1.8f; at.y = Ground(at) + 1.4f; } // on the trail, at eye height
                cam.transform.position = eye;
                cam.transform.LookAt(at);
                yield return new WaitForSeconds(2.5f); // the grass cells near the new view
                Shot(name);
                yield return new WaitForSeconds(0.5f);
            }
            var grass = FindAnyObjectByType<World.GrassField>();
            Debug.Log($"[Test] agencylook: grass {(grass ? grass.Generated : 0)} generated, {(grass ? grass.DrawnLastFrame : 0)} drawn; {1f / Time.smoothDeltaTime:0} fps");
            Application.Quit();
        }

        static float Ground(Vector3 p) => Physics.Raycast(new Vector3(p.x, 200f, p.z), Vector3.down, out var hit, 400f, ~0, QueryTriggerInteraction.Ignore) && hit.collider is TerrainCollider ? hit.point.y : 37f;

        static void Shot(string name)
        {
            string path = Path.Combine(SaveSystem.Root, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[Test] agencylook: {path} ({1f / Time.smoothDeltaTime:0} fps)");
        }
    }
}
