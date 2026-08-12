# 小欧公爵和小耶牧师桌宠

一对可以拖动、摸摸、自由活动并彼此互动的 Windows 桌宠。小欧公爵是一只黑猫，小耶牧师是一只白狗。

## 主要功能

- 单击摸摸，按住即可拖动
- 两只桌宠会主动靠近并触发贴贴、亲脸颊、碰鼻子、牵爪、跳舞、追逐、分享点心等互动
- “专注陪伴”和“全屏撒欢”两种活动模式
- 可坐在应用窗口上沿、躲到窗口后面或从窗口左右边缘一起探头
- 窗口左边缘探头时逆时针歪头，右边缘探头时顺时针歪头
- 屏幕边缘探头、安静模式、尺寸调节和托盘菜单

## 下载与运行

前往仓库的 **Releases** 页面下载 `小欧公爵和小耶牧师桌宠.zip`，解压后双击其中的 EXE 文件即可运行。

系统要求：Windows 10/11，64 位。发布包为自包含版本，不需要另外安装 .NET。

## 从源码构建

需要安装 .NET 8 SDK：

```powershell
dotnet publish PetFriends.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

更完整的操作说明见 [README.txt](README.txt)。

## 素材说明

角色形象与素材随本项目提供。请勿在未经原作者允许的情况下将角色素材用于商业用途或重新分发。
