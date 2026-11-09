from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

root = Path('Assets/GrandLibrary/UserSchool/Textures')
out = Path('Assets/LibraryCharacter/School/CampusTheme')
out.mkdir(parents=True, exist_ok=True)

# Preserve the source wall's layout and UVs while replacing neon-green molding.
wall = np.asarray(Image.open(root/'WallWithMolding-sharedassets2.assets-190.png').convert('RGB'), dtype=np.float32)
r,g,b = wall[:,:,0], wall[:,:,1], wall[:,:,2]
green = (g > r*1.3+8) & (g > b*1.3+8)
lum = np.clip((r+g+b)/3, 0, 255)
neutral = np.stack([lum*.56+101, lum*.55+105, lum*.53+111], axis=-1)
shade = np.clip(g/255,0,1)
blue = np.stack([22+42*shade, 60+91*shade, 92+113*shade], axis=-1)
wall = np.where(green[:,:,None],blue,neutral).clip(0,255).astype('uint8')
wall_image=Image.fromarray(wall).resize((1024,1024),Image.Resampling.BICUBIC)
wall_image.save(out/'CampusWall.png')
plain=np.asarray(wall_image).copy();plain[:140,:,:]=np.array([224,231,234],dtype='uint8')
Image.fromarray(plain).save(out/'CampusWallPlain.png')

# Replace the very high contrast pixel noise while retaining the existing light panels.
ceiling = np.asarray(Image.open(root/'Ceiling.png').convert('L'),dtype=np.float32)
base = np.clip(220+ceiling*.045,220,235)
light = ceiling>225
ceiling_rgb=np.stack([base,base+3,base+6],axis=-1)
ceiling_rgb[light]=(248,250,251)
Image.fromarray(ceiling_rgb.clip(0,255).astype('uint8')).resize((512,512),Image.Resampling.BICUBIC).filter(ImageFilter.GaussianBlur(.65)).save(out/'CampusCeiling.png')

# Neutral school flooring with restrained seams rather than saturated carpets.
rng=np.random.default_rng(341)
noise=rng.normal(0,1.5,(1024,1024,1))
base=np.empty((1024,1024,3));base[:]=[203,213,220];base+=noise
for n in range(0,1024,256):
    base[n:n+3,:,:]-=13;base[:,n:n+3,:]-=13
Image.fromarray(base.clip(0,255).astype('uint8')).save(out/'CampusFloor.png')

# Keep the locker vents and handles while recoloring the red metal to calm blue.
lock=np.asarray(Image.open(root/'Locker.png').convert('RGB'),dtype=np.float32)
r,g,b=lock[:,:,0],lock[:,:,1],lock[:,:,2]
red=(r>g*1.4+10)&(r>b*1.4+10)
v=r/255
recolor=np.stack([12+37*v,24+83*v,35+125*v],axis=-1)
lock=np.where(red[:,:,None],recolor,lock).clip(0,255).astype('uint8')
Image.fromarray(lock).resize((256,512),Image.Resampling.NEAREST).save(out/'CampusLocker.png')

font_path='C:/Windows/Fonts/arial.ttf'
def font(size,bold=False):
    p='C:/Windows/Fonts/arialbd.ttf' if bold else font_path
    return ImageFont.truetype(p,size)
def centered(draw,xy,text,f,fill):
    draw.text(xy,text,font=f,fill=fill,anchor='mm')

notice=wall_image.copy();d=ImageDraw.Draw(notice)
d.rectangle((142,220,882,790),fill=(31,75,108),outline=(235,241,242),width=16)
d.rectangle((162,240,862,770),outline=(106,174,205),width=4)
centered(d,(512,355),'CAMPUS',font(94,True),(245,248,249))
centered(d,(512,480),'CODING LAB',font(66,True),(177,222,240))
d.line((235,570,789,570),fill=(119,183,210),width=5)
centered(d,(512,650),'LEARN  |  CREATE  |  CONNECT',font(34),(235,243,245))
notice.save(out/'CampusNotice.png')

# Door texture is a full clean panel; the real hinge and collider remain in 3D.
im=Image.new('RGB',(512,1024),(35,95,145));d=ImageDraw.Draw(im)
d.rectangle((0,0,511,1023),outline=(23,65,99),width=18)
d.rectangle((28,26,484,995),outline=(87,146,188),width=4)
d.rounded_rectangle((105,155,407,416),radius=10,fill=(170,204,220),outline=(24,65,91),width=14)
d.rectangle((122,171,390,401),fill=(176,210,226))
d.polygon([(122,172),(250,172),(122,300)],fill=(198,224,235))
d.line((256,171,256,401),fill=(96,142,169),width=7)
d.rounded_rectangle((155,57,357,118),radius=5,fill=(232,239,242),outline=(21,63,93),width=4)
centered(d,(256,88),'CLASSROOM',font(24,True),(28,67,92))
d.rounded_rectangle((370,567,460,618),radius=13,fill=(34,66,84),outline=(170,191,199),width=4)
d.rounded_rectangle((383,579,461,596),radius=8,fill=(215,223,226))
d.line((56,815,456,815),fill=(68,126,170),width=3)
im.save(out/'CampusDoor.png')

# A simple glazed wall texture replaces the source's orange pixel-art window.
im=wall_image.copy();d=ImageDraw.Draw(im)
d.rectangle((142,240,882,770),fill=(67,111,144),outline=(88,105,113),width=15)
d.rectangle((164,258,860,748),fill=(146,193,216))
d.polygon([(164,258),(415,258),(164,530)],fill=(185,219,233))
d.line((512,258,512,748),fill=(226,234,235),width=20)
d.line((164,500,860,500),fill=(226,234,235),width=16)
im.save(out/'CampusWindow.png')

# Muted lift doors replace the placeholder green ELEVATE panels.
im=Image.new('RGB',(512,512),(117,148,166));d=ImageDraw.Draw(im)
d.rectangle((8,8,503,503),outline=(64,94,112),width=12)
d.line((256,14,256,498),fill=(58,91,110),width=8)
d.rectangle((28,34,238,478),outline=(155,180,192),width=3)
d.rectangle((274,34,484,478),outline=(155,180,192),width=3)
im.save(out/'CampusLift.png')
print('CAMPUS_TEXTURES_OK',out)
