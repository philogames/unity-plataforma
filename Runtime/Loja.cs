using System;
using System.Collections.Generic;
using UnityEngine;

namespace PhiloGames.Plataforma
{
    /// <summary>
    /// A porta única entre o jogo e a loja. <b>O jogo fala só com isto</b> — nunca com a Steam.
    ///
    /// <b>O desenho em uma frase:</b> quem <i>escreve</i> é a loja (<see cref="ILoja"/>), quem
    /// <i>responde</i> é o espelho nosso (<see cref="EspelhoDeConquistas"/>). A separação não é gosto:
    /// a API de conquistas da Unity 6.3 (Platform Toolkit) não lê estado, e o Switch não tem estado de
    /// sistema para ler. Fazer a leitura depender da loja foi o que deixou a tela de conquistas do
    /// Stone Cutter abrir vazia no editor durante anos.
    ///
    /// <b>Ninguém escolhe a implementação — ela se apresenta</b> (<see cref="Registrar"/>). A fachada
    /// não conhece a Steam, e é por isso que ela compila onde o assembly da Steam nem existe. Sem
    /// nenhuma registrada, a <see cref="LojaNenhuma"/> assume e o jogo roda inteiro.
    ///
    /// Saiu do <c>SC_Plataforma</c> do Stone Cutter em 06/10/2026, para servir a qualquer jogo.
    /// </summary>
    public static class Loja
    {
        static ILoja registrada;
        static readonly LojaNenhuma nenhuma = new LojaNenhuma();

        // Os ids de conquista que o jogo conhece. Não vêm daqui: quem os tem é o catálogo do jogo —
        // ver RegistrarCatalogo.
        static string[] catalogo = new string[0];

        // Já reconciliamos nesta sessão? A loja pode avisar mais de uma vez (troca de conta, stats
        // recebidas de novo); repetir é inofensivo, mas são chamadas nativas por conquista.
        static bool reconciliado;

        // ---------------------------------------------------------------- registro

        /// <summary>
        /// Chamado pela implementação de loja no boot — nunca pelo jogo.
        ///
        /// A última a se registrar ganha, e o log diz quem é. Em build só existe uma candidata; no
        /// editor, ver o nome no console evita a hora perdida testando contra a loja errada.
        /// </summary>
        public static void Registrar(ILoja loja)
        {
            if (loja == null) return;
            if (registrada != null && registrada != loja)
                Debug.LogWarning("[Plataforma] '" + loja.GetType().Name + "' substituiu '" +
                                 registrada.GetType().Name + "'. Em build isso não deveria acontecer.");
            registrada = loja;
            Debug.Log("[Plataforma] " + loja.GetType().Name + " no comando (pode escrever: " +
                      loja.PodeEscrever + ").");
        }

        /// <summary>
        /// Diz à fachada quais são as conquistas do jogo. <b>Sem isto a reconciliação não acontece</b>
        /// — ela precisa de uma lista de ids para perguntar à loja. Jogo sem conquista (uma demo) não
        /// chama, e nada de conquista acontece: nem leitura, nem arquivo.
        /// </summary>
        public static void RegistrarCatalogo(IEnumerable<string> ids)
        {
            if (ids == null) return;
            var lista = new List<string>();
            foreach (string id in ids)
                if (!string.IsNullOrEmpty(id)) lista.Add(id);
            catalogo = lista.ToArray();
            Debug.Log("[Plataforma] catálogo com " + catalogo.Length + " conquista(s).");

            // A loja pode ter ficado pronta antes do catálogo chegar (ordem de boot não é garantida).
            if (!reconciliado && Atual.SabeResponder) ReconciliarAgora();
        }

        /// <summary>A loja ativa, ou a de faz-de-conta. <b>Nunca é null</b>, de propósito.</summary>
        public static ILoja Atual
        {
            get { return registrada != null ? registrada : (ILoja)nenhuma; }
        }

        /// <summary>Há loja, e ela aceita escrita agora?</summary>
        public static bool PodeEscrever
        {
            get
            {
                try { return Atual.PodeEscrever; }
                catch (Exception) { return false; }
            }
        }

        // ---------------------------------------------------------------- página da loja

        /// <summary>
        /// Mostra a página de um produto — na Steam, no overlay, por cima do jogo. Se a loja não
        /// consegue (fora do ar, overlay desligado, editor), abre o <paramref name="enderecoReserva"/>
        /// pelo sistema (<c>Application.OpenURL</c>): um <c>steam://store/&lt;app&gt;</c> leva ao
        /// cliente da Steam; um <c>https://</c>, ao navegador.
        ///
        /// <b>Devolve true quando abriu no overlay</b>. No caminho reserva, a troca de janela pode
        /// demorar e o jogador fica sem retorno: é o caso em que vale o jogo mostrar um aviso.
        /// </summary>
        public static bool AbrirPagina(ulong idDoProduto, string enderecoReserva)
        {
            try
            {
                if (idDoProduto != 0 && Atual.AbrirPagina(idDoProduto)) return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Plataforma] a loja não abriu a página " + idDoProduto + ": " + e.Message +
                                 ". Indo pelo endereço reserva.");
            }
            if (!string.IsNullOrEmpty(enderecoReserva)) Application.OpenURL(enderecoReserva);
            return false;
        }

        // ---------------------------------------------------------------- conquistas: perguntas

        /// <summary>
        /// O jogador tem esta conquista? <b>Responde sempre</b> — no editor, sem loja, com a Steam
        /// fechada. É o espelho que responde, não a loja.
        /// </summary>
        public static bool Ganhou(string id)
        {
            return EspelhoDeConquistas.Tem(id);
        }

        /// <summary>Quantas destas o jogador tem (o "X" de um "X/Y").</summary>
        public static int Quantas(IEnumerable<string> ids)
        {
            return EspelhoDeConquistas.Quantas(ids);
        }

        // ---------------------------------------------------------------- conquistas: escrita

        /// <summary>
        /// Concede a conquista. <b>Devolve true só na primeira vez</b> — é esse retorno que um popup
        /// usa para não reaparecer numa conquista velha.
        ///
        /// <b>O espelho é marcado mesmo sem loja</b>, e é essa decisão que faz a coisa toda funcionar:
        /// quem joga no editor ou numa build sem loja continua colecionando. A loja, quando existe,
        /// recebe o mesmo desbloqueio logo depois — e se ela estiver fora do ar, o jogo não trava nem
        /// perde o feito: a próxima reconciliação empurra o que ficou para trás.
        /// </summary>
        public static bool Desbloquear(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning("[Plataforma] pediram desbloqueio com id vazio — nada foi feito.");
                return false;
            }

            bool primeiraVez = EspelhoDeConquistas.Marcar(id);

            // Mesmo quando já estava no espelho, vale insistir com a loja: o caso real é o espelho ter
            // sido escrito offline e a loja nunca ter sabido. A loja ignora repetição.
            if (PodeEscrever) EscreverNaLoja(id);

            return primeiraVez;
        }

        static void EscreverNaLoja(string id)
        {
            try
            {
                Atual.Desbloquear(id);
            }
            catch (Exception e)
            {
                // Uma conquista NUNCA pode derrubar o jogo. No Stone Cutter (2026-06), a exceção subiu
                // pela ActionList do Adventure Creator e parou a cutscene.
                Debug.LogWarning("[Plataforma] a loja recusou '" + id + "': " + e.Message +
                                 ". O espelho já anotou; o jogo segue.");
            }
        }

        /// <summary>
        /// A arte da loja, quando o jogo não tem a sua. Devolve false quando não há de onde pedir — e
        /// aí o chamador põe a arte local, que é o caminho normal.
        /// </summary>
        public static bool PedirIcone(string id, Action<Texture2D> quandoVier)
        {
            if (string.IsNullOrEmpty(id) || quandoVier == null) return false;
            try
            {
                return Atual.PedirIcone(id, quandoVier);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Plataforma] não consegui pedir o ícone de '" + id + "': " + e.Message);
                return false;
            }
        }

        // ---------------------------------------------------------------- reconciliação

        /// <summary>
        /// Acerta espelho e loja, <b>nos dois sentidos</b>. Chamado pela implementação de loja quando
        /// ela fica pronta — nunca por frame, nunca por tempo.
        ///
        /// 🔑 <b>Por que é a loja quem chama:</b> na Steam as estatísticas chegam assíncronas, num
        /// callback. Perguntar antes dele responde "não tem" para todas, e o jogador veria a coleção
        /// inteira sumir. Só a loja sabe a hora.
        ///
        /// <b>Puxar</b> (loja → espelho) protege quem já jogava antes do espelho existir.
        /// <b>Empurrar</b> (espelho → loja) cobre quem ganhou com o cliente fechado.
        /// <b>Nunca subtrair</b>: a loja não saber de uma conquista não prova que o jogador não a tem
        /// — pode ser conta trocada, serviço fora do ar, ou algo ganho offline que ainda não subiu.
        /// </summary>
        public static void ReconciliarAgora()
        {
            if (catalogo.Length == 0)
            {
                // Sem catálogo ainda (ou jogo sem conquista). Não é erro: RegistrarCatalogo tenta de
                // novo quando chegar.
                return;
            }
            if (!Atual.SabeResponder)
            {
                Debug.Log("[Plataforma] reconciliação adiada: a loja ainda não sabe responder.");
                return;
            }

            reconciliado = true;

            List<string> naLoja;
            try
            {
                naLoja = new List<string>(Atual.LerGanhas(catalogo));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Plataforma] a loja falhou ao listar: " + e.Message +
                                 ". O espelho fica como está.");
                return;
            }

            int puxadas = EspelhoDeConquistas.Semear(naLoja);

            // Empurrar: o que o espelho tem e a loja não.
            int empurradas = 0;
            if (PodeEscrever)
            {
                var conhecidas = new HashSet<string>(naLoja);
                foreach (string id in catalogo)
                {
                    if (conhecidas.Contains(id)) continue;
                    if (!EspelhoDeConquistas.Tem(id)) continue;
                    EscreverNaLoja(id);
                    empurradas++;
                }
            }

            Debug.Log("[Plataforma] reconciliado: " + puxadas + " vieram da loja, " + empurradas +
                      " foram enviadas a ela (de " + catalogo.Length + " conhecidas).");
        }

        /// <summary>
        /// Zera as estáticas entre plays no editor. Com Domain Reload desligado, a loja da sessão
        /// anterior continuaria registrada — e ela já morreu junto com o play.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void AoEntrarEmPlay()
        {
            registrada = null;
            catalogo = new string[0];
            reconciliado = false;
        }
    }
}
