"""Draw Screenline's own folded-paper icon, without upstream artwork."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parent
canvas = Image.new('RGBA', (256, 256))
d = ImageDraw.Draw(canvas)
d.rounded_rectangle((10, 10, 246, 246), 52, fill='#273E43', outline='#496068', width=2)
d.rounded_rectangle((58, 47, 199, 216), 17, fill='#172A30')
d.rounded_rectangle((53, 40, 194, 208), 17, fill='#F2F2EB')
d.polygon(((151, 40), (194, 83), (151, 83)), fill='#80B4A5')
d.polygon(((151, 40), (194, 40), (194, 83)), fill='#273E43')
for y, end in ((109, 164), (132, 160), (155, 137)):
    d.rounded_rectangle((80, y, end, y+6), 3, fill='#9EACA8')
canvas.save(root/'Screenline.ico', sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
