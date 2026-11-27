from pathlib import Path
from PIL import Image
import numpy as np

source=Path(r'C:\Users\Administrator\Desktop\huaban-1443162821.png')
target=Path(__file__).resolve().parents[1]/'public'/'images'/'origami-glyphs'
target.mkdir(parents=True,exist_ok=True)
im=Image.open(source).convert('RGBA')
a=np.asarray(im)
mask=(a[:,:,0]>155)&(a[:,:,0]>a[:,:,1]*1.8)&(a[:,:,0]>a[:,:,2]*1.45)&(a[:,:,1]<105)
# Search regions correspond to the three rows in the supplied reference artwork.
regions={
 '0':(620,0,1360,1120),'1':(1360,0,1927,1120),
 '2':(40,760,520,1450),'3':(520,700,925,1450),'4':(975,650,1430,1450),'5':(1450,650,1927,1450),
 '6':(40,1450,520,2507),'7':(540,1450,960,2507),'8':(980,1450,1430,2507),'9':(1450,1450,1927,2507)
}
for digit,(x0,y0,x1,y1) in regions.items():
 sub=mask[y0:y1,x0:x1]
 ys,xs=np.where(sub)
 if not len(xs): raise RuntimeError(f'No red pixels for {digit}')
 pad=8
 left=max(x0+int(xs.min())-pad,0); right=min(x0+int(xs.max())+pad+1,im.width)
 top=max(y0+int(ys.min())-pad,0); bottom=min(y0+int(ys.max())+pad+1,im.height)
 crop=im.crop((left,top,right,bottom))
 ca=np.asarray(crop).copy(); cm=mask[top:bottom,left:right]
 # Soft one-pixel edge from the source alpha/antialiasing; white background becomes transparent.
 alpha=np.where(cm,255,0).astype(np.uint8)
 alpha[1:-1,1:-1]=np.maximum(alpha[1:-1,1:-1],((cm[:-2,1:-1]|cm[2:,1:-1]|cm[1:-1,:-2]|cm[1:-1,2:])*90).astype(np.uint8))
 ca[:,:,3]=alpha
 Image.fromarray(ca,'RGBA').save(target/f'{digit}.png',optimize=True)
 print(digit,(left,top,right,bottom),crop.size)

sheet=Image.new('RGBA',(1000,720),(242,239,234,255))
for i in range(10):
 glyph=Image.open(target/f'{i}.png').convert('RGBA')
 glyph.thumbnail((160,290),Image.Resampling.LANCZOS)
 x=20+(i%5)*195+(160-glyph.width)//2; y=20+(i//5)*345
 sheet.alpha_composite(glyph,(x,y))
sheet.save(target/'sheet.png')
