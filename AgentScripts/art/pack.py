from lib import *
from PIL import ImageFont
proj=os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','..'))
res=os.path.join(proj,'Assets','Resources'); 
for d in ('Art','Sfx','Fonts'): os.makedirs(os.path.join(res,d),exist_ok=True)
# logo
def text_img(txt,font,size,fill_top,fill_bot,ol=10,olc=(40,27,32,255),pad=30):
    f=ImageFont.truetype(font,size); bb=f.getbbox(txt); w=bb[2]-bb[0]+pad*2; h=bb[3]-bb[1]+pad*2
    m=Image.new('L',(w,h),0); ImageDraw.Draw(m).text((pad-bb[0],pad-bb[1]),txt,font=f,fill=255)
    grad=np.zeros((h,w,4),np.uint8)
    for y in range(h):
        t=min(max((y-pad)/(h-2*pad),0),1); grad[y,:,:3]=mix(fill_top,fill_bot,t)
    g=Image.fromarray(grad,'RGBA'); g.putalpha(m)
    o=m.filter(ImageFilter.GaussianBlur(ol*0.6)).point(lambda v:255 if v>24 else 0).filter(ImageFilter.GaussianBlur(1))
    oi=Image.new('RGBA',(w,h),olc); oi.putalpha(o)
    sh=Image.new('RGBA',(w,h+10),(0,0,0,0)); sh.alpha_composite(oi,(0,8)); sh.alpha_composite(oi,(0,0)); sh.alpha_composite(g,(0,0))
    return sh
BK='/usr/share/fonts/opentype/urw-base35/URWBookman-Demi.otf'
a=text_img('SolveTheory',BK,150,(255,236,170),(240,150,60),12)
b=text_img('A D V E N T U R E',BK,62,(250,246,230),(200,214,190),8,pad=22)
logo=Image.new('RGBA',(max(a.size[0],b.size[0]),a.size[1]+b.size[1]-24),(0,0,0,0))
logo.alpha_composite(a,((logo.size[0]-a.size[0])//2,0)); logo.alpha_composite(b,((logo.size[0]-b.size[0])//2,a.size[1]-30))
logo.save(os.path.join(OUTDIR,'ui_logo.png')); 
meta={}
for f in os.listdir(OUTDIR):
    if f.startswith('_meta'): meta.update(json.load(open(os.path.join(OUTDIR,f))))
meta['ui_logo']=dict(w=logo.size[0],h=logo.size[1],px=0.5,py=0.5,ppu=100)
border={'ui_round':14,'ui_key':0}
names=[n for n in meta if not n.startswith('bg_')]
names.sort(key=lambda n:-meta[n]['h'])
AW=2048; PAD=3; x=y=PAD; rowh=0; pos={}
for n in names:
    w,h=meta[n]['w'],meta[n]['h']
    if x+w+PAD>AW: x=PAD; y+=rowh+PAD*2; rowh=0
    pos[n]=(x,y); x+=w+PAD*2; rowh=max(rowh,h)
AH=y+rowh+PAD; AHp=1
while AHp<AH: AHp*=2
print('atlas',AW,AH,'->',AHp)
atlas=np.zeros((AHp,AW,4),np.uint8)
lines=[]
for n in names:
    im=np.array(Image.open(os.path.join(OUTDIR,n+'.png')).convert('RGBA')); h,w=im.shape[:2]; px,py=pos[n]
    e=np.pad(im,((2,2),(2,2),(0,0)),mode='edge'); atlas[py-2:py+h+2,px-2:px+w+2]=e
    m=meta[n]; lines.append('%s %d %d %d %d %.4f %.4f %d %d'%(n,px,AHp-py-h,w,h,m['px'],m['py'],m['ppu'],border.get(n,0)))
Image.fromarray(atlas,'RGBA').save(os.path.join(res,'Art','atlas.png'))
preserved_backgrounds={'bg_canopy','bg_ground','bg_mid'}
for n in meta:
    if n.startswith('bg_'):
        if n not in preserved_backgrounds: Image.open(os.path.join(OUTDIR,n+'.png')).save(os.path.join(res,'Art',n+'.png'))
        m=meta[n]; lines.append('%s -1 0 %d %d %.4f %.4f %d 0'%(n,m['w'],m['h'],m['px'],m['py'],m['ppu']))
open(os.path.join(res,'Art','atlas_map.txt'),'w').write('\n'.join(lines))
import shutil
font_path=os.path.join(res,'Fonts','GameFont.ttf')
if not os.path.exists(font_path): shutil.copy('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf',font_path)
print('sprites',len(lines))
