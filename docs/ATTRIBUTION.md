# 来源与署名

Screenline 是改编自 **Alejandro Buján** 的 **Tendedero** 交互与产品设计的 Windows 适配实现。原项目使用 Swift/AppKit；本项目以 C#、WPF、Win32 和 OLE 独立实现 Windows 版本，并使用不同名称与图标，不代表原作者或获得原作者背书。

| 项目 | 来源 |
|---|---|
| 原作者 | [Alejandro Buján / alejandrobujan](https://github.com/alejandrobujan) |
| 原项目 | [alejandrobujan/tendedero](https://github.com/alejandrobujan/tendedero) |
| 适配研究基线 | [ed0618a67cc59e0a8621b97bb5e32ab9af3b0b69](https://github.com/alejandrobujan/tendedero/tree/ed0618a67cc59e0a8621b97bb5e32ab9af3b0b69) |
| 原始授权 | [完整上游 LICENSE](UPSTREAM-LICENSE.txt) |
| Windows 代码授权 | [MIT License](../LICENSE) |

承接的核心交互包括屏幕顶边晾衣绳、截图按比例悬挂、点击复制、长按标注、拖动分享和自动收起。Windows 版采用自己的截图选区、剪贴板监听、图像存储、标注窗口、拖拽及局部系统材质实现，具体功能和限制见 [使用说明](../windows/README.md)。

原项目的名称、图标艺术和文档图片不随代码的 MIT 授权一同提供。本项目没有再分发这些品牌素材；程序图标由 [make_icon.py](../windows/src/assets/make_icon.py) 生成，README 的图片来自本项目合成测试素材。

开发期间还研究了 [morluto/rea](https://github.com/morluto/rea) 和 [Jakeschincariol/replica-skill](https://github.com/Jakeschincariol/replica-skill)。它们不是发行程序依赖，本仓库不包含它们的代码、运行时或依赖包。
