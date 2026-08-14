# macOS 版本

这是“小欧公爵和小耶牧师”桌宠的 macOS/Avalonia 版本。Windows WPF 工程保持不变，Mac 版本位于独立的 `PetFriends.Mac` 目录。

## 下载最新版

当前版本：**v1.2.1**。发布包已包含 .NET 运行环境，目标 Mac 不需要安装或下载其他组件。

| Mac 类型 | DMG（推荐） | ZIP |
| --- | --- | --- |
| Apple Silicon（M1/M2/M3/M4 等） | [下载 DMG](https://github.com/dada0o/ohyeah-pet/releases/download/v1.2.1/PetFriends-v1.2.1-macOS-Apple-Silicon.dmg) | [下载 ZIP](https://github.com/dada0o/ohyeah-pet/releases/download/v1.2.1/PetFriends-v1.2.1-macOS-Apple-Silicon.zip) |
| Intel | [下载 DMG](https://github.com/dada0o/ohyeah-pet/releases/download/v1.2.1/PetFriends-v1.2.1-macOS-Intel.dmg) | [下载 ZIP](https://github.com/dada0o/ohyeah-pet/releases/download/v1.2.1/PetFriends-v1.2.1-macOS-Intel.zip) |

## 已实现

- 透明、无边框、始终置顶的双桌宠窗口
- 单击摸摸、按住拖动、触控板辅助点击/鼠标右键菜单
- 对话气泡、爱心、跳跃、摇摆和粒子动画
- 自动散步、专注陪伴、全屏撒欢和屏幕边缘探头
- 专注模式下根据屏幕尺寸自动调整活动范围和双宠间距
- 自动靠近以及亲脸颊、碰鼻子、牵爪、击掌、跳舞、零食、打盹等双宠互动
- 追逐游戏、双宠聊天以及右键菜单中的完整互动动作
- 使用 macOS 原生窗口 API 识别其他应用窗口，支持坐到窗口上沿、躲到窗口后面和双宠从窗口边缘探头
- 菜单栏状态图标、叫回桌面、切换模式、窗口互动和退出
- 迷你、默认、大号三档尺寸以及安静模式
- 默认开机自动启动，可从桌宠右键菜单或菜单栏图标随时关闭、重新开启
- Apple Silicon（`osx-arm64`）和 Intel（`osx-x64`）自包含打包

窗口互动使用系统公开的 Core Graphics / AppKit API，不需要辅助功能或屏幕录制权限。系统级窗口（例如桌面、Dock 和菜单栏）会被自动排除。

## 在 Mac 上构建

需要 macOS 12 或更高版本，以及 .NET 9 SDK。

```bash
chmod +x PetFriends.Mac/build-macos.sh

# M1/M2/M3/M4 等 Apple 芯片
./PetFriends.Mac/build-macos.sh arm64

# Intel Mac
./PetFriends.Mac/build-macos.sh x64
```

输出位置：

```text
PetFriends.Mac/dist/osx-arm64/小欧公爵和小耶牧师桌宠.app
PetFriends.Mac/dist/osx-arm64/小欧公爵和小耶牧师桌宠-macOS-arm64.zip
PetFriends.Mac/dist/osx-arm64/小欧公爵和小耶牧师桌宠-macOS-arm64.dmg
```

发布包是自包含版本，目标 Mac 不需要安装或下载 .NET。可以解压 ZIP 后双击 `小欧公爵和小耶牧师桌宠.app`；也可以打开 DMG，把应用拖到其中的 `Applications` 快捷方式后运行。

## 首次打开

当前脚本进行 ad-hoc 本地签名，没有使用付费 Apple Developer ID，也没有提交 Apple 公证。请只运行你自己构建或从可信仓库下载的版本。

第一次打开时，可在 Finder 中按住 Control 点击应用并选择“打开”。若系统仍然阻止，在“系统设置 → 隐私与安全性”中确认并选择“仍要打开”。

请先把 App 从 DMG 拖到“应用程序”文件夹。安装后的第一次运行会默认开启开机自动启动；取消菜单中的勾选后会记住你的选择。如果直接在 DMG 中运行，程序不会登记失效的自动启动路径。

## GitHub Actions

`.github/workflows/build-macos.yml` 会同时构建 Apple Silicon 和 Intel 压缩包。可在仓库 Actions 页面手动运行，或推送 `v*` 标签触发。
标签构建还会把对应架构的 DMG 和 ZIP 自动上传到同版本的 GitHub Release。
