using CampanhaRio.Dev;
using CampanhaRio.Net;
using UnityEngine;

namespace CampanhaRio.Campaign
{
    /// <summary>
    /// The first thing in Core (the persistent scene: network, players, van, audio, UI, save). It opens the session:
    ///   - solo (no switches): a private host on this machine, so solo and group play are the same code;
    ///   - -cc-host: a host friends can join;  -cc-join [ip]: join one.
    /// The host opens the campaign save and starts streaming from its checkpoint.
    /// (-cc-scene opens a dev scene instead, e.g. KayakTest or LookDev.)
    /// </summary>
    public class CoreBootstrap : MonoBehaviour
    {
        public SegmentStreamer streamer;

        void Start()
        {
            if (!string.IsNullOrEmpty(TestSwitches.Scene)) return;
            Application.runInBackground = true; // test instances side by side keep running
            TestSwitches.ApplyPlayerOverrides();
            var session = NetSession.Ensure();
            if (!session) return;
            NetSession.Ended += message => Debug.Log("[Campanha] Session ended: " + message);

            if (TestSwitches.Join)
            {
                session.JoinDirect(TestSwitches.JoinAddress);
                return;
            }
            var campaign = CampaignState.Open();
            if (session.HostDirect()) streamer.Begin(""); // the campaign is hub-based: every session starts at the agency (order[0])
        }

        void OnDestroy() => CampaignState.Close();
    }
}
