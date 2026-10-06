using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PhiloGames.Plataforma
{
    /// <summary>
    /// O que o jogador já ganhou, guardado por nós.
    ///
    /// <b>Por que existe.</b> A API de conquistas do Unity 6.3 (Platform Toolkit) <b>escreve e não
    /// lê</b>: o <c>IAchievementSystem</c> tem <c>Unlock</c> e <c>UpdateProgress</c>, e não há
    /// consulta de estado em tipo nenhum do namespace. Como uma tela de conquistas e um contador
    /// "X/Y" são leitura pura, sem um estado nosso eles não teriam a quem perguntar. No <b>Switch</b>
    /// isso deixa de ser inconveniência e vira a única saída: a Nintendo não tem conquista de sistema
    /// para consultar.
    ///
    /// <b>Não vai para o save do jogo, e isto é deliberado.</b> Save costuma ser <b>por slot</b>, e
    /// conquista não é: começar um jogo novo não desfaz o que o jogador conquistou. O lugar certo é o
    /// mesmo das opções: um arquivo do jogador, fora do save (<c>persistentDataPath</c>).
    ///
    /// <b>Nunca apaga sozinho.</b> Arquivo ilegível ou de versão futura é <b>ignorado</b>, não
    /// sobrescrito: se um dia a leitura falhar por bug nosso, o pior resultado é a lista aparecer
    /// vazia numa sessão — não o jogador perder o que ganhou.
    ///
    /// ⚠️ <b>O nome e o formato do arquivo são contrato:</b> o Stone Cutter, publicado, já grava o
    /// <c>conquistas.json</c> na versão 1 na máquina dos jogadores (desde o <c>SC_EspelhoDeConquistas</c>,
    /// de onde este saiu). Mudar o nome ou o formato sem migrar faz a coleção deles sumir.
    ///
    /// <b>Só toca o disco quando alguém pergunta ou marca uma conquista.</b> Jogo sem conquista (uma
    /// demo) nunca lê nem grava este arquivo.
    /// </summary>
    public static class EspelhoDeConquistas
    {
        const string ARQUIVO = "conquistas.json";
        const int VERSAO = 1;

        static HashSet<string> ganhas;
        static bool carregado;
        static bool somenteLeitura;   // ligado quando o arquivo não pôde ser lido: não gravamos por cima

        [Serializable]
        private class Dados
        {
            public int versao = VERSAO;
            public string[] ganhas = new string[0];
        }

        static string Caminho
        {
            get { return Path.Combine(Application.persistentDataPath, ARQUIVO); }
        }

        /// <summary>O jogador tem esta conquista? Responde sempre — com ou sem loja.</summary>
        public static bool Tem(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            Carregar();
            return ganhas.Contains(id);
        }

        /// <summary>Quantas destas o jogador tem. É o "X" do "X/Y" do capítulo.</summary>
        public static int Quantas(IEnumerable<string> ids)
        {
            if (ids == null) return 0;
            Carregar();
            int n = 0;
            foreach (string id in ids)
                if (!string.IsNullOrEmpty(id) && ganhas.Contains(id)) n++;
            return n;
        }

        /// <summary>
        /// Anota uma conquista. <b>Devolve false se ela já estava anotada</b> — é assim que a fachada
        /// sabe que não precisa incomodar a loja de novo, e que o popup não deve aparecer duas vezes.
        /// </summary>
        public static bool Marcar(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            Carregar();
            if (!ganhas.Add(id)) return false;
            Gravar();
            return true;
        }

        /// <summary>
        /// Adota de uma vez o que a loja disse que a conta já tem (a reconciliação da entrada).
        /// Devolve quantas eram novidade — 0 quer dizer que o espelho já estava em dia.
        /// </summary>
        public static int Semear(IEnumerable<string> ids)
        {
            if (ids == null) return 0;
            Carregar();
            int novas = 0;
            foreach (string id in ids)
                if (!string.IsNullOrEmpty(id) && ganhas.Add(id)) novas++;
            if (novas > 0) Gravar();
            return novas;
        }

        /// <summary>Tudo o que o jogador tem. Cópia: ninguém mexe no conjunto por fora.</summary>
        public static List<string> Todas()
        {
            Carregar();
            return new List<string>(ganhas);
        }

        /// <summary>
        /// Apaga o espelho. <b>Só para teste</b> — o jogo nunca chama. A conquista na loja continua
        /// lá: quem limpa a Steam é a Steam.
        /// </summary>
        public static void LimparParaTeste()
        {
            ganhas = new HashSet<string>();
            carregado = true;
            somenteLeitura = false;
            Gravar();
        }

        // ------------------------------------------------------------------ disco

        static void Carregar()
        {
            if (carregado) return;
            carregado = true;
            ganhas = new HashSet<string>();

            string caminho = Caminho;
            if (!File.Exists(caminho)) return;   // primeira vez: espelho vazio é o estado correto

            try
            {
                Dados d = JsonUtility.FromJson<Dados>(File.ReadAllText(caminho));
                if (d == null)
                {
                    // Arquivo existe mas não é nosso. Não gravamos por cima sem o dono ver.
                    somenteLeitura = true;
                    Debug.LogWarning("[Plataforma] '" + caminho + "' não é um espelho válido — as conquistas " +
                                     "vão aparecer como não ganhas e NADA será gravado nesta sessão.");
                    return;
                }
                if (d.versao > VERSAO)
                {
                    // Espelho de uma versão futura do jogo (jogador voltou para uma build antiga).
                    // Gravar aqui truncaria o que a versão nova sabe.
                    somenteLeitura = true;
                    Debug.LogWarning("[Plataforma] o espelho é da versão " + d.versao + " e esta build lê até a " +
                                     VERSAO + " — lendo o que dá, sem gravar.");
                }
                if (d.ganhas != null)
                    foreach (string id in d.ganhas)
                        if (!string.IsNullOrEmpty(id)) ganhas.Add(id);
            }
            catch (Exception e)
            {
                somenteLeitura = true;
                Debug.LogWarning("[Plataforma] não consegui ler '" + caminho + "': " + e.Message +
                                 ". Nada será gravado nesta sessão — o arquivo fica como está.");
            }
        }

        static void Gravar()
        {
            if (somenteLeitura) return;
            try
            {
                Dados d = new Dados();
                d.versao = VERSAO;
                d.ganhas = new List<string>(ganhas).ToArray();
                File.WriteAllText(Caminho, JsonUtility.ToJson(d));
            }
            catch (Exception e)
            {
                // Sem disco, sem permissão, pasta sumiu: a sessão continua com o que está em memória.
                Debug.LogWarning("[Plataforma] não consegui gravar o espelho: " + e.Message);
            }
        }

        /// <summary>
        /// O editor não recarrega estáticos entre um play e outro quando o Domain Reload está
        /// desligado — sem isto, o espelho da sessão anterior sobreviveria e mascararia o que o
        /// disco realmente tem.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void AoEntrarEmPlay()
        {
            ganhas = null;
            carregado = false;
            somenteLeitura = false;
        }
    }
}
