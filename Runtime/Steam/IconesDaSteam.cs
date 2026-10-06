using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace PhiloGames.Plataforma.SteamIntegracao
{
    /// <summary>
    /// Converte o ícone de conquista da Steam em <see cref="Texture2D"/>, inclusive quando ele
    /// <b>ainda não chegou</b>.
    ///
    /// <b>O detalhe que faz isto existir:</b> <c>GetAchievementIcon</c> devolve um handle, e devolve
    /// <b>0</b> quando a imagem ainda está sendo baixada. Nesse caso a Steam responde depois, por um
    /// callback global (<c>UserAchievementIconFetched_t</c>) — e quem não o escuta simplesmente nunca
    /// recebe o ícone. Era esse o caminho do popup de conquista do Stone Cutter, e é por isso que ele
    /// dependia de alguém bombear os callbacks (ver <see cref="SteamRuntime"/>).
    ///
    /// O normal é a arte ser do jogo; isto serve à conquista que ainda não tem arte local.
    /// </summary>
    internal static class IconesDaSteam
    {
        static Callback<UserAchievementIconFetched_t> callback;
        static readonly Dictionary<string, List<Action<Texture2D>>> esperando =
            new Dictionary<string, List<Action<Texture2D>>>();

        /// <summary>
        /// Guarda o pedido até a Steam avisar que a imagem chegou.
        ///
        /// 🐞 Guardar por <b>id</b>, e numa lista: dois cards podem pedir o mesmo ícone, e o callback
        /// é assíncrono. Uma versão anterior deste código usava um campo compartilhado e os ícones se
        /// acumulavam todos no último card que tivesse pedido.
        /// </summary>
        internal static void Esperar(string id, Action<Texture2D> quandoVier)
        {
            if (string.IsNullOrEmpty(id) || quandoVier == null) return;

            GarantirCallback();

            List<Action<Texture2D>> fila;
            if (!esperando.TryGetValue(id, out fila))
            {
                fila = new List<Action<Texture2D>>();
                esperando[id] = fila;
            }
            fila.Add(quandoVier);
        }

        static void GarantirCallback()
        {
            if (callback != null) return;
            callback = Callback<UserAchievementIconFetched_t>.Create(AoChegar);
        }

        static void AoChegar(UserAchievementIconFetched_t dados)
        {
            string id = dados.m_rgchAchievementName;
            List<Action<Texture2D>> fila;
            if (!esperando.TryGetValue(id, out fila)) return;
            esperando.Remove(id);

            Texture2D t = dados.m_nIconHandle == 0 ? null : Converter(dados.m_nIconHandle);
            if (t == null) return;   // a Steam desistiu do ícone; quem pediu fica com a arte local

            foreach (var avisar in fila)
            {
                if (avisar == null) continue;
                try { avisar(t); }
                catch (Exception e) { Debug.LogWarning("[Steam] callback de ícone falhou: " + e.Message); }
            }
        }

        /// <summary>
        /// Handle → textura. A Steam entrega RGBA cru, e <b>de cabeça para baixo</b> em relação ao
        /// que a Unity espera: sem inverter as linhas o ícone aparece espelhado na vertical.
        /// </summary>
        internal static Texture2D Converter(int handle)
        {
            if (handle == 0) return null;

            uint largura, altura;
            if (!SteamUtils.GetImageSize(handle, out largura, out altura) || largura == 0 || altura == 0)
                return null;

            int bytes = (int)(largura * altura * 4);
            var buffer = new byte[bytes];
            if (!SteamUtils.GetImageRGBA(handle, buffer, bytes)) return null;

            var invertido = new byte[bytes];
            int linha = (int)largura * 4;
            for (int y = 0; y < altura; y++)
                Buffer.BlockCopy(buffer, y * linha, invertido, (int)(altura - 1 - y) * linha, linha);

            var textura = new Texture2D((int)largura, (int)altura, TextureFormat.RGBA32, false);
            textura.LoadRawTextureData(invertido);
            textura.Apply();
            return textura;
        }

        /// <summary>
        /// Entre um play e outro, com Domain Reload desligado, o callback da sessão anterior
        /// continuaria registrado apontando para um delegate morto.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void AoEntrarEmPlay()
        {
            if (callback != null)
            {
                callback.Dispose();
                callback = null;
            }
            esperando.Clear();
        }
    }
}
