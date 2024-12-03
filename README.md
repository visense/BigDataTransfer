# 大数据中心文件传输助手

一个基于 FRPC 的文件传输工具，用于安全地传输文件。

## 功能特点

- 使用 FRPC 进行安全的文件传输
- 支持 SFTP 协议
- 简单易用的图形界面
- 自动管理连接和进程
- 详细的日志记录

## 系统要求

- Windows 操作系统
- .NET 8.0 或更高版本

## 使用说明

1. 运行程序
2. 输入连接信息（主机地址、用户名、密码、端口）
3. 点击连接按钮建立连接
4. 使用文件浏览器进行文件传输
5. 完成后关闭程序，程序会自动清理所有连接

## 最新更新

- 修复了程序关闭时端口未释放的问题
- 改进了进程终止机制
- 添加了详细的日志记录
- 优化了错误处理

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
git clone https://github.com/visense/BigDataTransfer.git
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

MIT License - 详见 [LICENSE](LICENSE) 文件

## 贡献

欢迎提交 Issue 和 Pull Request！

## 作者

[visense]
