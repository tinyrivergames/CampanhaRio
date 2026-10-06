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

            // ---- jobs (JobRun / JobBoard)
            { "Sunset in {0}", "Pôr do sol em {0}" },
            { "Delivered!", "Entregue!" },
            { "Before sunset", "Antes do pôr do sol" },
            { "Happy passenger", "Passageiro feliz" },
            { "Cargo in one piece", "Carga inteira" },
            { "Within {0}", "Em menos de {0}" },
            { "+{0} coins, +{1} reputation", "+{0} moedas, +{1} de reputação" },
            { "Orders board", "Quadro de pedidos" },
            { "{0} coins · reputation {1}", "{0} moedas · reputação {1}" },
            { "{0} · pays {1}", "{0} · paga {1}" },
            { "New!", "Novo!" },
            { "Accept", "Aceitar" },
            { "Close", "Fechar" },
            { "Rio Moinho", "Rio do Moinho" },
            { "Rio Teste", "Rio de teste" },
            { "The urgent letter", "A carta urgente" },
            { "The mayor of Vila do Moinho", "O prefeito da Vila do Moinho" },
            { "The village is cut off since the storm: this letter must get there before sunset. Keep it dry!", "A vila está isolada desde a tempestade: esta carta precisa chegar antes do pôr do sol. Não deixe molhar!" },
            { "The scared goat", "O bode medroso" },
            { "Dona Cabra, the cheese maker", "Dona Cabra, a queijeira" },
            { "Her goat must get to the fair downstream. He is scared of speed and jumps: if he panics, he jumps out!", "O bode dela precisa chegar à feira rio abaixo. Ele morre de medo de velocidade e de pulos: se entrar em pânico, pula do barco!" },
            { "Letter", "Carta" },
            { "dry", "seca" },
            { "a bit damp", "um pouco úmida" },
            { "wet", "molhada" },
            { "soaked", "encharcada" },
            { "Goat", "Bode" },
            { "Fear", "Medo" },
            { "calm", "calmo" },
            { "nervous", "nervoso" },
            { "scared", "assustado" },
            { "about to jump!", "quase pulando!" },
            { "in the water! Fetch him!", "na água! Vão buscar!" },

            // ---- the slice's loop (AgencyFlow)
            { "Pick an order at the board (E)", "Escolha um pedido no quadro (E)" },
            { "The host picks the order at the board", "Quem criou a sala escolhe o pedido no quadro" },
            { "Everyone to the van! (E next to it)", "Todos para a van! (E perto dela)" },
            { "Seu Alce drives you down to the river", "O Seu Alce leva vocês até o rio" },
            { "Back to the agency: everyone to the van (E)", "De volta à agência: todos para a van (E)" },
        };
    }
}
