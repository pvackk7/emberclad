from lib import *
W=2048
def wrapc(w,h,ss=2):
    c=C(w,h,out=1,ss=ss); return c
def each(fn,x):  # draw wrapped
    for dx in (-W,0,W): fn(x+dx)
# sky gradient
h=256; im=np.zeros((h,8,4),np.uint8)
stops=[(0,(30,62,66)),(0.35,(58,110,96)),(0.62,(130,180,128)),(0.8,(214,226,150)),(1,(226,232,170))]
for y in range(h):
    t=y/(h-1)
    for i in range(len(stops)-1):
        if stops[i][0]<=t<=stops[i+1][0]:
            k=(t-stops[i][0])/(stops[i+1][0]-stops[i][0]); col=mix(stops[i][1],stops[i+1][1],k)
    im[y,:,:3]=col; im[y,:,3]=255
save_raw('bg_sky',Image.fromarray(im,'RGBA'),100)
# far trees
def trees(name,seed,col,ccol,n,wmin,wmax,base_y,bush=None,canopy_y=170,h=900):
    random.seed(seed); c=wrapc(W,h)
    xs=sorted(random.uniform(0,W) for _ in range(n))
    for x in xs:
        w=random.uniform(wmin,wmax); lean=random.uniform(-30,30)
        def tr(xx,w=w,lean=lean):
            c.poly([(xx-w/2+lean,-10),(xx+w/2+lean,-10),(xx+w*0.62,base_y),(xx+w*1.0,base_y+30),(xx-w*1.0,base_y+30),(xx-w*0.62,base_y)],col)
            if random.random()<0.5: c.line([(xx+lean*0.5,300+random.uniform(-80,80)),(xx+lean*0.5+random.choice((-1,1))*random.uniform(60,120),200+random.uniform(-60,60))],col,w*0.22)
        each(tr,x)
    for _ in range(46):
        x=random.uniform(0,W); y=random.uniform(-20,canopy_y); r=random.uniform(60,130)
        each(lambda xx,y=y,r=r:c.circ(xx,y,r,ccol),x)
    if bush:
        for _ in range(70):
            x=random.uniform(0,W); y=base_y+random.uniform(-6,26); r=random.uniform(26,56)
            each(lambda xx,y=y,r=r:c.circ(xx,y,r,bush),x)
        c.poly([(0,base_y+20),(W,base_y+20),(W,h),(0,h)],bush)
    else: c.poly([(0,base_y+24),(W,base_y+24),(W,h),(0,h)],col)
    c.save(name)
trees('bg_far',21,(124,170,132),(104,152,122),13,34,70,430,bush=(112,160,126))
trees('bg_mid',22,(54,100,88),(40,82,74),9,60,120,455,bush=(48,94,78),canopy_y=150)
# canopy overlay
def canopy():
    random.seed(31); h=420; c=wrapc(W,h)
    D=(22,52,48);M=(36,78,58);L=(70,120,66);LL=(140,184,84)
    for _ in range(80):
        x=random.uniform(0,W); y=random.uniform(-30,150)+ (60 if random.random()<0.3 else 0); r=random.uniform(50,110)
        each(lambda xx,y=y,r=r:c.circ(xx,y,r,D),x)
    c.poly([(0,0),(W,0),(W,90),(0,90)],D)
    cl=[(random.uniform(0,W),random.uniform(40,230),random.uniform(22,46)) for _ in range(90)]
    def sh(t):
        for x,y,r in cl:
            for dx in (-W,0,W):
                t.arc(x+dx-r,y-r,x+dx+r,y+r,20,160,M,7)
        for x,y,r in cl[:40]:
            for dx in (-W,0,W): t.blob([(x+dx-10,y+r*0.8),(x+dx,y+r*0.6),(x+dx+12,y+r*0.85),(x+dx,y+r*1.05)],L,6)
        for x,y,r in cl[:14]:
            for dx in (-W,0,W): t.blob([(x+dx+16,y+r*0.9),(x+dx+26,y+r*0.74),(x+dx+34,y+r*0.95),(x+dx+24,y+r*1.1)],LL,6)
    c.inside(sh)
    # hanging vines
    for _ in range(16):
        x=random.uniform(0,W); y0=random.uniform(120,220); L_=random.uniform(60,170)
        def vine(xx,y0=y0,L_=L_):
            c.line([(xx,y0),(xx+6,y0+L_*0.5),(xx-3,y0+L_)],D,4,True)
            for k in range(3): c.blob([(xx,y0+L_*(0.3+0.3*k)),(xx+12,y0+L_*(0.3+0.3*k)-5),(xx+14,y0+L_*(0.3+0.3*k)+8)],M,6)
        each(vine,x)
    c.save('bg_canopy',(W/2,0))
canopy()
# ground tile
def ground():
    random.seed(41); h=480; c=wrapc(W,h)
    TOP=(52,96,66);MAIN=(96,146,76);PATH=(170,152,104);PATHD=(140,124,90);LOW=(74,120,70);DK=(64,108,66);LT=(128,176,84)
    edge=[(x,26+random.uniform(-10,10)) for x in range(-64,W+65,64)]; edge[-2]=(edge[-2][0],edge[1][1]); 
    ys={x:y for x,y in edge}
    for x,y in edge:
        each(lambda xx,y=y:c.circ(xx,y+14,34,TOP),x)
    c.poly([(0,40),(W,40),(W,h),(0,h)],TOP)
    def body(t):
        t.blob([(-100,96)]+[(x,100+18*math.sin(x/W*math.pi*4)+random.uniform(-6,6)) for x in range(0,W+1,128)]+[(W+100,96),(W+100,h+50),(-100,h+50)],MAIN,6)
        # path
        up=[(x,196+26*math.sin(x/W*math.pi*2)) for x in range(-128,W+129,64)]
        lo=[(x,330+22*math.sin(x/W*math.pi*2+1.0)) for x in range(W+128,-129,-64)]
        t.poly(smooth(up,6,False)+smooth(lo,6,False),PATHD)
        up2=[(x,y+8) for x,y in up]; lo2=[(x,y-10) for x,y in lo]
        t.poly(smooth(up2,6,False)+smooth(lo2,6,False),PATH)
        t.blob([(-100,400)]+[(x,404+14*math.sin(x/W*math.pi*6)) for x in range(0,W+1,128)]+[(W+100,400),(W+100,h+50),(-100,h+50)],LOW,6)
        for _ in range(60):   # path pebbles / scuffs
            x=random.uniform(0,W); y=random.uniform(220,310); r=random.uniform(3,8)
            for dx in (-W,0,W): t.ell(x+dx-r*1.6,y-r*0.6,x+dx+r*1.6,y+r*0.6,random.choice((PATHD,(190,174,124))))
        for _ in range(240):  # grass ticks
            x=random.uniform(0,W); y=random.choice((random.uniform(70,190),random.uniform(340,470))); col=random.choice((DK,LT,DK))
            for dx in (-W,0,W): t.line([(x+dx-5,y-8),(x+dx,y),(x+dx+6,y-9)],col,2.2)
        for _ in range(26):   # darker patches
            x=random.uniform(0,W); y=random.choice((random.uniform(80,180),random.uniform(350,460))); rx=random.uniform(40,90)
            for dx in (-W,0,W): t.ell(x+dx-rx,y-rx*0.22,x+dx+rx,y+rx*0.22,mix(MAIN,DK,0.55))
    c.inside(body)
    im=c.final()
    # sun dapples (soft)
    random.seed(43); dap=Image.new('RGBA',im.size,(0,0,0,0)); dd=ImageDraw.Draw(dap)
    for _ in range(22):
        x=random.uniform(0,W); y=random.uniform(120,440); rx=random.uniform(50,120)
        for dx in (-W,0,W): dd.ellipse([x+dx-rx,y-rx*0.3,x+dx+rx,y+rx*0.3],fill=(255,244,170,46))
    dap=dap.filter(ImageFilter.GaussianBlur(7)); a=ImageChops.multiply(dap.getchannel('A'),im.getchannel('A')); dap.putalpha(a)
    im=Image.alpha_composite(im,dap)
    save_raw('bg_ground',im,100,(0.5,1.0))
ground()
# foreground silhouettes
def fg():
    random.seed(51); h=300; c=wrapc(W,h); D=(14,34,36);M=(24,52,48)
    for _ in range(40):
        x=random.uniform(0,W); y=random.uniform(200,300); r=random.uniform(40,90)
        each(lambda xx,y=y,r=r:c.circ(xx,y,r,D),x)
    for _ in range(90):
        x=random.uniform(0,W); hh=random.uniform(70,190); lean=random.uniform(-40,40)
        each(lambda xx,hh=hh,lean=lean:c.blob([(xx-9,h),(xx+lean,h-hh),(xx+9,h)],D,6),x)
    for _ in range(10):
        x=random.uniform(0,W); 
        def frond(xx):
            for ang in (-60,-30,0,30,60):
                L=random.uniform(90,150); tx=xx+L*math.sin(math.radians(ang)); ty=h-20-L*math.cos(math.radians(ang))
                c.blob([(xx,h-10),((xx+tx)/2-14,(h+ty)/2),(tx,ty),((xx+tx)/2+14,(h+ty)/2)],D,6)
        each(frond,x)
    cl=[(random.uniform(0,W),random.uniform(190,290),random.uniform(24,50)) for _ in range(40)]
    c.inside(lambda t:[t.arc(x+dx-r,y-r,x+dx+r,y+r,200,340,M,6) for x,y,r in cl for dx in (-W,0,W)])
    c.save('bg_fg',(W/2,h))
fg()
write_meta('bg'); print('bg',len(META))
# ---- mock scene ----
sc=Image.new('RGBA',(1600,900)); sky=Image.open(OUTDIR+'/bg_sky.png').resize((1600,900)); sc.paste(sky,(0,0))
for n,off in (('bg_far',0),('bg_mid',300)):
    im=Image.open(OUTDIR+'/%s.png'%n); sc.alpha_composite(im.crop((off,0,off+1600,900)),(0,0))
g=Image.open(OUTDIR+'/bg_ground.png'); sc.alpha_composite(g.crop((0,0,1600,480)),(0,470))
def put(name,x,y,sc_=1.0):
    im=Image.open(OUTDIR+'/%s.png'%name); m=META.get(name) or json.load(open(OUTDIR+'/_meta_props.json')).get(name) or json.load(open(OUTDIR+'/_meta_chars.json')).get(name) or json.load(open(OUTDIR+'/_meta_fx.json'))[name]
    k=100.0/m['ppu']*sc_; im=im.resize((int(im.size[0]*k),int(im.size[1]*k)),Image.LANCZOS)
    sc.alpha_composite(im,(int(x-m['px']*im.size[0]),int(y-(1-m['py'])*im.size[1])))
for n,x,y in (('pr_trunk_a',200,560),('pr_trunk_b',520,540),('pr_pillar_tall',760,548),('pr_wall',1050,560),('pr_trunk_c',1400,570),('pr_bush_a',350,560),('pr_bush_c',1250,566),('pr_fern_a',620,566),('pr_pillar_broken',900,640),('pr_rock_a',300,700),('pr_shrooms',480,640),('pr_log',1200,760),('pr_shrine',1480,690),('pr_grass_a',700,720),('pr_grass_b',820,800),('pr_flower_w',760,790),('pr_flower_y',1000,720),('pr_bones',1100,820),('pr_stump',560,820),('pr_rock_b',930,830),('pr_fern_b',150,800),('pr_bush_b',80,660),('pr_flower_p',400,800),('pr_grass_c',1340,840)):
    put(n,x,y)
ch=Image.open(OUTDIR+'/../sheet_chars.png').convert('RGBA')
for (bx,x) in ((30,560),(1000,900)):
    sub=ch.crop((bx,150,bx+230,540)).resize((115,195)); 
cn=Image.open(OUTDIR+'/bg_canopy.png'); sc.alpha_composite(cn.crop((200,0,1800,420)),(0,-60))
f=Image.open(OUTDIR+'/bg_fg.png'); sc.alpha_composite(f.crop((0,0,1600,300)),(0,690))
v=Image.open(OUTDIR+'/fx_vignette.png').resize((1600,900)); sc.alpha_composite(v)
sc.convert('RGB').save(OUTDIR+'/../sheet_scene.png')
