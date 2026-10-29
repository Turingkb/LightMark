# LightMark 工作约定

Windows 原生轻量 Markdown 编辑器，C# 5 / .NET Framework 4.8 / WPF。使用系统自带 csc，不要求安装 SDK。

- `src/Program.cs` 是入口；`MainWindow` 只管理界面和命令，文件完整性放在 `DocumentFile`，Markdown 转换放在 `MarkdownView`。
- `scripts/build.ps1` 下载并校验固定的官方 NuGet 包，构建便携程序；`scripts/test.ps1` 运行文件安全和渲染集成测试。
- 修改后至少运行 `./scripts/test.ps1`；界面变更还应启动实际程序检查键盘操作和布局。
- 原始 Markdown 不得作为 XAML/HTML 执行。远程图片不自动加载；链接仅允许明确点击的 http/https、页内锚点和本地 Markdown。
- 保存必须保留编码、BOM 和换行风格；通过同目录临时文件原子替换；外部修改必须先提示，不得静默覆盖。
- 不改写 Windows UserChoice。注册仅作用于当前用户，默认应用通过系统界面由用户选择。
- `app/` 仅保留最新版便携程序，是固定的日常使用入口；`build/` 存测试、测量和最新压缩包；`.cache/` 缓存依赖。三者均不提交。
- 历史版本交给 Git 管理，不创建或保留 1.0.1、1.0.2 等版本号目录及旧压缩包。
- 依赖来源、版本和校验值以 `dependencies.json` 为准。
- 使用 software-project-lifecycle、code-writing-standards、software-testing-strategy 和 git-repository-workflow；仓库规则优先。
