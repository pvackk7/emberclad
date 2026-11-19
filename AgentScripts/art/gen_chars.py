from lib import *
RUST=(186,90,46);RUSTD=(140,60,32);RUSTDD=(104,44,30);RUSTL=(226,140,76);DARK=(46,28,30)
GLOW=(255,196,74);GLOWL=(255,240,190);RED=(198,58,48);REDD=(150,38,40);GOLD=(222,168,66);GOLDD=(170,116,44)
STEEL=(204,214,216);STEELD=(150,166,176)
BONE=(236,229,204);BONED=(192,182,152);BONEDD=(150,140,118);SOCK=(40,30,38);MOSS=(112,158,72);MOSSD=(78,120,58);EYE=(170,240,120)
PR=(206,90,44);PRL=(232,128,66);PRD=(160,62,34);CREAM=(250,234,206);PDK=(62,38,36)

# ---------------- KNIGHT ----------------
def k_helmet():
    c=C(100,96)
    c.blob([(34,14),(18,4),(6,12),(8,26),(20,26)],RED)  # plume
    c.blob([(14,52),(16,32),(28,16),(48,10),(68,15),(82,30),(87,50),(85,66),(74,79),(50,83),(26,81),(15,68)],RUST)
    def sh(t):
        t.blob([(10,56),(30,62),(50,70),(72,72),(90,62),(80,84),(50,90),(20,86),(8,70)],RUSTD)
        t.blob([(30,22),(44,15),(60,16),(52,22),(38,26),(28,34),(24,30)],RUSTL)
        t.blob([(20,40),(26,38),(27,46),(21,47)],RUSTD); t.blob([(60,24),(66,23),(68,29),(62,30)],RUSTD)
        t.blob([(36,46),(60,41),(90,44),(92,56),(62,58),(38,58)],DARK)   # visor
        t.line([(34,44),(60,38),(90,41)],RUSTDD,2.2,True)
        for x in (68,74,80): t.line([(x,63),(x,72)],DARK,2.4)
        t.line([(30,82),(50,85),(72,81)],RUSTDD,2)
    c.inside(sh)
    c.blob([(30,14),(48,5),(68,11),(50,13)],RUSTDD)  # crest
    for x,y in ((40,36),(50,34)): c.circ(x,y,1.8,RUSTL)
    c.outline()
    for x in (60,76):
        c.ell(x-4.5,45,x+4.5,55,GLOW); c.ell(x-2,47,x+2.5,52,GLOWL)
    c.save('k_head',(47,84))
def k_torso():
    c=C(68,64)
    for x0,x1,y in ((12,27,44),(26,42,46),(41,56,44)): c.rr(x0,y,x1,y+15,4,RUSTD)
    c.blob([(11,24),(15,9),(34,5),(53,9),(58,24),(56,40),(48,50),(34,53),(20,50),(12,40)],RUST)
    def sh(t):
        t.blob([(40,6),(60,10),(62,40),(50,54),(36,54),(46,40),(48,22)],RUSTD)
        t.blob([(18,12),(30,9),(28,20),(18,26)],RUSTL)
        t.rr(6,38,62,45,2,DARK)
        t.line([(34,8),(35,38)],RUSTDD,1.6)
    c.inside(sh)
    c.rr(29,36,39,47,2,GOLD)
    c.outline()
    c.circ(38,22,5,GLOW); c.circ(37.4,21.2,2.4,GLOWL)
    c.save('k_torso',(34,54))
def k_arm(name='k_arm'):
    c=C(34,52)
    c.rr(11,16,23,36,4,(84,62,58))
    c.blob([(7,31),(27,31),(29,43),(23,50),(11,50),(5,43)],RUST)
    c.ell(3,2,31,25,RUST)
    def sh(t):
        t.ell(12,8,36,30,RUSTD); t.blob([(8,6),(16,4),(14,12),(8,14)],RUSTL)
        t.line([(6,36),(28,36)],RUSTDD,1.8); t.blob([(18,34),(30,34),(30,52),(18,52)],RUSTD)
    c.inside(sh)
    c.outline(); c.save(name,(17,12))
def k_leg():
    c=C(34,42)
    c.rr(7,2,22,28,5,RUST)
    c.blob([(6,26),(22,26),(31,33),(31,40),(6,40)],RUSTDD,6)
    def sh(t):
        t.rr(15,2,24,28,3,RUSTD); t.ell(7,10,19,20,RUSTL); t.line([(6,27),(24,27)],RUSTDD,1.6)
    c.inside(sh)
    c.outline(); c.save('k_leg',(14,6))
def k_sword():
    c=C(40,128)
    c.poly([(12,90),(12,26),(20,8),(28,26),(28,90)],STEEL)
    def sh(t):
        t.poly([(20,8),(28,26),(28,90),(20,90)],STEELD)
        t.line([(20,30),(20,84)],(120,136,150),1.4)
    c.inside(sh)
    c.rr(4,88,36,96,3,GOLD); c.inside(lambda t:t.rr(4,93,36,97,1,GOLDD))
    c.rr(15.5,96,24.5,114,2,(92,60,44)); c.circ(20,118,5.5,GOLD)
    c.outline()
    for y in (40,54,68): c.line([(20,y),(20,y+6)],GLOW,2.2)
    c.save('k_sword',(20,106))
def k_cape():
    c=C(70,76)
    c.blob([(60,4),(64,16),(54,32),(48,52),(40,70),(30,58),(22,68),(14,50),(8,56),(10,38),(22,24),(38,12),(50,4)],RED,8)
    def sh(t):
        t.blob([(60,10),(50,34),(44,56),(40,72),(30,58),(40,40),(46,20)],REDD)
        t.line([(40,14),(24,36),(16,50)],REDD,2,True)
    c.inside(sh)
    c.outline(); c.save('k_cape',(58,8))
# ---------------- SKELETON ----------------
def s_skull(name='s_head',eye=EYE,moss=True):
    c=C(86,84)
    c.blob([(10,36),(14,18),(30,8),(52,8),(68,18),(73,36),(70,50),(62,58),(61,70),(32,70),(28,60),(15,54)],BONE)
    def sh(t):
        t.blob([(6,40),(18,48),(30,58),(34,74),(20,70),(6,56)],BONED)
        t.blob([(28,62),(62,62),(62,74),(28,74)],BONED)
        t.ell(34,26,52,48,SOCK); t.ell(56,26,72,48,SOCK)
        t.poly([(60,49),(66,49),(63,55)],SOCK)
        for x in (38,44,50,56): t.line([(x,62),(x,71)],SOCK,1.6)
        t.line([(32,61),(62,61)],BONEDD,1.4)
        t.line([(22,10),(27,18),(23,24),(29,30)],BONEDD,1.5)
        if moss:
            t.blob([(8,22),(16,8),(34,4),(44,10),(34,16),(24,18),(16,30)],MOSS); t.blob([(10,24),(18,16),(22,22),(14,30)],MOSSD)
    c.inside(sh)
    if moss:
        c.line([(26,8),(24,-1)],MOSSD,1.8); c.blob([(24,0),(30,-4),(34,1),(28,4)],MOSS,6)
    c.outline()
    c.circ(44.5,38,3.4,eye); c.circ(65,38,3.2,eye); c.circ(45,37.5,1.4,(240,255,220)); c.circ(65.5,37.5,1.3,(240,255,220))
    c.save(name,(42,72))
def s_jaw():
    c=C(44,24)
    c.blob([(6,4),(36,4),(40,12),(32,20),(12,20),(4,12)],BONE)
    c.inside(lambda t:(t.blob([(4,14),(40,14),(36,24),(8,24)],BONED),[t.line([(x,4),(x,10)],SOCK,1.5) for x in (14,20,26,32)]))
    c.outline(2.2); c.save('s_jaw',(8,6))
def s_torso():
    c=C(64,70)
    c.blob([(12,52),(30,46),(48,50),(52,60),(44,68),(36,60),(30,68),(22,62),(14,68)],(74,92,60),7)  # rag
    c.line([(32,8),(32,56)],BONE,6.5)
    for y,wd in ((12,46),(22,44),(32,36)):
        c.arc(32-wd/2,y-6,32+wd/2,y+14,160,380,BONE,5)
    c.blob([(16,50),(32,46),(48,50),(44,60),(32,57),(20,60)],BONE)
    def sh(t):
        t.poly([(36,0),(64,0),(64,70),(40,70)],BONED)
        for y in (18,28,38,46): t.line([(29,y),(35,y)],BONEDD,1.3)
        t.blob([(8,10),(20,6),(24,14),(14,20)],MOSS)
    c.inside(sh)
    c.outline(); c.save('s_torso',(32,60))
def bone(c,x0,y0,x1,y1,w=5.5,k=4.2):
    c.line([(x0,y0),(x1,y1)],BONE,w); c.circ(x0,y0,k,BONE); c.circ(x1,y1,k,BONE)
def s_arm():
    c=C(26,56)
    bone(c,13,8,13,25); bone(c,13,29,13,44,5,3.8); c.circ(13,48,5.4,BONE)
    c.inside(lambda t:t.poly([(14,0),(26,0),(26,56),(15,56)],BONED))
    c.outline(2.4); c.save('s_arm',(13,8))
def s_leg():
    c=C(32,50)
    bone(c,13,7,13,22); bone(c,13,25,13,39,5,3.8); c.blob([(8,38),(18,37),(28,41),(29,46),(8,46)],BONE,6)
    c.inside(lambda t:t.poly([(14,0),(32,0),(32,43),(15,43)],BONED))
    c.outline(2.4); c.save('s_leg',(13,7))
def s_dagger():
    c=C(28,72)
    c.poly([(8,46),(8,16),(14,3),(20,16),(20,36),(17,40),(20,46)],(176,170,150))
    c.inside(lambda t:(t.poly([(14,3),(20,16),(20,46),(14,46)],(134,126,112)),t.blob([(8,22),(12,20),(13,28),(8,30)],(150,96,60)),t.blob([(14,34),(19,32),(20,40),(15,41)],(150,96,60))))
    c.rr(4,45,24,51,2,(110,84,60)); c.rr(10.5,51,17.5,64,2,(70,52,44)); c.circ(14,66,3.6,(110,84,60))
    c.outline(2.4); c.save('s_dagger',(14,58))
def s_helm():
    c=C(96,60)
    c.blob([(8,20),(2,8),(8,2),(18,12),(24,22)],BONED,6); c.blob([(72,22),(80,12),(92,2),(96,10),(88,22)],BONED,6)
    c.blob([(14,50),(14,30),(26,14),(46,8),(66,14),(78,30),(79,50)],(128,110,96))
    c.inside(lambda t:(t.blob([(10,40),(84,40),(84,56),(10,56)],(98,82,74)),t.blob([(26,18),(42,12),(40,22),(28,28)],(160,142,124)),t.blob([(50,26),(60,24),(62,34),(52,36)],(150,96,60)),[t.circ(x,45,1.8,(170,150,130)) for x in (22,36,50,64)]))
    c.outline(); c.save('s_helm',(46,52))
def s_antlers():
    c=C(130,80); W=(96,66,50)
    for sx in (1,-1):
        X=lambda v:65+sx*v
        c.line([(X(8),74),(X(20),50),(X(40),34),(X(54),10)],W,6.5,True)
        c.line([(X(22),48),(X(30),28),(X(26),14)],W,5,True)
        c.line([(X(40),34),(X(58),34),(X(62),26)],W,4.6,True)
        c.line([(X(14),62),(X(4),46)],W,4.4)
    c.inside(lambda t:t.poly([(70,0),(130,0),(130,80),(76,80)],(74,50,40)))
    c.blob([(40,78),(50,66),(65,62),(80,66),(90,78)],MOSSD); c.inside(lambda t:t.blob([(44,74),(54,66),(66,64),(60,72)],MOSS))
    c.outline(); c.save('s_antlers',(65,76))
def s_cloak():
    c=C(90,110); G=(58,78,62)
    c.blob([(70,6),(82,20),(80,50),(84,80),(72,104),(62,90),(52,106),(42,88),(30,102),(22,80),(10,86),(14,56),(26,30),(44,10),(58,4)],G,8)
    c.inside(lambda t:(t.blob([(66,8),(84,22),(84,108),(60,108),(64,60)],(42,58,50)),t.line([(40,20),(28,60),(26,84)],(42,58,50),2.4,True),t.blob([(30,8),(50,2),(60,8),(46,18),(34,22)],MOSSD)))
    c.outline(); c.save('s_cloak',(62,10))
# ---------------- KHALID ----------------
def p_body():
    c=C(82,54)
    c.blob([(8,28),(14,13),(34,8),(58,10),(72,22),(72,36),(60,45),(30,46),(12,40)],PR)
    def sh(t):
        t.blob([(6,38),(30,35),(58,35),(76,28),(74,50),(8,50)],PDK)
        t.blob([(20,14),(36,10),(54,13),(40,17),(24,20)],PRL)
    c.inside(sh); c.outline(); c.save('p_body',(41,44))
def p_head(name,blink=False,happy=False):
    c=C(70,64)
    c.blob([(12,26),(8,6),(26,12),(28,20)],PR,6); c.blob([(42,16),(56,2),(60,22),(52,24)],PR,6)
    c.blob([(10,36),(12,22),(24,14),(42,14),(56,22),(60,36),(54,48),(38,53),(20,50)],PR)
    def sh(t):
        t.blob([(14,22),(11,10),(22,14)],CREAM,6); t.blob([(46,16),(55,7),(57,20)],CREAM,6)
        t.blob([(36,34),(48,30),(62,36),(60,50),(44,56),(34,48)],CREAM)   # muzzle
        t.blob([(8,38),(20,40),(26,48),(18,54),(6,48)],CREAM)             # back cheek
        t.ell(24,20,31,25,CREAM); t.ell(43,19,50,24,CREAM)                 # brows
        t.blob([(30,36),(36,38),(34,50),(26,46)],PRD)                     # tear stripe
        t.blob([(26,16),(40,15),(34,19)],PRL)
    c.inside(sh)
    c.outline()
    if blink or happy:
        c.arc(24,27,34,36,200,340,PDK,2.2) if happy else c.line([(24,32),(34,32)],PDK,2.2)
        c.arc(42,26,52,35,200,340,PDK,2.2) if happy else c.line([(42,31),(52,31)],PDK,2.2)
    else:
        c.ell(25,26,33,36,PDK); c.ell(43,25,51,35,PDK); c.circ(30.5,29,1.5,(255,255,255)); c.circ(48.5,28,1.5,(255,255,255))
    c.ell(53,36,60,41,PDK); c.arc(46,38,56,47,20,120,PDK,1.8)
    c.save(name,(28,46))
def p_tail():
    c=C(78,40)
    c.blob([(72,14),(54,7),(30,7),(11,11),(4,20),(11,30),(32,33),(56,30),(72,24)],PR)
    def sh(t):
        for x in (14,30,46): t.blob([(x,0),(x+8,0),(x+10,40),(x+2,40)],PRD,4)
        t.blob([(0,10),(10,8),(10,32),(0,30)],PDK); t.blob([(20,10),(60,10),(60,14),(20,14)],PRL)
    c.inside(sh); c.outline(); c.save('p_tail',(70,19))
def p_leg():
    c=C(20,28)
    c.rr(4,2,15,22,5,PDK); c.ell(3,17,18,26,PDK)
    c.inside(lambda t:t.ell(5,4,10,12,(88,56,50)))
    c.outline(2.3); c.save('p_leg',(9.5,5))
k_helmet();k_torso();k_arm();k_leg();k_sword();k_cape()
s_skull();s_skull('s_head_red',(255,120,80));s_jaw();s_torso();s_arm();s_leg();s_dagger();s_helm();s_antlers();s_cloak()
p_body();p_head('p_head');p_head('p_head_blink',blink=True);p_head('p_head_happy',happy=True);p_tail();p_leg()
write_meta('chars')
# --------- mock assembly sheet ---------
sheet=Image.new('RGBA',(1500,560),(70,110,72,255))
def put(name,x,y,rot=0,flip=False,sc=1.0):
    im=Image.open(os.path.join(OUTDIR,name+'.png')); m=META[name]
    px,py=m['px']*m['w'],(1-m['py'])*m['h']
    if sc!=1.0: im=im.resize((int(im.size[0]*sc),int(im.size[1]*sc)),Image.LANCZOS); px*=sc;py*=sc
    big=Image.new('RGBA',(700,700),(0,0,0,0)); big.paste(im,(int(350-px),int(350-py)))
    big=big.rotate(rot,center=(350,350),resample=Image.BICUBIC)
    sheet.alpha_composite(big,(int(x-350),int(y-350)))
def knight(x,y,arm=20,sw=-70,l1=0,l2=0):
    put('k_cape',x-28,y-138,-8); put('k_arm',x+22,y-128,-10); put('k_leg',x+16,y-56,l2); put('k_leg',x-18,y-56,l1)
    put('k_torso',x,y-48); put('k_head',x+2,y-132)
    a=math.radians(arm); hx=x-6+76*math.sin(a); hy=y-128+76*math.cos(a)
    put('k_sword',hx,hy,arm+sw+180); put('k_arm',x-6,y-128,arm)
def skel(x,y,arm=30,sc=1.0,helm=False,boss=False):
    if boss: put('s_cloak',x-20*sc,y-170*sc,0,sc=sc)
    put('s_arm',x+20*sc,y-150*sc,-15,sc=sc); put('s_leg',x+14*sc,y-78*sc,8,sc=sc); put('s_leg',x-14*sc,y-78*sc,-8,sc=sc)
    put('s_torso',x,y-68*sc,sc=sc); put('s_jaw',x-2*sc,y-176*sc,sc=sc); put('s_head',x+4*sc,y-170*sc,sc=sc)
    if helm: put('s_helm',x+4*sc,y-262*sc,sc=sc)
    if boss: put('s_antlers',x+2*sc,y-290*sc,sc=sc)
    a=math.radians(arm); hx=x-14*sc+80*sc*math.sin(a); hy=y-150*sc+80*sc*math.cos(a)
    put('s_dagger',hx,hy,arm+180-80,sc=sc); put('s_arm',x-14*sc,y-150*sc,arm,sc=sc)
def panda(x,y,head='p_head'):
    put('p_tail',x-58,y-62,12); 
    for dx in (-40,-24,30,46): put('p_leg',x+dx,y-36)
    put('p_body',x,y-18); put(head,x+52,y-70)
knight(170,500); knight(430,500,arm=140,sw=-40,l1=-25,l2=25)
panda(680,500); panda(900,500,'p_head_happy')
skel(1100,500); skel(1330,500,arm=120,sc=1.0,helm=True)
sheet.convert('RGB').save(os.path.join(OUTDIR,'..','sheet_chars.png'))
print('done',len(META))
