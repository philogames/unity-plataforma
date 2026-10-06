using System;
using System.Collections.Generic;
using UnityEngine;

namespace PhiloGames.Plataforma
{
    /// <summary>
    /// Sem loja: o editor sem Steam, uma build de teste, um porte que ainda não tem serviço.
    ///
    /// <b>Não é um stub para "desligar" conquista.</b> Com ela o jogo coleciona normalmente — quem
    /// guarda é o espelho, e a fachada marca o espelho antes de falar com a loja. O que esta classe
    /// faz é dizer "não há a quem contar".
    /// </summary>
    public sealed class LojaNenhuma : ILoja
    {
        public bool PodeEscrever { get { return false; } }

        /// <summary>
        /// <b>Falso de propósito.</b> Sem loja não há o que reconciliar, e dizer "sei responder"
        /// faria a fachada concluir que a conta está zerada.
        /// </summary>
        public bool SabeResponder { get { return false; } }

        public void Desbloquear(string id) { }

        /// <summary>Não há loja de onde vir arte — o chamador usa a do jogo.</summary>
        public bool PedirIcone(string id, Action<Texture2D> quandoVier) { return false; }

        /// <summary>Vazio quer dizer "não sei", e não "o jogador não tem nada".</summary>
        public IEnumerable<string> LerGanhas(IEnumerable<string> ids)
        {
            return new string[0];
        }

        /// <summary>Sem loja não há overlay: a fachada abre o endereço reserva.</summary>
        public bool AbrirPagina(ulong idDoProduto) { return false; }
    }
}
