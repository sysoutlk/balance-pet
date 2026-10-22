# Balance Pet

A desktop pet for Windows that keeps an eye on your API balances.

It shows the balance, today's spend, the **standard / discount pricing window with a live
countdown** (when the primary source is DeepSeek) and a 7-day spend chart in a speech bubble.
It walks around, hides at screen edges (sliding out with only its head peeking), can be
dragged and thrown, and can clone itself. Balances are not limited to DeepSeek: Kimi
(Moonshot), SiliconFlow and OpenRouter (covers GPT / Claude / Gemini / Grok) are queried
directly, and any other relay can be wired in through the "custom endpoint" provider.

## Highlights

- **Compiled, not scripted**: a single C# + WPF executable (~360 KB). No Node / Python /
  PowerShell runtime; frame rate is configurable (default 4 fps idle / 25 fps active) and
  capped at the monitor refresh rate.
- **Zero build dependencies**: `build.cmd` uses the C# compiler that ships with Windows.
  No .NET SDK needed; an SDK-style `src/BalancePet.csproj` is provided for IDEs and CI.
- **Pricing window aligned with the official DeepSeek schedule**: standard hours are Mon-Fri
  (excluding Chinese public holidays) 09:00-12:00 and 14:00-18:00 Beijing time; everything
  else, including weekends, holidays and weekend make-up workdays, is discounted to half price.
- **Multiple providers at once** with a selectable primary source.
- **Keys stored with Windows DPAPI** (decryptable only by the current user); no telemetry,
  nothing is uploaded anywhere.
- **Content you can extend**: turn one static PNG into a frame-by-frame animated role,
  particle actions (fireworks / hearts / sparkles / confetti), import & export of quip packs,
  and per-provider / per-plan quota alerts.
- **Neutral compatibility profiles**: auto-detect layered PNG sequences and WebM + preview
  packages, compose foreground/background frames, expand delayed frames, and fall back to
  bundled preview GIFs when `ffmpeg` is unavailable.
- **Batch pack import**: select a collection folder and every recognized child pack is
  converted, validated and installed in one pass.

## Fully standalone

The pet depends on nothing else on the machine: it only reads its own `pet-config.json` /
`pet-state.json` and only talks to the official provider APIs. No database, no other
application's folders; uninstalling means deleting the folder.

## Quick start

1. Install with `balance-pet-setup.exe`: choose a parent folder and it creates a `BalancePet`
   subfolder; desktop and Start Menu shortcuts have separate toggles. Or run the portable
   `balance-pet.exe` directly.
2. Run `balance-pet.exe`. Runtime data (config, usage history, roles) lives in `%APPDATA%\BalancePet`; put an empty `portable.txt` next to the exe for fully portable mode.
3. Right-click the pet, open the settings card and add your API keys under "余额接口".
4. Optional: `scripts\install-desktop-shortcut.ps1`, `scripts\install-autostart.ps1`.

## Data sources

| Provider | Endpoint | Notes |
| --- | --- | --- |
| DeepSeek | `GET /user/balance` | official balance API (granted / topped-up split) |
| Kimi (Moonshot) | `GET /v1/users/me/balance` | official balance API; use `https://api.moonshot.ai` outside China |
| SiliconFlow | `GET /v1/user/info` | official API with balance |
| OpenRouter | `GET /api/v1/credits` | USD credits; covers GPT / Claude / Gemini / Grok |
| Custom | any | URL + auth style (Bearer / x-api-key / none) + JSON path |

Qwen (DashScope), Doubao (Volcano Ark), Zhipu GLM, OpenAI, Anthropic, Google Gemini and
xAI do not expose a public per-key balance endpoint; the settings card says so and points
at the vendor console (or use OpenRouter / a custom endpoint).

## Build

```powershell
.\build.ps1            # in-box csc, no SDK, no network
dotnet build src\BalancePet.csproj -c Release   # optional, for SDK users
```

## License

[MIT](LICENSE). No character artwork is bundled; drop your own PNGs into `roles\`.

Product and API names are used only to describe compatibility. Their trademarks belong to
their respective owners and do not imply partnership, authorization, or endorsement.
