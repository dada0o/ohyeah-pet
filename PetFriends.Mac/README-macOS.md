# macOS 版本

这是“小欧公爵和小耶牧师”桌宠的 macOS/Avalonia 版本。Windows WPF 工程保持不变，Mac 版本位于独立的 `PetFriends.Mac` 目录。

## 已实现

- 透明、无边框、始终置顶的双桌宠窗口
- 单击摸摸、按住拖动、触控板辅助点击/鼠标右键菜单
- 对话气泡、爱心、跳跃、摇摆和粒子动画
- 自动散步、专注陪伴、全屏撒欢和屏幕边缘探头
- 自动靠近以及亲脸颊、碰鼻子、牵爪、击掌、跳舞、零食、打盹等双宠互动
- 菜单栏状态图标、叫回桌面、切换模式和退出
- 迷你、默认、大号三档尺寸以及安静模式
- Apple Silicon（`osx-arm64`）和 Intel（`osx-x64`）自包含打包

Windows 原版的“识别其他应用窗口并坐在窗口上沿/躲到窗口后面”依赖 Win32/DWM。Mac 版本当前改为屏幕范围活动与边缘探头，不请求 macOS 辅助功能权限。

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
PetFriends.Mac/dist/osx-arm64/小欧公爵和小耶牧师.app
PetFriends.Mac/dist/osx-arm64/PetFriends-osx-arm64.zip
```

发布包是自包含版本，目标 Mac 不需要另装 .NET。

## 首次打开

当前脚本进行 ad-hoc 本地签名，没有使用付费 Apple Developer ID，也没有提交 Apple 公证。请只运行你自己构建或从可信仓库下载的版本。

第一次打开时，可在 Finder 中按住 Control 点击应用并选择“打开”。若系统仍然阻止，在“系统设置 → 隐私与安全性”中确认并选择“仍要打开”。

## GitHub Actions

`.github/workflows/build-macos.yml` 会同时构建 Apple Silicon 和 Intel 压缩包。可在仓库 Actions 页面手动运行，或推送 `v*` 标签触发。
