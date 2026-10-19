# 贡献指南

欢迎 PR。几个约定：

- 代码保持 **C# 5 语法**（`build.ps1` 使用 Windows 自带的旧版编译器，这是「零依赖构建」的前提）：
  不用字符串插值、`?.`、表达式体成员等新语法。
- 源文件使用 UTF-8（无 BOM），界面文案中文即可。
- 新增余额平台时：
  1. 在 `src/Providers.cs` 的 `Builtin()` 里加定义，并在 `EndpointFor` / `Parse` 里补查询与解析；
  2. 若无公开余额接口，请把 `HasBalance` 设为 `false` 并在 `Note` 里写清楚去哪查；
  3. 在 README 的「余额接口」表格补一行。
- 提交信息：中文、一句话说清改了什么（必要时用分号串起同一主题的几项），不要写"update"之类。
- 不要提交下列内容（`.gitignore` 已覆盖，PR 里出现会被打回）：
  - 个人配置与密钥：`pet-config.json`、`pet-state.json`、`quips.json`；
  - 第三方素材：`roles\`、`props\` 里的任何图片（版权原因，仓库不附带任何立绘）；
  - 构建产物与诊断输出：`*.exe`、`dist\`、`pet-log.txt`、`*-report.txt`。
- CI（GitHub Actions）会跑 `build.ps1` + `--self-test` + `dotnet build src/BalancePet.csproj`，
  本地至少先跑 `.\build.ps1` 与 `.\balance-pet.exe --self-test` 再提交。
- 提交前请跑一遍：

  ```powershell
  .\build.ps1
  .\balance-pet.exe --self-test
  .\balance-pet.exe --smoke 4
  ```

  并确认控制台输出与 `%APPDATA%\BalancePet\` 下的 `self-test-report.txt` / `smoke-report.txt` 正常。
