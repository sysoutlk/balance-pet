# 贡献指南

欢迎 PR。几个约定：

- 代码保持 **C# 5 语法**（`build.ps1` 使用 Windows 自带的旧版编译器，这是「零依赖构建」的前提）：
  不用字符串插值、`?.`、表达式体成员等新语法。
- 源文件使用 UTF-8（无 BOM），界面文案中文即可。
- 新增余额平台时：
  1. 在 `src/Providers.cs` 的 `Builtin()` 里加定义，并在 `EndpointFor` / `Parse` 里补查询与解析；
  2. 若无公开余额接口，请把 `HasBalance` 设为 `false` 并在 `Note` 里写清楚去哪查；
  3. 在 README 的「数据源」表格补一行。
- 提交前请跑一遍：

  ```powershell
  .\build.ps1
  .\dsh-pet.exe --self-test
  .\dsh-pet.exe --smoke 4
  ```

  并确认控制台输出与 `self-test-report.txt` / `smoke-report.txt` 正常。
- 不要提交个人配置：`pet-config.json`、`pet-state.json`、`roles\` 内的图片都已在 `.gitignore` 中。
