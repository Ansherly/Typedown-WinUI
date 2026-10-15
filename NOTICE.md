# NOTICE / 出处与致谢

## 上游项目

本仓库是以下开源项目的派生作品（derivative work）：

- **项目**：Typedown
- **作者**：ZZF（[@byxiaozhi](https://github.com/byxiaozhi)）
- **仓库**：https://github.com/byxiaozhi/Typedown
- **许可**：MIT License
- **原始版权**：Copyright (c) 2022 ZZF

上游项目是一个面向 Windows 的轻量级 Markdown 编辑器，基于 **WPF + UWP XAML Islands**
（.NET Core 3.1）构建。

## 本仓库做了什么

本仓库（Typedown-WinUI）将上游应用**移植到 WinUI 3 / .NET 8**，以使其能够编译为
**原生 ARM64** 应用，并额外完成了依赖精简与中文本地化等工作。完整改动清单见
[README.md](./README.md) 的「相对上游的改动」一节。

移植过程**未修改上游的 MIT 许可条款**。本仓库继续以 MIT License 发布，并按要求
保留原始版权声明；同时新增了移植作者的版权行。

## 第三方组件

本仓库包含或依赖以下第三方组件，它们各自遵循其原有许可：

| 组件 | 许可 | 用途 |
|---|---|---|
| [Typedown](https://github.com/byxiaozhi/Typedown) | MIT | 上游项目 |
| [Typedown.XamlUI](https://www.nuget.org/packages/Typedown.XamlUI) | MIT | 上游的 XAML Islands 宿主（`nupkgs/` 内含离线包） |
| [Windows App SDK](https://github.com/microsoft/WindowsAppSDK) | MIT | WinUI 3 运行时 |
| [WebView2](https://learn.microsoft.com/microsoft-edge/webview2/) | Microsoft 专有（可再分发） | 编辑器宿主 |
| [Muya](https://github.com/marktext/muya) （随上游 `Dev/Typedown.Editor` 一同带来） | MIT | Markdown 编辑器内核 |
| [React](https://react.dev/) / [CodeMirror](https://codemirror.net/) / [KaTeX](https://katex.org/) / [Mermaid](https://mermaid.js.org/) 等前端依赖 | 各自许可（见 `Dev/Typedown.Editor/node_modules/*/LICENSE`） | 编辑器前端 |
| [.NET Runtime](https://github.com/dotnet/runtime) | MIT | 运行时 |

`Dev/Typedown.App/Resources/Statics/` 与 `Dev/Typedown.WinUI/Resources/Statics/` 下为
编辑器前端的构建产物，其中包含上述前端依赖的代码与其许可声明
（例如 `static/js/main.*.js.LICENSE.txt`）。

## 免责声明

本仓库为非官方移植，与上游作者 ZZF 无隶属关系，亦未获得其背书。
如上游作者希望调整署名方式或对本仓库有任何异议，请提 Issue，我们会配合处理。
