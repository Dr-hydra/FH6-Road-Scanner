# 地平线道路扫描器 / FH6 Road Scanner

一个用于《极限竞速：地平线 6》地图道路探索的辅助工具。程序逐行移动鼠标，并通过截图检测左下角“快速移动”提示是否消失，帮助定位可能遗漏的未探索道路。

本仓库是 [LahantziBade/FH6-Road-Scanner](https://github.com/LahantziBade/FH6-Road-Scanner) 的修改版本，当前项目仓库为 [Dr-hydra/FH6-Road-Scanner](https://github.com/Dr-hydra/FH6-Road-Scanner)。扫描业务原作者为 **飛飝LahantziBade**，界面作者为 **Dr.Hydra**。

## 功能

- 使用 QING.UIKIT 的 WPF 窗口、主题、动画与控件
- 自动逐行扫描地图并检测疑似未探索道路
- 命中后自动截图、蜂鸣并按配置停止
- 支持可配置的全局开始、停止快捷键，默认分别为 F7 和 F8
- 软件启动后立即显示置顶悬浮窗，实时展示状态、进度和快捷键提示
- 支持自定义扫描区域、检测区域、步长、延迟和阈值
- 配置文件兼容原版 `config.json` 字段

## 使用

1. 从 Releases 下载 Windows x64 压缩包并解压：
   - `self-contained`：自带 .NET 运行库，解压即用。
   - `framework-dependent`：文件更小，需要预先安装 .NET 10 Desktop Runtime x64。
2. 运行 `FH6RoadScanner.exe`。
3. 打开游戏地图，将鼠标放到已探索道路上，确认左下角出现“快速移动”。
4. 点击“截取 / 更新模板”，再点击“测试当前差异”。
5. 点击“开始扫描”或按默认快捷键 F7，在倒计时内切回游戏地图。
6. 扫描过程中可在程序中停止，或按默认快捷键 F8 停止。
7. 可在“设置”页修改开始和停止快捷键，支持 F1 至 F12。

两个版本均已包含 Python 后端，无需安装 Python。压缩包根目录中的 `FH6RoadScanner.exe` 是主程序，`FH6ScannerBackend.exe` 是随主程序自动启动的扫描后端。

也可在软件启动前直接修改程序目录中的 `config.json`。软件仅在启动时读取该文件，运行期间修改不会自动生效；`start_hotkey` 和 `stop_hotkey` 应填写不同的 F1 至 F12 键值。

## 开发

环境要求：

- Windows x64
- .NET 10 SDK
- Python 3.12+

```powershell
python -m pip install -r requirements-dev.txt
python -m unittest discover -s tests -v
dotnet build .\FH6RoadScanner.sln -c Release
dotnet run --project .\src\FH6RoadScanner\FH6RoadScanner.vbproj
```

生成便携发行包：

```powershell
.\scripts\build-release.ps1 -Version 1.1.0
```

产物位于：

- `artifacts/FH6RoadScanner-1.1.0-win-x64-self-contained.zip`
- `artifacts/FH6RoadScanner-1.1.0-win-x64-framework-dependent.zip`

## 来源与许可证

- Python 扫描业务：原作者飛飝LahantziBade，遵循根目录 `LICENSE`。
- WPF/UI：来自 [QING.UIKIT](https://github.com/Dr-hydra/QING.UIKIT)，遵循 GPLv3。
- QING.UIKIT 的界面代码修改自 [Meloong-Git/PCL](https://github.com/Meloong-Git/PCL)。
- QING.Core：遵循 `LICENSES/QING.Core-LICENSE.txt`。

完整修改说明见 [NOTICE.md](./NOTICE.md)。本软件完全免费，请勿用于售卖或倒卖。
