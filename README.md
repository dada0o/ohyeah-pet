# 小欧公爵和小耶牧师桌宠

一对可以拖动、摸摸、自由活动并彼此互动的 Windows / macOS 桌宠。小欧公爵是一只黑猫，小耶牧师是一只白狗。

## 主要功能

- 单击摸摸，按住即可拖动
- 两只桌宠会主动靠近并触发贴贴、亲脸颊、碰鼻子、牵爪、跳舞、追逐、分享点心等互动
- “专注陪伴”和“全屏撒欢”两种活动模式
- 可坐在应用窗口上沿、躲到窗口后面或从窗口左右边缘一起探头
- 从当前窗口左边缘探头时逆时针歪头，右边缘探头时顺时针歪头
- 躲到屏幕左边缘时顺时针旋转，右边缘时逆时针旋转
- 屏幕边缘探头、安静模式、尺寸调节和托盘菜单
- 默认开机自动启动，可从桌宠右键菜单或托盘菜单随时关闭、重新开启

## 下载与运行

macOS 最新版：**v1.2.1**；Windows 当前稳定版：**v1.2.0**。请选择与你的系统和芯片匹配的文件：

| 系统 | 推荐下载 | 备用下载 | 说明 |
| --- | --- | --- | --- |
| Windows 10 / 11（64 位） | [直接下载 EXE](https://github.com/dada0o/ohyeah-pet/releases/download/v1.2.0/PetFriends-v1.2.0-Windows10-11-x64.exe) | [下载 ZIP](https://github.com/dada0o/ohyeah-pet/releases/download/v1.2.0/PetFriends-v1.2.0-Windows10-11-x64.zip) | EXE 下载后双击即可使用；ZIP 需先完整解压 |
| Windows 7 SP1（32/64 位） | [下载 .NET 3.5.1 免安装版 ZIP](https://github.com/dada0o/ohyeah-pet/releases/download/v1.2.0/PetFriends-v1.2.0-Windows7-NoInstall-x86-x64.zip) | — | 完整解压后直接运行，不需要安装 .NET Framework 4.8 |
| macOS Apple 芯片（M1/M2/M3/M4 等） | [下载 v1.2.1 DMG](https://github.com/dada0o/ohyeah-pet/releases/download/v1.2.1/PetFriends-v1.2.1-macOS-Apple-Silicon.dmg) | [下载 ZIP](https://github.com/dada0o/ohyeah-pet/releases/download/v1.2.1/PetFriends-v1.2.1-macOS-Apple-Silicon.zip) | 推荐使用 DMG |
| macOS Intel 芯片 | [下载 v1.2.1 DMG](https://github.com/dada0o/ohyeah-pet/releases/download/v1.2.1/PetFriends-v1.2.1-macOS-Intel.dmg) | [下载 ZIP](https://github.com/dada0o/ohyeah-pet/releases/download/v1.2.1/PetFriends-v1.2.1-macOS-Intel.zip) | 推荐使用 DMG |

[查看 macOS v1.2.1 版本说明和全部文件](https://github.com/dada0o/ohyeah-pet/releases/tag/v1.2.1)

Windows 10 和 Windows 11 使用同一个自包含版本，不需要另外安装 .NET。Windows 11 会自动启用兼容渲染模式，避免透明桌宠窗口触发显卡驱动异常；程序还会阻止重复启动，并把启动、退出或异常记录到 `%LOCALAPPDATA%\PetFriends\runtime.log`，便于区分软件重启和系统重启。

Windows 7 SP1 使用单独的 `net35` 免安装版，依赖系统自带的 .NET Framework 3.5.1，不需要安装 .NET Framework 4.8。下载 ZIP 后须完整解压，并保留 EXE 与同名 EXE.config 在同一文件夹。

新版第一次运行后会默认开启开机自动启动。可在任意一只桌宠的右键菜单或托盘菜单中取消“开机自动启动”，程序会记住你的选择，不会在下次运行时自行恢复。macOS 用户请先把 App 从 DMG 拖到“应用程序”文件夹；如果直接在 DMG 中运行，程序不会登记失效的自动启动路径。

### 历史版本下载

| 版本 | 系统 | 下载 |
| --- | --- | --- |
| v1.2.0 | Windows 10 / 11 | [EXE](https://github.com/dada0o/ohyeah-pet/releases/download/v1.2.0/PetFriends-v1.2.0-Windows10-11-x64.exe) · [ZIP](https://github.com/dada0o/ohyeah-pet/releases/download/v1.2.0/PetFriends-v1.2.0-Windows10-11-x64.zip) |
| v1.2.0 | Windows 7 | [免安装版 ZIP](https://github.com/dada0o/ohyeah-pet/releases/download/v1.2.0/PetFriends-v1.2.0-Windows7-NoInstall-x86-x64.zip) |
| v1.2.0 | macOS Apple 芯片 | [DMG](https://github.com/dada0o/ohyeah-pet/releases/download/v1.2.0/PetFriends-v1.2.0-macOS-Apple-Silicon.dmg) · [ZIP](https://github.com/dada0o/ohyeah-pet/releases/download/v1.2.0/PetFriends-v1.2.0-macOS-Apple-Silicon.zip) |
| v1.2.0 | macOS Intel 芯片 | [DMG](https://github.com/dada0o/ohyeah-pet/releases/download/v1.2.0/PetFriends-v1.2.0-macOS-Intel.dmg) · [ZIP](https://github.com/dada0o/ohyeah-pet/releases/download/v1.2.0/PetFriends-v1.2.0-macOS-Intel.zip) |
| v1.1.2 | Windows 10 / 11 | [EXE](https://github.com/dada0o/ohyeah-pet/releases/download/v1.1.2/PetFriends-v1.1.2-Windows10-11-x64.exe) · [ZIP](https://github.com/dada0o/ohyeah-pet/releases/download/v1.1.2/PetFriends-v1.1.2-Windows10-11-x64.zip) |
| v1.1.2 | Windows 7 | [免安装版 ZIP](https://github.com/dada0o/ohyeah-pet/releases/download/v1.1.2/PetFriends-Windows7-Legacy-v1.0.0-x86-x64.zip) |
| v1.1.2 | macOS Apple 芯片 | [DMG](https://github.com/dada0o/ohyeah-pet/releases/download/v1.1.2/PetFriends-v1.1.2-macOS-Apple-Silicon.dmg) · [ZIP](https://github.com/dada0o/ohyeah-pet/releases/download/v1.1.2/PetFriends-v1.1.2-macOS-Apple-Silicon.zip) |
| v1.1.2 | macOS Intel 芯片 | [DMG](https://github.com/dada0o/ohyeah-pet/releases/download/v1.1.2/PetFriends-v1.1.2-macOS-Intel.dmg) · [ZIP](https://github.com/dada0o/ohyeah-pet/releases/download/v1.1.2/PetFriends-v1.1.2-macOS-Intel.zip) |
| v1.1.1 | macOS Apple 芯片 | [DMG](https://github.com/dada0o/ohyeah-pet/releases/download/v1.1.1/PetFriends-v1.1.1-macOS-Apple-Silicon.dmg) · [ZIP](https://github.com/dada0o/ohyeah-pet/releases/download/v1.1.1/PetFriends-v1.1.1-macOS-Apple-Silicon.zip) |
| v1.1.1 | macOS Intel 芯片 | [DMG](https://github.com/dada0o/ohyeah-pet/releases/download/v1.1.1/PetFriends-v1.1.1-macOS-Intel.dmg) · [ZIP](https://github.com/dada0o/ohyeah-pet/releases/download/v1.1.1/PetFriends-v1.1.1-macOS-Intel.zip) |
| v1.0.0 | Windows | [下载 ZIP](https://github.com/dada0o/ohyeah-pet/releases/download/v1.0.0/default.zip) |

[查看全部历史版本](https://github.com/dada0o/ohyeah-pet/releases)

## 从源码构建

需要安装 .NET 8 SDK：

```powershell
./build-windows.ps1 -Version 1.2.0 -Architecture x64
```

输出包含可直接双击的版本化 EXE 和 ZIP。GitHub 的 Windows 工作流会在 `v*` 标签发布时自动构建 Windows 10/11 与 Win7 `.NET 3.5.1` 免安装版，并上传到对应 Release。

### macOS

Mac 版本使用 Avalonia 12 和 .NET 9，支持 Apple Silicon 与 Intel：

```bash
chmod +x PetFriends.Mac/build-macos.sh
./PetFriends.Mac/build-macos.sh arm64  # Apple Silicon
./PetFriends.Mac/build-macos.sh x64    # Intel
```

构建脚本会同时生成 ZIP 和 DMG；两种格式都自带运行环境，目标 Mac 不需要另装 .NET。

完整功能和首次运行说明见 [PetFriends.Mac/README-macOS.md](PetFriends.Mac/README-macOS.md)。Mac 版本保留透明悬浮、拖动、摸摸、双宠互动、两种活动模式、屏幕边缘探头、尺寸与安静模式，并通过 macOS 原生 API 支持识别普通应用窗口、坐到窗口上沿、躲到窗口后面和双宠窗口边缘探头；不需要辅助功能或屏幕录制权限。

更完整的操作说明见 [README.txt](README.txt)。

## 素材说明

角色形象与素材随本项目提供。请勿在未经原作者允许的情况下将角色素材用于商业用途或重新分发。
