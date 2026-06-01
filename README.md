# KAnim GUI

KAnim GUI 是一个面向 Oxygen Not Included 动画资源的 Windows 图形工具。它把 `kanimal-cli.exe` 的常用转换命令包装成可拖放、可批量处理、带日志和预览器的桌面界面。

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
- 预览辅助：帧跳转、元素高亮、原点/包围盒显示、缩放和平移
- `.txt` 兼容：可选将 `_anim.txt` / `_build.txt` 自动复制为 `.bytes`

## 依赖

本工具本身是 GUI 外壳，实际格式转换依赖 [kanimal-SE](https://github.com/skairunner/kanimal-SE) 的 `kanimal-cli.exe`。

程序会按以下顺序查找 `kanimal-cli.exe`：

1. 设置中手动指定的路径
2. 程序当前工作目录
3. 程序所在目录
4. 系统 `PATH`

如果发布包里没有自带 `kanimal-cli.exe`，请把它放到 `KAnimGui.exe` 同目录，或在设置里指定完整路径。

## 下载与运行

1. 从 Release 下载 `KAnimGui-win-x64.zip`
2. 解压到任意目录
3. 准备 `kanimal-cli.exe`
4. 运行 `KAnimGui.exe`

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

## 日志

转换日志会显示在界面中，并自动写入本地 log 文件。日志里会包含实际执行的 `kanimal-cli.exe` 命令和标准输出/错误输出。

如果转换失败，优先查看日志中的：

- 是否找到 `kanimal-cli.exe`
- 输入文件是否配对
- 输出目录是否可写
- CLI 返回的错误信息

## 常见问题

### 提示找不到 kanimal-cli.exe

把 `kanimal-cli.exe` 放到 `KAnimGui.exe` 同目录，或打开设置并指定它的路径。

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

发布 win-x64 自包含包：

```powershell
dotnet publish KAnimGui/KAnimGui.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o artifacts/KAnimGui-win-x64
```

## 致谢

- [skairunner/kanimal-SE](https://github.com/skairunner/kanimal-SE)：核心 KAnim/SCML 转换命令
- [romen-h/kanim-explorer](https://github.com/romen-h/kanim-explorer)：KAnim 预览和结构检查方向的参考

