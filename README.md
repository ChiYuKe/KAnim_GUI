# KAnim GUI

KAnim GUI 是一个面向 Oxygen Not Included 动画资源的 Windows 图形工具。它提供可拖放、可批量处理、带日志和预览器的桌面界面，并内置常用 KAnim/SCML 转换内核。

<p align="center">
  <img src="https://github.com/ChiYuKe/ONI-Kanimal_GUI/blob/main/Assets/hqbase.png" width="180" height="180" alt="ONI">
</p>

https://github.com/user-attachments/assets/b1877eb9-6db7-4561-a3bf-86b44f2fe261

## 功能

- KAnim 转 SCML：`.png` + `_anim.bytes` + `_build.bytes` 转出 Spriter `.scml`
- SCML 转 KAnim：`.scml` 转回 ONI 可用的 KAnim 资源
- 批量转换：自动配对同名 `png/anim/build` 文件组
- 拖放导入：直接把资源文件拖进窗口
- 自动日志：转换过程自动写入 log 文件，便于排查失败原因
- KAnim 预览器：查看 build/symbol/frame/animation 结构，播放动画并检查帧元素
- 内置解码/诊断/转换：直接读取 `_anim.bytes` / `_build.bytes`，支持 KAnim 与 SCML 双向转换，并检查引用、计数、UV、颜色和矩阵异常
- ONI 资源桥客户端：连接本机 ONI Mod，查看游戏已加载的动画资源
- 预览辅助：帧跳转、元素高亮、原点/包围盒显示、缩放和平移
- `.txt` 兼容：可选将 `_anim.txt` / `_build.txt` 自动复制为 `.bytes`

## 依赖

本工具内置了 KAnim 解码、诊断、`KAnim -> SCML` 和 `SCML -> KAnim` 导出内核；如果找到 [kanimal-SE](https://github.com/skairunner/kanimal-SE) 的 `kanimal-cli.exe`，仍会优先沿用 CLI 输出。
如果未找到 `kanimal-cli.exe`，两种转换都会自动回退到内置内核。当前内置 `SCML -> KAnim` 不支持插值和去骨骼选项；需要这些高级处理时请配置 `kanimal-cli.exe`。

GIF 动画导出内置 FFmpeg 8.1.2，并使用 `palettegen` / `paletteuse` 和可选缩放算法（Lanczos、Bicubic、Spline、Nearest）改善颜色、抖动和边缘质量。播放速度、输出尺寸、缩放算法、输出目录和完成通知选项会持久化到用户配置。应用设置统一使用用户级持久化配置，不再依赖旧的 `KAnimGui.dll.config` 模板。FFmpeg 压缩包会随发布包复制到 `Resources/ffmpeg`，首次导出时解压到当前用户的 KAnimGui 工具缓存；导出的临时 PNG 和调色板会在完成后自动删除。FFmpeg 的许可证见 `KAnimGui/Resources/ffmpeg/LICENSE-FFMPEG.txt`。

程序会按以下顺序查找 `kanimal-cli.exe`：

1. 设置中手动指定的路径
2. 程序当前工作目录
3. 程序所在目录
4. 系统 `PATH`

如果发布包里没有自带 `kanimal-cli.exe`，普通双向转换仍可使用内置内核；如需 CLI 的严格兼容或高级处理，请把它放到 `KAnimGui.exe` 同目录，或在设置里指定完整路径。

## 字体许可

界面内置 HarmonyOS Sans SC（Regular/Bold）字体，字体版权归 Huawei Device Co., Ltd. 所有。字体随程序资源发布，完整许可协议见 `KAnimGui/Fonts/HarmonyOS_Sans_SC/LICENSE.txt`。

## 架构与扩展

项目按依赖方向分为四层：

- `KAnimGui.Core`：不依赖 WPF、HTTP 或用户目录的 KAnim 数据模型、二进制读写、解析规则与预览基础能力
- `KAnimGui.Application`：转换与资源桥的请求/结果类型和接口
- `KAnimGui.Infrastructure`：HTTP、文件导出、缩略图缓存、JSON 状态、CLI 进程和 `.txt` 输入准备
- `KAnimGui`：WPF 视图、ViewModel、窗口导航和渲染互操作；主工作台由 `MainWindowController` 协调转换、拖放和导航，预览器的加载、树模型、播放协调、参数检查、渲染缓存、文件选择与 PNG 操作分别由 Presentation 服务承载

Core 的二进制编解码器只接收 `Stream`，纹理以 `KAnimTextureData`（PNG 字节 + 尺寸）跨层传递；`BitmapImage` 只在 WPF 外层适配，避免纯业务层被 UI 类型污染。

应用启动时由 `App` 统一组装依赖。扩展新的转换器或资源类型时，先在 Application 增加强类型请求和接口，再在 Infrastructure 提供实现，最后在 ViewModel 注册命令；窗口和 ViewModel 通过文件系统/外部启动器网关访问本机资源，不直接依赖 `HttpClient`、`File` 或 `Process`。资源桥状态只保存导出布局，保存在 `%LOCALAPPDATA%\KAnimGui\ResourceBridgeState.json`；在线缩略图缓存在 `%LOCALAPPDATA%\KAnimGui\Cache\ResourceBridge`。

内置 `KAnim -> SCML` 导出会尽量兼容 `kanimal-cli.exe` 的坐标、角度、缩放、pivot 和帧时间；当动画引用当前 build 不包含的外部 symbol/frame 时，会写入透明 1x1 占位图并在诊断报告中标出。

## 兼容性说明

本项目是面向 Oxygen Not Included KAnim 资源格式的独立辅助工具，不包含、分发或替换游戏本体源码与素材。项目中的格式解析和预览逻辑用于资源兼容与 Mod 制作辅助；Oxygen Not Included、Klei 以及相关名称和素材权利归其各自所有者所有。

## 下载与运行

1. 从 Release 下载 `KAnimGui-win-x64.zip`
2. 解压到任意目录
3. 运行 `KAnimGui.exe`

发布包为 `win-x64` 自包含版本，通常不需要额外安装 .NET Runtime。

## 使用

### KAnim -> SCML

准备同一套资源：

```text
example.png
example_anim.bytes
example_build.bytes
```

在 `KAnim -> SCML` 页签中选择或拖入三个文件，选择输出目录后点击 `开始转换`。

常用选项：

- `严格模式`：传递 `-S`
- `严格文件顺序`：传递 `-f`，按 Build、Anim 的顺序交给 `kanimal-cli.exe`
- `批量转换`：选择包含多组 KAnim 文件的文件夹，程序会按基础文件名自动配对

### SCML -> KAnim

在 `SCML -> KAnim` 页签中选择 `.scml` 文件和输出目录，然后点击 `开始转换`。

常用选项：

- `启用插值`：传递 `-i`
- `去骨骼`：传递 `-b`
- `批量转换`：一次选择多个 `.scml` 文件逐个转换

## 预览器

点击顶部实验/预览入口可打开 KAnim 预览窗口。预览器支持：

- 打开 `.png`、`_anim.bytes`、`_build.bytes`
- 查看 Build、Symbol、Frame、Animation、Element 树
- 搜索 symbol / animation
- 播放、暂停、上一帧、下一帧
- 帧滑条快速跳转
- 属性、帧列表、元素列表页签
- 元素高亮
- 原点和包围盒叠加显示
- 鼠标滚轮缩放、左键拖动平移、双击恢复视图
- 导出或替换选中的 Symbol/Frame 图片
- 诊断当前 KAnim 包并生成结构报告

## ONI 资源桥

如果安装并启用了 `ONIResourceBridge` 模组，游戏启动后会在本机 `127.0.0.1` 开放资源桥端口。点击 KAnimGUI 顶部的资源桥按钮，可以查看游戏当前已加载的 KAnim/Sprite 资源、搜索名称或 Bundle，并在线加载缩略图；选中资源后可直接导出 PNG、`_anim.bytes` 和 `_build.bytes`，也可以批量导出当前筛选结果。

资源桥窗口只保留“筛选 → 缩略图预览 → 导出”主流程，不再维护收藏、标签或导入工作区状态。导出支持“按资源分目录”和“按文件类型分组”两种布局，并在底部显示批量进度、成功/失败数量和失败报告位置。

KAnim 资源行的操作按钮会显示为“预览”：点击后会把在线 KAnim 临时写入本地缓存，并自动打开 KAnim 预览器播放动画；不会把预览文件写入正式导出目录。Sprite 资源仍直接提供“导出”。

资源桥只绑定本机地址，不对局域网或公网开放。默认端口为 `17871`，被占用时会尝试到 `17890`，实际端口状态会写入 `%LOCALAPPDATA%\KAnimGui\ONIResourceBridge.json`。

## 日志

转换日志会显示在界面中，并自动写入本地 log 文件。使用 CLI 时，日志里会包含实际执行的 `kanimal-cli.exe` 命令和标准输出/错误输出；使用内置内核时，日志会显示内置导出的目标文件。

如果转换失败，优先查看日志中的：

- 是否找到 `kanimal-cli.exe`
- 输入文件是否配对
- 输出目录是否可写
- CLI 返回的错误信息

## 常见问题

### 提示找不到 kanimal-cli.exe

普通转换会自动使用内置内核；如果需要 SCML 插值、去骨骼或完全沿用 kanimal-SE 行为，把 `kanimal-cli.exe` 放到 `KAnimGui.exe` 同目录，或打开设置并指定它的路径。

### 批量转换找不到文件组

批量 KAnim 转 SCML 只扫描所选文件夹的顶层目录，并要求基础文件名一致：

```text
name.png
name_anim.bytes
name_build.bytes
```

### `.txt` 文件能不能转

可以。在设置中启用 `.txt` 转 `.bytes` 后，程序会把 `_anim.txt` / `_build.txt` 复制成对应 `.bytes` 再执行转换。

### 预览器播放卡顿

预览器会缓存动画帧，播放时尽量使用缓存画面。辅助显示如包围盒、元素高亮主要用于暂停检查，播放时会避免每帧重绘检查层。

## 开发

环境：

- Windows
- .NET 8 SDK
- WPF

构建：

```powershell
dotnet build KAnimGui.sln --no-restore
```

运行测试：

```powershell
dotnet test KAnimGui.sln --configuration Release --no-restore
```

采集并校验 Core/Application 行覆盖率（门槛 80%）：

```powershell
$out = Join-Path $env:TEMP "kanimgui-coverage"
dotnet test KAnimGui.sln -c Release --no-build --no-restore --settings coverage.runsettings --collect:"XPlat Code Coverage" --results-directory $out
$report = Get-ChildItem $out -Recurse -Filter coverage.cobertura.xml | Select-Object -First 1
./check-coverage.ps1 -CoverageFile $report.FullName
```

CI 会在 Windows runner 上完成 Release 构建、测试和 `win-x64` 自包含发布验证；发布目录是临时构建产物，不在 Git 中维护生成的 ZIP。

发布 win-x64 自包含包：

```powershell
dotnet publish KAnimGui/KAnimGui.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o artifacts/KAnimGui-win-x64
```

## 致谢

- [skairunner/kanimal-SE](https://github.com/skairunner/kanimal-SE)：核心 KAnim/SCML 转换命令
- [romen-h/kanim-explorer](https://github.com/romen-h/kanim-explorer)：KAnim 预览和结构检查方向的参考
