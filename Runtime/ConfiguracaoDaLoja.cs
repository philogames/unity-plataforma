using UnityEngine;

namespace PhiloGames.Plataforma
{
    /// <summary>
    /// O que identifica o jogo em cada loja. Um asset por jogo, numa pasta <c>Resources</c>, com o
    /// nome <see cref="CAMINHO_RESOURCES"/> (menu Create › PhiloGames › Configuração da loja).
    ///
    /// <b>Por que asset, e não constante no código:</b> o pacote serve a vários jogos, e o mesmo jogo
    /// pode ter dois apps — o HuniKuin tem o jogo (1064250) e a demo (2252740), que é outro app na
    /// Steam, com outro App ID. No Stone Cutter o App ID era uma constante na classe da Steam; ao
    /// virar pacote, o dado foi para o jogo.
    ///
    /// <b>Sem o asset, ou com o App ID 0, não há Steam</b>: a fachada fica com a
    /// <see cref="LojaNenhuma"/> e o jogo roda inteiro. Para tirar a Steam de uma variante de build
    /// (uma build de feira, por exemplo), o caminho certo é o define <c>PHILOGAMES_SEM_STEAM</c>: aí
    /// o assembly da Steam nem compila, e a build não tenta reabrir o jogo pela Steam.
    /// </summary>
    [CreateAssetMenu(menuName = "PhiloGames/Configuração da loja", fileName = CAMINHO_RESOURCES)]
    public sealed class ConfiguracaoDaLoja : ScriptableObject
    {
        public const string CAMINHO_RESOURCES = "ConfiguracaoDaLoja";

        [Tooltip("O App ID deste executável na Steam (o da demo, se for a demo). 0 = sem Steam.")]
        public uint appIdSteam;

        [Tooltip("Fora do editor, se o jogo foi aberto direto pelo .exe, a Steam o reabre por ela " +
                 "(SteamAPI_RestartAppIfNecessary) e este processo fecha. A doc da Steam recomenda " +
                 "deixar ligado; com um steam_appid.txt ao lado do .exe a Steam não reabre (é o jeito " +
                 "de testar a build fora dela).")]
        public bool reabrirPelaSteam = true;

        /// <summary>O asset do jogo, ou null se ele não tem um.</summary>
        public static ConfiguracaoDaLoja Carregar()
        {
            return Resources.Load<ConfiguracaoDaLoja>(CAMINHO_RESOURCES);
        }
    }
}
