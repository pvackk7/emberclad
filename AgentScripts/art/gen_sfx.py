import numpy as np, wave, os
SR=44100
out=os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','..','Assets','Resources','Sfx'))
rng=np.random.default_rng(3)
def T(d): return np.arange(int(SR*d))/SR
def save(name,x,peak=0.9):
    x=np.asarray(x,float); m=np.max(np.abs(x)) or 1; x=x/m*peak
    w=wave.open(os.path.join(out,name+'.wav'),'wb'); w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes((x*32767).astype('<i2').tobytes()); w.close()
def filt(x,lo=0,hi=None,soft=0.25):
    X=np.fft.rfft(x); f=np.fft.rfftfreq(len(x),1/SR); g=np.ones_like(f)
    if lo: g*=1/(1+(lo/np.maximum(f,1))**4)
    if hi: g*=1/(1+(f/hi)**4)
    return np.fft.irfft(X*g,len(x))
def env(d,a=0.005,r=None,p=2.0):
    t=T(d); r=r or d; e=np.minimum(t/a,1)*np.clip(1-(t-a)/max(r-a,1e-4),0,1)**p; return e
def noise(d): return rng.standard_normal(int(SR*d))
def sweep(d,f0,f1,shape='sin'):
    t=T(d); f=f0*(f1/f0)**(t/d); ph=2*np.pi*np.cumsum(f)/SR
    if shape=='sin': return np.sin(ph)
    if shape=='saw': return 2*((ph/(2*np.pi))%1)-1
    return np.sign(np.sin(ph))
def whoosh(d,f0,f1,q=0.5):
    n=noise(d); t=T(d); N=len(n); outp=np.zeros(N); seg=8
    for i in range(seg):
        k=(i+0.5)/seg; fc=f0*(f1/f0)**k; band=filt(n,fc*(1-q),fc*(1+q)); w=np.clip(1-np.abs(t/d-k)*seg,0,1); outp+=band*w
    return outp*np.sin(np.pi*np.clip(t/d,0,1))**1.3
def pad(x,n): return np.concatenate([x,np.zeros(max(0,n-len(x)))])
def mixat(buf,x,t0,g=1.0):
    i=int(t0*SR); n=min(len(x),len(buf)-i)
    if n>0: buf[i:i+n]+=x[:n]*g
def clack(f,d=0.05): return (np.sin(2*np.pi*f*T(d))+0.5*np.sin(2*np.pi*f*2.7*T(d)))*env(d,0.001,d,3)
save('swing1',whoosh(0.16,500,2200)); save('swing2',whoosh(0.15,700,2800)); save('swing3',whoosh(0.22,300,1800)); save('swing_heavy',whoosh(0.32,180,1300,0.6))
def hit(d,f0,f1,nz,nhi):
    return sweep(d,f0,f1)*env(d,0.002,d,2.2)+filt(noise(d),200,nhi)*env(d,0.001,d*0.5,3)*nz
save('hit1',hit(0.13,210,70,0.8,3500)); save('hit2',hit(0.12,260,80,0.9,4500)); save('hit3',hit(0.14,180,60,0.7,3000))
x=hit(0.3,150,38,1.0,2500); x=np.tanh(x*2.2); save('hit_heavy',x)
b=np.zeros(int(SR*0.22)); 
for t0,f in ((0,900),(0.03,1300),(0.07,700),(0.11,1100)): mixat(b,clack(f),t0)
save('bone_hit',b,0.7)
b=np.zeros(int(SR*0.6)); mixat(b,hit(0.2,160,50,0.9,3000),0,1.2)
for i in range(11): mixat(b,clack(rng.uniform(500,1700),rng.uniform(0.04,0.08)),rng.uniform(0.02,0.42),rng.uniform(0.4,1))
save('bone_break',b)
save('jump',whoosh(0.14,400,1500)*0.8+pad(sweep(0.1,260,420)*env(0.1,0.01,0.1,2)*0.35,int(SR*0.14))[:int(SR*0.14)])
save('land',hit(0.1,120,50,0.5,1200),0.6)
save('dodge',whoosh(0.3,250,1100,0.6))
x=sweep(0.22,340,130,'sq')*env(0.22,0.002,0.22,1.5); save('hurt',filt(x,100,2500)+filt(noise(0.22),300,3000)*env(0.22,0.001,0.1,2)*0.6,0.8)
d=0.55; x=filt(noise(d),100,2600)*env(d,0.03,d,1.6)*(0.6+0.4*np.sin(2*np.pi*23*T(d)))+sweep(d,160,620)*env(d,0.05,d,2)*0.5; save('cast',x)
d=0.5; x=filt(noise(d),40,900)*env(d,0.002,d,2.2)*1.6+sweep(d,110,34)*env(d,0.002,d,2); save('boom',np.tanh(x*1.8))
def chime(freqs,d=0.5,gap=0.08):
    b=np.zeros(int(SR*(d+gap*len(freqs))))
    for i,f in enumerate(freqs):
        t=T(d); mixat(b,(np.sin(2*np.pi*f*t)+0.3*np.sin(2*np.pi*2*f*t)+0.12*np.sin(2*np.pi*3*f*t))*env(d,0.003,d,2.5),i*gap)
    return b
save('pickup',chime([1318.5,1568,2093],0.35,0.06),0.6); save('mana',chime([880,1318.5],0.4,0.07),0.5)
save('ui_start',chime([440,659.3,880,1318.5],0.9,0.09),0.7)
save('victory',chime([523.3,659.3,784,1046.5,784,1046.5,1318.5],1.4,0.15),0.8)
save('defeat',chime([440,415.3,349.2,261.6],1.3,0.24),0.7)
def squeak(f0,f1,f2,d=0.13):
    t=T(d); f=np.where(t<d/2,f0+(f1-f0)*t/(d/2),f1+(f2-f1)*(t-d/2)/(d/2)); ph=2*np.pi*np.cumsum(f)/SR
    return (np.sin(ph)+0.4*np.sin(2*ph))*env(d,0.01,d,1.2)
b=np.zeros(int(SR*0.36)); mixat(b,squeak(1300,2000,1600),0); mixat(b,squeak(1500,2300,1900),0.17); save('khalid',b,0.5)
b=np.zeros(int(SR*0.3)); mixat(b,squeak(1700,2500,2200,0.1),0); mixat(b,squeak(1900,2700,2500,0.1),0.1);mixat(b,squeak(2100,2900,2700,0.08),0.2); save('khalid_happy',b,0.5)
b=np.zeros(int(SR*0.7)); mixat(b,filt(noise(0.6),40,500)*env(0.6,0.1,0.6,1.5),0,1.4)
for i in range(7): mixat(b,clack(rng.uniform(500,1400)),rng.uniform(0.15,0.6),0.5)
save('spawn',b,0.7)
d=1.1; t=T(d); x=(sweep(d,84,58,'saw')*(0.6+0.4*np.sin(2*np.pi*31*t))+filt(noise(d),80,700)*0.8)*env(d,0.08,d,1.3); save('roar',filt(np.tanh(x*2),50,1800))
save('tele',chime([1760],0.18,0),0.35)
save('step',filt(noise(0.06),150,1400)*env(0.06,0.002,0.06,2.5),0.35)
# ---- ambience loop (24s) ----
d=24; n=int(SR*d); t=np.arange(n)/SR
wind=filt(rng.standard_normal(n),60,650)*(0.55+0.3*np.sin(2*np.pi*t/12)+0.15*np.sin(2*np.pi*t/8+1))
leaves=filt(rng.standard_normal(n),2500,7000)*(0.10+0.08*np.sin(2*np.pi*t/6+2))
amb=wind*0.9+leaves
def bird(base):
    k=rng.integers(2,5); b=np.zeros(int(SR*0.9))
    for i in range(k): mixat(b,squeak(base,base*rng.uniform(1.1,1.5),base*rng.uniform(0.9,1.2),rng.uniform(0.07,0.12)),i*rng.uniform(0.12,0.2))
    return b
for i in range(9): mixat(amb,bird(rng.uniform(2200,3800)),rng.uniform(0.5,d-1.5),rng.uniform(0.15,0.4))
save('ambience',amb,0.5)
# ---- music (16 bars @ 96bpm = 40s) ----
bpm=96; beat=60/bpm; bars=16; d=bars*4*beat; n=int(SR*d); mus=np.zeros(n); drums=np.zeros(n)
def note(m): return 440*2**((m-69)/12)
chords=[(57,[57,60,64,69]),(53,[53,57,60,65]),(48,[48,52,55,64]),(55,[55,59,62,67])]*4
def padtone(f,dur):
    t=T(dur); x=sum(np.sin(2*np.pi*f*h*t+rng.uniform(0,6))/h**1.6 for h in (1,2,3,4))*(1+0.004*np.sin(2*np.pi*5*t))
    a=np.minimum(t/0.8,1)*np.minimum((dur-t)/0.9,1); return x*np.clip(a,0,1)
def pluck(f,dur=0.9):
    t=T(dur); return (np.sin(2*np.pi*f*t)+0.5*np.sin(2*np.pi*2*f*t)*np.exp(-t*6)+0.25*np.sin(2*np.pi*3*f*t)*np.exp(-t*9))*np.exp(-t*4.2)*np.minimum(t/0.004,1)
mel=[76,None,72,None,74,76,None,69, 72,None,69,None,65,67,None,69, 67,None,64,None,67,72,None,71, 71,None,74,None,71,67,None,64]
for bi,(root,ch) in enumerate(chords):
    t0=bi*4*beat
    for m in ch: mixat(mus,padtone(note(m),4*beat+0.8),t0,0.10)
    mixat(mus,padtone(note(root-12),4*beat+0.5),t0,0.22)
    arp=[ch[0],ch[2],ch[3],ch[2],ch[1],ch[2],ch[3],ch[2]]
    for i,m in enumerate(arp): mixat(mus,pluck(note(m+12)),t0+i*beat/2,0.16 if i%2 else 0.22)
    if bi>=4:
        mm=mel[((bi-4)%8)*4:((bi-4)%8)*4+4]
        for i,m in enumerate(mm):
            if m: mixat(mus,pluck(note(m+12),1.6)+0.5*pluck(note(m),1.6),t0+i*beat,0.2)
    for i in range(8):
        tt=t0+i*beat/2
        if i in (0,3,6): mixat(drums,sweep(0.22,120,45)*env(0.22,0.002,0.22,2),tt,0.9)
        if i in (2,6): mixat(drums,filt(noise(0.12),900,5000)*env(0.12,0.001,0.12,2.5),tt,0.35)
        mixat(drums,filt(noise(0.04),5000,12000)*env(0.04,0.001,0.04,2),tt,0.12 if i%2 else 0.2)
    if bi%4==3:
        for i in range(4): mixat(drums,sweep(0.2,150-20*i,60)*env(0.2,0.002,0.2,2),t0+3*beat+i*beat/4,0.7)
tail=int(SR*1.0)   # wrap pad tails for seamless loop
save('music',filt(mus,40,6000),0.55); save('music_drums',drums,0.6)
print('sfx ok', len(os.listdir(out)))
