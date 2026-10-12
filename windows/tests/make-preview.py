"""Package frames rendered by the actual WPF app; no desktop capture."""
import json
import sys
from pathlib import Path
from PIL import Image

evidence=Path(sys.argv[1]).resolve() if len(sys.argv)>1 else Path(__file__).resolve().parents[1]/'qa-visual/evidence'
version=sys.argv[2] if len(sys.argv)>2 else '0.4.1'
output=evidence/f'screenline-{version}.gif'
recording=json.loads((evidence/'recording.json').read_text(encoding='utf-8-sig'))
frames=[Image.open(evidence/'frames'/name).convert('RGB') for name in recording['files']]
durations=[round((i+1)*recording['elapsed_ms']/len(frames)/10)*10-round(i*recording['elapsed_ms']/len(frames)/10)*10 for i in range(len(frames))]
frames[0].save(output,save_all=True,append_images=frames[1:],duration=durations,loop=0,optimize=True,disposal=2)
with Image.open(output) as result:
    assert result.is_animated and result.n_frames>=6
    total=0
    for i in range(result.n_frames):result.seek(i);total+=result.info['duration']
    assert abs(total-recording['elapsed_ms'])<=10
    print(json.dumps({'gif':str(output),'frames':result.n_frames,'duration_ms':total}))
