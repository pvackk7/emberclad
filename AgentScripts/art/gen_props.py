from lib import *
random.seed(11)
BK=(92,66,60);BKD=(64,46,52);BKL=(124,92,72);MOSS=(112,158,72);MOSSD=(78,120,58);MOSSL=(150,190,90)
G1=(58,104,64);G2=(84,138,70);G3=(122,172,80);G4=(160,204,96)
ST=(138,150,136);STD=(98,112,108);STL=(176,186,164);STDD=(72,84,86)
def trunk(name,wf=1.0,seed=1,knot=True):
    random.seed(seed); c=C(240,600,out=1,ss=3); X=lambda v:120+(v-120)*wf
    pts=[(72,-10),(80,180),(74,380),(64,480),(44,540),(6,578),(40,592),(88,576),(120,594),(152,576),(200,592),(234,578),(196,540),(176,480),(166,380),(160,180),(168,-10)]
    c.blob([(X(x),y) for x,y in pts],BK)
    def sh(t):
        t.blob([(X(128),-10),(X(170),-10),(X(170),380),(X(182),480),(X(240),600),(X(140),600),(X(136),400)],BKD)
        t.blob([(X(76),-10),(X(92),-10),(X(94),300),(X(84),470),(X(60),540),(X(70),420),(X(80),200)],BKL)
        for i in range(9):
            x=random.uniform(84,160); y0=random.uniform(0,420); L=random.uniform(50,130)
            t.line([(X(x),y0),(X(x+random.uniform(-5,5)),y0+L/2),(X(x+random.uniform(-4,4)),y0+L)],BKD,3,True)
        if knot: 
            t.ell(X(104),250,X(134),296,BKD); t.ell(X(110),258,X(128),288,(40,28,36))
        patches={1:((74,505,0.9),),2:((126,535,0.82),),3:((66,520,0.78),(150,548,0.68))}
        for mx,my,s in patches.get(seed,((74,505,0.85),)):
            edge=[(-14,7),(-12,1),(-6,-3),(1,-5),(8,-11),(15,-8),(20,-3),(28,-5),(35,-1),(43,5),(39,10),(30,12),(19,10),(9,13),(-2,11)]
            shade=[(-12,7),(-4,6),(5,8),(17,7),(29,9),(40,6),(43,9),(34,13),(22,14),(8,13),(-4,12)]
            t.blob([(X(mx+dx*s),my+dy*s) for dx,dy in shade],MOSSD,6)
            t.blob([(X(mx+dx*s),my+dy*s) for dx,dy in edge],MOSS,6)
            for dx in (2,17,31):
                t.blob([(X(mx+dx*s),my+2*s),(X(mx+(dx+2)*s),my-4*s),(X(mx+(dx+7)*s),my-7*s),(X(mx+(dx+8)*s),my-1*s),(X(mx+(dx+4)*s),my+3*s)],MOSSL,5)
            t.line([(X(mx-4*s),my+7*s),(X(mx+11*s),my+6*s),(X(mx+25*s),my+8*s)],MOSSD,1.6,True)
        if seed==2:
            t.line([(X(145),318),(X(138),341),(X(146),365),(X(139),391),(X(148),414)],MOSSD,2.2,True)
            for px,py,side in ((141,336,-1),(142,357,1),(141,382,-1),(144,404,1)):
                leaf=[(px,py),(px+side*5,py-6),(px+side*13,py-8),(px+side*10,py-2),(px+side*3,py+3)]
                t.blob([(X(x),y) for x,y in leaf],MOSS,6)
                t.line([(X(px),py),(X(px+side*8),py-3)],MOSSL,1.2,True)
    c.inside(sh); c.outline(3.0); c.save(name,(120,584))
trunk('pr_trunk_a',1.0,1); trunk('pr_trunk_b',0.72,2,False); trunk('pr_trunk_c',1.25,3)
def bush(name,seed,w=170,h=120):
    c=C(w,h,out=2,ss=3)
    profiles={
        3:[(0.02,0.84),(0.05,0.7),(0.12,0.65),(0.18,0.56),(0.27,0.53),(0.34,0.42),(0.43,0.45),(0.48,0.32),(0.58,0.31),(0.64,0.43),(0.74,0.38),(0.82,0.49),(0.92,0.52),(0.98,0.68),(0.97,0.88),(0.88,0.96),(0.12,0.96),(0.04,0.91)],
        5:[(0.02,0.82),(0.06,0.68),(0.15,0.6),(0.22,0.48),(0.31,0.45),(0.38,0.52),(0.47,0.39),(0.57,0.35),(0.63,0.48),(0.72,0.43),(0.79,0.52),(0.9,0.5),(0.98,0.67),(0.96,0.89),(0.86,0.96),(0.14,0.96),(0.04,0.91)],
        8:[(0.02,0.83),(0.04,0.69),(0.13,0.64),(0.2,0.55),(0.29,0.58),(0.34,0.46),(0.44,0.43),(0.53,0.5),(0.59,0.37),(0.69,0.4),(0.76,0.51),(0.84,0.46),(0.94,0.54),(0.99,0.7),(0.96,0.9),(0.88,0.96),(0.12,0.96),(0.03,0.91)]
    }
    silhouette=[(x*w,y*h) for x,y in profiles[seed]]
    c.blob(silhouette,G2,7)
    def sh(t):
        shadow=[(0.04,0.79),(0.17,0.77),(0.29,0.82),(0.42,0.77),(0.55,0.83),(0.68,0.78),(0.82,0.81),(0.97,0.76),(0.97,0.93),(0.88,0.96),(0.12,0.96),(0.04,0.91)]
        t.blob([(x*w,y*h) for x,y in shadow],G1,7)
        patches={
            3:[(0.19,0.57,0.1),(0.49,0.48,0.12),(0.78,0.56,0.09)],
            5:[(0.22,0.56,0.09),(0.53,0.49,0.1),(0.82,0.56,0.08)],
            8:[(0.17,0.58,0.08),(0.43,0.52,0.1),(0.7,0.48,0.09),(0.87,0.59,0.07)]
        }
        for cx,cy,s in patches[seed]:
            rx=w*s; ry=h*s*0.34
            leaf_mass=[(-1,0),(-0.82,-0.45),(-0.45,-0.55),(-0.2,-0.95),(0.08,-0.6),(0.42,-0.7),(0.8,-0.28),(0.94,0.12),(0.62,0.46),(0.2,0.38),(-0.28,0.54),(-0.72,0.36)]
            t.blob([(w*cx+dx*rx,h*cy+dy*ry) for dx,dy in leaf_mass],G3,7)
    c.inside(sh); c.outline(2.2)
    c.save(name,(w/2,h-10))
bush('pr_bush_a',3); bush('pr_bush_b',5,130,96); bush('pr_bush_c',8,200,130)
def fern(name,seed):
    random.seed(seed); c=C(120,100)
    for i,ang in enumerate((-72,-44,-18,8,34,60)):
        a=math.radians(ang-90+90); L=random.uniform(52,72); col=G2 if i%2 else G3
        bx,by=60,94; tx=bx+L*math.sin(math.radians(ang)); ty=by-L*math.cos(math.radians(ang))*0.95
        mx=(bx+tx)/2+ (8 if ang>0 else -8); my=(by+ty)/2-14
        nx,ny=(ty-by),-(tx-bx); n=math.hypot(nx,ny); nx,ny=nx/n*8,ny/n*8
        c.blob([(bx,by),(mx+nx,my+ny),(tx,ty),(mx-nx,my-ny)],col,8)
        c.inside(lambda t:t.line([(bx,by),(mx,my),(tx,ty)],G1,1.6,True))
    c.outline(2.6); c.save(name,(60,94))
fern('pr_fern_a',1); fern('pr_fern_b',4)
def tuft(name,seed,col=G3,cold=G2):
    random.seed(seed); c=C(60,48)
    for i,dx in enumerate((-20,-11,-3,6,14,22)):
        hh=random.uniform(22,38); lean=dx*0.5+random.uniform(-4,4)
        c.blob([(30+dx-4,44),(30+dx+lean,44-hh),(30+dx+4,44)],col if i%2 else cold,6)
    c.outline(2.4); c.save(name,(30,44))
tuft('pr_grass_a',1); tuft('pr_grass_b',2,G4,G3); tuft('pr_grass_c',3,G2,G1)
def flower(name,col):
    c=C(34,50); c.line([(17,48),(16,30),(18,18)],G2,2.6,True); c.blob([(17,40),(8,34),(10,30)],G3,6)
    for i in range(5):
        a=math.pi*2*i/5-1.57; c.circ(18+7*math.cos(a),15+7*math.sin(a),5.4,col)
    c.outline(2.3); c.circ(18,15,3.4,(246,196,80)); c.save(name,(17,48))
flower('pr_flower_w',(250,246,232)); flower('pr_flower_y',(250,214,110)); flower('pr_flower_p',(214,150,220))
def shrooms():
    c=C(80,64); CAP=(120,216,224);CAPD=(74,160,184);STEM=(238,230,210)
    for x,y,s in ((22,40,0.8),(44,30,1.2),(62,44,0.7)):
        c.rr(x-4*s,y,x+4*s,60,3*s,STEM); c.blob([(x-16*s,y+3*s),(x-12*s,y-9*s),(x,y-14*s),(x+12*s,y-9*s),(x+16*s,y+3*s),(x,y+5*s)],CAP)
    def sh(t):
        for x,y,s in ((22,40,0.8),(44,30,1.2),(62,44,0.7)):
            t.blob([(x,y-14*s),(x+12*s,y-9*s),(x+16*s,y+6*s),(x+2*s,y+6*s)],CAPD); t.circ(x-6*s,y-5*s,2.2*s,(220,250,250)); t.circ(x+3*s,y-8*s,1.6*s,(220,250,250))
    c.inside(sh); c.outline(2.4); c.save('pr_shrooms',(40,60))
shrooms()
def rock(name,w,h,seed):
    random.seed(seed); c=C(w,h)
    c.blob([(6,h-8),(10,h*0.45),(w*0.3,h*0.12),(w*0.62,h*0.08),(w*0.9,h*0.4),(w-6,h-8)],ST,8)
    def sh(t):
        t.blob([(w*0.55,0),(w,0),(w,h),(w*0.4,h),(w*0.62,h*0.5)],STD)
        t.blob([(8,h*0.5),(w*0.3,h*0.14),(w*0.6,h*0.1),(w*0.5,h*0.24),(w*0.3,h*0.3),(w*0.14,h*0.6)],MOSS)
        t.line([(w*0.36,h*0.4),(w*0.44,h*0.6),(w*0.4,h*0.8)],STDD,1.8)
        t.blob([(0,h-14),(w,h-14),(w,h),(0,h)],STDD)
    c.inside(sh); c.outline(2.8); c.save(name,(w/2,h-10))
rock('pr_rock_a',130,90,1); rock('pr_rock_b',76,54,2)
def drum(t,x0,y0,x1,y1): 
    t.rr(x0,y0,x1,y1,5,ST)
def pillar(name,h,broken,seed):
    random.seed(seed); c=C(130,h,out=1,ss=3); n=int((h-50)/52)
    top=30
    c.rr(14,h-34,116,h-8,6,ST); c.rr(24,h-52,106,h-30,5,ST)
    y=h-52
    for i in range(n):
        c.rr(32,y-52,98,y+2,6,ST); y-=50
    if broken: c.poly([(32,y+6),(32,y-10),(48,y-30),(60,y-12),(74,y-36),(88,y-16),(98,y-22),(98,y+6)],ST)
    else: c.rr(22,y-18,108,y+4,5,ST); c.rr(14,y-34,116,y-14,5,ST)
    def sh(t):
        t.poly([(74,0),(130,0),(130,h),(70,h)],STD)
        yy=h-52
        for i in range(n+1):
            t.line([(30,yy),(100,yy)],STDD,2.2); yy-=50
        for x in (44,56,68,80): t.line([(x,y+10),(x,h-56)],STD if x<70 else STDD,1.6)
        t.blob([(14,h-40),(50,h-48),(70,h-30),(40,h-10),(10,h-10)],MOSS)
        for _ in range(4):
            mx=random.uniform(30,84); my=random.uniform(y+20,h-80); t.blob([(mx,my),(mx+16,my-6),(mx+22,my+10),(mx+6,my+14)],MOSSD)
        t.line([(40,y+30),(52,y+60),(46,y+90)],STDD,1.8)
    c.inside(sh)
    # ivy
    for (ix,iy,L) in ((36,y+10,120),(92,y+40,90)):
        pts=[(ix,iy)]; 
        for k in range(1,6): pts.append((ix+random.uniform(-8,8),iy+L*k/5))
        c.line(pts,MOSSD,2.4,True)
        for px,py in pts[1:]: c.blob([(px,py),(px+9,py-4),(px+12,py+5),(px+3,py+8)],MOSS,6); c.blob([(px,py),(px-9,py-2),(px-10,py+7)],MOSSL,6)
    c.outline(3.0); c.save(name,(65,h-14))
pillar('pr_pillar_broken',230,True,1); pillar('pr_pillar_tall',460,False,2); pillar('pr_pillar_mid',330,True,3)
def wall():
    random.seed(5); c=C(340,230,out=1,ss=3)
    c.poly([(8,222),(8,120),(40,120),(40,70),(96,70),(96,30),(150,30),(150,56),(210,56),(210,100),(262,100),(262,150),(310,150),(310,180),(332,180),(332,222)],ST)
    def sh(t):
        for row in range(8):
            yy=30+row*26; t.line([(0,yy),(340,yy)],STD,2)
            for k in range(8): 
                x=k*46+(23 if row%2 else 0); t.line([(x,yy),(x,yy+26)],STD,2)
        for _ in range(14):
            x=random.randrange(0,7)*46+random.choice((2,25)); yy=30+random.randrange(0,7)*26; t.rr(x+2,yy+2,x+42,yy+24,3,random.choice((STL,STD,ST)))
        t.blob([(0,190),(80,200),(200,196),(340,204),(340,230),(0,230)],STDD)
        for (mx,my,s) in ((40,70,1.4),(150,30,1.2),(262,100,1.1),(96,120,1.0)):
            t.blob([(mx-20*s,my),(mx+10*s,my-4),(mx+40*s,my+4),(mx+30*s,my+22*s),(mx+6*s,my+34*s),(mx-14*s,my+18*s)],MOSS)
            t.blob([(mx-8*s,my+10),(mx+20*s,my+8),(mx+12*s,my+26*s)],MOSSD)
    c.inside(sh); c.outline(3.0); c.save('pr_wall',(170,214))
wall()
def log():
    c=C(240,96); 
    c.rr(20,24,224,84,28,BK); c.ell(4,22,56,86,BKL)
    def sh(t):
        t.rr(20,58,224,90,0,BKD); t.ell(4,22,56,86,(196,160,116)); t.ell(13,33,47,75,(170,132,96)); t.ell(22,44,38,64,(140,104,80))
        for x in (84,120,170): t.line([(x,30),(x+14,34),(x+40,30)],BKD,2.4,True)
        t.blob([(100,20),(140,16),(190,22),(180,36),(140,32),(110,36)],MOSS)
    c.inside(sh); c.outline(2.8); c.save('pr_log',(120,82))
log()
def stump():
    c=C(120,96); c.blob([(20,30),(100,30),(104,66),(116,88),(84,84),(60,90),(36,84),(4,88),(16,66)],BK,6); c.ell(18,14,102,46,(196,160,116))
    def sh(t):
        t.blob([(66,30),(120,30),(120,96),(60,96)],BKD); t.ell(18,14,102,46,(196,160,116)); t.ell(32,20,88,40,(170,132,96)); t.ell(48,25,72,35,(140,104,80))
        t.blob([(10,60),(34,54),(44,74),(20,84)],MOSS)
    c.inside(sh); c.outline(2.8); c.save('pr_stump',(60,84))
stump()
def shrine():
    c=C(180,300,out=1,ss=3); BR=(200,150,70);BRD=(150,100,50)
    c.rr(10,262,170,292,6,ST); c.rr(26,238,154,266,6,ST); c.rr(62,110,118,242,8,ST); c.rr(48,96,132,118,6,ST)
    c.blob([(30,96),(36,60),(144,60),(150,96),(120,104),(60,104)],BR,6)
    def sh(t):
        t.poly([(96,0),(180,0),(180,300),(92,300)],STD); 
        t.blob([(30,96),(36,60),(144,60),(150,96),(120,104),(60,104)],BR); t.blob([(96,56),(150,56),(154,100),(96,108)],BRD); t.rr(30,58,150,68,3,(232,190,100))
        t.line([(26,266),(154,266)],STDD,2.2); t.line([(62,242),(118,242)],STDD,2.2)
        t.blob([(10,270),(50,262),(70,280),(30,292),(10,292)],MOSS); t.blob([(62,200),(84,190),(90,214),(66,224)],MOSSD)
    c.inside(sh); c.outline(3.0)
    for yy in (140,162,184): c.line([(82,yy),(98,yy)],(255,196,74),3); 
    c.line([(90,130),(90,196)],(255,196,74),2.4)
    c.save('pr_shrine',(90,284))
shrine()
def bonepile():
    BONE=(236,229,204);BONED=(192,182,152); c=C(110,60)
    for (x0,y0,x1,y1) in ((14,44,60,30),(40,48,96,40),(30,30,78,46),(60,26,90,36)):
        c.line([(x0,y0),(x1,y1)],BONE,6); [c.circ(x,y,4.4,BONE) for x,y in ((x0,y0),(x1,y1))]
    c.blob([(40,26),(44,12),(58,8),(70,14),(72,26),(64,34),(48,34)],BONE)
    c.inside(lambda t:(t.poly([(0,44),(110,40),(110,60),(0,60)],BONED),t.ell(48,16,56,26,(40,30,38)),t.ell(60,16,68,26,(40,30,38))))
    c.outline(2.5); c.save('pr_bones',(55,50))
bonepile()
write_meta('props'); print('props',len(META))
