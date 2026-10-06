using System;
using System.Collections.Generic;
using UnityEngine;

namespace PhiloGames.Plataforma
{
    /// <summary>
    /// A loja onde o jogo está rodando — Steam hoje, outra amanhã, nenhuma no editor.
    ///
    /// <b>É uma interface estreita de propósito.</b> Nome, descrição e arte de conquista são dado do
    /// jogo. Sobrou para a loja o que só ela pode fazer: <b>registrar o desbloqueio na conta do
    /// jogador</b> e <b>mostrar uma página dela</b>. Quem responde "o jogador já ganhou?" é o
    /// <see cref="EspelhoDeConquistas"/> — o porquê está em <see cref="Loja"/>.
    ///
    /// <b>Quem implementa não é escolhido por ninguém: se apresenta.</b> Cada implementação se
    /// registra na fachada no boot (<see cref="Loja.Registrar"/>). É isso que deixa a da Steam viver
    /// num assembly que <b>não existe</b> nas plataformas sem Steam sem que nada aqui precise saber —
    /// ver o asmdef <c>PhiloGames.Plataforma.Steam</c>.
    ///
    /// <b>Não há <c>AtualizarProgresso</c></b>, e é decisão herdada do Stone Cutter (de onde esta
    /// camada saiu, em 06/10/2026): o jogo faz a conta dos contadores e só pede o desbloqueio ao bater
    /// o alvo. Um método sem chamador é um método que ninguém testa; ele entra quando algum jogo pedir
    /// progresso parcial na UI da loja.
    /// </summary>
    public interface ILoja
    {
        /// <summary>
        /// Dá para registrar desbloqueio agora? Falso quando a loja não subiu — cliente fechado,
        /// jogador deslogado, build sem loja.
        ///
        /// ⚠️ <b>Gateia só a ESCRITA.</b> No Stone Cutter, um guarda que fechava leitura e escrita
        /// juntas fez a tela de conquistas abrir toda bloqueada sem Steam (2026-06). A leitura não passa
        /// por aqui.
        /// </summary>
        bool PodeEscrever { get; }

        /// <summary>
        /// A loja já tem as respostas, ou ainda está buscando?
        ///
        /// <b>Existe para desfazer uma ambiguidade que estragaria a reconciliação.</b> Na Steam as
        /// estatísticas chegam <b>assíncronas</b>: entre o <c>Init</c> e o <c>UserStatsReceived_t</c>,
        /// perguntar por qualquer conquista responde "não tem". Sem distinguir os dois casos, a
        /// reconciliação concluiria que a conta está zerada.
        /// </summary>
        bool SabeResponder { get; }

        /// <summary>Registra o desbloqueio na conta. Chamado pela fachada, nunca pelo jogo direto.</summary>
        void Desbloquear(string id);

        /// <summary>
        /// Pede à loja a arte da conquista. <b>Devolve false quando não há de onde vir arte</b> — e aí
        /// o chamador usa a do jogo. O callback pode vir em outro frame (na Steam vem), ou não vir.
        /// </summary>
        bool PedirIcone(string id, Action<Texture2D> quandoVier);

        /// <summary>
        /// Quais destes ids a conta já tem, segundo a loja.
        ///
        /// Só faz sentido quando <see cref="SabeResponder"/> é verdadeiro; fora disso devolve vazio,
        /// que aqui significa <b>"não sei"</b>, nunca "não tem".
        /// </summary>
        IEnumerable<string> LerGanhas(IEnumerable<string> ids);

        /// <summary>
        /// Mostra a página de um produto da loja <b>por cima do jogo</b> (na Steam, o overlay), sem
        /// tirar o jogador da janela. É o caminho que a Steam pede para a demo levar ao jogo completo
        /// (<c>ISteamFriends::ActivateGameOverlayToStore</c>, doc "Demos" do Steamworks).
        ///
        /// <b>Devolve false quando não deu</b> — loja fora do ar, overlay desligado pelo jogador, id
        /// que não é desta loja —, e aí a fachada usa o endereço reserva que o jogo passou.
        /// </summary>
        bool AbrirPagina(ulong idDoProduto);
    }
}
