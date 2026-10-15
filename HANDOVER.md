# 交接文档 / HANDOVER

> 目标读者：在**另一台机器**上接手本项目的开发者（含 AI 助手）。
> 本文只写「接手需要知道的事」，不重复 README 的项目介绍。
> 最后更新：对应提交 `56e668c`（2026-10-15）。

---

## 1. 一句话现状

Typedown 已从 **WPF + UWP XAML Islands (.NET Core 3.1)** 移植到 **WinUI 3 + .NET 8**，
可构建、运行、打包为**原生 ARM64**。本地已安装 `Typedown.Arm64 1.2.19.0 (Arm64)`，
两个高危 bug（关窗崩溃、文件选择器全失效）已修复并验证。**其余菜单项尚未逐项回归。**

```
仓库   : https://github.com/Ansherly/Typedown-WinUI   (public)
分支   : main
最新   : 56e668c  "Bump package version to 1.2.19"
上游   : https://github.com/byxiaozhi/Typedown  (MIT, Copyright (c) 2022 ZZF)
```

---

## 2. 这是「为什么」：上游无法出 ARM64 的硬约束

理解这一点能避免走回头路。三层死锁，**都已实测**：

| 层 | 约束 | 证据 |
|---|---|---|
| WPF | .NET Core 3.1 没有 ARM64 桌面运行时包 | `NETSDK1082`；`Microsoft.WindowsDesktop.App.Runtime.win-arm64` 在 NuGet 上最早为 `5.0.0`，无任何 3.x |
| XAML Islands | 仅支持 .NET Core 3.x；.NET 5 移除了内置 WinRT | `NETSDK1149`：无法引用 `Typedown.Core.dll` / `Typedown.XamlUI.dll` |
| PDF | `PdfiumViewer` 原生库只有 x64 | 包内仅 `runtimes/win10-x64/native` |

`WPF ARM64 ⇒ 需 .NET ≥ 5` 与 `XAML Islands ⇒ 仅 .NET Core 3.x` **交集为空**。
本移植的破局点是**彻底弃用 WPF 与 XAML Islands，改纯 WinUI 3**。

---

## 3. 环境搭建（新机器）

### 必需

| 组件 | 版本/说明 |
|---|---|
| Windows | **ARM64**（推荐；跨架构构建原生组件受限） |
| Visual Studio 2022/2026 | 需含 **.NET 8 SDK**、**Windows 11 SDK (10.0.26100+)**、**MSIX 打包工具** |
| Node.js + yarn | 构建编辑器前端 |
| .NET SDK | 本项目在 `10.0.402` 上验证 |

### Windows SDK 路径的坑（重要）

本机 SDK 装在 **`D:\Windows Kits\10\`**，而 `Typedown.XamlUI` 的 targets **硬编码回退到
`C:\Program Files (x86)\Windows Kits\...`**。因此构建**上游工程**（`Dev\Typedown`）时必须显式传入：

```powershell
$sdk = (Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows Kits\Installed Roots').KitsRoot10
# 若 SDK 不在注册表默认位置，改成实际路径并核对版本号目录
New-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment' -Name KitsRoot10 ...
```

实际构建时直接给属性（已在本机验证可用）：

```powershell
/p:TargetPlatformVersion=10.0.26100.0 `
/p:ManifestTool="$sdk\bin\10.0.26100.0\x86\mt.exe" `
/p:MakePri="$sdk\bin\10.0.26100.0\x86\makepri.exe" `
/p:VCRTForwarders-IncludeDebugCRT=false
```

> **主工程 `Typedown.App` 不需要这些**（它是 SDK-style，不 import `Typedown.XamlUI` 的 targets）。
> 只有构建上游的 `Dev\Typedown`（WPF 外壳）才需要。
> 上游还锁定 `TargetPlatformVersion=10.0.19041.0`（本机未安装），必须覆盖为已装版本。

### 需要重新生成的东西（**不在 git 里，故意如此**）

`*.pfx` / `*.cer` 已被 `.gitignore` 排除（私钥绝不能进公开仓库）。新机器上要自己生成：

```powershell
$pfx = "Dev\Typedown.App\Typedown.WinUI_TemporaryKey.pfx"
$cer = "Dev\Typedown.App\Typedown.WinUI_TemporaryKey.cer"
$pw  = "<自定密码>"
$c = New-SelfSignedCertificate -Type Custom -Subject "CN=Typedown" -KeyUsage DigitalSignature `
       -CertStoreLocation "Cert:\CurrentUser\My" `
       -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3","2.5.29.19={text}")
$sp = ConvertTo-SecureString $pw -Force -AsPlainText
Export-PfxCertificate -Cert $c -FilePath $pfx -Password $sp | Out-Null
Export-Certificate   -Cert $c -FilePath $cer | Out-Null
```

**`CN=Typedown` 必须与 `Package.appxmanifest` 的 `Publisher` 一致**，否则签名/安装失败。
换机器后若重新生成证书，**旧安装必须先卸载**（证书指纹变了，无法覆盖升级）。

---

## 4. 构建与打包（照抄即可）

### 4.1 编辑器前端（一次性，改了前端才需重跑）

```powershell
cd Dev\Typedown.Editor
yarn
yarn build      # 产物直接输出到 Dev\Typedown\Resources\Statics
```

### 4.2 构建应用（推荐用 dotnet，最快）

```powershell
dotnet build Dev\Typedown.App\Typedown.App.csproj -c Release -r win-arm64
# 产物: Dev\Typedown.App\bin\Release\net8.0-windows10.0.19041.0\win-arm64\Typedown.exe
```

> 免安装可直接运行（需 .NET 8 桌面运行时）。

### 4.3 打包 MSIX

```powershell
$msbuild = "D:\VS2026\IDE\MSBuild\Current\Bin\arm64\MSBuild.exe"   # 换成你的 VS 路径
& $msbuild Dev\Typedown.App\Typedown.App.csproj `
   /p:Configuration=Release /p:RuntimeIdentifier=win-arm64 /p:Platform=ARM64 `
   /p:AppxPackageSigningEnabled=true `
   "/p:PackageCertificateKeyFile=D:\VS\Repos\Typedown\Dev\Typedown.App\Typedown.WinUI_TemporaryKey.pfx" `
   "/p:PackageCertificatePassword=<你的密码>" `
   "/p:AppxPackageDir=D:\VS\Repos\Typedown\AppPackages\" `
   /p:GenerateAppxPackageOnBuild=true /p:UapAppxPackageBuildMode=SideloadOnly `
   /restore /v:m
```

**`EXIT=1` 是预期的**：最后一步 `mspdbcmf.exe`（符号处理）会报 `MSB6006`，但 **`.msix` 已成功产出且签名 `Valid`**，不影响安装。别被它误导。

### 4.4 安装

本机**开发者模式**已启用（`AllowDevelopmentWithoutDevLicense = 1`），因此自签名包可以
**免管理员直接安装**：

```powershell
Add-AppxPackage -Path "AppPackages\Typedown.App_1.2.19.0_arm64_Test\Typedown.App_1.2.19.0_arm64.msix"
```

新机器若**未启用开发者模式**，则必须先信任证书（**需管理员**）：

```powershell
Import-Certificate -FilePath "<...>.cer" -CertStoreLocation Cert:\LocalMachine\TrustedPeople
Add-AppxPackage -Path "<...>.msix"
```

### 4.5 版本号（升级安装必看）

**两个位置必须同步改**，否则无法覆盖升级：

- `Dev\Typedown.App\Package.appxmanifest` → `<Identity ... Version="1.2.19.0" />`
- `Dev\Typedown.App\Typedown.App.csproj` → `<Version>1.2.19</Version>`

---

## 5. 关键文件地图

| 路径 | 作用 |
|---|---|
| `Dev/Typedown.App/` | **★ 主工程**（WinUI 3 + .NET 8）。`Typedown.Core` 的 175 个源文件已合并进此程序集 |
| `Dev/Typedown.App/MainWindow.xaml(.cs)` | 窗口、自绘标题栏、拖拽区域、HWND 发布、生命週期 |
| `Dev/Typedown.App/Controls/MarkdownEditor.cs` | 编辑器宿主（WebView2）+ 宿主侧消息桥 |
| `Dev/Typedown.App/Utilities/FilePickersExtensions.cs` | **所有文件选择器的宿主窗口绑定**（历史 bug 高发区） |
| `Dev/Typedown.App/Utilities/WindowHelper.cs` | HWND 解析、窗口位置、开新窗口 |
| `Dev/Typedown.App/Resources/Strings/<lang>/*.resw` | 75 种语言（默认中文 `zh-Hans`） |
| `Dev/Typedown/` | 上游 WPF 外壳（保留可编译，作 x64 对照；构建需第 3 节的 SDK 覆盖参数） |
| `Dev/Typedown.Core/` | 上游 UWP 控件库（原始副本，`Typedown.App` 内已含其拷贝） |
| `Dev/Typedown.Editor/` | 编辑器前端（React + Muya） |
| `Dev/Typedown.WinUI/` | 早期 ARM64 可行性验证用最小工程（参考，非主线） |
| `NOTICE.md` | 出处与第三方许可 |
| `README.md` | 项目说明 + 移植改动清单 + 已修复缺陷表 |

### 架构要点（改代码前必读）

```
WinUI 3 Window (Typedown.App)
  └─ RootControl            (Typedown.Core.Controls)  ← 真实 UI 树入口
       ├─ Caption           (自绘标题栏: logo + 标题 + 后退)
       └─ GlobalFrame (Frame)
            └─ MainPage
                 ├─ MenuBar / MainContent / StatusBar
                 └─ MainContent → EditorContainer → ContentPresenter → MarkdownEditor(WebView2)
```

- **DI**：`Typedown.App/Injection.cs`，每个窗口一个 `IServiceScope`。
- **编辑器通信**：JS ↔ 宿主走 `Transport.EmitWebViewMessage()`（实现 invoke/diffmsg 协议）。
  前端契约在 `Dev/Typedown.Editor/src/services/transport.ts` 与 `remote/common.ts`。
- **窗口句柄**：`AppViewModel.MainWindow` 必须由 `MainWindow.OnRootLoaded` 发布，
  否则所有选择器失效（见 §6 Bug 2）。

---

## 6. 已修复的 bug（含根因，避免重蹈覆辙）

### Bug 1 — 新建窗口后关闭它，整个程序崩溃

```
COMException 0x800710DD: The WinUI Desktop Window object has already been closed.
  at Typedown.MainWindow.UpdateDragRegions()            ← 读取 Window.Content
  at MainWindow.<InitializeCustomTitleBar>b__26_2        ← LayoutUpdated 处理器
```

**根因**：为刷新自绘标题栏的拖拽区域，订阅了 `RootControl.LayoutUpdated`，但关闭时**未退订**。
窗口销毁后事件仍触发一次，读 `Content` 抛异常；异常从 `void` 事件处理器逃逸，进程终止。

**修复**：事件改为命名处理器（`OnTitleBarRootLayoutUpdated` 等）并在 `OnClosed` 中
`DetachTitleBarHandlers()` 退订；加 `isClosed` 标志让 `UpdateDragRegions()` 在拆卸阶段直接返回。

**教训**：WinUI 3 里任何 `Window` 成员在**关闭后**访问都会抛 `0x800710DD`。
订阅 `LayoutUpdated`/`SizeChanged` 时必须准备退订路径。

### Bug 2 — 打开/打开最近/打开文件夹/保存/另存为/导入/导出 全部无反应

**两个独立原因叠加**：

1. `picker as object as IInitializeWithWindow` **转换返回 null** → `SetOwnerWindow` 抛 `NullReferenceException`
2. **`AppViewModel.MainWindow` 从未被赋值**，始终 `IntPtr.Zero`
   （上游 WPF 的 `MainWindow.OnLoaded` 中有 `AppViewModel.MainWindow = Handle;`，重写外壳时遗漏）

**修复**：
- 改用 SDK 投影自带的 `WinRT.Interop.InitializeWithWindow.Initialize(object, IntPtr)`（签名已反射确认）
- `MainWindow.OnRootLoaded` 中发布 HWND：`AppViewModel.MainWindow = Handle`

**顺带**：移除了会把选择器错误**上报到 `typedown.ownbox.cn/report`** 的遥测调用，改为本地日志。

### Bug 3 — 打开文件后进设置页再返回，文档消失、编辑器变白板

**根因**：`MarkdownEditor` 是按窗口注册的**单例**（`AddScoped<IMarkdownEditor, MarkdownEditor>`），
`EditorContainer` 在页面卸载/重新加载时只是把**同一个实例**从可视树上摘下来再挂回去；
此时 WebView2 与其前端文档都还活着（实测 `document.readyState=complete`、DOM 内容完整）。
但 `MarkdownEditor.OnLoaded` 开头会无条件执行：

```csharp
Opacity = 0;
IsEditorLoaded = false;
IsEditorLoadFailed = false;

if (webView != null)
    return;          // ← 重新挂载时从这里就返回了
```

而 `Opacity` 只由前端的 `ContentLoaded` 回调恢复，**每个 `CoreWebView2` 只会回调一次**。
于是重新挂载后 `Opacity` 永远是 0：文档还在、点进去盲打字符数也会变，但屏幕上一片白。

**修复**：把「重置加载状态」移到提前返回之后——重新挂载且已加载完成时，只把 `Opacity` 恢复为 1。

**教训**：WinUI 3 里「控件重新进入可视树」≠「控件重新初始化」。
`Loaded` 里的一次性状态重置必须先区分**首次初始化**与**重新挂载**；依赖一次性握手回调
（`ContentLoaded`）恢复的状态，尤其不能在 `Loaded` 里被清掉。

> 本次同时**彻底移除了第三方上报**：`Log.Report` 不再 POST 到上游作者的 `typedown.ownbox.cn`，
> 改为写本地 `startup.log`；「关于」页的「发送反馈」按钮与 `FeedbackDialog` 一并删除。
> 包身份同时由 `Typedown.Arm64` 改名为 **`TypedownWinUI`**（`Package.appxmanifest` 的
> `Identity Name`），因此 PackageFullName / AUMID 都变了，旧包需要卸载、旧包数据不迁移。
> 以上改动已构建并安装为 **1.2.21**（`AppPackages\Typedown.App_1.2.21.0_x64_Test`，
> 安装后为 `TypedownWinUI_1.2.21.0_x64__8jrz9ffqrj418`）。

### Bug 4 — 任何对话框都打不开；修好宿主后又被塞进标题栏那一行

**根因（两处，都是移植换了外壳的后果）**：

1. `AppContentDialog.ShowAsync()` 要求 `XamlRoot.Content` **是 `Grid`**——它把对话框当子元素手工挂到窗口根面板上。
   WPF 外壳的窗口根面板确实是 `Grid`，但 WinUI 3 外壳的 `MainWindow.Content` 是 `RootControl`
   （`UserControl`），这个前置判断恒为真，于是**每一个用它的对话框都直接抛异常**——
   `startup.log` 里是 `UNHANDLED: System.InvalidOperationException`，栈顶 `AppContentDialog.ShowAsync`。
2. 宿主修好之后，对话框又被加进根 Grid 的**第一个单元格**。`RootControl` 的根 Grid 是
   「标题栏(Auto) + 内容(*)」两行，于是对话框长在标题栏那一行里，还把整页内容往下顶。

**修复**：`GetHostPanel()` 先看 `XamlRoot.Content` 是否为 `Panel`，不是就沿可视树往下找第一个 `Panel`；
添加时用 `Grid.SetRowSpan/SetColumnSpan` 跨满宿主的全部行列（`StretchAcrossHost()`）。

**教训**：手工 hosting 的浮层要同时确认两件事——宿主**存在**，以及对话框在宿主里的**位置与跨度**是对的。
换了外壳之后，「根就是 Grid、而且只有一个单元格」这类假设会全部失效。

> 「关于」页的第三方组件清单就是用这种对话框呈现的（14 项两列，`确定` 按钮可见可关闭）。

### 其他已修的坑（更早阶段）

| 现象 | 根因 | 修复 |
|---|---|---|
| 窗口空白、编辑器不渲染 | `WebView2` 放进 `Canvas`，而 **Canvas 给子元素 0 期望尺寸** → WebView2 实际 0×0 | 改为 `Grid` 拉伸布局，dummy rectangle 作 hit-test 透明的覆盖层 |
| XAML 解析崩溃 `Markup extension could not provide value` | `LocaleString.ProvideValue()` 在键缺失时返回 **null**，WinUI 3 对此抛异常 | 返回 `Key` 兜底，绝不返回 null |
| 找不到资源字符串 | Core 合并进 app 程序集后，MRT 子树键是**扁平**的（`CommonResources/Add`），不带程序集/路径前缀 | `Locale.GetSubtree` 改为取裸名 |
| `mspdbcmf.exe MSB6006` 掩盖真实错误 | 签名失败（未传密码）导致构建中断，而真实 XAML 错误只打印在**日志最末尾**（格式为 `XamlCompiler error WMC0091`，不匹配 `: error`） | 关闭默认签名；排查时**务必看构建输出末尾**，不要只 grep `: error` |

---

## 7. 待办 / 未验证项（接手后优先）

### 7.1 未完成的功能回归（**最高优先**）

用户报告的第 4 项尚未独立验证：**保存 / 另存为 / 导出（`FileSavePicker` 路径）**。
`FileOpenPicker` 与 `FolderPicker` 已实测通过：

```
picker owner set: FileOpenPicker(markdown) hwnd=0x12094C   ✓
picker owner set: FolderPicker           hwnd=0x12094C   ✓
```

`FileSavePicker` 走**同一个** `SetOwnerWindow` helper，按代码路径应已修复，但**无实测证据**。
另外**未逐项回归**：设置页各面板、导出配置、图片上传、打印、侧栏（文件/大纲）、快捷键。

### 7.2 自绘标题栏交互未自动化验证

拖拽区域是通过 `InputNonClientPointerSource.SetRegionRects(Caption, …)` 注册的，
需要**真实鼠标**验证：拖动移动窗口、双击最大化、以及**紧凑模式**下切换到
`MenuBar` 的 `LeftDragBar`/`RightDragBar` 的路径。

### 7.3 体积已到平台下界，勿再尝试裁剪/AOT（**已实测，别浪费time**）

| 方案 | 结果 |
|---|---|
| 裁剪 + 框架依赖 | **构建失败** `NETSDK1102: 所选发布配置不支持优化程序集的大小` |
| 裁剪 + 自包含 | 反而**变大到 53.2 MB**（强制自包含带入整个 .NET 运行时） |
| 裁剪后能否运行 | **不能**：`XamlParseException` —— `LocaleString` 等反射类型被裁掉 |
| Native AOT | 单体 exe **78 MB** > 整个 MSIX 39.3 MB；且是 unpackaged，与 MSIX 模型冲突 |

39.3 MB 里 **30 MB 是原生 DLL + WinRT 投影元数据**，裁剪器（只动托管 IL）无能为力。
唯一残余可选项：删掉中文/英文外的 ~80 个语言包，省约 **1.6 MB**（代价是以后加语言需重新打包）。

### 7.4 已知技术债

- `AppViewModel` 有 **终结器 `~AppViewModel()` 调用 `Dispose()`**，其中访问托管对象
  （`List<T>`、`CompositeDisposable`）。终结器线程上访问托管对象是危险写法，建议改为
  `Dispose(false)` 模式或直接移除终结器。
- `nupkgs/Typedown.XamlUI.1.0.1.nupkg`（17.5 MB）随仓库分发，因为 `NuGet.Config` 把它当本地源。
  仅上游 WPF 工程需要；若不打算再构建上游，可移除。
- `Dev/Typedown.App/Resources/Statics/` 与 `Dev/Typedown.WinUI/Resources/Statics/` 各存一份
  5.3 MB 的前端产物（重复）。`Typedown.WinUI` 若废弃，可删其产物。
- 文档未覆盖：`Tools/`、`Tests/` 下的工程未做 ARM64 移植与验证。

---

## 8. 调试手法（本会话验证有效，建议沿用）

### 8.1 看日志

崩溃与失败路径写入 **`%LOCALAPPDATA%\Typedown\startup.log`**：

- `App.Log(...)`：启动/生命周期/标题栏/选择器的失败信息
- `UNHANDLED:`：`Application.UnhandledException` 与 `AppDomain.UnhandledException`

干净启动应该**连日志文件都不产生**（当前实现只在失败时写）。

### 8.2 看视觉树（定位「控件没渲染」类问题）

WinUI 3 里「进程活着」不等于「UI 正确」。可直接 dump：

```csharp
// 递归 VisualTreeHelper，打印 类型/名字/ActualWidth×ActualHeight/Visibility/Opacity
```

本会话正是靠这个发现 `WebView2 552x242 vis=Visible` 但内容区**只有 1 种颜色**（实为空白）。

### 8.3 探 DOM（定位编辑器问题）

```csharp
var json = await webView.CoreWebView2.ExecuteScriptAsync(
  "JSON.stringify({hasEditor: !!document.getElementById('editor')," +
  " textLen: (document.getElementById('editor')||{}).innerText?.length, " +
  " errors: (window.__jsErrors||[]).length})");
```

### 8.4 UI Automation（本会话用于复现 Bug 1）

```
文件菜单 AutomationId = "MenuBarFileItem"
菜单项文本为中文：新建 / 新建窗口 / 打开 / 打开文件夹 / 打开最近 / 保存 / 另存为 / 导入 / 导出 / 打印 / 设置 / 关闭
```

注意：UIA 操作菜单可能触发 `0x8001010E`（在错误线程调用）或
`0x800710DD`（在错误线程打开对话框）——**这是自动化脚本的限制，不是产品缺陷**，
不要据此改产品代码。

---

## 9. 许可与合规（改动时勿破坏）

- 上游 MIT，**必须保留** `Copyright (c) 2022 ZZF` 声明。
  `LICENSE` 现为双版权行；`NOTICE.md` 记录出处、改动范围与全部第三方组件。
- **绝不提交** `*.pfx` / `*.p12` / `*.snk` / `*.cer`（`.gitignore` 已排除；提交前用
  `git ls-files | Select-String '\.(pfx|cer)$'` 复查）。
- 改了依赖需同步更新 `NOTICE.md` 的第三方组件表。

---

## 10. 快速上手命令（照抄）

```powershell
git clone https://github.com/Ansherly/Typedown-WinUI.git
cd Typedown-WinUI

# 1) 生成签名证书（新机器必须做一次；CN 必须等于 Package.appxmanifest 的 Publisher）
#    见 §3 的脚本

# 2) 前端（仅当改了 Dev\Typedown.Editor 才需重跑）
cd Dev\Typedown.Editor; yarn; yarn build; cd ..\..

# 3) 构建
dotnet build Dev\Typedown.App\Typedown.App.csproj -c Release -r win-arm64

# 4) 打包（EXIT=1 属正常，检查 .msix 是否产出且签名为 Valid）
#    见 §4.3

# 5) 安装
Add-AppxPackage -Path "AppPackages\Typedown.App_<版本>_arm64_Test\Typedown.App_<版本>_arm64.msix"
```

启动方式：开始菜单 **Typedown**，或
`Start-Process "shell:AppsFolder\Typedown.Arm64_8jrz9ffqrj418!App"`

---

## 11. 提问清单（如果你需要外部协助）

把下列信息一并给出，定位会快很多：

1. `%LOCALAPPDATA%\Typedown\startup.log` 全文（含时间戳）
2. 复现步骤 + 期望/实际行为
3. 是**打包版**还是**免安装版**（两者日志同一路径，但路径不同：
   打包版来自 `C:\Program Files\WindowsApps\...`）
4. `Get-AppxPackage Typedown.Arm64 | fl Name,Version,Architecture`
5. 若是渲染问题：视觉树 dump 或编辑器 DOM 探针输出
