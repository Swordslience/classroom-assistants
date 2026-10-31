# Windows 构建

需要 .NET 8 SDK、Windows 10 SDK 和 NuGet 网络访问。拍照客户端目标为 .NET Framework 4.8 x86，Windows 需支持 .NET Framework 4.8；独立 OCR 为 .NET 8 win-x64 子进程。希沃客户端目标为 .NET 6 Windows Desktop，并通过 dotnetCampus.EasiPlugin.Sdk 引用本机希沃安装目录；没有希沃时可以只构建拍照客户端。

```powershell
# 两个客户端（不自动安装希沃插件）
./scripts/build.ps1
# 只有独立拍照客户端
./scripts/build.ps1 -PhotoOnly
# Windows SDK 不同版本时
./scripts/build.ps1 -WindowsMetadataPath 'C:/Program Files (x86)/Windows Kits/10/UnionMetadata/10.0.22621.0/Windows.winmd'
```

共享更新器位于 `PhotoTutor/CloudUpdates`，必须先构建。两个客户端均引用它生成的 exe。不要把 bin/obj 中的 SDK、希沃 dll 或生成代码提交回仓库。

`Native/SeewoPlugin/CompileOnly.csproj` 用于安全编译，关闭了 SDK 的自动安装和开发注册钩子。正式打包需要按 SDK 官方流程操作；本仓库不分发希沃本体及 SDK 工具副本。不要在正在授课的电脑上直接运行常规插件开发安装命令。

`PhotoTutor/LocalOcr/runtime.zip` 是最新交付使用的独立 OCR 运行时，包含模型和第三方声明，构建时原样嵌入；其 SHA256 见 runtime-manifest.json。独立 OCR 源码也在 LocalOcr。修改算法后应重新 publish 该项目、用 models-source.json 指定来源及哈希核对模型，并重新生成完整运行时清单；不可只改 exe 而保留旧清单。

```powershell
# 仅编译 OCR 源码；不是自动替换已经嵌入的运行时
dotnet build PhotoTutor/LocalOcr/LocalOcr.csproj -c Release
# 离线回归（不调用付费模型，不安装插件）
./scripts/test-offline.ps1
```

自动化测试输出到 artifacts，测试模拟数据不代表对数学答案正确性或真实展台清晰度的保证。
