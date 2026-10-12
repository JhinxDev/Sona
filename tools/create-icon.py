"""Rebuild Murmur's original icon; requires Pillow only for this design tool."""
from pathlib import Path
from PIL import Image, ImageDraw
root = Path(__file__).resolve().parents[1] / "src" / "Murmur.App" / "Assets"
root.mkdir(parents=True, exist_ok=True)
image = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
draw = ImageDraw.Draw(image)
draw.rounded_rectangle((32, 32, 992, 992), radius=244, fill="#191623")
for x, top, bottom in ((264, 424, 600), (424, 280, 744), (584, 360, 664), (744, 440, 584)):
    draw.rounded_rectangle((x - 44, top, x + 44, bottom), radius=44, fill="#C2AAFF")
image.save(root / "Murmur.ico", sizes=[(16,16), (24,24), (32,32), (48,48), (64,64), (128,128), (256,256)])
image.resize((256,256), Image.Resampling.LANCZOS).save(root / "Murmur.png")
