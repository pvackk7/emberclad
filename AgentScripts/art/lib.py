from PIL import Image, ImageDraw, ImageFilter, ImageChops
import numpy as np, math, random, json, os
OL=(40,27,32,255)
OUTDIR=os.path.join(os.path.dirname(os.path.abspath(__file__)),'out')
META={}
def smooth(pts,n=10,closed=True):
    P=np.array(pts,float);N=len(P);out=[]
    rng=range(N) if closed else range(N-1)
    for i in rng:
        if closed: p0,p1,p2,p3=P[(i-1)%N],P[i],P[(i+1)%N],P[(i+2)%N]
        else: p0,p1,p2,p3=P[max(i-1,0)],P[i],P[i+1],P[min(i+2,N-1)]
        for t in np.linspace(0,1,n,endpoint=False):
            out.append(0.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t**3))
    if not closed: out.append(P[-1])
    return [(float(x),float(y)) for x,y in out]
class C:
    def __init__(s,w,h,out=2,ss=2):
        s.w,s.h,s.out=w,h,out; s.S=out*ss
        s.img=Image.new('RGBA',(int(w*s.S),int(h*s.S)),(0,0,0,0)); s.d=ImageDraw.Draw(s.img)
    def _p(s,pts): return [(x*s.S,y*s.S) for x,y in pts]
    def poly(s,pts,col): s.d.polygon(s._p(pts),fill=col); return s
    def blob(s,pts,col,n=10): s.d.polygon(s._p(smooth(pts,n)),fill=col); return s
    def ell(s,x0,y0,x1,y1,col): s.d.ellipse([x0*s.S,y0*s.S,x1*s.S,y1*s.S],fill=col); return s
    def circ(s,x,y,r,col): return s.ell(x-r,y-r,x+r,y+r,col)
    def rr(s,x0,y0,x1,y1,r,col): s.d.rounded_rectangle([x0*s.S,y0*s.S,x1*s.S,y1*s.S],radius=r*s.S,fill=col); return s
    def line(s,pts,col,w=2,sm=False):
        if sm: pts=smooth(pts,10,False)
        P=s._p(pts); W=max(1,int(round(w*s.S)))
        s.d.line(P,fill=col,width=W,joint='curve')
        r=W/2
        for x,y in (P[0],P[-1]): s.d.ellipse([x-r,y-r,x+r,y+r],fill=col)
        return s
    def arc(s,x0,y0,x1,y1,a0,a1,col,w=2): s.d.arc([x0*s.S,y0*s.S,x1*s.S,y1*s.S],a0,a1,fill=col,width=max(1,int(w*s.S))); return s
    def inside(s,fn):
        t=C(s.w,s.h,s.out,s.S//s.out if s.S%s.out==0 else 2); t.S=s.S
        t.img=Image.new('RGBA',s.img.size,(0,0,0,0)); t.d=ImageDraw.Draw(t.img)
        fn(t)
        a=ImageChops.multiply(t.img.getchannel('A'),s.img.getchannel('A')); t.img.putalpha(a)
        s.img=Image.alpha_composite(s.img,t.img); s.d=ImageDraw.Draw(s.img); return s
    def under(s,fn):
        t=C(s.w,s.h,s.out); t.S=s.S; t.img=Image.new('RGBA',s.img.size,(0,0,0,0)); t.d=ImageDraw.Draw(t.img)
        fn(t); s.img=Image.alpha_composite(t.img,s.img); s.d=ImageDraw.Draw(s.img); return s
    def outline(s,w=2.6,col=OL):
        a=s.img.getchannel('A'); r=w*s.S
        b=a.filter(ImageFilter.GaussianBlur(r*0.62)).point(lambda v:255 if v>28 else 0).filter(ImageFilter.GaussianBlur(s.S*0.35))
        o=Image.new('RGBA',s.img.size,col); o.putalpha(b)
        s.img=Image.alpha_composite(o,s.img); s.d=ImageDraw.Draw(s.img); return s
    def blur(s,r): s.img=s.img.filter(ImageFilter.GaussianBlur(r*s.S)); s.d=ImageDraw.Draw(s.img); return s
    def fade(s,k):
        a=s.img.getchannel('A').point(lambda v:int(v*k)); s.img.putalpha(a); s.d=ImageDraw.Draw(s.img); return s
    def final(s): return s.img.resize((int(s.w*s.out),int(s.h*s.out)),Image.LANCZOS)
    def save(s,name,pivot=None,img=None):
        im=img if img is not None else s.final()
        im.save(os.path.join(OUTDIR,name+'.png'))
        px,py=pivot if pivot else (s.w/2,s.h/2)
        META[name]=dict(w=im.size[0],h=im.size[1],px=px/s.w,py=1-py/s.h,ppu=100*s.out)
        return im
def save_raw(name,im,ppu=100,pivot=(0.5,0.5)):
    im.save(os.path.join(OUTDIR,name+'.png')); META[name]=dict(w=im.size[0],h=im.size[1],px=pivot[0],py=pivot[1],ppu=ppu)
def write_meta(tag):
    json.dump(META,open(os.path.join(OUTDIR,'_meta_%s.json'%tag),'w'))
def mix(a,b,t): return tuple(int(a[i]+(b[i]-a[i])*t) for i in range(3))
