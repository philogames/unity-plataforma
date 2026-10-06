using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace PhiloGames.Plataforma.SteamIntegracao
{
    /// <summary>
    /// A nossa conversa com a Steam — <b>Steamworks.NET puro</b>, sem plugin de terceiro no meio (o
    /// Heathen saiu do Stone Cutter em 2026-09, e nunca entrou aqui).
    ///
    /// <b>Vive num assembly só dela</b> (<c>PhiloGames.Plataforma.Steam</c>), e isso não é arrumação:
    /// o Steamworks.NET só existe no editor, no PC e no Android, e código que fala Steam não pode
    /// morar no assembly que compila para o resto. Por isso a fachada não conhece esta classe — é ela
    /// que <b>se registra</b> na fachada, sozinha, no boot (<see cref="Ligar"/>). O asmdef só compila
    /// com o Steamworks.NET instalado e sem o define <c>PHILOGAMES_SEM_STEAM</c>.
    ///
    /// <b>Ela é a dona da API</b>: inicializa, mantém (<see cref="SteamRuntime"/>) e desliga. O App ID
    /// vem da <see cref="ConfiguracaoDaLoja"/> do jogo.
    ///
    /// O roteiro é o da doc "Steamworks API Overview": <c>RestartAppIfNecessary</c> antes de tudo, fora
    /// do editor; <c>Init</c>; <c>RunCallbacks</c> por quadro; <c>Shutdown</c> ao sair.
    /// </summary>
    public sealed class LojaSteam : ILoja
    {
        readonly uint appId;
        bool ligada;
        bool statsChegaram;
        Callback<UserStatsReceived_t> aoReceberStats;

        LojaSteam(uint appId)
        {
            this.appId = appId;
        }

        /// <summary>
        /// Sobe sozinha antes da primeira cena, sem nada em cena nenhuma.
        ///
        /// <b>Antes da primeira cena, e não depois:</b> quando a Steam reabre o jogo
        /// (<c>RestartAppIfNecessary</c>), este processo fecha — e quanto antes, menos a primeira cena
        /// chega a aparecer. E uma conquista concedida no <c>Awake</c> da primeira cena já encontra a
        /// loja registrada. (No Stone Cutter o boot era depois da primeira cena; mudou ao virar pacote.)
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Ligar()
        {
            ConfiguracaoDaLoja config = ConfiguracaoDaLoja.Carregar();
            if (config == null || config.appIdSteam == 0)
            {
                // Sem App ID não há o que iniciar: a fachada fica com a LojaNenhuma e o jogo roda
                // inteiro. Avisar alto, porque com o Steamworks.NET instalado isto quase sempre é
                // esquecimento.
                Debug.LogWarning("[Steam] sem ConfiguracaoDaLoja (Resources/" + ConfiguracaoDaLoja.CAMINHO_RESOURCES +
                                 ") ou com o App ID 0: o jogo roda sem Steam.");
                return;
            }

            var steam = new LojaSteam(config.appIdSteam);
            steam.Iniciar(config.reabrirPelaSteam);
            if (steam.ligada) Loja.Registrar(steam);
            // Sem Steam não registramos nada: a fachada fica com a LojaNenhuma e o jogo roda inteiro.
            // É o caso normal no editor com o cliente fechado.
        }

        void Iniciar(bool reabrirPelaSteam)
        {
            try
            {
                if (!Packsize.Test())
                {
                    Debug.LogError("[Steam] Packsize.Test() falhou — o binário do steam_api não casa com " +
                                   "esta plataforma. Sem Steam nesta sessão.");
                    return;
                }
                if (!DllCheck.Test())
                {
                    Debug.LogError("[Steam] DllCheck.Test() falhou — a DLL da Steam é de outra versão. " +
                                   "Sem Steam nesta sessão.");
                    return;
                }
            }
            catch (Exception e)
            {
                // DllNotFoundException: a steam_api64.dll não foi junto com a build.
                Debug.LogError("[Steam] a DLL da Steam não carregou: " + e.Message + ". Sem Steam nesta sessão.");
                return;
            }

#if !UNITY_EDITOR
            // Se o jogador abriu o .exe direto, a Steam relança o jogo por ela mesma e este processo
            // morre aqui. Sem isto, abrir fora do cliente faz o Init falhar e o jogador joga sem Steam
            // sem entender por quê. Com um steam_appid.txt ao lado do .exe a Steam devolve false (é
            // como se testa a build fora dela) — e por isso ele nunca vai para a loja.
            if (reabrirPelaSteam)
            {
                try
                {
                    if (SteamAPI.RestartAppIfNecessary(new AppId_t(appId)))
                    {
                        Application.Quit();
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Steam] RestartAppIfNecessary falhou: " + e.Message);
                }
            }
#endif
            try
            {
                string erro;
                ESteamAPIInitResult resultado = SteamAPI.InitEx(out erro);
                ligada = resultado == ESteamAPIInitResult.k_ESteamAPIInitResult_OK;
                if (!ligada)
                {
                    // Caso comum e esperado: cliente fechado, jogador deslogado, conta sem o app. Não
                    // é erro — é "hoje não tem loja".
                    Debug.Log("[Steam] não conectou (" + resultado + ": " + erro + "). O jogo segue sem Steam.");
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Steam] SteamAPI.InitEx lançou: " + e.Message + ". O jogo segue sem Steam.");
                ligada = false;
                return;
            }

            SteamRuntime.Garantir();

            EscutarAsStats();

            // O app que a Steam reconheceu tem de ser o nosso. Divergência aqui significa que o
            // steam_appid.txt aponta para outro jogo — e as conquistas iriam parar na conta errada.
            uint reconhecido = SteamUtils.GetAppID().m_AppId;
            if (reconhecido != appId)
                Debug.LogError("[Steam] a Steam acha que este jogo é o app " + reconhecido +
                               ", mas a ConfiguracaoDaLoja diz " + appId + ". Confira o steam_appid.txt.");

            Debug.Log("[Steam] conectada. App " + reconhecido + ", conta " + SteamFriends.GetPersonaName() + ".");
        }

        /// <summary>
        /// Escuta a chegada das estatísticas da conta.
        ///
        /// 🔑 Isto é o que faz a leitura ter o que responder. As stats chegam <b>assíncronas</b>: entre
        /// o <c>Init</c> e o callback, perguntar por qualquer conquista devolve "não tem". Desde o SDK
        /// 1.61 a Steam as busca sozinha no <c>Init</c> (o <c>RequestCurrentStats</c> saiu da API), e
        /// o aviso só é entregue no primeiro <c>RunCallbacks</c> — que vem depois deste registro.
        /// </summary>
        void EscutarAsStats()
        {
            aoReceberStats = Callback<UserStatsReceived_t>.Create(dados =>
            {
                if (dados.m_nGameID != (ulong)SteamUtils.GetAppID().m_AppId) return;
                if (dados.m_eResult != EResult.k_EResultOK)
                {
                    Debug.LogWarning("[Steam] as estatísticas não vieram: " + dados.m_eResult +
                                     ". A leitura continua pelo espelho local.");
                    return;
                }

                statsChegaram = true;
                Loja.ReconciliarAgora();
            });
        }

        // ---------------------------------------------------------------- ILoja

        public bool PodeEscrever
        {
            get { return ligada; }
        }

        public bool SabeResponder
        {
            get { return ligada && statsChegaram; }
        }

        public void Desbloquear(string id)
        {
            if (!ligada) return;
            // SetAchievement só marca em memória; sem o Store a Steam não sabe de nada e o popup dela
            // não aparece.
            SteamUserStats.SetAchievement(id);
            SteamUserStats.StoreStats();
        }

        public bool PedirIcone(string id, Action<Texture2D> quandoVier)
        {
            if (!ligada || string.IsNullOrEmpty(id) || quandoVier == null) return false;

            int handle = SteamUserStats.GetAchievementIcon(id);
            if (handle == 0)
            {
                // 0 = ainda baixando. A resposta vem pelo callback global.
                IconesDaSteam.Esperar(id, quandoVier);
                return true;
            }

            Texture2D t = IconesDaSteam.Converter(handle);
            if (t == null) return false;
            quandoVier(t);
            return true;
        }

        public IEnumerable<string> LerGanhas(IEnumerable<string> ids)
        {
            var ganhas = new List<string>();
            if (!SabeResponder || ids == null) return ganhas;

            foreach (string id in ids)
            {
                if (string.IsNullOrEmpty(id)) continue;
                bool ganhou;
                // GetAchievement devolve false quando o ID não existe no painel da Steam — que é
                // diferente de "não conquistou". Os dois casos dão no mesmo aqui: não somar.
                if (SteamUserStats.GetAchievement(id, out ganhou) && ganhou) ganhas.Add(id);
            }
            return ganhas;
        }

        /// <summary>
        /// A página do app no overlay da Steam (<c>ActivateGameOverlayToStore</c>, o que a doc "Demos"
        /// pede para a demo levar ao jogo completo). <b>Só quando o overlay está de pé</b>: o jogador
        /// pode tê-lo desligado, e ele leva alguns segundos para engatar depois que o jogo abre — nesse
        /// caso devolve false e a fachada abre o endereço reserva.
        /// </summary>
        public bool AbrirPagina(ulong idDoProduto)
        {
            if (!ligada || idDoProduto == 0 || idDoProduto > uint.MaxValue) return false;
            if (!SteamUtils.IsOverlayEnabled()) return false;
            SteamFriends.ActivateGameOverlayToStore(new AppId_t((uint)idDoProduto),
                                                    EOverlayToStoreFlag.k_EOverlayToStoreFlag_None);
            return true;
        }

        // ---------------------------------------------------------------- só para teste

        /// <summary>
        /// Apaga as conquistas <b>na Steam</b>. Não é caminho de jogo: existe porque testar conquista
        /// gasta a conta real do dono, e sem isto cada teste só poderia ser feito uma vez na vida. O
        /// espelho local se limpa à parte (<see cref="EspelhoDeConquistas.LimparParaTeste"/>).
        /// </summary>
        public void LimparNaSteamParaTeste(IEnumerable<string> ids)
        {
            if (!ligada || ids == null) return;
            foreach (string id in ids)
                if (!string.IsNullOrEmpty(id)) SteamUserStats.ClearAchievement(id);
            SteamUserStats.StoreStats();
        }
    }
}
