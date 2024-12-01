# 大数据中心文件传输助手

一个用于管理和控制 FRPC 网络隧道的 Windows 桌面应用程序。

## 功能特点

- 系统托盘集成
- 自动管理 FRPC 服务
- 实时状态监控
- 开机自启动选项
- 中文界面

## 系统要求

- Windows 7 及以上版本
- .NET 6.0 运行时
- 管理员权限（用于服务管理）

## 安装

1. 下载最新的安装程序 `大数据中心文件传输助手_Setup.exe`
2. 运行安装程序
3. 根据向导完成安装

## 开发环境

- Visual Studio 2022
- .NET 6.0 SDK
- Inno Setup 6（用于制作安装程序）

## 构建说明

1. 克隆仓库
```bash
git clone https://github.com/YourUsername/BigDataTransfer.git
```

2. 使用 Visual Studio 打开解决方案

3. 构建项目
```bash
dotnet build --configuration Release
```

4. 生成安装程序
```bash
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" setup.iss
```

## 目录结构

- `FRPCManager.cs` - 主程序代码
- `FRPCManager.csproj` - 项目配置
- `setup.iss` - 安装程序脚本
- `1.ico`, `2.ico` - 程序图标
- `frpc.exe` - FRPC 核心程序
- `frpc.toml` - FRPC 配置文件

## 许可证

[MIT License](LICENSE)

## 贡献

欢迎提交 Issue 和 Pull Request！

## 作者

[Your Name]
