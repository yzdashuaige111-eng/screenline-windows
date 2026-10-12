# 开发与验收

从仓库根目录运行 `./windows/build.ps1`。构建输出到 `windows/dist`；用户截图和设置也留在该目录，均不会加入 Git。`./windows/package.ps1` 仅打包程序、配置、使用说明、验收说明及授权文件。

## 不操作鼠标的检查

```powershell
./windows/test-annotation.ps1
./windows/test-async-store.ps1
./windows/render-design.ps1
./windows/test-performance.ps1
```

分别核对标注导出与撤销/重做、后台图片队列与去重、实际组件渲染、当前构建的图片收纳性能。性能探针使用合成 4K 图片，结果写入 `windows/qa-performance/current/image-pipeline.json`。

## 桌面回归

下列测试会使用真实鼠标、剪贴板和全局快捷键。请在独立验收时段逐个运行，期间不要操作鼠标，不要并发运行这些脚本。

```powershell
./windows/test-ui.ps1
./windows/test-visual.ps1
./windows/test-features.ps1
./windows/test-material.ps1
python -B windows/tests/verify-material.py
./windows/test-material.ps1 -GalleryOnly
```

测试使用自己生成的图片和窗口，测试后尝试恢复鼠标与剪贴板。材质采集保留窗口归属和遮挡检查，不以其他应用内容作为测试素材。Python 图像检查脚本需要 Pillow。

## 已发布证据与历史对照

本仓库仅收录选定的合成测试结果、预览与性能原始记录；运行时、用户图片、日志、完整旧版目录和研究下载不公开。`delivery-manifest-v0.4.1.json` 记录的是 2026-10-11 受测发行二进制；重新编译的 EXE 可能因编译时间等产生不同 SHA256。

`make-design-review.py`、`make-performance-report.py` 和 `verify-delivery.py` 用于维护者的完整本机验收工作区。重新生成历史性能报告还需要 `windows/versions/0.4.0/dist/Screenline.exe`；旧版二进制不随源码仓库分发。已有基线原始数据、汇总和 SHA256 已保留在 `windows/qa-performance`，可直接审阅。完整交付校验需先重新生成脚本要求的全部证据及运行时。
