"""Inspect the user's downloaded skybox package without executing package code."""
import os,tarfile,json,io,re
from PIL import Image,ImageDraw
Image.MAX_IMAGE_PIXELS=None
root=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
package=r'C:\Users\ghwls\AppData\Roaming\Unity\Asset Store-5.x\BG Studio\Textures MaterialsSkies\8K Skybox Pack Free.unitypackage'
with open(os.path.join(root,'Design','GrandLibrary','subscribed-skybox-index.json'),encoding='utf-8') as f: rows=json.load(f)
byguid={row['guid']:row['path'].splitlines()[0] for row in rows}
selected={guid:path for guid,path in byguid.items() if re.search(r'/sky-\d+\.png$',path,re.I)}
sheet=Image.new('RGB',(1200,900),(28,29,31)); draw=ImageDraw.Draw(sheet)
with tarfile.open(package,'r:gz') as tar:
    for member in tar:
        parts=member.name.split('/')
        if len(parts)!=2 or parts[1]!='asset' or parts[0] not in selected: continue
        path=selected[parts[0]]; n=int(re.search(r'sky-(\d+)',path).group(1))
        image=Image.open(io.BytesIO(tar.extractfile(member).read())).convert('RGB')
        image.thumbnail((390,200))
        x=((n-1)%3)*400; y=((n-1)//3)*225
        sheet.paste(image,(x,y+22)); draw.text((x+8,y+3),f'sky-{n}',fill='white')
        print('SKY_PREVIEW',n,flush=True)
out=os.path.join(root,'Design','GrandLibrary','skybox-contact.png'); sheet.save(out)
print('SKYBOX_CONTACT_OK',out)
