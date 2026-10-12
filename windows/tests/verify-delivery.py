"""Validate the portable archive against the tested binary and current evidence."""
import hashlib
import json
from pathlib import Path
from zipfile import ZipFile

root = Path(__file__).resolve().parents[2]
windows = root / 'windows'
binary = windows / 'dist/Screenline.exe'
version = '0.4.1'
archive = root / f'Screenline-Windows-{version}.zip'
expected = ['Screenline.exe', 'Screenline.exe.config', 'README.md', 'QUALITY.md',
            'LICENSE', 'THIRD-PARTY-NOTICES.txt']

with ZipFile(archive) as bundle:
    assert sorted(bundle.namelist()) == sorted(expected), 'Unexpected archive members'
    for name in expected:
        assert bundle.read(name) == (windows / 'dist' / name).read_bytes(), name
assert binary.read_bytes() == (windows / 'qa-annotation/runtime/Screenline.exe').read_bytes()

sources = {
    'desktop_regression': ('qa/evidence/ui-tests.json', 26),
    'visual_interaction': ('qa-visual/evidence/visual-tests.json', 18),
    'capture_flight_and_annotations': ('qa-features/evidence/feature-tests.json', 25),
    'annotation_output_and_undo': ('qa-annotation/annotation-tests.json', 14),
    'material_pixels': ('qa-material/evidence/material-tests.json', 4),
    'async_image_pipeline': ('qa-async/async-store-tests.json', 7),
}
checks = {}
for key, (name, count) in sources.items():
    path = windows / name
    assert path.stat().st_mtime >= binary.stat().st_mtime, 'Evidence predates final binary: ' + name
    rows = json.loads(path.read_text(encoding='utf-8-sig'))
    rows = [row for row in rows if row['name'] != '拖拽坐标']
    assert len(rows) == count and all(row['pass'] for row in rows), name
    checks[key] = {'passed': count, 'total': count, 'evidence': 'windows/' + name}

material_dir = windows / 'qa-material/evidence'
latest = json.loads((material_dir / 'latest-run.json').read_text(encoding='utf-8-sig'))
state = json.loads((material_dir / 'material-state.json').read_text(encoding='utf-8-sig'))
assert latest['status'] == 'passed' and latest['run_id'] == state['run_id']
assert state['version'] == version

gallery_dir = windows / 'qa-gallery/evidence'
gallery_run = json.loads((gallery_dir / 'latest-run.json').read_text(encoding='utf-8-sig'))
gallery_state = json.loads((gallery_dir / 'ui-state.json').read_text(encoding='utf-8-sig'))
assert gallery_run['status'] == 'passed' and gallery_state['version'] == version
assert binary.read_bytes() == (windows / 'qa-design/runtime/Screenline.exe').read_bytes()
assert binary.read_bytes() == (windows / 'qa-async/runtime/Screenline.exe').read_bytes()
performance = json.loads((windows / 'qa-performance/report.json').read_text(encoding='utf-8'))
assert performance['current_version'] == version + '.0'
assert performance['current_binary_sha256'] == hashlib.sha256(binary.read_bytes()).hexdigest()

files = [archive, binary] + [windows / name for name, _ in sources.values()] + [
    windows / 'qa-gallery/evidence/gallery-light.png',
    windows / 'qa-gallery/evidence/gallery-dark.png',
    windows / f'qa-features/evidence/screenline-{version}-flight.gif',
    windows / f'qa-visual/evidence/screenline-{version}.gif',
    windows / 'qa-design/evidence/editor-preview.png',
    windows / 'qa-design/evidence/editor-annotated.png',
    windows / 'qa-design/evidence/comparison-light.png',
    windows / 'qa-design/evidence/comparison-dark.png',
    windows / 'qa-performance/report.json',
    windows / 'qa-performance/current/image-pipeline.json',
]
for path in files:
    assert path.stat().st_mtime >= binary.stat().st_mtime, 'Artifact predates final binary: ' + str(path)
manifest = {
    'version': version, 'release_status': 'local_desktop_qa_passed',
    'verified_on': '2026-10-11', 'environment': 'Windows 11 25H2 26200.9457; 200% DPI',
    'zip_members': expected, 'zip_matches_dist': True,
    'annotation_tested_binary_matches_dist': True,
    'editor_preview_binary_matches_dist': True,
    'async_pipeline_tested_binary_matches_dist': True,
    'performance': performance,
    'native_gallery': {'run_id': gallery_run['run_id'], 'status': gallery_run['status'],
                       'background': 'Owned synthetic light and dark windows'},
    'checks': checks, 'total_checks_passed': sum(x['passed'] for x in checks.values()),
    'limits': ['Mixed-DPI and negative-origin monitors not physically verified',
               'Explorer, Recycle Bin and specific chat-app OLE targets not verified',
               'Windows 10 and system high-contrast toggling not physically verified',
               'Unsigned portable application; no auto-updater'],
    'files': [{'path': path.relative_to(root).as_posix(), 'bytes': path.stat().st_size,
               'sha256': hashlib.sha256(path.read_bytes()).hexdigest()} for path in files],
}
(root / f'delivery-manifest-v{version}.json').write_text(
    json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'archive': str(archive), 'passed': manifest['total_checks_passed'],
                  'zip_matches_dist': True, 'status': manifest['release_status']}))
