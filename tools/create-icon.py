"""Rasterize Sona's original monochrome vector geometry for Windows ICO only."""
from pathlib import Path
from PIL import Image, ImageDraw
root = Path(__file__).resolve().parents[1] / "src" / "Sona.App" / "Assets"
root.mkdir(parents=True, exist_ok=True)
image = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
draw = ImageDraw.Draw(image)
draw.rounded_rectangle((32, 32, 992, 992), radius=220, fill="#171717")
for x, y, width, height in ((4,10,4,12), (9,6,4,20), (14,2,4,28), (19,6,4,20), (24,10,4,12)):
    draw.rounded_rectangle((x*32, y*32, (x+width)*32, (y+height)*32), radius=64, fill="#FFFFFF")
image.save(root / "Sona.ico", sizes=[(16,16), (24,24), (32,32), (48,48), (64,64), (128,128), (256,256)])
image.resize((256,256), Image.Resampling.LANCZOS).save(root / "Sona.png")
