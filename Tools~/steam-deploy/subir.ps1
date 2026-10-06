<#
.SYNOPSIS
    Sobe uma build de um jogo da PhiloGames para a Steam, conferindo antes o que o SteamPipe nao
    confere. Saiu do steam-deploy do The Stone Cutter (2026-09-21), generalizado em 2026-10-06.

.DESCRIPTION
    Cada jogo (ou cada app: a demo e outro app) tem uma PASTA DE DEPLOY fora do repositorio:

        <pasta>\steam-deploy.json   {"jogo": "...", "appId": 2252740, "depotId": 2252741}
        <pasta>\builder\steamcmd.exe   a ferramenta da Valve (do SDK: tools\ContentBuilder\builder\)
        <pasta>\content\            a build do Unity vai AQUI - e isto que sobe
        <pasta>\output\             logs e cache do SteamPipe (a 2a subida e bem mais rapida)
        <pasta>\scripts\            os dois .vdf, GERADOS por este script a cada uso

    O steamcmd sobe o que voce mandar, calado. Este script existe pelas conferencias que ele nao
    faz, e que so aparecem depois - quando alguem baixa:

      1. a build tem executavel?
      2. a DLL da Steam foi junto? (sem ela o jogo nao conecta: sem conquista, sem overlay)
      3. o steam_appid.txt ficou na pasta? (arquivo de DESENVOLVIMENTO: publicado, ele faz o jogo
         pular a checagem de licenca do cliente - nao pode ir)
      4. sobrou pasta de depuracao do Unity? (as exclusoes do depot cuidam, mas vale ver o peso)

    NADA e publicado por este script: o "setlive" do app_build fica vazio, a build so aparece no
    painel, e quem escolhe a branch e voce, em Steamworks > app > Builds.

    Os .vdf sao gerados em ASCII puro, e este script e ASCII puro: o Windows PowerShell 5.1 le
    arquivo sem BOM na codepage ANSI, e no Stone Cutter um travessao dentro do .vdf voltou
    corrompido na primeira rodada (2026-09-21).

.PARAMETER Pasta
    A pasta de deploy do app (a que tem o steam-deploy.json).

.PARAMETER Usuario
    Sua conta Steam de parceiro. A senha e o Steam Guard sao digitados por voce, no console -
    este script nao guarda, nao le e nao pede credencial nenhuma. Na primeira vez o steamcmd
    grava a sessao na maquina; nas seguintes ele nao pergunta mais.

.PARAMETER Descricao
    O que e esta build. Vai para a lista do painel - sem isso, daqui a um mes ninguem sabe qual
    e qual.

.PARAMETER Preview
    Ensaio: o SteamPipe processa tudo e NAO envia. Use na primeira vez.

.EXAMPLE
    .\subir.ps1 -Pasta D:\workspace\steam-deploy\hunikuin-demo -Usuario minhaconta -Descricao "demo H1 2026-10" -Preview
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $Pasta,
    [Parameter(Mandatory = $true)] [string] $Usuario,
    [Parameter(Mandatory = $true)] [string] $Descricao,
    [switch] $Preview
)

$ErrorActionPreference = 'Stop'

function Falhar($msg) { Write-Host ""; Write-Host "  X  $msg" -ForegroundColor Red; Write-Host ""; exit 1 }
function Alertar($msg) { Write-Host "  !  $msg" -ForegroundColor Yellow }
function Ok($msg)      { Write-Host "  ok $msg" -ForegroundColor Green }

# ---------------------------------------------------------------- a pasta do app

if (-not (Test-Path $Pasta)) { Falhar "a pasta $Pasta nao existe" }
$Pasta    = (Resolve-Path $Pasta).Path
$jsonPath = Join-Path $Pasta 'steam-deploy.json'
if (-not (Test-Path $jsonPath)) { Falhar "nao achei $jsonPath (ver o LEIA-ME ao lado deste script)" }
$cfg = Get-Content $jsonPath -Raw | ConvertFrom-Json
$appId   = [uint32]$cfg.appId
$depotId = [uint32]$cfg.depotId
if ($appId -eq 0 -or $depotId -eq 0) { Falhar "steam-deploy.json sem appId ou depotId" }
$jogo = $cfg.jogo
if (-not $jogo) { $jogo = "app $appId" }

$content  = Join-Path $Pasta 'content'
$output   = Join-Path $Pasta 'output'
$scripts  = Join-Path $Pasta 'scripts'
$steamcmd = Join-Path $Pasta 'builder\steamcmd.exe'

Write-Host ""
Write-Host "$jogo -> Steam (app $appId, depot $depotId)" -ForegroundColor Cyan
Write-Host "----------------------------------------------------------"

if (-not (Test-Path $steamcmd)) { Falhar "steamcmd.exe nao esta em builder\. Extraia do SDK: sdk\tools\ContentBuilder\builder\" }
New-Item -ItemType Directory -Force $output, $scripts | Out-Null

# ---------------------------------------------------------------- o que vai subir

if (-not (Test-Path $content)) { Falhar "a pasta content\ nao existe" }
$itens = Get-ChildItem $content -Recurse -File -ErrorAction SilentlyContinue
if (-not $itens -or $itens.Count -eq 0) { Falhar "content\ esta VAZIA. Gere a build do Unity para ca antes de subir." }

$exes = @(Get-ChildItem $content -Filter '*.exe' -File | Where-Object { $_.Name -ne 'UnityCrashHandler64.exe' -and $_.Name -ne 'UnityCrashHandler32.exe' })
if ($exes.Count -eq 0) { Falhar "nao ha .exe do jogo em content\. Isso nao e uma build de Windows." }
Ok ("executavel: " + (($exes | ForEach-Object { $_.Name }) -join ', '))

# A DLL da Steam: sem ela o jogo roda, mas nao fala com a Steam - e o defeito so aparece
# quando um jogador reclama.
$dll = @(Get-ChildItem $content -Recurse -Filter 'steam_api64.dll' -File -ErrorAction SilentlyContinue)
if ($dll.Count -eq 0) {
    Alertar "steam_api64.dll NAO esta na build. Sem ela nao ha conquista nem overlay."
    Alertar "  (so ignore se esta build for de proposito sem Steam)"
} else {
    Ok "steam_api64.dll presente"
}

# O steam_appid.txt e ferramenta de desenvolvimento. Publicado, faz o jogo deixar de checar a
# licenca pelo cliente - a Valve pede para nao enviar.
$appidTxt = @(Get-ChildItem $content -Recurse -Filter 'steam_appid.txt' -File -ErrorAction SilentlyContinue)
if ($appidTxt.Count -gt 0) {
    Write-Host ""
    Alertar "steam_appid.txt esta dentro de content\ - ele NAO pode ir para a Steam:"
    $appidTxt | ForEach-Object { Write-Host ("       " + $_.FullName) -ForegroundColor Yellow }
    $r = Read-Host "Apagar esses arquivos da pasta content e continuar? (s/N)"
    if ($r -eq 's' -or $r -eq 'S') {
        $appidTxt | Remove-Item -Force
        Ok "removido(s)"
    } else {
        Falhar "cancelado - tire o steam_appid.txt da build e rode de novo"
    }
}

# Peso, e o que as exclusoes vao tirar
$mb = [math]::Round(($itens | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host ("  .. " + $itens.Count + " arquivos, $mb MB em content\")
$lixo = Get-ChildItem $content -Recurse -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match 'BackUpThisFolder|BurstDebugInformation' }
if ($lixo) {
    $lixoMb = [math]::Round((($lixo | ForEach-Object { Get-ChildItem $_.FullName -Recurse -File } |
               Measure-Object Length -Sum).Sum) / 1MB, 1)
    Ok ("pastas de depuracao do Unity encontradas (" + $lixoMb + " MB) - as exclusoes do depot as deixam de fora")
}

# ---------------------------------------------------------------- os .vdf, gerados

# A descricao vai para o painel. Aspas quebrariam o VDF; acento viraria lixo (o arquivo e ASCII).
$desc = ($Descricao -replace '"', "'")
$desc = [System.Text.Encoding]::ASCII.GetString([System.Text.Encoding]::ASCII.GetBytes($desc))
$modoPreview = '0'
if ($Preview) { $modoPreview = '1' }

$appVdf   = Join-Path $scripts ("app_build_" + $appId + ".vdf")
$depotVdf = Join-Path $scripts ("depot_build_" + $depotId + ".vdf")

# "setlive" VAZIO = nao vai para branch nenhuma; a build fica no painel. Escolher a branch e um
# clique consciente em Steamworks > app > Builds. NUNCA "default" aqui: e a branch publica.
$app = @"
// Gerado pelo subir.ps1 (unity-plataforma) - nao edite: e reescrito a cada uso.
"appbuild"
{
	"appid"			"$appId"
	"desc"			"$desc"
	"buildoutput"	"$output\"
	"contentroot"	"$content\"
	"setlive"		""
	"preview"		"$modoPreview"
	"local"			""
	"depots"
	{
		"$depotId"	"$depotVdf"
	}
}
"@

# As exclusoes nao sao decoracao: as pastas que o Unity gera ao lado do .exe e nomeou avisando
# para nao enviar engordam o download e entregam simbolos de depuracao junto com o jogo.
$depot = @"
// Gerado pelo subir.ps1 (unity-plataforma) - nao edite: e reescrito a cada uso.
"DepotBuild"
{
	"DepotID"		"$depotId"
	"ContentRoot"	"$content\"
	"FileMapping"
	{
		"LocalPath"	"*"
		"DepotPath"	"."
		"recursive"	"1"
	}
	"FileExclusion"	"*_BackUpThisFolder_ButDontShipItWithYourGame*"
	"FileExclusion"	"*_BurstDebugInformation_DoNotShip*"
	"FileExclusion"	"*.pdb"
	"FileExclusion"	"*.log"
	"FileExclusion"	"steam_appid.txt"
	"FileExclusion"	"Thumbs.db"
	"FileExclusion"	".DS_Store"
}
"@

[System.IO.File]::WriteAllText($appVdf, $app, [System.Text.Encoding]::ASCII)
[System.IO.File]::WriteAllText($depotVdf, $depot, [System.Text.Encoding]::ASCII)
Ok "vdf gerados em scripts\ (setlive vazio - a build vai para o painel, nao para os jogadores)"

# ---------------------------------------------------------------- confirmar e subir

Write-Host ""
if ($Preview) {
    Write-Host "  MODO ENSAIO: processa e NAO envia." -ForegroundColor Cyan
} else {
    Write-Host "  Vai SUBIR de verdade (para o painel, sem publicar em branch)." -ForegroundColor Cyan
}
Write-Host ("  conta:     " + $Usuario)
Write-Host ("  descricao: " + $desc)
Write-Host ""
$c = Read-Host "Digite SUBIR para continuar"
if ($c -ne 'SUBIR') { Write-Host "cancelado."; exit 0 }

Write-Host ""
Write-Host "-- steamcmd (a senha e o Steam Guard sao pedidos por ele, nao por mim) --" -ForegroundColor DarkGray
& $steamcmd +login $Usuario +run_app_build $appVdf +quit
$code = $LASTEXITCODE

Write-Host ""
if ($code -eq 0) {
    Ok "steamcmd terminou sem erro."
    Write-Host "  Logs em output\. A build aparece em Steamworks > app $appId > Builds," -ForegroundColor DarkGray
    Write-Host "  onde voce escolhe a branch." -ForegroundColor DarkGray
} else {
    Falhar ("steamcmd saiu com codigo $code. Veja output\ - o erro costuma ser depot errado " +
            "('Depot not found': confira o depotId no painel), conta sem permissao no app, ou Steam Guard nao concluido.")
}
