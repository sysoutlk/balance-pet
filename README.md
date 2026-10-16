# DSH Pet

把 DeepSeek 余额做成一只真正的 Windows 桌面宠物。

它会在气泡里显示余额、今日消耗、**峰谷时段与切换倒计时**、近 7 天花费柱状图；平时会自己散步、
贴边、做小动作，可以整只拎起来扔出去，还能分身。余额数据不限于 DeepSeek：Kimi（月之暗面）、
硅基流动、OpenRouter（覆盖 GPT / Claude / Gemini / Grok）都支持直连查询，其他平台可以走
「自定义接口」。

![screenshot](assets/screenshot.png)

## 特性

- **编译版，不是脚本**：C# + WPF 单文件 exe（约 180 KB），无 Node / Python / PowerShell 依赖，
  空闲时渲染循环自动降频到 ~4 fps，动作时才升到 ~25 fps。
- **零构建依赖**：`build.cmd` 使用 Windows 自带的 C# 编译器直接出包，不需要安装 .NET SDK；
  仓库同时提供 `src/DshPet.csproj` 供 IDE / `dotnet build` 使用。
- **峰谷对齐官方口径**：高峰为北京时间周一至周五（不含中国法定节假日）9:00-12:00、14:00-18:00，
  其余时段（含周末、节假日、调休上班的周末）均为空闲，价格是高峰的一半。
- **多数据源**：同时查询多个平台，气泡里主平台大号显示、其他平台小字列出；主平台可以切换。
- **安全存储**：API Key 使用 Windows DPAPI 加密（只限当前用户可解密）。
- **完整桌宠行为**：散步 / 贴边 / 作息 / 台词开关、8 种动作、Ctrl+滚轮无极缩放、拖拽抛出、分身。

## 快速开始

1. 构建：双击 `build.cmd`（或下载 Release 中的 `dsh-pet.exe`）。
2. 双击 `dsh-pet.exe` 启动桌宠。首次运行会在同目录生成 `pet-config.json`。
3. 右键桌宠打开设置卡片，在「数据源」里填各平台 API Key 并点「应用」。
4. 可选：运行 `scripts\install-desktop-shortcut.ps1` 创建桌面快捷方式；
   运行 `scripts\install-autostart.ps1` 加入开机自启。

![settings](assets/screenshot-settings.png)

### 交互

| 操作 | 效果 |
| --- | --- |
| 单击 | 余额气泡（含峰谷倒计时与 7 天柱状图） |
| 双击 | 随机动作 |
| 右键 | 设置卡片 |
| 拖拽松手 | 移动 / 甩出去 |
| Ctrl + 滚轮 | 无极缩放 |

### 命令行

| 参数 | 说明 |
| --- | --- |
| 无参数 | 启动桌宠（重复启动会唤醒已有实例的气泡） |
| `--settings` | 启动并打开设置卡片 |
| `--demo` | 演示模式：气泡常显 + 自动动作 |
| `--smoke [秒]` | 启动后在 N 秒自动退出并写 `smoke-report.txt` |
| `--self-test` | 不开窗口，检查数据源并写 `self-test-report.txt` |
| `--make-icon <路径>` | 把内置小鲸鱼导出为多尺寸 `.ico` |

## 数据源

| 平台 | 接口 | 说明 |
| --- | --- | --- |
| DeepSeek | `GET /user/balance` | 官方余额接口，含赠金 / 充值拆分 |
| Kimi 月之暗面 | `GET /v1/users/me/balance` | 官方余额接口（现金 + 代金券）；国际站改 baseUrl 为 `https://api.moonshot.ai` |
| 硅基流动 SiliconFlow | `GET /v1/user/info` | 官方接口，含充值 / 赠送 |
| OpenRouter | `GET /api/v1/credits` | 美元额度，可覆盖 GPT / Claude / Gemini / Grok |
| 自定义接口 | 任意 | 填 URL、认证方式（Bearer / x-api-key / 无）、JSON 路径 |

以下平台**没有公开的 API Key 余额接口**，设置卡片里会注明，需要用官方控制台查看；
有网关或中转的可以走「自定义接口」：
通义千问 DashScope、豆包（火山方舟）、智谱 GLM、OpenAI、Anthropic Claude、Google Gemini、xAI Grok。

「自定义接口」的 JSON 路径示例：`data.balance`、`balance_infos[currency=CNY].total_balance`、
`items[0].balance`；留空表示响应体本身就是数字。URL 里可以用 `{key}` 占位（适合把 key 放在
查询参数里的接口）。

### 完全独立

桌宠不依赖任何其它软件：只读取自己的 `pet-config.json` / `pet-state.json`，只通过各平台
官方接口查询余额。不使用数据库、不读取别的程序的目录，卸载就是删掉文件夹。

## 峰谷规则

来源：DeepSeek 官方定价页（北京时间）。

- 高峰：周一至周五（不含中国法定节假日）9:00-12:00、14:00-18:00
- 空闲：其余所有时段，包括周末与法定节假日全天；调休上班的周末也按空闲计费
- 空闲价格为高峰价格的一半

节假日列表内置了 2026 年版本，可以在 `pet-config.json` 里用 `peakHolidays` 覆盖
（`["2026-01-01", ...]`）。

## 角色形象

仓库**不附带任何角色立绘**（版权原因）。默认使用内置的矢量小鲸鱼；把喜欢的 PNG 图片放进
`roles\` 目录，就会出现在设置卡片的外观列表里。详见 [roles/README.md](roles/README.md)。

## 配置文件

`pet-config.json` 首次运行时自动生成，常用项：

| 键 | 说明 |
| --- | --- |
| `role` | 角色文件名（`roles\` 下）；留空使用内置小鲸鱼 |
| `scale` / `opacity` | 大小 / 不透明度 |
| `primaryProvider` | 气泡主平台（`deepseek` / `moonshot` / `siliconflow` / `openrouter` / `custom`） |
| `providers.<id>.on` / `providers.<id>.apiKey` | 各平台开关与 Key（DPAPI 加密后存储） |
| `walk.*` `snap.*` `throw.*` `sleep.*` `actions.*` `quips.*` | 行为、物理与台词配置 |
| `maxClones` | 分身数量上限 |
| `alert.below` | 余额预警阈值（0 = 关闭），设置卡片里也能改 |
| `alert.budget` | 今日预算（0 = 关闭），设置卡片里也能改 |
| `peakHolidays` | 自定义节假日列表 |

## 构建

```powershell
.\build.ps1              # 用系统自带 csc 构建 dsh-pet.exe（无需 SDK / 联网）
.\build.ps1 -SkipIcon    # 跳过图标嵌入
```

也可以使用 .NET SDK（CI 同时验证两条路径）：

```powershell
dotnet build src\DshPet.csproj -c Release
```

想换图标：运行 `scripts\make-icon.ps1 -Source roles\你的图.png` 生成 `assets\app.ico`，
再重新 `build.cmd` 即可。

## 许可

[MIT](LICENSE)
