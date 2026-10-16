# Balance Pet

A desktop pet for Windows that keeps an eye on your API balances.

It shows the balance, today's spend, the **peak / off-peak window with a live countdown**
and a 7-day spend chart in a speech bubble. It walks around, snaps to screen edges, can be
dragged and thrown, and can clone itself. Balances are not limited to DeepSeek: Kimi
(Moonshot), SiliconFlow and OpenRouter (covers GPT / Claude / Gemini / Grok) are queried
directly, and any other relay can be wired in through the "custom endpoint" provider.

## Highlights

- **Compiled, not scripted**: a single C# + WPF executable (~180 KB). No Node / Python /
  PowerShell runtime, and the render loop drops to ~4 fps while idle.
- **Zero build dependencies**: `build.cmd` uses the C# compiler that ships with Windows.
  No .NET SDK needed; an SDK-style `src/DshPet.csproj` is provided for IDEs and CI.
- **Peak pricing aligned with the official DeepSeek schedule**: peak is Mon-Fri
  (excluding Chinese public holidays) 09:00-12:00 and 14:00-18:00 Beijing time; everything
  else, including weekends, holidays and weekend make-up workdays, is off-peak at half price.
- **Multiple providers at once** with a selectable primary source.
- **Keys stored with Windows DPAPI** (decryptable only by the current user).

## Fully standalone

The pet depends on nothing else on the machine: it only reads its own `pet-config.json` /
`pet-state.json` and only talks to the official provider APIs. No database, no other
application's folders; uninstalling means deleting the folder.

## Quick start

1. Build with `build.cmd` (or grab `balance-pet.exe` from Releases).
2. Run `balance-pet.exe`. `pet-config.json` is created on first launch.
3. Right-click the pet, open the settings card and add your API keys under "数据源".
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
dotnet build src\DshPet.csproj -c Release   # optional, for SDK users
```

## License

[MIT](LICENSE). No character artwork is bundled; drop your own PNGs into `roles\`.
