# Screenline 0.4.1：减少收纳阻塞和动画开销

本版保留 0.4.0 的晾台、浅色工具栏和标注窗口设计，针对图片收纳、截图飞入、系统材质更新和鼠标拖拽优化响应。0.4.0 的源码、程序与测试证据保留在 `versions/0.4.0`，旧便携包保留。

## 实测性能

同一机器、同组自生成 3840 × 2160 BGRA 图片，每版三次，表中取中位数。探针调用两版真实的图片收纳路径，不操作鼠标或剪贴板。

| 测量 | 0.4.0 | 0.4.1 | 降低 |
|---|---:|---:|---:|
| 图片收纳总耗时 | 787.22 ms | 406.94 ms | 48.3% |
| 收纳期间 UI 心跳最大间隔 | 787.28 ms | 31.57 ms | 96.0% |
| 重复图片收纳耗时 | 539.63 ms | 29.54 ms | 94.5% |

UI 心跳采用 16ms、Input 优先级的 DispatcherTimer，观察收纳期间的长时间阻塞；这不是屏幕呈现帧率，也不能据此保证 60 FPS。图片生成和屏幕像素读取不计入收纳耗时。结果只代表本机合成图片测试，实际图片、磁盘、设备和显示器会影响表现。首次启动读取历史图片没有单独进行性能对照。

原始数据见 `qa-performance/baseline/image-pipeline.json` 与 `current/image-pipeline.json`；方法及二进制 SHA256 见同目录 `REPORT.md`、`report.json`。

## 实现变化

| 部位 | 0.4.1 的处理 |
|---|---|
| 图片收纳 | 解码、像素哈希、PNG 编码和写盘使用串行后台任务；列表提交回到 UI 线程。先比较像素哈希，再决定是否编码，跨像素格式的重复图片不会产生多余文件 |
| 图片内存与编辑 | 缩略图独立复制像素，避免间接保留完整大图；原图使用弱引用缓存，近期复制和打开编辑器可复用。标注保存与导出将编码和写盘移到后台 |
| 晾台动画 | 用连续缓动曲线代替逐个构建的 16ms 关键帧；保留原有弹簧曲线与动画时长 |
| 截图飞入 | 使用围绕照片的局部透明窗口，按渲染时钟推进；通过绘图与变换更新，减少每帧尺寸和布局修改。最终 720 × 400 截图回归中，窗口面积为 483,840 像素，桌面为 6,400,000 像素，减少约 92.4% |
| 系统材质 | 缓存窗口位置与整数轮廓，几何没有变化时跳过原生区域更新；展开时以 16ms 调度，收起后以 80ms 检查顶边，材质静止时降低刷新频率 |
| 鼠标命中与拖拽 | 照片和按钮显式返回可点击命中，留白返回透明命中；材质更新留在 Dispatcher 调度中。OLE 拖拽反馈直接跟随鼠标，备用定时器跳过未移动的位置 |
| 连续操作与退出 | 截图处理中阻止重复触发；后台收纳串行执行并去重；退出等待正在写入的任务完成 |

## 最终程序验收

2026-10-11，Windows 11 25H2（26200.9457）、200% 缩放，最终版本 0.4.1：

| 检查 | 结果 | 证据 |
|---|---|---|
| 原有功能、长按、跨进程 OLE 复制/移动、监听、重启与顶边交互 | 26 / 26 通过 | `qa/evidence/ui-tests.json`；另两条拖拽坐标为诊断 |
| 透明度、真实点击穿透、单击/双击、标题栏、裁剪与动画 | 18 / 18 通过 | `qa-visual/evidence/visual-tests.json` |
| 实际截图、局部飞入窗口、选区像素、形状/中文标注和设置 | 25 / 25 通过 | `qa-features/evidence/feature-tests.json` |
| 标注形状、导出像素、撤销/重做、分辨率和源文件 | 14 / 14 通过 | `qa-annotation/annotation-tests.json` |
| 原生背景模糊和透明留白像素对照 | 4 / 4 通过 | `qa-material/evidence/material-tests.json` |
| 后台队列、并发去重、UI 线程提交、像素和缩略图、重启恢复 | 7 / 7 通过 | `qa-async/async-store-tests.json` |

合计 94 项通过。真实照片单击复制、双击预览、标题栏最大化/还原/关闭和留白点击穿透均通过。应用内截图保留物理选区 1240,480 与 720 × 400 分辨率，像素与受控测试窗口一致；跨进程 OLE 复制和移动、标注保存、裁剪、重启恢复继续通过。

材质验收对照同一轮材质开/关图像及运行 ID。照片边缘条纹对比度约为关闭时的 0.13，透明留白平均像素差为 0。采集前检查窗口归属，其他应用遮挡时中止；所有背景、窗口和图片均为测试素材。

发行包仅包含程序、配置和四份说明/授权文件，不包含用户缓存。上级 `delivery-manifest-v0.4.1.json` 核对 ZIP 成员与 `dist` 完全一致，标注/后台队列测试及编辑器渲染使用同一二进制，证据晚于最终编译，并记录 SHA256。

## 外观和动画证据

- `qa-gallery/evidence/gallery-light.png`、`gallery-dark.png`：最终程序在受控浅色、深色背景上的原生窗口采集，包含 Windows 合成效果。
- `qa-design/evidence/editor-preview.png`、`editor-annotated.png`：实际预览/标注组件渲染，含真实标注结果。
- `qa-design/evidence/comparison-light.png`、`comparison-dark.png`：0.3.0 与当前版本的实际 WPF 照片组件，在同一背景上对照，不包含系统合成层。
- `qa-visual/evidence/screenline-0.4.1.gif`、`qa-features/evidence/screenline-0.4.1-flight.gif`：实际程序渲染帧生成，分别有 28、16 个不同画面，没有手工补帧。GIF 用于审阅过程，不作为实际帧率测量。

功能和像素检查不能代替外观评价，0.4.1 未重新设计界面。

## 尚未覆盖

Windows 局部材质与 macOS ultraThinMaterial 仍有差别。Win+Shift+S 外部截图没有可靠的原始选区坐标，真实选区飞入需使用 Ctrl+Alt+S。文字放置后栅格化，不能单独拖动再次编辑；尚无系统 Markup 的签名、放大镜等全部工具。

混合 DPI、负坐标显示器、Explorer/回收站及具体聊天应用拖拽仍待专项验收。Windows 10 和系统高对比度切换未做物理设备验收。当前程序未签名，没有自动更新、开机自启或磁盘配额。

接口和性能依据：[WPF 线程模型](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/threading-model)、[布局与设计性能](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-layout-and-design)、[硬件渲染性能](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-taking-advantage-of-hardware)、[WPF 使用 Windows 合成层](https://learn.microsoft.com/en-us/windows/uwp/composition/using-the-visual-layer-with-wpf)。
