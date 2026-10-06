---
tipo: nota
---

# Changelog

## 0.1.0 — 2026-10-06

Primeira versão, extraída da camada de plataforma do Stone Cutter (`Assets/_Plataforma/`).

- A fachada `Loja`, o contrato `ILoja`, a `LojaNenhuma` e o `EspelhoDeConquistas` (o mesmo
  `conquistas.json`, versão 1, do Stone Cutter).
- A `LojaSteam` sobre o Steamworks.NET: `RestartAppIfNecessary` fora do editor, `InitEx`, callbacks por
  quadro num objeto que atravessa as cenas, `Shutdown` ao sair; conquistas e ícones como no Stone Cutter.
- **Novo:** `Loja.AbrirPagina` (overlay da Steam na página de um app, com endereço reserva), para a demo
  levar ao jogo completo.
- **Mudou em relação ao Stone Cutter:** o App ID sai do código para a `ConfiguracaoDaLoja` do jogo; a Steam
  sobe antes da primeira cena (era depois); o `RequestCurrentStats` saiu (a API o removeu no SDK 1.61, e as
  estatísticas chegam sozinhas depois do `Init`); o define `PHILOGAMES_SEM_STEAM` tira a Steam de uma
  variante de build.
- A conferência da build de PC (`ConferenciaDaBuild`) e o `subir.ps1` genérico (`Tools~/steam-deploy`).
