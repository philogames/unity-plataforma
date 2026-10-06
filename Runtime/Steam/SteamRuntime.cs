using Steamworks;
using UnityEngine;

namespace PhiloGames.Plataforma.SteamIntegracao
{
    /// <summary>
    /// O objeto que mantém a Steam viva enquanto o jogo roda: <b>bombeia os callbacks todo frame</b> e
    /// desliga a API ao sair.
    ///
    /// <b>Por que um objeto que atravessa as cenas:</b> tudo o que a Steam responde de forma
    /// assíncrona (as estatísticas da conta, o ícone de uma conquista) só chega quando alguém chama
    /// <c>RunCallbacks</c>. No Stone Cutter, o bombeamento morava num objeto da primeira cena, sem
    /// <c>DontDestroyOnLoad</c>: da segunda cena em diante ninguém bombeava, e o popup de conquista,
    /// que espera o ícone, não aparecia. A doc da Steam pede ao menos uma vez por segundo; a maioria
    /// dos jogos chama uma vez por quadro.
    ///
    /// <b>Não é componente em cena, de propósito.</b> Sobe por código (<see cref="Garantir"/>), e o
    /// jogo não precisa pôr nada em cena nenhuma.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class SteamRuntime : MonoBehaviour
    {
        internal const string NOME = "[Steam] runtime";

        static SteamRuntime instancia;

        internal static SteamRuntime Garantir()
        {
            if (instancia != null) return instancia;

            LimparSobras();

            var go = new GameObject(NOME);
            // HideInHierarchy, e NUNCA HideAndDontSave: o "DontSave" tira o objeto da limpeza que a
            // Unity faz ao sair do play, e a cópia sobrevive para o play seguinte ainda recebendo
            // Update (o Stone Cutter pagou por isso em 2026-09-10, com 10 cópias vivas de uma sonda).
            // Uma bomba de callbacks da Steam duplicada seria bem pior.
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);

            instancia = go.AddComponent<SteamRuntime>();
            return instancia;
        }

        /// <summary>
        /// Mata por tipo qualquer cópia que tenha sobrevivido a um play anterior. Com Domain Reload
        /// desligado, a estática <c>instancia</c> sozinha não basta — ela pode estar nula e o objeto,
        /// vivo.
        /// </summary>
        static void LimparSobras()
        {
            foreach (var antigo in Resources.FindObjectsOfTypeAll<SteamRuntime>())
            {
                if (antigo == null || antigo.gameObject == null) continue;
                DestroyImmediate(antigo.gameObject);
            }
        }

        void Update()
        {
            SteamAPI.RunCallbacks();
        }

        void OnEnable()
        {
            // Application.quitting, e NÃO OnDestroy: o OnDestroy roda também ao sair do play mode no
            // editor, e um ciclo Shutdown/Init dentro da mesma sessão do editor — com Domain Reload
            // desligado — leva ao "Tried to Initialize the SteamAPI twice" na entrada seguinte.
            Application.quitting += Desligar;
        }

        void OnDisable()
        {
            Application.quitting -= Desligar;
        }

        void Desligar()
        {
            // A doc da Steam: chamar no fim do processo, se possível. Não desengata o overlay — não
            // há garantia de que o render terminou de usá-lo.
            SteamAPI.Shutdown();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void AoEntrarEmPlay()
        {
            instancia = null;
        }
    }
}
