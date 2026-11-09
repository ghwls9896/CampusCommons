import os,tarfile,json
root=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
package=r'C:\Users\ghwls\AppData\Roaming\Unity\Asset Store-5.x\BG Studio\Textures MaterialsSkies\8K Skybox Pack Free.unitypackage'
rows=[]
with tarfile.open(package,'r:gz') as tar:
    for member in tar:
        if member.name.endswith('/pathname') and member.isfile():
            f=tar.extractfile(member)
            path=f.read(8192).decode('utf-8','replace').strip()
            rows.append({'guid':member.name.split('/')[0],'path':path})
out=os.path.join(root,'Design','GrandLibrary','subscribed-skybox-index.json')
with open(out,'w',encoding='utf-8') as f: json.dump(rows,f,indent=2,ensure_ascii=False)
for row in rows: print(row['guid'],row['path'])
print('SKYBOX_PACKAGE_INDEX_OK',len(rows))
