# 来源与修改说明

本仓库是 `FH6 Road Scanner` 的修改版本。

## 扫描业务

- 原项目：[LahantziBade/FH6-Road-Scanner](https://github.com/LahantziBade/FH6-Road-Scanner)
- 原作者：飛飝LahantziBade
- Bilibili UID：9997742
- 许可：仓库根目录 `LICENSE`

本版本保留原扫描思路和 Python 实现，将原 Tkinter 界面移除，并把扫描逻辑改造成供 WPF 前端调用的 NDJSON 后端。

## WPF 界面

- 界面作者：Dr.Hydra
- 当前项目仓库：[Dr-hydra/FH6-Road-Scanner](https://github.com/Dr-hydra/FH6-Road-Scanner)
- UI 框架：[Dr-hydra/QING.UIKIT](https://github.com/Dr-hydra/QING.UIKIT)
- QING.UIKIT 的界面代码修改自：[Meloong-Git/PCL](https://github.com/Meloong-Git/PCL)
- 许可：`LICENSES/QING.UIKIT-GPL-3.0.txt`

本版本使用 QING.UIKIT 的窗口壳、主题、动画、卡片和输入控件，并针对道路扫描器重新设计业务页面。

## 基础组件

- QING.Core 许可：`LICENSES/QING.Core-LICENSE.txt`

各部分继续遵循各自许可证。本说明不将这些许可证合并或替换为单一许可证。
