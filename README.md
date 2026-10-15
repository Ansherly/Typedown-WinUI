<p align="center">
  <img alt="Typedown Logo" src="./logo.png" width="100px" />
  <h1 align="center">Typedown-WinUI</h1>
  <p align="center">Typedown 的 WinUI 3 / .NET 8 移植版 · 原生 ARM64</p>
</p>

> **本仓库是基于 [byxiaozhi/Typedown](https://github.com/byxiaozhi/Typedown) 的派生作品。**
> 原项目由 **ZZF（[@byxiaozhi](https://github.com/byxiaozhi)）** 开发，采用 MIT 许可。
> 完整出处说明与致谢见 [NOTICE.md](./NOTICE.md)。

---

## 这个仓库解决什么问题

上游 Typedown 基于 **WPF + UWP XAML Islands（.NET Core 3.1）**。这套技术栈**在原理上就无法编译出原生 ARM64 版本**，
因此官方从未提供 ARM64 包。本仓库把它移植到 **WinUI 3 / .NET 8**，从而可以构建、运行并打包
**原生 ARM64** 应用。

### 为什么上游做不出 ARM64（三层硬约束）

| 层 | 约束 | 实测证据 |
|---|---|---|
| WPF | .NET Core 3.1 **没有** ARM64 桌面运行时 | `NETSDK1082`；`Microsoft.WindowsDesktop.App.Runtime.win-arm64` 在 NuGet 上最早为 `5.0.0`，不存在任何 3.x |
| XAML Islands | .NET 5 移除了内置 WinRT 支持，而 Islands 依赖它 | `NETSDK1149`：无法引用 `Typedown.Core.dll` / `Typedown.XamlUI.dll` |
| PDF 打印 | `PdfiumViewer` 的原生 `pdfium.dll` 只有 x64 | 包内仅 `runtimes/win10-x64/native` |

三者构成死锁：**WPF ARM64 需要 .NET ≥ 5，而 XAML Islands 仅支持 .NET Core 3.x**，交集为空。
本仓库通过**改用纯 WinUI 3（彻底去掉 WPF 与 XAML Islands）**打破了这个死锁。

---

## 相对上游的改动

| 类别 | 内容 |
|---|---|
| **技术栈** | WPF + UWP XAML Islands (.NET Core 3.1) → **WinUI 3 + .NET 8** |
| **目标平台** | x64 / x86 → **x64 / ARM64（原生）** |
| **外壳重写** | `Typedown.XamlUI` 的 `XamlWindow`/`XamlApplication` → WinUI 3 `Window` + `AppWindow` |
| **编辑器宿主** | XAML Islands `WebViewController`（原生 controller + 显式 bounds）→ `Microsoft.UI.Xaml.Controls.WebView2` |
| **窗口装饰** | Win32 标题栏 → **自绘标题栏**（`ExtendsContentIntoTitleBar` + `InputNonClientPointerSource` 拖拽区域） |
| **调度器** | `CoreWindow.CoreDispatcher` → `DispatcherQueue`（附适配层，保持原有调用点不变） |
| **光标** | `Window.Current.CoreWindow.PointerCursor` → `UIElement.ProtectedCursor` |
| **PDF 打印** | `PdfiumViewer`（x64-only 原生库）→ WebView2 `PrintToPdfAsync` / `ShowPrintUI` |
| **本地化** | 默认语言改为**中文**（`zh-CN`，资源在 `Resources/Strings/zh-Hans`），保留全部 75 种语言 |
| **包体精简** | 剔除 Windows App SDK 携带的 **DirectML / onnxruntime** 等 AI/ML 载荷，**55.6 MB → 39.3 MB（-29%）** |
| **修复** | 上游构建阻塞（Windows SDK 路径硬编码、`TargetPlatformVersion` 缺失）、`LocaleString` 返回 null 导致 XAML 解析崩溃、`WebView2` 放进 `Canvas` 导致 0×0 不渲染 |

### 关于包体精简的说明

`Microsoft.WindowsAppSDK` 元包会强制依赖 `AI` / `ML` / `Widgets`。但**真正的大头不在那里**：
`DirectML.dll`(17.6 MB) 与 `onnxruntime.dll`(20.7 MB) **内嵌在 `Microsoft.WindowsAppRuntime.1.8.msix` 里**，
而自包含模式会把该框架包整个解开铺进项目。因此本仓库：

1. 不再引用元包，改为只引用需要的子包（WinUI / Foundation / InteractiveExperiences / DWrite / Runtime）；
2. 在打包阶段过滤 `PackagingOutputs` 中的 AI/ML 载荷，并删除其暂存副本。

刻意**保留 `.winmd` 元数据**，以免影响 WinRT 类型解析。

---

## 目录结构

```
Dev/
  Typedown/            上游 WPF 外壳（保留可编译，用于 x64 对照）
  Typedown.Core/       上游 UWP 控件库与 ViewModel（本移植的复用主体）
  Typedown.Editor/     编辑器前端（React + Muya），yarn build 产出到 Resources/Statics
  Typedown.App/        ★ 本移植的主工程（WinUI 3 + .NET 8，含 Typedown.Core 全部源码）
  Typedown.WinUI/      ARM64 可行性验证用最小工程（自绘标题栏 + 编辑器宿主原型）
Tools/                 上游的打包/工具工程
Tests/                 上游测试工程
nupkgs/                离线 NuGet 源（含 Typedown.XamlUI）
```

> `Typedown.App` 是把 `Typedown.Core` 的 175 个源文件（73 个 XAML、300 个 resw）合并进同一程序集后的
> 主工程；`Typedown.WinUI` 是早期用于验证 ARM64 可行性的最小工程，保留供参考。

---

## 构建

### 环境要求

- **Visual Studio 2022/2026**，包含：
  - .NET 8 SDK
  - Windows 11 SDK（10.0.26100 或更高）
  - MSIX 打包工具
- **Node.js + yarn**（构建编辑器前端）
- 构建 ARM64 建议在 ARM64 设备上进行（跨架构构建 AOT/原生组件受限）

### 1. 构建编辑器前端

```powershell
cd Dev\Typedown.Editor
yarn
yarn build          # 产物直接输出到 Dev\Typedown\Resources\Statics
```

### 2. 构建应用（ARM64）

```powershell
dotnet build Dev\Typedown.App\Typedown.App.csproj -c Release -r win-arm64
```

产出：`Dev\Typedown.App\bin\Release\net8.0-windows10.0.19041.0\win-arm64\Typedown.exe`
（免安装可直接运行，需已安装 .NET 8 桌面运行时）

### 3. 生成 MSIX 安装包

> 自签名的 `.pfx` **刻意不纳入版本库**，请自行生成（见下）。

```powershell
# 生成自签名证书（仅需一次；CN 必须与 Package.appxmanifest 的 Publisher 一致）
$pfx = "Dev\Typedown.App\Typedown.WinUI_TemporaryKey.pfx"
$cer = "Dev\Typedown.App\Typedown.WinUI_TemporaryKey.cer"
$pw  = "你的密码"
$c = New-SelfSignedCertificate -Type Custom -Subject "CN=Typedown" `
       -KeyUsage DigitalSignature -CertStoreLocation "Cert:\CurrentUser\My" `
       -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3","2.5.29.19={text}")
$sp = ConvertTo-SecureString $pw -Force -AsPlainText
Export-PfxCertificate -Cert $c -FilePath $pfx -Password $sp | Out-Null
Export-Certificate   -Cert $c -FilePath $cer | Out-Null

# 打包
msbuild Dev\Typedown.App\Typedown.App.csproj `
  /p:Configuration=Release /p:Platform=ARM64 /p:RuntimeIdentifier=win-arm64 `
  /p:AppxPackageSigningEnabled=true "/p:PackageCertificateKeyFile=$pfx" `
  "/p:PackageCertificatePassword=$pw" /p:AppxPackageDir=AppPackages\ `
  /p:GenerateAppxPackageOnBuild=true /p:UapAppxPackageBuildMode=SideloadOnly `
  /restore
```

### 4. 安装

MSIX 的自签名证书需先导入受信任存储（**需要管理员权限**）：

```powershell
Import-Certificate -FilePath "<你的>.cer" -CertStoreLocation Cert:\LocalMachine\TrustedPeople
Add-AppxPackage -Path "AppPackages\Typedown.App_1.2.18.0_arm64_Test\Typedown.App_1.2.18.0_arm64.msix"
```

---

## 已实测验证

在 Windows 11 ARM64 设备上：

```
Typedown.exe / Typedown.dll / Microsoft.ui.xaml.dll   → 全部 ARM64
IsWow64Process2 本机机器 = 0xAA64 (ARM64)，IsWow64 = False   → 非仿真
appLanguages = [zh-Hans-CN, ja]；Open='打开' Settings='设置'  → 中文默认
自绘标题栏：无 Win32 标题栏，1 个拖拽区域注册成功
编辑器：editorHtmlLen=3602、errors:0，成功渲染标题/粗体/列表
MSIX：39.3 MB，签名 Valid，464 条目，AI/ML 0 项
```

---

## 已修复的缺陷

移植过程中发现并修复的运行时缺陷（均已实测复现与验证）：

| 缺陷 | 根因 | 修复 |
|---|---|---|
| **点击「新建窗口」后再关闭该窗口，整个程序崩溃** | `MainWindow` 订阅了 `RootControl.LayoutUpdated` 用于更新标题栏拖拽区域，但关闭时未退订。窗口销毁后该事件仍会触发一次，此时读取 `Window.Content` 抛出 `COMException 0x800710DD`（*The WinUI Desktop Window object has already been closed*）；事件处理器内的异常无人接管，导致进程终止 | 事件改用命名处理器并在 `OnClosed` 中退订，同时以 `isClosed` 标志让 `UpdateDragRegions()` 在拆卸阶段直接返回 |
| **打开 / 打开最近 / 打开文件夹 / 保存 / 另存为 / 导入 / 导出 全部无反应** | 两处叠加：① 转换 `picker as object as IInitializeWithWindow` 返回 null，抛出 `NullReferenceException`；② `AppViewModel.MainWindow`（选择器的宿主窗口句柄）从未被赋值，始终为 `IntPtr.Zero` | ① 改用 Windows SDK 投影自带的 `WinRT.Interop.InitializeWithWindow.Initialize()`；② 在 `RootControl.Loaded` 中把本窗口 HWND 发布给 ViewModel |

> 顺带移除了一条会把选择器错误上报到 `typedown.ownbox.cn/report` 的遥测调用，改为写入本地日志。

## 已知限制

- **拖拽/双击最大化**等标题栏交互是通过 `InputNonClientPointerSource` 注册的，未做自动化测试，
  建议手动验证（含「紧凑模式」下切换到 `MenuBar` 拖拽条的路径）。
- **未做逐项功能回归**：菜单、设置页、文件对话框、导出等 UI 均已编译并随窗口加载。
  已实测确认通过的部分见上文「已实测验证」与「已修复的缺陷」；其余菜单项仍建议手动逐项确认。
- **不可再显著精简**：`.NET` 的 `PublishTrimmed` 在框架依赖模式下会直接报 `NETSDK1102` 失败；
  强制自包含后裁剪反而使包增大到 53.2 MB，且会因裁掉 `LocaleString` 等反射类型导致
  `XamlParseException` 崩溃。Native AOT 单体 exe 达 78 MB，大于整个 MSIX，且与本项目的
  MSIX 模型冲突。
- **PDFium 已移除**：PDF 打印改走 WebView2 自带打印 UI，不再依赖 x64-only 的 `PdfiumViewer`。

---

## 致谢

特别感谢 **[ZZF / @byxiaozhi](https://github.com/byxiaozhi)** 开发了 Typedown 并以 MIT 许可开放源码。
本项目复用了其几乎全部业务代码（`Typedown.Core`、编辑器前端、本地化资源等），
仅在 UI 宿主层与构建配置上做了移植改动。

同时也感谢上游所依赖的开源项目：Muya、React、CodeMirror、KaTeX、Mermaid 等
（详见 [NOTICE.md](./NOTICE.md)）。

## 许可

[MIT License](./LICENSE)

```
Copyright (c) 2022 ZZF (original Typedown — https://github.com/byxiaozhi/Typedown)
Copyright (c) 2026 Typedown-WinUI contributors (WinUI 3 / ARM64 port)
```

本项目为非官方移植，与上游作者无隶属关系。
