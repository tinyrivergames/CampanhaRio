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
            { "Upgrades", "Melhorias" },
            { "Room", "Sala" },
            { "Friends join with this address: {0}:{1}", "Os amigos entram com este endereço: {0}:{1}" },
            { "Players: {0}", "Jogadores: {0}" },
            { "Join a friend's room (their address):", "Entrar na sala de um amigo (o endereço dele):" },
            { "Join", "Entrar" },
            { "Online rooms with a code come later (they need the Unity Cloud link). For now: the same network, or a virtual LAN like Radmin VPN.", "Salas online com código vêm depois (precisam da Unity Cloud). Por enquanto: a mesma rede, ou uma rede virtual como o Radmin VPN." },
            { "The valley", "O vale" },
            { "(M closes the map)", "(M fecha o mapa)" },
            { "(coming soon)", "(em breve)" },
            { "New on the map: {0}", "Novo no mapa: {0}" },
            { "Grandma Nina's agency", "A agência da Vó Nina" },
            { "The valley road", "A estrada do vale" },
            { "Rio do Moinho", "Rio do Moinho" },
            { "Vila do Moinho", "Vila do Moinho" },
            { "Rio das Pedras", "Rio das Pedras" },
            { "Vila das Pedras", "Vila das Pedras" },
            { "Buy ({0})", "Comprar ({0})" },
            { "Owned", "Comprado" },
            { "Needs reputation {0}", "Precisa de reputação {0}" },
            { "{0} coins", "{0} moedas" },
            { "The host buys the upgrades", "Quem criou a sala compra as melhorias" },
            { "Roof rack for the van", "Bagageiro na van" },
            { "Seu Alce drives a little faster with everything strapped on top.", "Com tudo amarrado em cima, o Seu Alce dirige um pouco mais rápido." },
            { "A bigger orders board", "Um quadro de pedidos maior" },
            { "Room for one more order on the board.", "Cabe mais um pedido no quadro." },
            { "A new sign for the agency", "Uma placa nova para a agência" },
            { "Grandma Nina's old sign, repainted. The village notices.", "A placa velha da Vó Nina, repintada. A vila repara." },
            { "I took the shortcut. The shortcut took longer.", "Peguei o atalho. O atalho demorou mais." },
            { "The van and I are the same age. She aged better.", "A van e eu temos a mesma idade. Ela envelheceu melhor." },
            { "I waved at a bear on the road. He waved back. I think.", "Acenei para um urso na estrada. Ele acenou de volta. Acho." },
            { "I parked on the first try. Don't ask about the other tries.", "Estacionei de primeira. Não perguntem das outras vezes." },
            { "Seu Alce never gets lost. The road does.", "O Seu Alce nunca se perde. A estrada é que se perde." },
            { "I brought snacks. I ate the snacks. It was a long road.", "Trouxe lanche. Comi o lanche. A estrada era longa." },
            { "Pick an order at the board (E)", "Escolha um pedido no quadro (E)" },
            { "The host picks the order at the board", "Quem criou a sala escolhe o pedido no quadro" },
            { "Everyone to the van! (E next to it)", "Todos para a van! (E perto dela)" },
            { "Seu Alce drives you down to the river", "O Seu Alce leva vocês até o rio" },
            { "Back to the agency: everyone to the van (E)", "De volta à agência: todos para a van (E)" },
        };
    }
}
