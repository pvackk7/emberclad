from lib import *
random.seed(7)
W=(255,255,255)
def radial(n,power=2.0,col=W):
    y,x=np.mgrid[0:n,0:n]; d=np.sqrt((x-n/2+.5)**2+(y-n/2+.5)**2)/(n/2)
    a=np.clip(1-d,0,1)**power
    im=np.zeros((n,n,4),np.uint8); im[...,0]=col[0];im[...,1]=col[1];im[...,2]=col[2];im[...,3]=(a*255).astype(np.uint8)
    return Image.fromarray(im,'RGBA')
save_raw('fx_glow',radial(128,2.0),100)
save_raw('fx_soft',radial(64,0.7),100)
# shadow
n=96;y,x=np.mgrid[0:40,0:n];d=np.sqrt(((x-n/2)/(n/2))**2+((y-20)/20.0)**2);a=np.clip((1-d)*2.2,0,1)
im=np.zeros((40,n,4),np.uint8);im[...,0]=18;im[...,1]=28;im[...,2]=24;im[...,3]=(a*150).astype(np.uint8);save_raw('fx_shadow',Image.fromarray(im,'RGBA'),100)
# slash arc: crescent opening to the left, bulging right
def slash(name,r=70,th=24,span=80,col=W,edge=(255,226,150)):
    c=C(160,160)
    def cres(t,rr,thk,colr):
        pts=[]
        for i in range(41):
            a=math.radians(-span+2*span*i/40); pts.append((80+rr*math.cos(a)-12,80+rr*math.sin(a)))
        for i in range(40,-1,-1):
            a=math.radians(-span+2*span*i/40); k=math.cos(math.radians((i/40-0.5)*170))
            pts.append((80+(rr-thk*max(k,0)**0.8)*math.cos(a)-12,80+(rr-thk*max(k,0)**0.8)*math.sin(a)))
        t.poly(pts,colr)
    cres(c,r,th,edge); cres(c,r-2.5,th-7,col)
    c.save(name)
slash('fx_slash'); slash('fx_slash_big',r=74,th=34,span=88)
# spark burst
def spark(name,n=9,r0=12,r1=44,seed=1):
    random.seed(seed); c=C(100,100); pts=[]
    for i in range(n*2):
        a=math.pi*2*i/(n*2); r=(r1*random.uniform(0.6,1.0)) if i%2==0 else r0*random.uniform(0.8,1.2)
        pts.append((50+r*math.cos(a),50+r*math.sin(a)))
    c.poly(pts,(255,214,110)); 
    pts2=[(50+(x-50)*0.62,50+(y-50)*0.62) for x,y in pts]; c.poly(pts2,W)
    c.save(name)
spark('fx_spark',9,12,46,1); spark('fx_spark2',7,10,44,5)
# ring
c=C(128,128); c.arc(6,6,122,122,0,360,W,5); c.save('fx_ring')
# puff
c=C(64,64); [c.circ(x,y,r,W) for x,y,r in ((24,36,16),(40,34,17),(32,24,14),(20,26,9),(46,24,9))]; c.save('fx_puff')
# flame shapes
def flame(name,seed):
    random.seed(seed); c=C(80,100)
    def fl(k,col):
        j=lambda v:v+random.uniform(-3,3)
        c.blob([(40,96),(40-26*k,80),(40-22*k,j(56)),(40-10*k,j(40)),(40-8*k,j(18)+ (1-k)*30),(40+4*k,j(34)),(40+14*k,j(24)+(1-k)*26),(40+20*k,j(54)),(40+26*k,78)],col)
    fl(1.0,(240,96,40)); fl(0.72,(255,170,50)); fl(0.42,(255,236,150))
    c.save(name,(40,94))
flame('fx_flame1',1); flame('fx_flame2',2); flame('fx_flame3',3)
# ember wave projectile (facing right)
c=C(150,110)
def wv(k,col,dx):
    c.blob([(118+dx,55),(96+dx,55-44*k),(60+dx,55-50*k),(30,55-30*k),(8,55-34*k),(34,55-12*k),(14,55),(34,55+12*k),(8,55+34*k),(30,55+30*k),(60+dx,55+50*k),(96+dx,55+44*k)],col)
wv(1,(236,90,40),20); wv(0.74,(255,160,48),12); wv(0.46,(255,232,140),4)
c.save('fx_wave')
# bones
BONE=(236,229,204);BONED=(192,182,152)
c=C(40,18); c.line([(8,9),(32,9)],BONE,5); [c.circ(x,y,3.6,BONE) for x,y in ((7,6),(7,12),(33,6),(33,12))]; c.outline(2.2); c.save('fx_bone1')
c=C(34,26); c.arc(4,4,30,40,190,350,BONE,5); c.outline(2.2); c.save('fx_bone2')
c=C(22,22); c.blob([(4,8),(12,3),(18,9),(15,18),(6,17)],BONE); c.outline(2.2); c.save('fx_bone3')
# leaves
for i,(a,b) in enumerate((((120,170,72),(86,132,60)),((214,170,70),(176,128,52)))):
    c=C(26,18); c.blob([(2,9),(10,3),(22,6),(24,10),(14,15),(6,14)],a,6); c.inside(lambda t:t.blob([(2,10),(24,10),(14,18),(4,16)],b)); c.line([(4,10),(22,9)],b,1.2); c.save('fx_leaf%d'%(i+1))
# berry pickup
c=C(48,48)
c.blob([(22,12),(30,4),(40,8),(34,16)],(112,158,72),6)
for x,y,r in ((17,26,10),(31,27,10),(24,36,10)): c.circ(x,y,r,(214,52,70))
c.inside(lambda t:[t.circ(x+4,y+5,9,(160,34,60)) for x,y in ((17,26),(31,27),(24,36))]) 
c.outline(2.4)
for x,y in ((14,22),(28,23),(21,32)): c.circ(x,y,2.4,(255,210,214))
c.save('fx_berry',(24,44))
# mana ember orb icon
c=C(40,40); c.blob([(20,36),(8,28),(8,18),(15,12),(18,3),(25,12),(32,18),(32,28)],(255,150,50)); c.inside(lambda t:t.blob([(20,36),(13,30),(14,22),(20,14),(26,22),(27,30)],(255,232,150))); c.outline(2.4); c.save('ui_ember')
c=C(40,40); c.blob([(20,36),(8,28),(8,18),(15,12),(18,3),(25,12),(32,18),(32,28)],(70,56,60)); c.outline(2.4); c.save('ui_ember_off')
# heart
c=C(40,38); c.blob([(20,34),(5,20),(5,10),(12,5),(20,10),(28,5),(35,10),(35,20)],(222,60,70)); c.inside(lambda t:t.blob([(20,36),(10,26),(30,26),(36,16),(38,30)],(170,40,60))); c.outline(2.4); c.circ(12,12,2.6,(255,200,204)); c.save('ui_heart')
# ui: rounded panel (9-slice), key cap, arrow, pixel
c=C(48,48,out=1,ss=4); c.rr(2,2,46,46,12,W); c.save('ui_round')
c=C(64,64); c.rr(4,6,60,60,10,(60,48,50)); c.rr(4,3,60,52,10,(246,238,220)); c.inside(lambda t:t.rr(4,40,60,60,0,(214,202,180))); c.outline(2.4); c.save('ui_key')
c=C(150,64); c.rr(4,6,146,60,10,(60,48,50)); c.rr(4,3,146,52,10,(246,238,220)); c.outline(2.4); c.save('ui_keywide')
c=C(120,80); c.poly([(8,26),(70,26),(70,6),(114,40),(70,74),(70,54),(8,54)],(255,214,110)); c.inside(lambda t:t.poly([(8,42),(114,42),(70,80),(8,60)],(232,160,60))); c.outline(3); c.save('ui_arrow')
save_raw('ui_white',Image.new('RGBA',(8,8),(255,255,255,255)),100)
# god ray
h,w=512,256; y,x=np.mgrid[0:h,0:w]; cx=w/2
core=np.clip(1-np.abs(x-cx)/(w*0.42),0,1)**1.6; fade=np.clip(1-(y/h),0,1)**0.6*np.clip(y/40.0,0,1)
im=np.zeros((h,w,4),np.uint8); im[...,0]=255;im[...,1]=236;im[...,2]=170; im[...,3]=(core*fade*150).astype(np.uint8); save_raw('fx_ray',Image.fromarray(im,'RGBA'),100,(0.5,1.0))
# vignette
h,w=270,480; y,x=np.mgrid[0:h,0:w]; d=np.sqrt(((x-w/2)/(w/2))**2*0.9+((y-h/2)/(h/2))**2*1.0); a=np.clip((d-0.55)/0.75,0,1)**1.7
im=np.zeros((h,w,4),np.uint8); im[...,0]=8;im[...,1]=20;im[...,2]=22; im[...,3]=(a*210).astype(np.uint8); save_raw('fx_vignette',Image.fromarray(im,'RGBA'),100)
write_meta('fx'); print('fx',len(META))
