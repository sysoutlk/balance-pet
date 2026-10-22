# 余额桌宠（Balance Pet）

把各家 API 的余额做成一只真正的 Windows 桌面宠物。

它会在气泡里显示余额、今日消耗、**计价时段与切换倒计时**（主接口是 DeepSeek 时）、近 7 天花费柱状图；平时会自己散步、
贴边、做小动作，可以整只拎起来扔出去，还能分身。余额数据不限于 DeepSeek：Kimi（月之暗面）、
硅基流动、OpenRouter（覆盖 GPT / Claude / Gemini / Grok）都支持直连查询，其他平台可以走
「自定义接口」。

## 特性

- **编译版，不是脚本**：C# + WPF 单文件 exe（约 360 KB），无 Node / Python / PowerShell 依赖；
  帧率可调（默认空闲 4 fps、动作 25 fps），上限自动取当前显示器刷新率。
- **零构建依赖**：`build.cmd` 使用 Windows 自带的 C# 编译器直接出包，不需要安装 .NET SDK；
  仓库同时提供 `src/BalancePet.csproj` 供 IDE / `dotnet build` 使用。
- **计价时段对齐官方口径**：标准时段为北京时间周一至周五（不含中国法定节假日）9:00-12:00、14:00-18:00，
  其余时段（含周末、节假日、调休上班的周末）均为优惠时段，价格是标准价的一半。
- **多数据源**：同时查询多个平台，气泡里主平台大号显示、其他平台小字列出；主平台可以切换。
- **安全存储**：API Key 使用 Windows DPAPI 加密（只限当前用户可解密）。
- **完整桌宠行为**：散步 / 贴边（滑出屏幕只露头）/ 作息 / 台词开关、**26 种动作**、
  多只互撞会带动、Ctrl+滚轮无极缩放、拖拽抛出、自动分身。
- **内容可扩展**：单张 PNG 自动建立 2D 网格（呼吸 / 歪头 / 眨眼 / 看鼠标，不需要拆层），
  静态图一键转逐帧动画、特效动作（放烟花 / 比心 / 闪光 / 撒花）、
  台词库导入导出、内置每动作 5 至 16 句台词。

## 快速开始

1. 安装：双击 `balance-pet-setup.exe` 选择上级目录，安装器会自动创建 `BalancePet` 子目录并安装
   （默认 `%LOCALAPPDATA%\Programs\BalancePet`）；桌面和开始菜单快捷方式可以分别选择。
   也可以直接使用便携版 `balance-pet.exe`。
2. 从快捷方式或 `balance-pet.exe` 启动桌宠。运行数据默认保存在 `%APPDATA%\BalancePet`，不会写到 exe 所在目录；想完全便携，在 exe 同目录放一个空的 `portable.txt`。
3. 右键桌宠打开设置卡片，在「余额接口」里填各平台 API Key 并点「应用」。
4. 可选：运行 `scripts\install-autostart.ps1` 加入开机自启；便携用户也可以用 `scripts\install-desktop-shortcut.ps1` 建快捷方式。

### 交互

| 操作 | 效果 |
| --- | --- |
| 单击 | 余额气泡（含计价时段倒计时与 7 天柱状图） |
| 右键 | 设置卡片 |
| 拖拽松手 | 移动 / 甩出去 |
| Ctrl + 滚轮 | 缩放鼠标下这一只桌宠（每只独立；同角色新分身会沿用该角色上次的大小） |

### 命令行

| 参数 | 说明 |
| --- | --- |
| 无参数 | 启动桌宠（重复启动会唤醒已有实例的气泡） |
| `--settings` | 启动并打开设置卡片 |
| `--demo` | 演示模式：气泡常显 + 自动动作 |
| `--smoke [秒]` | 启动后在 N 秒自动退出并写 `smoke-report.txt` |
| `--self-test` | 不开窗口，检查余额/用量接口并写 `self-test-report.txt` |
| `--make-icon <路径>` | 把内置小鲸鱼导出为多尺寸 `.ico` |
| `--make-anim <图片>` | 把一张静态图生成逐帧动画角色（可选 `--name` / `--width`） |
| `--hardware-render` | 清除软件渲染回退标记，重新尝试硬件渲染 |

## 余额接口

| 平台 | 接口 | 说明 |
| --- | --- | --- |
| DeepSeek | `GET /user/balance` | 官方余额接口，含赠金 / 充值拆分；可选平台 Token 读取精确的按 Key 消耗与账户累计消耗 |
| Kimi 月之暗面 | `GET /v1/users/me/balance` | 官方余额接口（现金 + 代金券）；国际站改 baseUrl 为 `https://api.moonshot.ai` |
| 硅基流动 SiliconFlow | `GET /v1/user/info` | 官方接口，含充值 / 赠送 |
| OpenRouter | `GET /api/v1/credits` | 美元额度，可覆盖 GPT / Claude / Gemini / Grok |
| 自定义接口 | 任意 | 填 URL、认证方式（Bearer / x-api-key / 无）、JSON 路径 |

### 套餐额度（5 小时 / 7 天窗口）

对订阅制编程套餐来说，「余额」没有意义，重要的是窗口用量。桌宠为此内置了一组**套餐额度**来源，
按 5 小时 / 每周窗口显示百分比（点击气泡到「套餐额度」页，或看设置卡片里的状态行）。
每个窗口会单独显示重置时间：优先显示绝对时间和剩余倒计时；接口没有返回重置字段时会明确标注
“接口未提供”，不会只留下一个百分比：

| 平台 | 接口 | 说明 |
| --- | --- | --- |
| Kimi Code | `GET api.kimi.com/coding/v1/usages` | kimi.com 编程套餐，用套餐 Key（`sk-kim…`）；请求头对齐官方 KimiCLI |
| Codex（ChatGPT 套餐） | `GET chatgpt.com/backend-api/wham/usage` | 自动复用本机 `~/.codex/auth.json` 登录；也可手动粘贴 access token |
| 智谱 GLM 编程套餐 | `GET open.bigmodel.cn/api/monitor/usage/quota/limit` | 5 小时 / 每周额度（Key 不加 Bearer 前缀） |
| Z.AI（GLM 国际站） | `GET api.z.ai/api/monitor/usage/quota/limit` | 同上，国际站域名 |
| MiniMax 编程套餐 | `GET api.minimaxi.com/v1/api/openplatform/coding_plan/remains` | Coding Plan 额度 |
| OpenCode Go | `GET opencode.ai/zen/go/v1/usage` | $10/月订阅，美元额度三窗口 |
| 自定义用量接口 | 任意 | 给 URL（可选 JSON 路径），会自动识别 `limit / used / percentage / 重置时间` 等字段 |

火山方舟（豆包）的 Agent / Coding Plan 走的是需要 AK/SK 签名的控制面 OpenAPI，暂未内置；
如果有网关把它包装成普通 JSON，用「自定义用量接口」即可接入。

### DeepSeek 精确消耗

DeepSeek 的 API Key 余额接口**不提供**按 Key 的用量。要统计“当前 API Key 今日消耗 / 全部 Key
今日消耗 / 账户累计消耗”，需要在设置卡片「余额接口 → DeepSeek」里另外填写平台 Token：

1. 在浏览器登录 `platform.deepseek.com`；
2. 从浏览器开发者工具读取 `localStorage.userToken` 的 `value`；
3. 粘贴到「平台 Token」并点击「应用」。

Token 只保存在本机 `pet-config.json`，使用 Windows DPAPI 加密。平台 Token 过期后精确消耗会停止
更新，但官方 API Key 余额查询不受影响。未填写平台 Token 时，仍显示本机通过余额变化追踪的近似值。
如果程序在午夜附近仍在采样，新的一天会继承最后余额；如果跨夜停机，则从恢复时余额重新起算，
避免把前一天的晚间消耗误算到今天。气泡会标注本机统计的起始时间，停机期间的变化仍无法还原。

填写平台 Token 后，趋势图会直接使用平台最近 30 天的按日成本，7 / 14 / 30 天范围均可在气泡顶部
点击切换；没有平台 Token 时，图表会明确标记为“本机近 N 天”。

「显示当前 Key 消耗」开关控制气泡里是否展示当前 Key 行；全部 Key 今日消耗和账户累计消耗仍会显示。

### 网络与代理

查询默认**直连**（.NET 默认会继承系统代理，残留的代理设置会让 chatgpt.com 之类请求卡住）。
如果出口本身受限（例如 OpenAI 对某些线路返回 `HTTP 451`），在设置卡片「余额接口」的
**HTTPS 代理**里填一个可用代理（如 `http://127.0.0.1:7890`）即可，清空则恢复直连；
对应配置项是 `network.proxy`。

以下平台**没有公开的 API Key 余额接口**，设置卡片里会注明，需要用官方控制台查看；
有网关或中转的可以走「自定义接口」：
通义千问 DashScope、豆包（火山方舟）、智谱 GLM、OpenAI、Anthropic Claude、Google Gemini、xAI Grok。

「自定义接口」的 JSON 路径示例：`data.balance`、`balance_infos[currency=CNY].total_balance`、
`items[0].balance`；留空表示响应体本身就是数字。URL 里可以用 `{key}` 占位（适合把 key 放在
查询参数里的接口）。

### 完全独立

桌宠不依赖任何其它软件：只读取自己的 `pet-config.json` / `pet-state.json`，只通过各平台
余额接口和你主动授权填写的平台用量接口查询数据。不使用数据库、不读取别的程序的目录，
卸载就是删掉文件夹。

## 计价时段规则（DeepSeek 错峰优惠）

来源：DeepSeek 官方定价页（北京时间）。

- 标准时段：周一至周五（不含中国法定节假日）9:00-12:00、14:00-18:00
- 优惠时段：其余所有时段，包括周末与法定节假日全天；调休上班的周末也按优惠计
- 优惠价格为标准价格的一半

只有主接口是 DeepSeek 时才会出现「计价时段」页与倒计时；换成 Kimi、Qwen 等平台
时不显示，避免出现和该平台无关的字。

节假日列表内置了 2026 年版本，可以在 `pet-config.json` 里用 `peakHolidays` 覆盖
（`["2026-01-01", ...]`）。

## 角色与动画

内置的小鲸鱼是矢量绘制并带真实动画的（摆尾、眨眼、水花），不需要任何图片。

仓库**不附带任何角色立绘**（版权原因），但支持把自己的素材放进 `roles\` 目录，四种形式：

| 形式 | 做法 |
| --- | --- |
| GIF | 直接放 `.gif`（例如 `roles\小鲸鱼·rua.gif`），按 GIF 自带帧率逐帧播放；切换状态（走路/睡觉/动作）时改变播放速度 |
| 序列帧目录 | `roles\小喵\` 里放 `1.png 2.png 3.png ...`；也可以再分子目录 `idle\`、`walk\`、`wave\` 等，让每个状态播各自的帧 |
| 雪碧图 | 放一张 PNG，并在 `pet-config.json` 的 `roles.sheets` 里描述行列与每个状态的帧范围，例如 `"cat.png": {"cols":4,"rows":5,"fps":8,"states":{"idle":[0,0,3],"walk":[1,0,3],"wave":[2,0,3]}}` |
| 单张 PNG | 保持静态立绘，只能整体位移与形变（旧行为） |

可用状态名（用于子目录 / 雪碧图状态键）：`idle`、`walk`、`sleep`、`edge`（贴边收进去）、
`peek`（收起后探头），以及每个动作键；子目录里没有的状态会自动退回用根目录那套帧。
详见 [roles/README.md](roles/README.md)。

外观页的「选择图片或动图…」可直接导入 PNG / JPG / GIF / BMP（重名自动加序号并立刻换上）；每个自定义角色右上角都有删除按钮，确认后直接从 `roles\` 移除；在资源管理器里手动删掉文件后，切回外观页列表也会自动刷新。
外观页顶部会显示素材库总数，并可通过「打开素材目录」直接打开
`%APPDATA%\BalancePet\roles`，核对所有单图、动图和导入宠物包。

### 宠物包（自有格式）

如果一套角色有很多状态，推荐整理成宠物包：

```text
roles\my-pet\
├── pet.json
├── idle\     1.png 2.png ...
├── walk\     1.png 2.png ...
├── click\    1.png 2.png ...
└── drag\     1.png 2.png ...
```

`pet.json` 可以省略；省略时会自动把 `idle` / `walk` / `click` / `drag` 等状态目录识别成动画。
也可以显式描述帧率、循环和状态池：

```json
{
  "name": "My Pet",
  "states": {
    "idle": ["idle"],
    "walk": ["walk"],
    "click": ["click"],
    "drag": ["drag"]
  },
  "clips": {
    "idle":  { "path": "idle",  "fps": 8,  "loop": true },
    "walk":  { "path": "walk",  "fps": 10, "loop": true },
    "click": { "path": "click", "fps": 12, "loop": false },
    "drag":  { "path": "drag",  "fps": 8,  "loop": true },
    "wave":  { "path": "wave",  "fps": 12, "loop": false }
  },
  "actions": {
    "wave": {
      "name": "挥手",
      "clips": ["wave"],
      "seconds": 2.4,
      "weight": 20,
      "walking": true,
      "category": "idle"
    }
  }
}
```

`actions` 是宠物包自己的动作表。导入后，自动动作池、动作按钮、动作预览和实际播放都会使用
这里声明的动作，不再套用软件内置的姿态动作：

- `clips`：动作可随机使用的一个或多个片段；片段必须存在于 `clips`。
- `seconds`：单次动作时长，默认 `2.0` 秒。
- `weight`：是否默认进入自动动作池；设为 `0` 时只出现在手动动作按钮中。
  进入动作池后所有动作等概率随机，不再按权重改变概率。
- `walking`：散步途中是否允许叠加这个动作。
- `category`：动作池界面的分组，可用 `idle` / `move` / `fx`。

大尺寸素材会在显示时自动按比例缩到 220px 基准高度，原始像素不会直接变成桌面尺寸。
需要在包内固定基准尺寸时可以覆盖：

```json
"display": { "height": 180, "width": 240 }
```

`width` / `height` 是等比适配的上限，不是拉伸尺寸；设置页里的单只缩放会继续作用在这个基准上。
如果素材本身是裁切过的小尺寸预览图，可再加 `"upscale": true`，让 `width` / `height`
作为等比放大的目标框使用。

动作数量没有固定上限，几十个动作都按同一张表调度。
`idle`、`walk`、`drag`、`click`、`sleep`、`edge`、`peek`、`fall`、`thrown`、`bump` 是行为状态，
直接由 `states` 提供对应片段。没有写在 `actions` 里的额外状态目录会自动成为动作，
默认时长 2 秒、权重 10。

设置卡片「外观」页有 **「选择素材包或总目录…」**：可以选单个宠物包或动画素材目录，
也可以直接选包含多套素材的上级总目录；程序会扫描全部可识别子包并逐个转换、校验、
复制到 `roles\<id>\`。批量导入不会强行切换当前外观，完成后会显示成功和失败数量。
导入时会逐个片段验证图片能否解码；同名包会自动编号，校验或复制失败会清理未完成的目录。
播放器使用双缓冲交叉淡入；idle 池会按状态链继续挑下一段，
点击会优先播放一次 `click`，结束后回到当前状态。

导入器只识别本项目 `pet.json`、状态帧目录，以及通用的 XML 精灵、WebP / PNG 图集和
每个动作一个子目录的图片或单 GIF 素材。选择对应角色目录即可，不需要手工调整格式。

内置的中性兼容 Profile 还会按文件结构自动识别两类素材包：

- **分层 PNG 序列素材包**：支持多层前后景合成、帧延迟展开和可选动作目录。
- **WebM + 预览图素材包**：优先使用本机 `ffmpeg` 转换 WebM；没有时使用包内预览 GIF。

自动识别不依赖素材包目录名，界面只显示中性格式名称。

XML 精灵会按固定白名单转换 `idle`、`walk`、`drag`、`sleep`、`edge`、`peek` 等状态；
已知动作使用中性键和中文标签，未知动作使用 `action_01` 顺序编号，嵌入式互动和分裂类动作会跳过。
每个 XML 图片目录只导入一个角色，不会把同级的其它角色混进同一个动作池。

WebP / PNG 图集支持常见 8 列、192 x 208 单元布局，也能读取清单中的行列、单元尺寸和帧率。
导入时只提取有内容的帧，输出 `idle`、`walk`、`click` 和 7 个中性动作；方向预留行不会误播成普通动画。
动作文件夹会识别 `idle`、`walk`、`drag`、`click`、`sleep`、`edge`、`peek` 和常见动作名，
未知目录统一编号为 `action_01`，因此目录名不会直接进入界面。

仓库不附带任何第三方宠物包或动画素材；包内容、授权和署名由使用者自行负责。

命令行也可以导入，成功时会输出安装后的角色 id：

```powershell
.\balance-pet.exe --import-pack D:\Pets\my-pet
.\balance-pet.exe --import-pack D:\Pets
.\balance-pet.exe --import-pack D:\Pets\my-pet --activate
```

`--import-pack` 指向总目录时会批量导入扫描到的全部子包。加 `--activate` 会在导入成功后
把最后一个成功导入的包设为当前宠物。

### 一张静态图 → 整套逐帧动画

不想画几十帧也能让贴图"动起来"：设置卡片「外观」页点 **「由图片生成动画…」**，
选一张 PNG，程序会按 25 个状态（空闲 / 走路 / 睡觉 / 每个动作 / 被撞）程序化生成逐帧动画，
写进 `roles\<图片名>-动图\`，然后直接给桌宠换上。命令行等价写法：

```powershell
.\balance-pet.exe --make-anim roles\你的角色.png --name 我的动图 --width 256
```

生成的帧就是普通 PNG（空闲帧在角色目录根下，其余按状态分子目录），播放帧率取
`pet-config.json` 的 `roles.fps`（默认 8）。原理是把整张图切成横条做挤压/拉伸/摇摆/扭转，
越靠下的横条相位越滞后——所以有呼吸感和果冻感：

- 想要更细腻：直接逐帧精修这些 PNG（或用 AI 生成的动作帧替换掉某个状态的目录）；
- 只换某一个状态：把该状态的目录内容替换掉即可，其余状态继续用生成的；
- 真正的"换姿势"（抬腿、挥手、扭头）需要真实素材：要么自己画/委托画师，要么用
  图生视频/图生帧的 AI 工具生成动作帧，再按上面的目录结构放进来——引擎已经全部支持。

## 桌宠行为

- **散步 / 贴边 / 边缘隐藏**：贴到任意屏幕边缘静置一段时间后，桌宠会**整只滑到屏幕外**，
  只在边上留一块——而且**不管贴哪条边，留下的都是"头"**：贴左右边时它会**侧躺 90°**滑出去，
  贴上边时**倒过来 180°**，贴下边保持原样，所以屏幕上永远是一颗脑袋探出来。
  露出多少按角色尺寸取比例（`edgeHide.sliverRatio`，默认 0.32 = 留 32%，设置卡片「行为」页有滑杆），
  滑到位会冲过头一点再回弹，收好之后还会隔几秒轻轻探一下头；鼠标拖一下就滑回来。
  全程**不做压缩变形**——把贴图压扁成饼一眼就假。角色动画可以为这个动作配
  `edge`（收进去的过程）与 `peek`（收起后/探头）两组帧。
  内置角色与所有导入宠物包共用同一套吸附距离、左右侧躺、顶部翻转、回弹和露出比例规则。
  气泡尾巴使用平滑三角填充与两侧外轮廓，并会跟随宠物中心横向移动；气泡被屏幕边界推回时
  仍能正确指向宠物，不会固定留在卡片正中。
  左右侧躺时角色会重新居中，不会被窗口矩形裁掉；进入贴边状态后会立即停止所有动作，
  自动动作和散步也不会启动。点击露出的部分只弹出余额气泡，不会把宠物拖出来。
  默认**只有手动把桌宠拖到边缘并松手**才会贴边；设置卡片「行为」页的「自动贴边」打开后，
  散步、动作或启动时靠近边缘也会自动吸附。
- **持续散步**：「行为」页的「持续散步」开启后，桌宠会一直溜达（走完一段只歇 1 至 4 秒）；
  散步途中也会从自动动作池里挑动作（挥手、点头、烟花等）叠加播放，不会因为做动作而停下。
  关闭开关后立刻停在原地。
- **自动分裂**：会自己慢慢分裂出新个体，可在设置里关掉或设置数量上限；
  `0` 表示不设置用户上限，但内部最多保留 20 只，避免无限创建窗口拖垮程序。
  「分身」页可选择新分身使用随机角色、跟随主宠或指定某个角色；
  「循环涨落」到上限后逐只回落到下限，停一下再逐只涨回上限；收回的分身会记住原来的
  角色、大小和位置，涨回时原样恢复，不会随机换一只。
- **点击翻页**：单击依次切换「余额 → 今日消耗 → 计价时段（DeepSeek）→ 余额接口」视图；
  双击不会打开设置；右键任意桌宠或在面板上右键都能收起（Esc 同样有效）；
  Ctrl+滚轮只缩放鼠标下那一只，
  设置卡片里的「大小」调整当前这只，「全部」可同步给所有桌宠。
- **渲染自愈**：如果显卡驱动或 WPF 媒体内存导致渲染线程失效，程序会先关闭所有透明窗口，
  记录 `render-software.txt`，自动用软件渲染重启；需要重新尝试硬件渲染时运行
  `balance-pet.exe --hardware-render`。
- **动作库**：26 种动作（蹦跳、转圈、打滚、点头、摇头、招手、鼓掌、伸懒腰、打哈欠、害羞、
  吓一跳、左看右看、趴一会、睡觉、踱步、探头、溜达、冲刺、跟随鼠标、打招呼、摇摆、连蹦，
  外加放烟花、比心、闪光、撒花四种特效动作），
  动作点播页以「自动动作」为主：勾选想让它自己演的动作池（支持全选/清空），只保留
  “单次时长”和“动作间隔”两个滑杆；选中任意动作后可以打开「一直执行所选动作」，它会在关闭设置页后连续重复，
  不再参与随机。未固定时从动作池等概率抽取，并尽量避免连续两次抽到同一动作。

### 特效动作

除了"同一张贴图做形变"，还有两层给内容加料的机制：

- **特效层（放烟花、比心、闪光、撒花）**：纯代码画的粒子，不需要任何素材。
  放烟花会先窜起一发火箭、在空中炸成一把彩色火花；比心是爱心往上飘；闪光是一圈星星眨眼；
  撒花是彩纸从头顶落下来。它们在「动作点播」里可以直接点，也进了随机动作池
  （`actions.pool`），想关掉整层就把 `fx.enabled` 设成 false。

## 目录与文件（便携式）

默认安装/绿色两用：运行数据放在 `%APPDATA%\BalancePet`（不会在 exe 旁边散落文件）；在 exe 同目录放一个空的 `portable.txt` 就切回完全便携模式，所有数据回到 exe 旁边。卸载 = 删除安装目录和 `%APPDATA%\BalancePet`。
除了 exe 与 `assets\app.ico`，**下面这些都不进仓库**（见 `.gitignore`）：

| 路径 | 是什么 | 是否入库 |
| --- | --- | --- |
| `balance-pet.exe` / `balance-pet-setup.exe` | 程序与安装包（`build.cmd` 生成） | 否 |
| `%APPDATA%\BalancePet\pet-config.json` | 你的配置与 API Key（DPAPI 加密，只限当前用户解密） | 否 |
| `%APPDATA%\BalancePet\pet-state.json` | 用量历史（本地统计用） | 否 |
| `%APPDATA%\BalancePet\roles\` | 角色素材（立绘/GIF/序列帧/雪碧图） | 否，版权自理 |
| `%APPDATA%\BalancePet\quips.json` | 可选台词包，启动时自动合并 | 否 |
| `%APPDATA%\BalancePet\pet-log.txt` / `*-report.txt` | 诊断日志与自检/冒烟报告 | 否 |
| `%APPDATA%\BalancePet\render-software.txt` | 渲染线程故障后的软件渲染回退标记 | 否 |
| `portable.txt`（exe 旁边） | 放了它就切到完全便携模式，以上数据全部改放 exe 旁边 | 否 |

## 隐私与版权

- **不收集、不上传任何数据**：没有遥测、没有统计上报；程序只直连你在设置里启用的
  官方余额/用量接口（以及你自填的代理）。
- **API Key 只存本机**：用 Windows DPAPI 按当前用户加密后写进 `pet-config.json`，
  且该文件已在 `.gitignore` 中，不会随仓库或发布包分发。
- **不附带任何角色/字体素材**：`roles\` 全部由使用者自备，仓库里没有任何
  第三方立绘、贴图或字体文件（内置小鲸鱼是代码绘制的矢量图形）。
  自带素材的版权与授权由使用者自行确认。
- **产品与接口名称仅用于兼容说明**：相关商标归各自权利人，不表示官方合作、授权或背书。
- **日志与报告只在本地生成**，同样被 `.gitignore` 忽略。

## 配置文件

`pet-config.json` 首次运行时自动生成，常用项：

布局预设会同时切换内容结构：`minimal` 隐藏完整图表与公共页脚，只显示标题、主值和一行摘要；
`compact` 用迷你图或双行时段；`wide` 使用左右分栏；`standard` / `dashboard` 使用完整页面。

| 键 | 说明 |
| --- | --- |
| `role` | 角色文件名（`roles\` 下）；留空使用内置小鲸鱼 |
| `scale` / `opacity` | 新桌宠的默认大小 / 全局不透明度 |
| `roleScales.<角色名>` | 每个角色上次调整的大小（同角色的新分身会沿用；`__mascot` 为内置小鲸鱼） |
| `rigRoles.<角色名>.mesh` / `.intensity` | 当前单图各自的网格动态开关 / 强度（0 至 2，默认 1） |
| `rigRoles.<角色名>.blink` / `.gaze` | 当前单图各自的眨眼 / 眼神跟随鼠标 |
| `rigRoles.<角色名>.faceX` / `.faceY` / `.faceW` / `.faceH` | 当前单图各自的眨眼区域与脸部位置 |
| `rig.*` | 旧版单图设置；没有 `rigRoles` 专属值时作为兼容默认值 |
| `primaryProvider` | 气泡主平台（`deepseek` / `moonshot` / `siliconflow` / `openrouter` / `qwen` / `doubao` / … / `custom`） |
| `providers.<id>.on` / `providers.<id>.apiKey` | 各平台开关与 Key（DPAPI 加密后存储） |
| `providers.deepseek.platformToken` | DeepSeek 平台登录 Token（DPAPI 加密，用于按 Key 精确成本） |
| `providers.deepseek.showKeyUsage` | 气泡是否显示当前 DeepSeek Key 的今日消耗（默认开启） |
| `providers.deepseek.usageRefreshSec` | 平台精确成本刷新间隔（默认 180 秒） |
| `bubble.trendDays` | 气泡趋势统计范围：7 / 14 / 30 天（默认 7） |
| `bubble.layout` | 气泡布局：`minimal` / `compact` / `standard`（原版）/ `dashboard` / `wide` / `custom` |
| `bubble.customWidth` / `bubble.customHeight` | 仅 `custom` 布局使用的固定卡片尺寸 |
| `ui.theme` | 设置界面自身主题：`system` / `light` / `dark`，不会改变气泡主题 |
| `bubble.theme` / `bubble.effect` | 气泡主题与动效；设置页的“风格”会一次应用一组推荐组合 |
| `bubble.effectReverse` | 仅反转流动、旋转、闪动、漂移的方向，不改变配色或明暗 |
| `walk.*` `snap.*` `throw.*` `sleep.*` `actions.*` `quips.*` | 行为、物理与台词配置 |
| `actions.pool` | 自动动作池（空闲与散步途中都会从中挑动作） |
| `actionPin.<角色名>.enabled` / `.action` | 当前角色是否固定执行一个动作，以及固定的动作键 |
| `actions.durationScale` | 单次动作时长倍率（0.5 至 3） |
| `actions.pauseMinSec` | 自动动作间隔（秒）；轻微自然变化由程序内部处理，不需要配置 |
| `clones.pauseMinSec` / `clones.jitter` | 分裂的基础间隔 / 间隔随机上浮比例（与动作间隔同一套算法） |
| `clones.roleMode` / `clones.role` | 新分身外观：`random` 随机、`current` 跟随主宠、`manual` 使用同一指定角色、`slots` 按槽位自选 |
| `cloneSlots.<槽位>` | `slots` 模式下的独立角色；槽位 1 对应第 2 只，依此类推；`@current` 表示跟随主宠 |
| `snap.distancePx` | 松手后离屏幕边多近才贴边吸附（默认 56px，行为页「吸附距离」可调） |
| `snap.auto` | 是否允许散步/动作/启动时自动吸附边缘（默认 false；关闭后只有手动拖到边缘才贴边） |
| `walk.pauseMinSec` / `walk.pauseMaxSec` | 持续散步每段之间的短歇时间（默认 1.5 至 4 秒） |
| `frames.activePerSecond` / `.idlePerSecond` | 动作/空闲帧率（上限自动取显示器刷新率） |
| `edgeHide.sliverRatio` | 贴边收起来后露出多少（按尺寸取比例，默认 0.32） |
| `fx.enabled` | 特效层总开关（放烟花 / 比心 / 闪光 / 撒花） |
| `maxClones` / `clones.max` | 分身数量上限；`0` 使用内部安全上限 20，所有值最多按 20 执行 |
| `alerts.<平台>.below` / `.budget` | 该平台的余额预警阈值 / 今日预算（0 或留空 = 不提醒），设置卡片「提醒」页里改 |
| `alerts.<套餐>.fiveHour` / `.sevenDay` | 套餐额度提醒：5 小时 / 7 天窗口用量达到多少百分比就提醒（0 或留空 = 不提醒） |
| `alert.balanceWarn` / `.budgetWarn` / `.quotaWarn` | 三类提醒的总开关 |
| `ui.title` | 设置卡片标题（默认「余额桌宠」） |
| `peakHolidays` | 自定义节假日列表 |

## 台词自定义

- 每个动作都有一池台词（`pet-config.json` 的 `quips.*`），触发时随机抽一句。
- 设置卡片「台词与互动」页有「导入台词库…」「导出台词库…」：导出一份 JSON 就是模板（中文动作名 → 台词数组），
  改完再导入即可；导入是**合并去重**，不会动你已有的台词，重复导入同一份也不会翻倍。
- 把一份 `quips.json` 直接放在 `balance-pet.exe` 旁边，启动时会自动合并进来（删掉文件就不再合并）。
- 键可以写中文动作名，也可以写英文动作键；顶层直接写一个字符串数组 = 「点击时说的话」。例如：

  ```json
  {
    "点击": ["点我干嘛？", "余额我盯着呢"],
    "蹦一下": ["蹦！"],
    "睡觉": ["呼……呼……"]
  }
  ```

  可用的中文名：点击 / 蹦一下 / 连蹦 / 转圈 / 打滚 / 摇摆 / 点头 / 摇头 / 招手 / 鼓掌 / 伸懒腰 /
  打哈欠 / 害羞 / 吓一跳 / 趴一会 / 睡觉 / 左看右看 / 踱步 / 探头 / 溜达 / 冲刺 / 跟随 / 撞人 / 打招呼。

## 构建

```powershell
.\build.ps1              # 用系统自带 csc 构建 balance-pet.exe（无需 SDK / 联网）
.\build.ps1 -SkipIcon    # 跳过图标嵌入
```

也可以使用 .NET SDK（CI 同时验证两条路径）：

```powershell
dotnet build src\BalancePet.csproj -c Release
```

想换图标：运行 `scripts\make-icon.ps1 -Source roles\你的图.png` 生成 `assets\app.ico`，
再重新 `build.cmd` 即可。

## 许可

[MIT](LICENSE)。变更记录见 [CHANGELOG.md](CHANGELOG.md)。
