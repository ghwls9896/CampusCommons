"""Draw a second, expressive close-up from the existing crow sprite."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
root = Path(__file__).resolve().parents[1]
source = Image.open(root / 'Assets/LibraryCharacter/Sprites/ScholarBird_Billboard.png').convert('RGBA')
# Draw at quarter size and scale with nearest-neighbour to retain the pixel look.
layer = Image.new('RGBA', (source.width // 4, source.height // 4))
d = ImageDraw.Draw(layer)
def points(values):
    return [(round(x/4), round(y/4)) for x,y in values]
def ellipse(box, fill):
    d.ellipse(tuple(round(x/4) for x in box), fill=fill)
# A narrowed, glowing red eye framed by the original eye rim.
ellipse((485, 535, 560, 638), (24, 5, 7, 255))
ellipse((494, 548, 553, 629), (108, 5, 12, 255))
ellipse((505, 557, 549, 619), (232, 37, 31, 255))
ellipse((517, 566, 539, 613), (255, 177, 111, 255))
d.polygon(points([(472,524),(561,544),(557,563),(474,551)]),fill=(23,12,12,255))
# Open beak with an uneven, sharply outlined mouth and small teeth.
d.polygon(points([(302,707),(455,621),(451,663),(431,701),(390,731),(339,742),(302,729)]),fill=(20,8,10,255))
d.polygon(points([(318,710),(443,643),(430,679),(384,714),(344,725)]),fill=(83,13,18,255))
for tooth in [[(348,695),(359,691),(357,710)],[(374,682),(385,677),(381,697)],[(400,667),(411,662),(407,683)],[(423,653),(434,648),(428,669)]]:
    d.polygon(points(tooth),fill=(231,219,191,255))
layer=layer.resize(source.size,Image.Resampling.NEAREST)
scare=Image.alpha_composite(source,layer).crop((120,230,716,826))
# Subtle crimson aura stays behind the existing silhouette.
small=scare.resize((149,149),Image.Resampling.LANCZOS)
alpha=small.getchannel('A').filter(ImageFilter.MaxFilter(15)).filter(ImageFilter.GaussianBlur(5))
glow=Image.new('RGBA',small.size,(135,0,18,0));glow.putalpha(alpha.point(lambda a:int(a*.48)))
result=Image.alpha_composite(glow,small).resize((894,894),Image.Resampling.NEAREST)
result.save(root / 'Assets/LibraryCharacter/Resources/CrowJumpscare.png')
