# x64 机器部署记录 / DEPLOY-x64

> 补充 `HANDOVER.md`：那份文档假设目标是 **ARM64** 设备，本文记录在**一台 x64 机器**上
> 实际部署（2026-10-15）的差异、坑与复现命令。
> 结论：**同一份源码可以原样构建出可运行的 x64 包**，只有构建/打包/安装参数需要改架构。

---

## 1. 本机环境（与 HANDOVER §3 的差异）

| 项 | HANDOVER 假设 | 本机实测 |
|---|---|---|
| CPU / 系统 | Windows **ARM64** | **AMD Ryzen 7 6800HS，x64**（Windows 11 10.0.29683） |
| 目标 RID | `win-arm64` | **`win-x64`** |
| Visual Studio | `D:\VS2026\IDE\...` | **`D:\VS\IDE`**（vswhere 只报这一个） |
| MSBuild | `...\Bin\arm64\MSBuild.exe` | 只有 `D:\VS\IDE\MSBuild\Current\Bin\MSBuild.exe` 与 `...\Bin\amd64\MSBuild.exe` |
| Windows SDK | `C:\Program Files (x86)\Windows Kits`（硬编码回退） | `D:\Windows Kits\10\`，`KitsRoot10` 已指向；`bin\10.0.26100.0\{x86,x64,arm64}` 齐全 |
| .NET SDK | `10.0.402` | `10.0.401 / 10.0.203 / 10.0.100-rc.1`；运行时 `Microsoft.WindowsDesktop.App 8.0.31` 已装（免安装运行够用） |
| yarn | 需 Node + yarn | **yarn 未安装** → 需要时用 `corepack enable`/`npm i -g yarn`；本机 `Dev\Typedown.App\Resources\Statics` 已带 90 个文件 / 8.7 MB 前端产物，**跳过前端构建** |

`Typedown.App` 本身已经是 `Platforms=x64;ARM64` + `RuntimeIdentifiers=win-x64;win-arm64`，
所以主工程**不需要改任何代码或工程文件**，只换命令行参数。

---

## 2. 本次踩到的坑（HANDOVER 未覆盖）

1. **`NU1301: 本地源 "D:\VS\Repos\Typedown\nupkgs" 不存在`，还原直接失败。**
   `NuGet.Config` 只声明了本地源 `nupkgs`，而 §7.4 说的那个 17.5 MB `Typedown.XamlUI.1.0.1.nupkg`
   在本检出里**并不存在**（`.gitignore:186 *.nupkg` 会挡掉它）。
   主工程其实不需要这个包，**建一个空的 `nupkgs\` 目录**即可让还原通过（nuget.org 来自用户级
   `%APPDATA%\NuGet\NuGet.Config`，仓库级配置没有 `<clear/>`，所以两个源是叠加的）。
   只有要构建上游 WPF 工程（`Dev\Typedown`）时才真的需要那个 nupkg。

2. **`Dev\Typedown.Core\Typedown.Core.csproj` / `Tools\XamlDesignApp\XamlDesignApp.csproj`
   的 `TargetPlatformVersion` 已被本地改成 `10.0.26100.0`**（工作区里处于 modified 状态），
   与 HANDOVER §3「上游锁定 19041、本机没装」一致。主工程 `Typedown.App` 不受影响。

3. **打包时的 `APPX0105` / `APPX0107` 警告可以忽略。**
   ```
   warning APPX0105: Cannot import the key file '...pfx'
   warning APPX0107: The certificate specified is not valid for signing
   ```
   但 `.msix` 产出后 `Get-AuthenticodeSignature` 显示签名者确实是 `CN=Typedown`，
   指纹与自签证书一致 —— 包**是签好的**，别被这两行吓到，也别急着去换证书。
   `EXIT=1`（`mspdbcmf.exe MSB6006` 符号处理）与 HANDOVER §4.3 描述完全一致，同样是预期。

4. **开发者模式 ≠ 可以装自签名包。**
   本机 `AllowDevelopmentWithoutDevLicense = 1`，但 `Add-AppxPackage <msix>` 仍然失败：
   ```
   0x80073CF0 / 0x800B0109: 应用包或捆绑包中的签名的根证书必须是受信任的证书
   ```
   把 `.cer` 导入 **`Cert:\CurrentUser\TrustedPeople` 不够**（AppX 部署服务以 SYSTEM 运行，
   只看机器存储），`-AllowUnsigned` 也不管用。**必须管理员**导入
   `Cert:\LocalMachine\TrustedPeople`（导入 `CurrentUser\Root` 会在无交互会话里因弹窗而失败）。

5. **免管理员的临时替代方案**（本机验证可用，先跑起来了才去要管理员）：
   ```powershell
   Add-AppxPackage -Register "Dev\Typedown.App\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\AppxManifest.xml"
   ```
   松散文件注册（`IsDevelopmentMode: True`），不需要信任证书，UI 与功能完全一样；
   代价是**应用依赖那个 bin 目录**，`dotnet clean` 之后就失效。
   正式安装（下面的 §4）会先把这条注册 `Remove-AppxPackage -PreserveApplicationData` 掉。

---

## 3. 构建与打包（x64 版命令）

```powershell
# 0) 一次性：让本地 NuGet 源存在
New-Item -ItemType Directory -Force D:\VS\Repos\Typedown\nupkgs | Out-Null

# 1) 证书（CN=Typedown 必须等于 Package.appxmanifest 的 Publisher）
$pfx = "Dev\Typedown.App\Typedown.WinUI_TemporaryKey.pfx"
$cer = "Dev\Typedown.App\Typedown.WinUI_TemporaryKey.cer"
$pw  = "<自定密码>"   # 本次存在 %LOCALAPPDATA%\Typedown-Deploy\cert-password.txt
$c = New-SelfSignedCertificate -Type Custom -Subject "CN=Typedown" -KeyUsage DigitalSignature `
       -KeyAlgorithm RSA -KeyLength 2048 -CertStoreLocation "Cert:\CurrentUser\My" `
       -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3","2.5.29.19={text}")
$sp = ConvertTo-SecureString $pw -Force -AsPlainText
Export-PfxCertificate -Cert $c -FilePath $pfx -Password $sp | Out-Null
Export-Certificate   -Cert $c -FilePath $cer | Out-Null

# 2) 构建（约 2 分钟；产物 495 文件 / 114 MB，WASDK 自包含）
dotnet build Dev\Typedown.App\Typedown.App.csproj -c Release -r win-x64 -p:Platform=x64

# 3) 打包签名（EXIT=1 属正常，检查 .msix 是否产出）
$msbuild = "D:\VS\IDE\MSBuild\Current\Bin\amd64\MSBuild.exe"
& $msbuild Dev\Typedown.App\Typedown.App.csproj `
   /p:Configuration=Release /p:RuntimeIdentifier=win-x64 /p:Platform=x64 `
   /p:AppxPackageSigningEnabled=true `
   "/p:PackageCertificateKeyFile=D:\VS\Repos\Typedown\Dev\Typedown.App\Typedown.WinUI_TemporaryKey.pfx" `
   "/p:PackageCertificatePassword=<自定密码>" `
   "/p:AppxPackageDir=D:\VS\Repos\Typedown\AppPackages\" `
   /p:GenerateAppxPackageOnBuild=true /p:UapAppxPackageBuildMode=SideloadOnly `
   /restore /v:m
# 产出：AppPackages\Typedown.App_1.2.19.0_x64_Test\Typedown.App_1.2.19.0_x64.msix  (41 MB)
```

## 4. 安装（需要一次管理员）

```powershell
# 提权后：
Import-Certificate -FilePath Dev\Typedown.App\Typedown.WinUI_TemporaryKey.cer `
                   -CertStoreLocation Cert:\LocalMachine\TrustedPeople
Add-AppxPackage -Path AppPackages\Typedown.App_1.2.19.0_x64_Test\Typedown.App_1.2.19.0_x64.msix
```

启动：开始菜单 **Typedown**，或
`Start-Process "shell:AppsFolder\TypedownWinUI_8jrz9ffqrj418!App"`

> 包身份名是 `TypedownWinUI`（1.2.21 起；更早的 1.2.19/1.2.20 叫 `Typedown.Arm64`，已随之改名，见 §8）。
> 与商店版上游 `62082Surprise.Typedown`（1.2.18，x64）**互不冲突**，开始菜单里会有两个 Typedown。

---

## 5. 本次实测结果

- 安装位置（1.2.21 起）`C:\Program Files\WindowsApps\TypedownWinUI_1.2.21.0_x64__8jrz9ffqrj418`，
  `IsDevelopmentMode: False`，`Status: Ok`，MSIX 签名 `Valid (CN=Typedown)`；旧身份 `Typedown.Arm64` 已卸载。
- 进程 `Typedown.exe` 从 `WindowsApps` 目录启动，窗口 `Responding: True`。
- **干净启动不产生 `%LOCALAPPDATA%\Typedown\startup.log`**（符合 HANDOVER §8.1 的判据）。
- 界面渲染正常：自绘标题栏（logo + Typedown + 最小化/最大化/关闭）、
  菜单栏 文件/编辑/段落/格式/查看、设置齿轮、Muya 编辑器占位符、状态栏 `0 字`。
- 手工点测（用户）：`文件 → 打开` 弹出选择器并成功打开
  `D:\图书馆\11408\CS\计算机网络-网络层.md`，说明 `FileOpenPicker` OWNER 绑定与
  `AppViewModel.MainWindow` 发布链路在本机构建下同样有效；随后关闭窗口**未崩溃、无日志**（Bug 1 修复有效）。

## 6. 仍未验证（沿用 HANDOVER §7）

保存 / 另存为 / 导出（`FileSavePicker` 路径）、设置页各面板、导出配置、图片上传、打印、
侧栏（文件/大纲）、快捷键、自绘标题栏的真实鼠标拖拽/双击最大化。

---

## 7. 本次顺带修的 bug（1.2.20）

部署验证过程中发现并修复：**打开文件 → 点设置 → 点返回，文档消失、编辑器区变白板**。
根因与修复见 `HANDOVER.md` §6 Bug 3 ——`MarkdownEditor` 是每窗口单例，重新挂载可视树时
`OnLoaded` 把 `Opacity` 清 0，而前端的 `ContentLoaded` 每个 `CoreWebView2` 只回调一次。
已按用户原步骤实测复现与验证，并以 x64 重新打包安装为
`Typedown.Arm64_1.2.20.0_x64__8jrz9ffqrj418`。

> 升级安装时两处版本号必须同步：`Package.appxmanifest` 的 `Version` 与 `Typedown.App.csproj` 的
> `<Version>`（本次 1.2.19 → 1.2.20）。证书已在本机 `LocalMachine\TrustedPeople` 中，
> 因此**升级安装不再需要管理员**。

---

## 8. 第二轮修改（1.2.21）

| 改动 | 说明 |
|---|---|
| **包身份改名** | `Package.appxmanifest` 的 `Identity Name`：`Typedown.Arm64` → **`TypedownWinUI`**。PackageFullName 现为 `TypedownWinUI_1.2.21.0_x64__8jrz9ffqrj418`，AUMID 为 `TypedownWinUI_8jrz9ffqrj418!App`。**旧身份是另一个包**：装上新的之后要 `Remove-AppxPackage Typedown.Arm64_...`，且旧包的应用数据（LocalState 里的 Settings.json / Storage.db）**不会迁移**。证书与 `Publisher`（`CN=Typedown`）没变，所以签名与信任关系不受影响。
| **侧栏顶部标签的悬停背景** | 侧栏用 `NavigationView(PaneDisplayMode=Top)` 做「文件 / 大纲」标签，悬停/按下时 WinUI 默认会画一层 `SubtleFillColorSecondary/Tertiary` 底色，看起来像一个多余的超链接高亮。在 `LeftPane.xaml` 里把 `TopNavigationViewItemBackgroundPointerOver/Pressed`（以及 `SelectorBarItemBackgroundPointerOver/Pressed`，以防某些 WinUI 版本用 SelectorBar 承载顶部项）覆盖为 `Transparent`；选中态由强调色下划线表示，不受影响。 |
| **关于页** | 「基于开源软件」改为弹出对话框，列出随包分发/依赖的第三方组件与许可（`AboutApp.xaml.cs` 的 `OpenSourceComponents`，与 `NOTICE.md` 对应）；「应用商店页面」改为 **GitHub 仓库**，指向 `https://github.com/Ansherly/Typedown-WinUI`。资源键 `About.AppStorePage` 在全部 75 个语言文件中改名为 `About.GithubRepository`（中/英给出完整译名，其余语言先用中性词 `GitHub`）。 |
| **移除第三方上报** | 删掉「发送反馈」按钮与其 `FeedbackDialog`（原来会把内容 POST 到 `typedown.ownbox.cn/feedback`）；`Core.Utilities.Log.Report` 不再 POST 到 `typedown.ownbox.cn/report`，改为写本地 `%LOCALAPPDATA%\Typedown\startup.log`；`About.SendFeedback*` 的 4 个资源键在 75 个语言文件中一并删除。App 工程内已无任何指向第三方服务器的端点（仅剩 GitHub 链接与 WebView2 官方文档链接）。 |

### 8.1 顺带发现并修掉的：对话框宿主

做「关于」页那个清单弹窗时才发现，**这个移植版里所有 `AppContentDialog` 弹窗都是坏的**（不是新引入的）：

1. `AppContentDialog.ShowAsync()` 手工把对话框挂到窗口根面板，并要求 `XamlRoot.Content` **是 `Grid`**；
   WinUI 3 外壳的窗口内容是 `RootControl`（`UserControl`），该判断恒为真 → 一调用就抛
   `InvalidOperationException`（`startup.log` 里是 `UNHANDLED:`，栈顶 `AppContentDialog.ShowAsync`）。
2. 宿主修好后还有第二处：对话框被加进根 Grid 的**第一个单元格**，而 `RootControl` 的根 Grid 是
   「标题栏(Auto) + 内容(*)」两行 → 对话框长在标题栏那一行里，还把整页内容往下顶。

修法：`GetHostPanel()` 沿可视树取第一个 `Panel`（`XamlRoot.Content` 是 `Panel` 时直接用），
并用 `Grid.SetRowSpan/SetColumnSpan` 跨满宿主全部行列。清单弹窗本身用两列紧凑布局（14 项），
`MaxHeight` 480，`确定` 按钮可见可关闭。
> 版本号同样两处同步：`1.2.20` → `1.2.21`。安装：先卸载旧身份，再 `Add-AppxPackage` 新的 `.msix`（无需管理员）。
