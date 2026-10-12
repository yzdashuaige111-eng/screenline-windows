"""Compare the production WPF component renders on identical synthetic backgrounds."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

windows = Path(__file__).resolve().parents[1]
output = windows / 'qa-design/evidence'
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 16)
sources = [windows / 'qa-design/baseline/evidence/shelf-clean.png',
           windows / 'qa-gallery/evidence/shelf-clean.png']
for dark in (False, True):
    background = '#242A25' if dark else '#EFF1EC'
    ink = '#E8ECE5' if dark else '#52604F'
    result = Image.new('RGB', (1200, 730), background)
    for row, path in enumerate(sources):
        source = Image.open(path).convert('RGBA')
        left = (source.width - 1200) // 2
        view = source.crop((left, 20, left + 1200, 350))
        result.paste(view, (0, row * 365 + 35), view)
        ImageDraw.Draw(result).text((22, row * 365 + 10),
                                   '0.3 / BEFORE' if row == 0 else '0.4.1 / AFTER',
                                   font=font, fill=ink)
    result.save(output / ('comparison-dark.png' if dark else 'comparison-light.png'))
print('Saved like-for-like WPF component comparisons. OS backdrop is verified separately.')
