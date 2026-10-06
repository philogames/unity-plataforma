using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace PhiloGames.Plataforma.Editor
{
    /// <summary>
    /// Confere a build de PC logo depois de gerada, pelo que a Unity e o SteamPipe deixam passar
    /// calados — e que só aparece quando um jogador reclama:
    ///
    ///   - a build tem a Steam (o assembly <c>PhiloGames.Plataforma.Steam</c> foi junto) mas a
    ///     <c>steam_api64.dll</c> não: o jogo roda e não fala com a Steam;
    ///   - a build tem a Steam e a <see cref="ConfiguracaoDaLoja"/> do projeto não tem App ID;
    ///   - há um <c>steam_appid.txt</c> ao lado do executável: é arquivo de desenvolvimento, e
    ///     publicado faz o jogo pular a checagem de licença do cliente (a doc da Steam: "You should
    ///     not ship this with your builds").
    ///
    /// <b>Olha a pasta gerada, e não os defines:</b> uma variante de build pode tirar a Steam por um
    /// define extra (<c>PHILOGAMES_SEM_STEAM</c>) que este assembly de editor nunca vê. O que foi para
    /// a pasta é a verdade. Só sabe disso em build Mono (a pasta <c>Managed</c>); em IL2CPP confere
    /// só o <c>steam_appid.txt</c>.
    /// </summary>
    static class ConferenciaDaBuild
    {
        [PostProcessBuild(1000)]
        static void Conferir(BuildTarget alvo, string caminho)
        {
            if (alvo != BuildTarget.StandaloneWindows64 && alvo != BuildTarget.StandaloneWindows &&
                alvo != BuildTarget.StandaloneLinux64)
                return;

            string pasta = Path.GetDirectoryName(caminho);
            string dados = Path.Combine(pasta, Path.GetFileNameWithoutExtension(caminho) + "_Data");

            string appIdTxt = Path.Combine(pasta, "steam_appid.txt");
            if (File.Exists(appIdTxt))
                Debug.LogWarning("[Plataforma] há um steam_appid.txt na build (" + appIdTxt + "). Serve para testar " +
                                 "fora da Steam, mas NÃO pode ir para a loja.");

            string managed = Path.Combine(dados, "Managed");
            if (!Directory.Exists(managed)) return;   // IL2CPP: não há como saber se a Steam foi junto
            if (!File.Exists(Path.Combine(managed, "PhiloGames.Plataforma.Steam.dll"))) return;   // build sem Steam

            string plugins = Path.Combine(dados, "Plugins");
            bool temDll = Directory.Exists(plugins) &&
                          (Directory.GetFiles(plugins, "steam_api64.dll", SearchOption.AllDirectories).Length > 0 ||
                           Directory.GetFiles(plugins, "steam_api.dll", SearchOption.AllDirectories).Length > 0 ||
                           Directory.GetFiles(plugins, "libsteam_api.so", SearchOption.AllDirectories).Length > 0);
            if (!temDll)
                Debug.LogError("[Plataforma] a build tem a Steam, mas a DLL dela não foi junto (" + plugins + "). " +
                               "O jogo vai rodar sem falar com a Steam.");

            ConfiguracaoDaLoja config = null;
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(ConfiguracaoDaLoja).Name))
            {
                config = AssetDatabase.LoadAssetAtPath<ConfiguracaoDaLoja>(AssetDatabase.GUIDToAssetPath(guid));
                if (config != null) break;
            }
            if (config == null || config.appIdSteam == 0)
                Debug.LogError("[Plataforma] a build tem a Steam, mas o projeto não tem ConfiguracaoDaLoja com App ID " +
                               "(Resources/" + ConfiguracaoDaLoja.CAMINHO_RESOURCES + "). O jogo vai rodar sem Steam.");
            else if (temDll)
                Debug.Log("[Plataforma] build conferida: Steam com o App ID " + config.appIdSteam + ", DLL presente.");
        }
    }
}
