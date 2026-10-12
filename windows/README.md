# Screenline · 截图晾台

改编自 **[Alejandro Buján](https://github.com/alejandrobujan)** 的 **[Tendedero](https://github.com/alejandrobujan/tendedero)**。原项目的截图晾衣绳交互与产品设计归原作者；本项目使用 C#、WPF、Win32 和 OLE 独立实现 Windows 适配，采用 Screenline 名称与自己生成的图标，独立维护，不代表原作者或获得其背书。

当前版本：**0.4.1**。透明的屏幕顶边浮层：下垂细绳、小夹子、按原比例显示的照片。保留 0.4.0 的浅色预览与标注窗口，本版集中优化响应：图像处理和写盘移到后台、重复图片提前去重、截图飞入使用局部窗口并减少动画中的布局更新。照片使用局部系统背景模糊；管理操作集中在系统托盘，照片悬停时显示删除按钮，空白区域允许点击穿透到下方应用。

本机合成 4K 图片测试，每版三次取中位数：收纳总耗时由 787ms 降至 407ms，收纳期间 UI 心跳最大间隔由 787ms 降至 32ms，重复图片收纳由 540ms 降至 30ms。UI 心跳用于检查阻塞，不代表实际屏幕帧率。测量方法、原始记录和边界见 `qa-performance/REPORT.md`。

## 运行

双击 `dist/Screenline.exe`，或解压便携包后双击 `Screenline.exe`。适用于 Windows 10/11 x64，需要系统已有的 .NET Framework 4.8。当前开发机器已实测，其他机器尚未逐一验证。无需管理员权限、账号或网络。

首次运行会显示空晾台。按 **Ctrl+Alt+S** 拖选区域，松开后截图从原选区缩小并飞到晾衣绳上；Esc 取消。也可继续用 Win+Shift+S，开启剪贴板监听后自动收纳。收起后仍在系统托盘运行，托盘菜单可退出。图片、设置和错误日志保存在程序旁的 `data/`，导出默认使用 `data/exports/`。把整个程序目录放在可写目录，推荐 D 盘。

## 操作

| 操作 | 结果 |
|---|---|
| Win+Shift+S | 使用系统区域截图，剪贴板中的图片自动收纳 |
| Ctrl+Alt+T | 展开或收起 |
| Ctrl+Alt+S / 托盘“区域截图” / 空晾台 | 应用内拖选截图，复制到剪贴板并从选区飞入；Esc 取消 |
| 托盘“Windows 系统截图” | 打开 Windows 系统截图入口 |
| 鼠标停在屏幕顶边 | 展开；移开约半秒后收起 |
| 单击 / Enter | 复制图片，保留原始分辨率 |
| 双击 | 预览 |
| 长按约 0.46 秒 | 标注，支持画笔、荧光笔、橡皮、箭头、矩形、椭圆、文字、裁剪、撤销和重做 |
| 拖拽到接受图片或文件的应用 | OLE 传递 PNG、位图和文件路径；由目标选择操作 |
| 拖拽到支持移动的文件夹 | 文件移动成功后，晾台移除对应副本 |
| 右键 | 复制、预览、标注、另存为、移除副本 |
| 悬停在照片上，点击左上角 × | 副本落下后移除；导入的原图保留 |
| 托盘菜单 → 导入图片 / 拖入图片文件 | 收纳副本，保留源文件 |
| 托盘菜单 → 设置 | 暂停剪贴板监听、选择额外监听目录、减少动画、关闭系统材质 |
| 托盘菜单 → 保持展开 | 固定晾台；进入全屏应用时仍收起 |
| 照片数量超过屏幕容量时 | 使用两端箭头或鼠标滚轮翻组；较早的图片仍保留 |
| Esc | 收起晾台或关闭预览/标注窗口 |

剪贴板无法可靠区分截图和一般复制图片，因此开启监听后会收纳所有新的位图。监听可在设置中停用，应用内截图仍可正常收纳。文件目录监听只接收启动后新增或更新的图片，不会扫描历史图片。静态截图按实际像素去重。

标注中选择箭头、矩形或椭圆后在图片上拖动；选择文字后输入内容，再点击图片放置。文字和图形均可撤销或重做，保存为新的截图，不覆盖原图。文字当前为放置后栅格化，尚不支持单独拖动和再次编辑文字对象。

预览窗口可拖动标题栏、拖动边缘调整尺寸，右上角按钮支持最大化、还原和关闭。“保存副本”为主要操作；“复制”和“另存为”保留原有功能。0.4.0 同时修正了矩形标注导出时被曲线平滑的问题。

## 与 macOS 原项目的差异

- 使用 Windows 剪贴板监听和可选目录监听；不修改系统截图路径或缩略图设置。
- 标注采用内置 InkCanvas 和矢量绘制，支持常用工具及完整图像状态的撤销/重做；尚未包含系统 Markup 的签名、放大镜等全部能力。
- Windows 11 支持时，照片使用 `CreateHostBackdropBrush` 局部背景模糊，并遵循系统透明度和高对比度设置。Windows 10、关闭透明度或不支持接口时采用半透明回退。当前材质不是 macOS 的系统材质，两者外观仍有差别。应用不会通过后台截图模拟毛玻璃。
- 应用内截图会保留物理选区坐标，播放约 650ms 的缩小、弧线飞入和落位摆动。Windows 外部截图和普通剪贴板图片仍无法可靠取得原始屏幕坐标，采用落下和摆动。开启“减少动画”后直接收纳。
- 文件夹是否选择 Copy 或 Move 由 Windows 拖拽目标决定；只有源缓存实际消失时才移除条目。跨进程 WinForms 目标已实测，具体聊天应用、Explorer 和回收站还需要分别验证。
- 最近 60 张图片跨重启保存，屏幕上每组最多 12 张，按显示器可用宽度自动调整。超过 60 张的旧副本仍在 `data/images`，可从托盘菜单打开缓存目录管理；本版本没有磁盘配额或自动清理。
- 主机 200% 缩放、顶部展开、移开收起与全屏抑制有既有验证。新增区域截图使用物理坐标；混合 DPI 多显示器切换、负坐标显示器和系统截图 UI 尚需额外验收。
- 程序目前未签名，没有自动更新或开机自启。退出不改系统截图设置。

## 构建和验证

Windows PowerShell / PowerShell 7：

```powershell
./build.ps1
./test-ui.ps1
./test-visual.ps1
./test-material.ps1
python -B ./tests/verify-material.py
./test-features.ps1
./test-annotation.ps1
./test-async-store.ps1
./test-performance.ps1
python -B ./tests/make-performance-report.py
./test-material.ps1 -GalleryOnly
./render-design.ps1
python -B ./tests/make-preview.py ./qa-visual/evidence 0.4.1
python -B ./tests/make-preview.py ./qa-features/evidence 0.4.1-flight
python -B ./tests/make-design-review.py
./package.ps1
python -B ./tests/verify-delivery.py
```

构建使用 Windows 自带 .NET Framework C# 编译器、WinRT 元数据和系统数值库，不要求 .NET SDK 或 NuGet。UI 验证使用 Microsoft UI Automation、真实剪贴板、鼠标和跨进程 OLE，输入为合成图片，测试后尝试恢复原有剪贴板和鼠标位置。证据位于 `qa/evidence`、`qa-visual/evidence`、`qa-material/evidence` 和 `qa-features/evidence`。动画证据只渲染应用本身；系统材质证据只采集受控合成测试窗口内的区域，采集前检查该区域归属测试进程。测试不记录用户桌面或其他应用内容。材质验证脚本需要本机 Python 和 Pillow，发行程序无需 Python。

0.1.0、0.2.0、0.3.0、0.4.0 的程序和源码保留在 `versions`，旧便携包也保留。新版的变化、验证结果和剩余差距见 `QUALITY.md`。

2026-10-11，最终 0.4.1 程序在本机 Windows 11 25H2（26200.9457）、200% 缩放下完成桌面验收：26 项原有功能、18 项视觉交互、25 项新增功能、14 项标注图像、4 项系统材质像素检查和 7 项后台图像管线检查，共 94 项通过。新增标题栏的最大化、还原和真实鼠标关闭通过，照片点击及透明区域穿透经过真实鼠标复测；标注检查核对矩形四条直边、椭圆轮廓和箭头。其余检查覆盖 720 × 400 截图的坐标与像素、飞入落位、中文标注、长按、跨进程 OLE 复制/移动、裁剪、重启恢复、顶边展开和全屏抑制。测试输入均为自生成图片与窗口；这些结果不等同于已验证所有显示器和目标应用，也不能代替外观评价。

原生浅色、深色预览见 `qa-gallery/evidence/gallery-light.png` 和 `gallery-dark.png`；背景由测试程序生成。标注窗口实际组件渲染见 `qa-design/evidence/editor-preview.png` 和 `editor-annotated.png`；新旧外观对照见同目录的 `comparison-light.png` 和 `comparison-dark.png`。组件对照不包含系统合成层，材质单独验收。飞入录屏见 `qa-features/evidence/screenline-0.4.1-flight.gif`，原有交互录屏见 `qa-visual/evidence/screenline-0.4.1.gif`，均由实际程序渲染帧生成。发行包与受测程序的 SHA256 核对见上级 `delivery-manifest-v0.4.1.json`。

原项目：[alejandrobujan/tendedero](https://github.com/alejandrobujan/tendedero)，原作者 **Alejandro Buján**。适配依据的提交：[`ed0618a67cc59e0a8621b97bb5e32ab9af3b0b69`](https://github.com/alejandrobujan/tendedero/tree/ed0618a67cc59e0a8621b97bb5e32ab9af3b0b69)。版权和完整原始授权见 `LICENSE`、`THIRD-PARTY-NOTICES.txt`；公开源码仓库另附 `docs/ATTRIBUTION.md`。
