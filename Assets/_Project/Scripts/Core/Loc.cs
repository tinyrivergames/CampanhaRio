using System.Collections.Generic;
using UnityEngine;

namespace CampanhaRio.Core
{
    /// <summary>
    /// The game's text in two languages (ported from CayaCozy): Português (BR), the default because the players are Brazilian, and
    /// English. One table, no package. The English text is the key, so code stays readable and a missing entry just shows
    /// the English. Formats use {0}, {1}… (Loc.F). Settings > Language saves the choice.
    /// </summary>
    public static class Loc
    {
        public enum Lang { PtBR, En }

        static Lang? current;
        public static Lang Current
        {
            get { if (!current.HasValue) current = PlayerPrefs.GetString("cr.lang", "pt") == "en" ? Lang.En : Lang.PtBR; return current.Value; }
            set { current = value; PlayerPrefs.SetString("cr.lang", value == Lang.En ? "en" : "pt"); Changed?.Invoke(); }
        }

        public static event System.Action Changed;

        public static string T(string english)
        {
            if (string.IsNullOrEmpty(english) || Current == Lang.En) return english;
            return Pt.TryGetValue(english, out var pt) ? pt : english;
        }

        public static string F(string english, params object[] args) => string.Format(T(english), args);

        static readonly Dictionary<string, string> Pt = new Dictionary<string, string>
        {
            // ---- network (NetSession / NetOnline)
            { "Opening the room…", "Abrindo a sala…" },
            { "Room open on {0}:{1}", "Sala aberta em {0}:{1}" },
            { "Connecting to {0}…", "Conectando a {0}…" },
            { "Connected", "Conectado" },
            { "Couldn't open the room: is the port already in use (another host on this PC)?", "Não deu para abrir a sala: a porta já está em uso (outra sala neste PC)?" },
            { "Couldn't start the connection.", "Não deu para iniciar a conexão." },
            { "\"{0}\" isn't an IP address (like 192.168.0.12 or 100.64.1.5).", "\"{0}\" não é um endereço IP (como 192.168.0.12 ou 100.64.1.5)." },
            { "Couldn't reach the host at {0} (timed out). Check the address and the host's firewall.", "Não deu para alcançar a sala em {0} (tempo esgotado). Confira o endereço e o firewall de quem criou a sala." },
            { "Version mismatch: the host runs {0} (build {1}), you run {2} (build {3}). Update both to the same build.", "Versões diferentes: a sala roda {0} (build {1}), você roda {2} (build {3}). Atualizem os dois para a mesma build." },
            { "The room is full ({0}/{1}).", "A sala está cheia ({0}/{1})." },
            { "The host closed the room.", "A sala foi fechada." },
            { "Couldn't connect to the host. Check the address and the host's firewall.", "Não deu para conectar. Confira o endereço e o firewall de quem criou a sala." },
            { "The connection failed (network error).", "A conexão caiu (erro de rede)." },
            { "Connecting to Unity's online services…", "Conectando aos serviços online da Unity…" },
        };
    }
}
