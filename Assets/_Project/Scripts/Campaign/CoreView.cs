using CampanhaRio.Net;
using UnityEngine;

namespace CampanhaRio.Campaign
{
    /// <summary>
    /// Core's camera and a small debug panel (graybox: no game UI yet). The camera follows the local player's body
    /// from behind and above; the panel shows the session, the loaded segments and the checkpoint.
    /// </summary>
    public class CoreView : MonoBehaviour
    {
        public Vector3 offset = new Vector3(0f, 6f, -10f);
        public float smoothing = 6f;

        void LateUpdate()
        {
            var target = NetworkPlayer.Local;
            if (!target) return;
            Vector3 want = target.transform.position + offset;
            transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation(target.transform.position + Vector3.up - transform.position);
        }

        void OnGUI()
        {
            var session = NetSession.Instance;
            string net = session ? $"{(session.IsHost ? "host" : "cliente")} - {Core.Loc.T(session.Status)}" : "sem rede";
            string segs = SegmentStreamer.Instance ? string.Join(", ", SegmentStreamer.Instance.LoadedSegments) : "-";
            string cp = CampaignState.Current != null ? CampaignState.Current.Save.checkpoint : "(do host)";
            GUI.Label(new Rect(12, 8, 900, 22), $"CampanhaRio (graybox)   rede: {net}   jogadores: {NetworkPlayer.All.Count}");
            GUI.Label(new Rect(12, 28, 900, 22), $"trechos carregados: {segs}   checkpoint: {cp}   WASD anda, Shift corre");
        }
    }
}
