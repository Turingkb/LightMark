# LightMark

轻量 Windows Markdown 编辑器，使用 C# / WPF 原生控件、Markdig 解析。无浏览器进程、无服务端、无遥测。Windows 10/11，需系统 .NET Framework 4.8。当前 1.0.4 版的 `app/LightMark.exe` 内嵌全部解析依赖，可独立复制运行。

## 文件位置

- `app/LightMark.exe`：日常使用的固定入口，仅保留最新版。
- `src/`、`tests/`、`scripts/`：源码、测试、构建工具。
- `build/`：测试与测量结果；`build/package/LightMark-portable.zip` 是最新便携包。
- `.cache/`：可复用的依赖下载缓存。历史版本由 Git 管理，本地不保留版本号目录和旧安装包。

## 运行与默认打开方式

双击 `app/LightMark.exe`，或运行 `LightMark.exe "D:\笔记\我的文档.md"`。打开文件、最近文档和使用指南时默认仅阅读，编辑区及格式工具隐藏；点击右上角“编辑”或“分栏”再修改。主动新建文档、恢复草稿会显示编辑区。

左侧为可折叠的树形目录，以 H1–H6 标记、缩进、字号和字重区分层级；标题缺级时挂在最近的上级标题下。支持拖入文件、目录跳转、查找替换、常用格式快捷键和列表续写。当前查找定位在源码中，找到时会切换到分栏。

1.0.2 起在创建窗口前设置固定任务栏应用标识 `Turingkb.LightMark`，多个文档窗口可合并到同一任务栏图标组。最终显示方式遵循 Windows 的任务栏合并设置。升级后先关闭旧版窗口，再打开新版；如果曾固定旧版图标，需要取消固定并重新固定新版。[Windows 应用标识接口](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-setcurrentprocessexplicitappusermodelid)。

将 EXE 放到长期保留的目录，选择 **文件 → 注册为 Markdown 打开方式**。然后在 Windows 默认应用设置里搜索 `.md`，选择 LightMark；也可右键 Markdown 文件 → 打开方式 → 选择其他应用 → 找到 `LightMark.exe` → 始终。`.markdown`、`.mdown`、`.mkd` 可单独设置。

注册仅作用于当前用户，不需管理员权限，也不篡改 UserChoice。[Windows 默认应用说明](https://support.microsoft.com/en-us/windows/apps/change-default-apps-in-windows)。移动 EXE 后需重新注册。需要移除注册时运行发行目录中的 `unregister.ps1`，并在系统设置中选定替代程序。

## 深浅色模式

首次使用以及旧设置升级后默认深色。通过右上角“浅色模式 / 深色模式”按钮，或“外观”菜单切换；选择立即保存到 settings.xml，下次启动恢复。源码、预览、代码块、表格、目录、菜单、滚动条及数学公式均随主题变化。切换不修改文件内容，阅读位置尽量保留。文件打开/保存和提示框属于系统界面，外观遵循 Windows；标题栏在系统支持时适配。

## 保存与恢复

- 新文件使用无 BOM 的 UTF-8。读取 UTF-8、带 BOM 的 UTF-8/UTF-16/UTF-32；非 UTF-8 的中文文件按严格 GB18030 解码。状态栏显示编码，保存不可表示的字符时会报错并保留编辑内容。
- 保存保留原编码、BOM 和首个换行的风格。混合换行会统一为该风格。通过同目录临时文件和原子替换保存；磁盘文件变化会提示，禁止静默覆盖。
- 未保存内容在停止输入约 3 秒后写到 `%LOCALAPPDATA%\LightMark\Recovery`。异常退出后用“文件 → 恢复未保存的草稿”找回，恢复为新文档再另存为。恢复草稿不附带原路径，避免覆盖原文件。正常保存或明确放弃编辑后清除当前备份；手动恢复过的旧草稿仍保留，可自行删除。
- 窗口尺寸、字号和最近文件位于 `%LOCALAPPDATA%\LightMark\settings.xml`。多个窗口关闭时按最后写入的设置保留最近记录。
- 可选环境变量 `LIGHTMARK_DATA_DIR` 可将设置和草稿放到指定目录；自动化测试和性能测量用它隔离用户状态。

## 预览范围

支持 CommonMark、表格、任务列表、删除线、本地 PNG/JPEG/GIF/BMP 图片。网页链接仅在点击时用系统浏览器打开；本地 Markdown 链接在编辑器内打开。其他文件链接不会执行。页内锚点按小写标题和空格转短横线匹配，重复标题追加编号。

图片不会从网络或 UNC 路径自动加载。本地图片上限 10 MB。支持 `$…$` 行内公式和 `$$…$$` 独立公式，使用 WPF-Math 离线矢量排版分式、上下标、根号、希腊字母、求和、积分及矩阵。每次预览最多排版 128 个公式，单式限 4096 字符及 48 层嵌套；未知命令或超限公式保留原文并附提示。不支持完整 LaTeX 宏包或自定义宏。HTML 按源码显示；Mermaid 和脚注暂无专用渲染。滚动同步按源码滚动比例单向同步到预览，可在视图菜单关闭。文件上限 8 MB；预览上限 150,000 字符，最多转换 3,000 个块和 30,000 个行内节点，保存保留完整源码。当前为单文档窗口，可启动多个窗口。

## 开发

无需 SDK，使用 Windows 自带的 .NET Framework 编译器和 PowerShell：

```powershell
./scripts/build.ps1          # 首次需要连接 NuGet 官方源
./scripts/build.ps1 -Offline # 已缓存依赖时离线构建
./scripts/test.ps1           # 文件安全和 WPF 渲染集成测试
./scripts/measure.ps1        # 三次窗口启动与内存测量
```

`src/` 是应用，`tests/` 是测试，`scripts/` 是构建和验证入口。依赖精确版本、官方包 SHA-256 与许可证记录于 `dependencies.json`。最新版程序位于 `app/`，测试输出位于 `build/`，均不纳入 Git。Markdig 负责语法解析，WPF 渲染层不依赖第三方 HTML/浏览器控件。

源码仓库：[Turingkb/LightMark](https://github.com/Turingkb/LightMark)（私有）。本地项目已配置提交身份，稳定版本保留在 main，独立功能在功能分支开发。构建信息与 EXE SHA-256 写到 `app/build-info.json`。测试与实际性能记录见 `VALIDATION.md`。

## 当前未完成的部分

核心文件读写、阅读预览、目录导航和草稿恢复已实现。以下是明确剩余项，不代表全部属于当前版本的必需功能：

- **验收待补**：真实桌面的鼠标/快捷键完整流程、中文输入法候选框、高 DPI/跨屏、实际设置与撤销系统默认关联、强制终止进程后的恢复流程。自动化已覆盖文件完整性和真实控件行为，桌面截图工具在上一轮超时。
- **阅读体验待增强**：直接在预览中查找并高亮（目前定位源码）、目录随滚动自动高亮、目录折叠状态记忆。基本阅读和手动目录导航已可用。
- **可选扩展未实现**：代码语法高亮、Mermaid、脚注专用排版，以及打印/PDF 导出、多标签页。
- **大文档仍有限制**：最大打开 8 MB，预览前 150,000 字符并限制节点数量。完整源码仍可保存。
