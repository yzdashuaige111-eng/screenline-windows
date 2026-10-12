"""Compare measured production paths on owned, synthetic 4K images."""
import hashlib
import json
from pathlib import Path
from statistics import median

windows = Path(__file__).resolve().parents[1]
root = windows / 'qa-performance'
before = json.loads((root / 'baseline/image-pipeline.json').read_text(encoding='utf-8-sig'))
after = json.loads((root / 'current/image-pipeline.json').read_text(encoding='utf-8-sig'))
binary = windows / 'dist/Screenline.exe'
assert (root / 'current/image-pipeline.json').stat().st_mtime >= binary.stat().st_mtime
assert before['source'] == after['source']
assert before['version'] == '0.4.0.0' and after['version'] == '0.4.1.0'
measurements = {}
for name in ['add_ms', 'ui_max_gap_ms', 'duplicate_ms']:
    old = median(row[name] for row in before['samples'])
    new = median(row[name] for row in after['samples'])
    measurements[name] = {'baseline_median_ms': round(old, 2), 'current_median_ms': round(new, 2),
                          'reduction_percent': round((1 - new / old) * 100, 1)}
report = {
    'baseline_version': before['version'], 'current_version': after['version'],
    'current_binary_sha256': hashlib.sha256(binary.read_bytes()).hexdigest(),
    'baseline_binary_sha256': hashlib.sha256((windows / 'versions/0.4.0/dist/Screenline.exe').read_bytes()).hexdigest(),
    'source': before['source'], 'samples_per_version': 3,
    'ui_measurement': '16 ms DispatcherPriority.Input timer; maximum observed heartbeat gap within each add; median of three trials',
    'measurements': measurements,
    'limits': ['Image creation and screen capture are outside the measured add operation',
               'Heartbeat gaps are not presented-frame measurements or a 60 FPS guarantee',
               'Local synthetic-image results; image content, disk and machine affect performance'],
}
(root / 'report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
lines = ['# Screenline 0.4.1 性能对照', '', '同机、同组自生成 3840 × 2160 图片，每版三次，表中取中位数。', '',
         '| 测量 | 0.4.0 | 0.4.1 | 降低 |', '|---|---:|---:|---:|']
for name, label in [('add_ms', '图片收纳总耗时'), ('ui_max_gap_ms', '收纳期间 UI 心跳最大间隔'), ('duplicate_ms', '重复图片收纳耗时')]:
    row = measurements[name]
    lines.append(f"| {label} | {row['baseline_median_ms']} ms | {row['current_median_ms']} ms | {row['reduction_percent']}% |")
lines += ['', 'UI 心跳采用 16ms 的 Input 优先级 DispatcherTimer。它用于观察长时间阻塞，不是屏幕呈现帧率。',
          '图片生成及屏幕像素读取不计入收纳耗时；后台编码仍需时间，但期间可继续处理界面消息。',
          '结果只代表本机合成图片测试，不保证所有图片、设备或显示器达到同样数值。', '',
          '原始记录：baseline/image-pipeline.json、current/image-pipeline.json。二进制 SHA256 及汇总：report.json。', '']
(root / 'REPORT.md').write_text('\n'.join(lines), encoding='utf-8')
print(json.dumps(measurements, ensure_ascii=False))
