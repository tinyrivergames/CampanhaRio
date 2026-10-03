using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace CampanhaRio.Net
{
    /// <summary>
    /// Online rooms with a join code (Stage 9): Unity Multiplayer Services **Sessions** (Relay + Lobby). Gated: it needs the
    /// project linked to a Unity Cloud project (Edit > Project Settings > Services). Until then <see cref="Available"/> is
    /// false, the menu explains why, and direct IP (or a virtual LAN) keeps working.
    /// With WithRelayNetwork() the Sessions API starts Netcode's host / client itself; NetSession's callbacks do the rest.
    /// </summary>
    public static class NetOnline
    {
        public const string SetupHint = "Online rooms need the project linked to Unity Cloud (Relay + Lobby). Direct IP works without it.";

        /// <summary>Is the project linked to a cloud project? (Nothing else can be checked before initializing.)</summary>
        public static bool Available => !string.IsNullOrEmpty(Application.cloudProjectId);

        static ISession current;

        static async Task<string> SignIn()
        {
            if (!Available) return SetupHint;
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized) await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Campanha] Online: " + e);
                return "Couldn't reach Unity's online services (" + Short(e) + "). Check the internet connection.";
            }
        }

        /// <summary>Opens an online room and returns (code, error).</summary>
        public static async Task<(string code, string error)> Host(NetSession session)
        {
            string error = await SignIn();
            if (error != null) return (null, error);
            try
            {
                session.PrepareOnline();
                var options = new SessionOptions { MaxPlayers = session.Config.maxPlayers, IsPrivate = true }.WithRelayNetwork();
                var host = await MultiplayerService.Instance.CreateSessionAsync(options);
                current = host;
                session.OnlineStarted(true, host.Code);
                return (host.Code, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Campanha] Online host: " + e);
                return (null, "Couldn't open an online room (" + Short(e) + ").");
            }
        }

        public static async Task<string> Join(NetSession session, string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length < 4) return "Type the room code your friend sees (like K7QX2P).";
            string error = await SignIn();
            if (error != null) return error;
            try
            {
                session.PrepareOnline();
                current = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                session.OnlineStarted(false, code);
                return null;
            }
            catch (SessionException e)
            {
                Debug.LogWarning("[Campanha] Online join: " + e);
                return e.Error == SessionError.SessionNotFound
                    ? $"No room with the code {code}. Check it with your friend (codes expire when the room closes)."
                    : "Couldn't join (" + Short(e) + ").";
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Campanha] Online join: " + e);
                return "Couldn't join (" + Short(e) + ").";
            }
        }

        public static void LeaveSession()
        {
            var s = current;
            current = null;
            if (s == null) return;
            try { _ = s.LeaveAsync(); } catch (Exception) { }
        }

        static string Short(Exception e) => e.GetBaseException().Message.Split('\n')[0];
    }
}
