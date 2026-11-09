"""Create a bright, recognizable elevator door texture for the school."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
root=Path(__file__).resolve().parents[1]
out=root/'Assets/LibraryCharacter/Resources/SchoolElevatorDoor.png'
im=Image.new('RGB',(512,512),(255,234,157));d=ImageDraw.Draw(im)
d.rectangle((0,0,511,511),fill=(255,216,76))
d.rectangle((14,14,497,497),fill=(255,245,202),outline=(124,78,12),width=8)
for left,right in ((28,250),(262,484)):
    d.rectangle((left,32,right,482),fill=(255,250,226),outline=(212,160,50),width=6)
    d.rectangle((left+17,142,right-17,436),fill=(249,235,178),outline=(229,199,112),width=3)
    d.rectangle((left+25,150,right-25,428),fill=(255,246,212))
d.rectangle((248,31,263,485),fill=(92,76,47))
d.rectangle((152,45,360,125),fill=(30,72,83),outline=(255,255,241),width=5)
font=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',58)
text='EXIT';box=d.textbbox((0,0),text,font=font);d.text(((512-(box[2]-box[0]))/2,50),text,font=font,fill=(255,252,211),stroke_width=1)
d.ellipse((221,246,241,266),fill=(49,122,124));d.ellipse((270,246,290,266),fill=(49,122,124))
d.rectangle((14,484,497,503),fill=(234,184,50))
im.save(out)
