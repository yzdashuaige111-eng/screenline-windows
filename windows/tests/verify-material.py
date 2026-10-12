"""Verify native compositor pixels over an owned synthetic fixture, never the user desktop."""
import json
from pathlib import Path
from statistics import pstdev
from PIL import Image

evidence = Path(__file__).resolve().parents[1] / 'qa-material/evidence'
state = json.loads((evidence / 'material-state.json').read_text(encoding='utf-8-sig'))
latest = json.loads((evidence / 'latest-run.json').read_text(encoding='utf-8-sig'))
assert latest['status'] == 'passed' and state.get('run_id') == latest['run_id'], 'Current capture is incomplete; do not validate stale images.'
on, off = [Image.open(evidence / name).convert('RGB') for name in ('material-on.png', 'material-off.png')]
x, y, w, h = state['sample_area']
alpha = Image.open(evidence / 'material-alpha.png').convert('RGBA').crop((x, y, x + w, y + h))
gap_diffs, rim_diffs, strips = [], [], []
for j in range(h):
    native_row, plain_row = [], []
    for i in range(w):
        a, b = on.getpixel((i, j)), off.getpixel((i, j))
        delta = sum(abs(v - z) for v, z in zip(a, b)) / 3
        opacity = alpha.getpixel((i, j))[3]
        if opacity == 0:
            gap_diffs.append(delta)
        if 12 <= opacity <= 120:
            rim_diffs.append(delta)
            native_row.append(a[0])
            plain_row.append(b[0])
    if len(plain_row) > 80 and pstdev(plain_row) > 50:
        strips.append((pstdev(native_row) / pstdev(plain_row), j, len(plain_row)))
best = min(strips, default=(1, -1, 0))
checks = [
    {'name': '系统合成接口已成功启用', 'pass': bool(state['material_applied'] and state['material_allowed'])},
    {'name': '照片边缘真实模糊合成背景条纹', 'pass': best[0] < .3, 'contrast_ratio': best[0], 'row': best[1], 'pixels': best[2]},
    {'name': '透明留白未被系统材质染色', 'pass': len(gap_diffs) > 10000 and sum(gap_diffs) / len(gap_diffs) < .5, 'mean_pixel_difference': sum(gap_diffs) / max(1, len(gap_diffs))},
    {'name': '玻璃边缘具有实际可见变化', 'pass': sum(d > 15 for d in rim_diffs) > 200, 'changed_rim_pixels': sum(d > 15 for d in rim_diffs)},
]
(evidence / 'material-tests.json').write_text(json.dumps(checks, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(checks, ensure_ascii=False))
assert all(check['pass'] for check in checks)
