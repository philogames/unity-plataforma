---
tipo: referencia
status: rascunho
---

# PhiloGames — Plataforma (`com.philogames.plataforma`)

A loja onde o jogo roda (a Steam hoje, outras amanhã) atrás de uma fachada nossa. O jogo fala só com a
`Loja`, nunca com a Steam. A camada saiu do Stone Cutter (`Assets/_Plataforma/`, 2026-09), onde
substituiu o Heathen Steamworks, e virou pacote em 06/10/2026 para servir a qualquer jogo da
PhiloGames.

| Peça | O que faz |
|---|---|
| `Loja` | a fachada: conquistas (`Desbloquear`, `Ganhou`, `Quantas`, `PedirIcone`), a página da loja (`AbrirPagina`), o registro da loja ativa |
| `ILoja`, `LojaNenhuma` | o contrato, e a loja de faz-de-conta que assume sem Steam (editor, build sem loja) |
| `EspelhoDeConquistas` | o que o jogador ganhou, num arquivo nosso (`conquistas.json` no `persistentDataPath`): responde sempre, com ou sem loja |
| `ConfiguracaoDaLoja` | o asset do jogo com o App ID da Steam |
| `LojaSteam` (assembly `PhiloGames.Plataforma.Steam`) | inicia a Steam, mantém os callbacks, desliga ao sair; conquistas e overlay |
| `ConferenciaDaBuild` (editor) | depois da build de PC: avisa se a DLL da Steam não foi junto, se falta App ID, ou se há `steam_appid.txt` na pasta |
| `Tools~/steam-deploy/subir.ps1` | sobe a build pelo SteamPipe, conferindo antes o que ele não confere |

## Instalar num jogo

No `Packages/manifest.json` do jogo, as duas linhas:

```json
"com.philogames.plataforma": "https://github.com/philogames/unity-plataforma.git#0.1.0",
"com.rlabrecque.steamworks.net": "https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.1",
```

O Steamworks.NET vai no jogo, e não como dependência do pacote, porque o gerenciador de pacotes da Unity
não aceita dependência por git dentro de um `package.json`. Sem ele, o pacote compila sem a Steam (a
`LojaNenhuma` assume). Foi testado com o Steamworks.NET **2025.164.1** (SDK 1.64): a `LojaSteam` usa o
`SteamAPI.InitEx`, que versões antigas do Steamworks.NET não têm.

O repositório é privado: o git da máquina precisa estar logado na conta da PhiloGames (o `gh auth login`
resolve).

Unity: 2022.3 ou mais nova (o HuniKuin está na 2022.3, o Stone Cutter na 6000.3).

## Configurar

1. **Create › PhiloGames › Configuração da loja**, numa pasta `Resources`, com o nome
   `ConfiguracaoDaLoja`.
2. **`appIdSteam`**: o App ID **deste executável**. A demo é outro app na Steam, com App ID próprio
   (doc "Demos" do Steamworks): a build da demo leva o App ID da demo.
3. **`reabrirPelaSteam`** (ligado por padrão): fora do editor, o jogo aberto direto pelo `.exe` é reaberto
   pela Steam (`SteamAPI_RestartAppIfNecessary`, recomendado pela doc).
4. **`steam_appid.txt` na raiz do projeto**, com o mesmo App ID: é por ele que o **editor** se apresenta como
   o jogo, com o cliente da Steam aberto. O Steamworks.NET cria um com `480` (o app de teste da Valve) se não
   houver. **Ele nunca vai para a build publicada.** Para testar a build fora da Steam, ponha um ao lado do
   `.exe` (a Steam deixa de reabrir) e tire antes de subir; o `subir.ps1` recusa enviar com ele.

**Uma variante de build sem Steam** (uma build de feira, por exemplo): o define `PHILOGAMES_SEM_STEAM`. O
assembly da Steam nem compila, e o jogo não tenta reabrir pela Steam numa máquina que não a tem.

## Usar

```csharp
using PhiloGames.Plataforma;

// a página do jogo completo, no overlay da Steam (sem overlay: o endereço reserva)
bool noOverlay = Loja.AbrirPagina(1064250, "steam://store/1064250");

// conquistas
Loja.RegistrarCatalogo(idsDoJogo);          // uma vez, no boot: sem isto não há reconciliação
bool primeiraVez = Loja.Desbloquear("H1_FIM");
bool tem = Loja.Ganhou("H1_FIM");            // responde sempre: é o espelho
```

A `LojaSteam` sobe sozinha antes da primeira cena (`RuntimeInitializeOnLoadMethod`): o jogo não põe nada em
cena. Sem a Steam (cliente fechado, conta sem o app), o jogo roda inteiro, e o log diz por quê.

**Jogo sem conquista** (uma demo) não chama `RegistrarCatalogo` nem `Desbloquear`: o espelho nunca toca o
disco. A doc da Steam recomenda conquistas desligadas na demo.

## Contratos que não se mudam sem migrar

- **`conquistas.json`, versão 1**: o Stone Cutter, publicado, já grava este arquivo na máquina dos
  jogadores. Mudar o nome ou o formato sem migrar faz a coleção deles sumir.
- **O GUID do script `ConfiguracaoDaLoja`**: os assets dos jogos apontam para ele.

## Subir a build (`Tools~/steam-deploy`)

O procedimento está em [`Tools~/steam-deploy/LEIA-ME.md`](Tools~/steam-deploy/LEIA-ME.md).

## Fontes

- [Steamworks API Overview](https://partner.steamgames.com/doc/sdk/api) — `RestartAppIfNecessary`, `Init`,
  `RunCallbacks`, `Shutdown`, `steam_appid.txt`
- [Demos](https://partner.steamgames.com/doc/store/application/demos) — App ID próprio, overlay para a loja,
  conquistas desligadas
- [SteamPipe](https://partner.steamgames.com/doc/sdk/uploading)
- [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) (MIT)
