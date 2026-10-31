# 课堂工具开发实践 / Classroom Assistants

分享教师在课堂工具开发与使用中的思路和参考实现，包含两个 Windows 桌面客户端。

| 工具 | 代码基线 | 目录 |
| --- | --- | --- |
| 深度求索：拍照选题、单页多区域与跨页组题、流式讲解、公式显示 | 1.6.4 | `PhotoTutor/` |
| 希沃课堂助教（历史界面名“奶龙课堂助教”）：课堂问答、配图、照片点评、语音及课件操作 | 0.4.19 | `Native/` |

项目需求、课堂流程和反馈由教师提出，代码实现主要采用 AI 辅助开发，并使用现有开源组件及模型服务。这里分享的是应用型工程实践，不宣称原创基础模型、通用最优算法或所有课堂设备上的稳定性。

## 分享范围

本仓库提供桌面应用工程源码，面向研究、构建和进一步开发。首次发布不附公众安装包。模型密钥均留空，需使用者自行申请并配置；云服务费用由使用者自己的账号承担。

本轮不包含手机网页与服务端、作者的云部署、账号数据、绑定关系、课件和私人材料。桌面端保留相关接口，但手机遥控需要自行部署兼容服务，复杂公式的云排版需要自行配置对应端点。未配置时不能视为与内部环境完全相同。

## 构建

Windows 上准备 .NET 8 SDK、.NET Framework 4.8 与 Windows 10 SDK（默认 Windows.winmd 版本 10.0.22621.0）。希沃端还需要合法取得的希沃白板 5 和 .NET 6 Windows Desktop Runtime。

```powershell
# 只有拍照客户端
powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -PhotoOnly
# 两个客户端；使用 CompileOnly 项目，不自动注册或安装希沃插件
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
```

输出为 `PhotoTutor/src/bin/Release/net48/拍照解题助教.exe` 和 `Native/SeewoPlugin/bin/Release/net6.0-windows/`。详细前置条件见 [构建说明](docs/BUILD.md)。独立 OCR 源码和现有运行时清单位于 `PhotoTutor/LocalOcr/`；运行时带原有哈希和第三方声明。

## 配置

使用客户端现有的接口设置填写自己的 Key。文字、视觉、生图、语音分别需要相应模型权限；当前模型标识需与使用者账号开通情况相符。空配置示例在 `examples/`，详见 [配置说明](docs/CONFIGURATION.md)。

可选复杂公式端点使用 `CLASSROOM_SERVICE_URL`；自建签名更新源使用 `CLASSROOM_UPDATE_SERVER_URL` 和 `CLASSROOM_UPDATE_PUBLIC_KEY_PATH`。默认不连接作者自用服务，不附发布私钥，也不会默认安装作者内部更新包。

## 核验与已知边界

公开副本在维护者本机完成两个桌面端、更新器和 OCR 源码构建；公开配置检查 14 项通过，更新器离线自检通过。本轮未调用付费模型、安装插件或重新测试学校实体展台。自动题框需要人工确认，真实识别准确率、图像清晰度、校园网络及模型回答正确性仍需现场验证。

欢迎通过 Issues 提供可复现问题，或通过 Pull requests 分享修改。请勿提交 API Key、云凭证、学生照片、原始账号数据或更新私钥。

## 许可和依赖

项目有权许可的代码采用 [MIT](LICENSE)。第三方库、OCR 模型、字体及 SDK 保留各自许可，见 [第三方说明](THIRD_PARTY_NOTICES.md)。希沃 SDK 以依赖形式引用，本仓库不分发希沃本体及 SDK 工具；本项目不代表希沃、DeepSeek、火山引擎或“奶龙”权利人的官方产品。
