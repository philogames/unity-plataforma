---
tipo: referencia
status: rascunho
---

# Subir uma build para a Steam

O `subir.ps1` sobe uma build pelo SteamPipe (o `steamcmd`) e confere antes o que ele não confere. Saiu do
`steam-deploy` do Stone Cutter (2026-09-21) e foi generalizado: o app e o depot vêm de um arquivo de cada
jogo.

## A pasta de deploy (uma por app, fora do repositório)

`steamcmd`, a build e os logs não entram no git. Cada app tem a sua pasta (a demo é outro app):

```
<pasta>\steam-deploy.json      {"jogo": "Huni Kuin - demo", "appId": 2252740, "depotId": 2252741}
<pasta>\builder\steamcmd.exe   do SDK: tools\ContentBuilder\builder\ (ele se atualiza sozinho)
<pasta>\content\               a build do Unity vai AQUI: é isto que sobe
<pasta>\output\                logs e cache do SteamPipe
<pasta>\scripts\               os dois .vdf, gerados pelo script a cada uso
```

**O depot:** a Steam cria o primeiro depot com o App ID + 1, mas só o painel diz o seu (Steamworks › app ›
SteamPipe › Depots). Errado, o upload falha com "Depot not found": falha limpa, não estraga nada.

## Subir

```powershell
& "<clone do unity-plataforma>\Tools~\steam-deploy\subir.ps1" -Pasta <pasta> -Usuario SUA_CONTA -Descricao "o que é esta build" -Preview
```

`-Preview` é ensaio: processa tudo e **não envia**. Sem ele, sobe de verdade e ainda assim **não publica**:
o `"setlive"` fica vazio, a build fica no painel, e a branch quem escolhe é você, em Steamworks › app ›
Builds. A senha e o Steam Guard são pedidos pelo `steamcmd`, no seu console.

## O que o script confere

| | por que importa |
|---|---|
| há `.exe` em `content\` | pasta vazia ou build errada sobe calada |
| `steam_api64.dll` foi junto | sem ela **não há conquista nem overlay**, e o defeito só aparece quando um jogador reclama |
| `steam_appid.txt` **não** foi junto | é arquivo de desenvolvimento: publicado, faz o jogo pular a checagem de licença do cliente. O script oferece apagar, e o depot também o exclui |
| peso, e o que as exclusões tiram | `*_BackUpThisFolder_ButDontShipItWithYourGame` e `*_BurstDebugInformation_DoNotShip` podem passar de 1 GB |

## Armadilha já paga

O `subir.ps1` e os `.vdf` são **ASCII puro**: o Windows PowerShell 5.1 lê arquivo sem BOM na codepage ANSI,
e no Stone Cutter um travessão dentro do `.vdf` voltou corrompido na primeira rodada (2026-09-21). A
descrição passada ao script perde os acentos pelo mesmo motivo.
