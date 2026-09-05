from pathlib import Path
from PIL import Image, ImageDraw
import numpy as np, math, json, base64
from collections import deque
P=Path(__file__).resolve().parent
names=['head','torso','pelvis','near_upper_arm','near_forearm','near_hand','far_upper_arm','far_forearm','far_hand','near_thigh','near_shin','near_foot','far_thigh','far_shin','far_foot','bow','cape','quiver']
front=[(25,75,290,370),(320,110,510,385),(540,150,765,360),(820,130,910,385),(975,130,1060,310),(1120,265,1200,340),(100,445,185,730),(260,470,345,665),(430,565,510,675),(625,450,730,720),(825,450,905,720),(1020,540,1190,730),(75,805,165,1100),(250,825,325,1100),(390,905,560,1110),(555,760,735,1165),(750,845,1020,1140),(1080,855,1195,1110)]
rear=[(60,30,300,295),(355,60,555,325),(600,85,845,325),(910,50,1015,310),(1100,65,1210,310),(1300,150,1410,300),(110,400,195,545),(325,365,435,585),(575,420,690,570),(845,365,945,600),(1075,360,1175,600),(1290,395,1465,595),(85,670,205,895),(295,665,400,900),(490,695,660,900),(710,600,905,980),(925,645,1210,960),(1260,650,1445,950)]
# Width/height are rig-space dimensions; source variants normalized to the same joint frame.
sizes=[(130,140),(94,132),(100,82),(34,80),(34,70),(30,35),(31,80),(31,70),(28,35),(43,103),(32,105),(66,66),(40,103),(30,105),(64,66),(99,255),(155,190),(48,125)]
pivots=[(.52,.90),(.50,.95),(.50,.35),(.50,.12),(.50,.12),(.5,.20),(.50,.12),(.50,.12),(.5,.20),(.50,.12),(.50,.12),(.30,.15),(.50,.12),(.50,.12),(.30,.15),(.57,.65),(.78,.13),(.5,.4)]
parts={}
for variant,boxes,filename in [('front',front,'atlas_source.png'),('rear',rear,'atlas_rear_source.png')]:
    src=Image.open(P/filename).convert('RGBA')
    atlas=Image.new('RGBA',src.size)
    for name,box,size,pv in zip(names,boxes,sizes,pivots):
        im=src.crop(box); ar=np.array(im); rgb=ar[:,:,:3].astype(int)
        eligible=(rgb.max(2)-rgb.min(2)<19)&(rgb.min(2)>222)
        h,w=eligible.shape; seen=np.zeros((h,w),bool); q=deque()
        for y in range(h):
            for x in (0,w-1):
                if eligible[y,x]: q.append((y,x));seen[y,x]=True
        for x in range(w):
            for y in (0,h-1):
                if eligible[y,x]: q.append((y,x));seen[y,x]=True
        while q:
            y,x=q.popleft()
            for yy,xx in ((y-1,x),(y+1,x),(y,x-1),(y,x+1)):
                if 0<=yy<h and 0<=xx<w and eligible[yy,xx] and not seen[yy,xx]:
                    seen[yy,xx]=True;q.append((yy,xx))
        if name=='bow': seen|=eligible
        ar[seen,3]=0
        im=Image.fromarray(ar); tight=im.getbbox()
        if not tight: raise RuntimeError(name)
        atlas.alpha_composite(im,(box[0],box[1]))
        im=im.crop(tight); folder=P/'parts'/variant;folder.mkdir(parents=True,exist_ok=True)
        im.save(folder/(name+'.png'))
        parts[variant+'/'+name]={'file':'parts/'+variant+'/'+name+'.png','source_rect':list(box),'trim_rect':list(tight),'source_size':list(im.size),'size':size,'pivot':[pv[0]*size[0],pv[1]*size[1]],'pivot_normalized':pv}
    atlas.save(P/('atlas_'+variant+'_rgba.png'))
# Intermediate profile: only torso and pelvis; other IDs retain their original art.
side_source=Image.open(P/'atlas_side_source.png').convert('RGBA')
side_atlas=Image.new('RGBA',side_source.size)
for name,box,size,pv in [
    ('torso',(270,70,665,825),(82,132),(.50,.95)),
    ('pelvis',(835,505,1285,945),(92,82),(.50,.35))]:
    im=side_source.crop(box);ar=np.array(im);rgb=ar[:,:,:3].astype(int)
    eligible=(rgb.max(2)-rgb.min(2)<19)&(rgb.min(2)>222)
    h,w=eligible.shape;seen=np.zeros((h,w),bool);q=deque()
    seeds=[(y,x) for y in range(h) for x in (0,w-1)]+[(y,x) for x in range(w) for y in (0,h-1)]
    # Explicit empty shoulder socket, seen in the generated atlas.
    if name=='torso': seeds.append((240,110))
    for y,x in seeds:
        if eligible[y,x] and not seen[y,x]: seen[y,x]=True;q.append((y,x))
    while q:
        y,x=q.popleft()
        for yy,xx in ((y-1,x),(y+1,x),(y,x-1),(y,x+1)):
            if 0<=yy<h and 0<=xx<w and eligible[yy,xx] and not seen[yy,xx]:
                seen[yy,xx]=True;q.append((yy,xx))
    ar[seen,3]=0;im=Image.fromarray(ar);tight=im.getbbox()
    side_atlas.alpha_composite(im,(box[0],box[1]));im=im.crop(tight)
    folder=P/'parts'/'side';folder.mkdir(parents=True,exist_ok=True);im.save(folder/(name+'.png'))
    parts['side/'+name]={'file':'parts/side/'+name+'.png','source_rect':list(box),'trim_rect':list(tight),'source_size':list(im.size),'size':size,'pivot':[pv[0]*size[0],pv[1]*size[1]],'pivot_normalized':pv}
side_atlas.save(P/'atlas_side_rgba.png')
N=120
def surface_at(phase,offset=0):
    turn=math.cos(2*math.pi*(phase+offset))
    return 'side' if abs(turn)<=0.50 else ('front' if turn>0 else 'rear')

def rot(p,a):
    c,s=math.cos(a),math.sin(a);return [c*p[0]-s*p[1],s*p[0]+c*p[1]]
def add(a,b): return [a[0]+b[0],a[1]+b[1]]
def ik(dx,dy,l1,l2):
    d=math.hypot(dx,dy); mid=math.atan2(dy,dx)
    off=math.acos(max(-1,min(1,(l1*l1+d*d-l2*l2)/(2*l1*d))))
    a=mid-off-math.pi/2
    k=rot([0,l1],a)
    b=math.atan2(dy-k[1],dx-k[0])-math.pi/2
    return a,b-a
def pose(t):
    phase=t%1; c=math.cos(2*math.pi*phase); bob=3*(1-math.cos(4*math.pi*phase))
    # Explicit alternate surfaces at opposite contact poses. Identity never swaps.
    variant='front' if c>=0 else 'rear'
    nodes={}
    def node(name,parent,xy,a=0,art=None,z=0):
        nodes[name]={'parent':parent,'xy':xy,'angle':a,'art':art or variant+'/'+name,'z':z}
    node('pelvis',None,[310,345+bob],0,surface_at(phase)+'/pelvis',40)
    node('torso','pelvis',[0,-7],-.025*c,surface_at(phase,.025)+'/torso',45)
    node('head','torso',[5,-103],.025*c,'front/head',60)
    node('cape','torso',[-25,-102],.035*math.sin(2*math.pi*phase),'front/cape',5)
    node('quiver','torso',[-44,-32],.15,'front/quiver',8)
    for side,shift,z in [('far',.5,15),('near',0,70)]:
        p=(phase+shift)%1
        if p<=.5: x=40-160*p;y=510
        else:
            u=(p-.5)*2;x=-40+80*(3*u*u-2*u*u*u);y=510-34*math.sin(math.pi*u)**2
        hip=[-9,9] if side=='far' else [9,10]
        a,b=ik(x, y-(345+bob+hip[1]),86,87)
        node(side+'_thigh','pelvis',hip,a,z=z)
        node(side+'_shin',side+'_thigh',[0,86],b,z=z+1)
        node(side+'_foot',side+'_shin',[0,87],-a-b,z=z+2)
        swing=.48*c*(1 if side=='near' else -1)
        shoulder=([12,-94] if side=='far' else [-17,-90]) if nodes['torso']['art']=='side/torso' else [(-19 if side=='far' else 14),-104]
        node(side+'_upper_arm','torso',shoulder,swing,z=z+5)
        node(side+'_forearm',side+'_upper_arm',[0,61],-.30,z=z+6)
        node(side+'_hand',side+'_forearm',[0,52],0,z=z+8)
    node('bow','near_hand',[2,13],-.1,'front/bow',77)
    return {'phase':phase,'surface':nodes['torso']['art'].split('/')[0]+' / '+nodes['pelvis']['art'].split('/')[0],'nodes':nodes}
def world(p):
    result={}
    def resolve(name):
        if name in result:return result[name]
        n=p['nodes'][name]
        if n['parent']:
            par=resolve(n['parent']);v={'xy':add(par['xy'],rot(n['xy'],par['angle'])),'angle':par['angle']+n['angle']}
        else:v={'xy':n['xy'],'angle':n['angle']}
        result[name]=v;return v
    for n in p['nodes']:resolve(n)
    return result
samples=[pose(i/N) for i in range(N)]
rig={'parts':parts,'samples':samples,'cycle_seconds':1.2,'ground_y':566,'canvas':[640,640],'key_samples':[0,30,60,90],'surface_schedule':{'threshold':0.5,'torso_phase_offset':0.025,'pelvis_phase_offset':0,'intermediate_parts':['torso','pelvis']},'note':'Angles radians, positive clockwise; local parent-child transforms. Torso/pelvis use front-side-rear-side; limbs retain two-view art; head fixed right. No identity swaps.'}
(P/'rig.json').write_text(json.dumps(rig,indent=2),encoding='utf-8')
sprites={k:Image.open(P/v['file']).convert('RGBA').resize(tuple(v['size']),Image.Resampling.LANCZOS) for k,v in parts.items()}
def render(p,joints=False,equipment=True):
    out=Image.new('RGBA',(640,640),(24,31,39,255));d=ImageDraw.Draw(out)
    d.line((0,566,640,566),fill=(90,111,125),width=2)
    # Treadmill grid travels at stance velocity; evaluate foot against ground, not screen.
    for x in range(-100,800,40):
        xx=(x-p['phase']*160)%720-40;d.line((xx,568,xx-30,610),fill=(42,55,67),width=1)
    wld=world(p)
    for name,n in sorted(p['nodes'].items(),key=lambda kv:kv[1]['z']):
        if not equipment and name in ('bow','cape','quiver'): continue
        tr=wld[name];meta=parts[n['art']];pv=meta['pivot'];a=tr['angle'];c,s=math.cos(a),math.sin(a);x,y=tr['xy']
        coeff=(c,s,pv[0]-c*x-s*y,-s,c,pv[1]+s*x-c*y)
        layer=sprites[n['art']].transform(out.size,Image.Transform.AFFINE,coeff,Image.Resampling.BICUBIC)
        out.alpha_composite(layer)
    d=ImageDraw.Draw(out)
    if joints:
        for name,n in p['nodes'].items():
            x,y=wld[name]['xy'];color=(255,180,65) if name.startswith('near') else (60,205,245)
            if n['parent']:d.line((*wld[n['parent']]['xy'],x,y),fill=color,width=2)
            d.ellipse((x-3,y-3,x+3,y+3),fill=color)
    d.text((20,18),f"PARTS WALK / {p['phase']:.2f} / {p['surface'].upper()}",fill='white')
    return out
keys=[]
for i,s in enumerate([0,30,60,90]):
    im=render(samples[s]);im.save(P/f'walk_key_{i+1:02}.png');keys.append(im)
sheet=Image.new('RGB',(1280,1280))
for i,im in enumerate(keys):sheet.paste(im,((i%2)*640,(i//2)*640))
sheet.save(P/'comparison_sheet.png')
render(samples[0],True).save(P/'joints_key_01.png')
# Body-only strips show both side passages without cape/weapon occlusion.
strip=Image.new('RGB',(640*4,640))
for i,t in enumerate([0,.25,.5,.75]): strip.paste(render(pose(t),equipment=False),(640*i,0))
strip.resize((1280,320),Image.Resampling.LANCZOS).save(P/'body_surface_strip.png')
frames=[render(pose(i/48)).convert('RGB') for i in range(48)]
frames[0].save(P/'walk_loop.gif',save_all=True,append_images=frames[1:],duration=25,loop=0)
# Geometry verification over analytic cycle; artwork quality evaluated separately.
errors=[];support=[];bow=[]
for i in range(N):
    p=pose(i/N);w=world(p)
    for side,shift in [('near',0),('far',.5)]:
        for parent,child,length in [(side+'_thigh',side+'_shin',86),(side+'_shin',side+'_foot',87),(side+'_upper_arm',side+'_forearm',61),(side+'_forearm',side+'_hand',52)]:
            errors.append(abs(math.dist(w[parent]['xy'],w[child]['xy'])-length))
        ph=(i/N+shift)%1
        if ph<.5:
            support.append(abs(w[side+'_foot']['xy'][1]-510))
    expected=add(w['near_hand']['xy'],rot([2,13],w['near_hand']['angle']))
    bow.append(math.dist(expected,w['bow']['xy']))
metrics={'samples':N,'max_joint_length_error_px':max(errors),'max_stance_ankle_height_error_px':max(support),'max_bow_grip_attachment_error_px':max(bow),'loop_endpoint_equal':pose(0)==pose(1),'near_foot_contact_x':[world(samples[k])['near_foot']['xy'][0] for k in [0,60]],'near_hand_contact_x':[world(samples[k])['near_hand']['xy'][0] for k in [0,60]],'torso_surfaces_at_keys':[samples[k]['nodes']['torso']['art'] for k in [0,30,60,90]],'pelvis_surfaces_at_keys':[samples[k]['nodes']['pelvis']['art'] for k in [0,30,60,90]],'warning':'Attachment metrics validate coordinate anchors, not painted grip or pixel-level joint coverage. Three torso/pelvis views still use discrete swaps; limb silhouettes retain the baseline issues.'}
(P/'validation.json').write_text(json.dumps(metrics,indent=2),encoding='utf-8')
assets={k:'data:image/png;base64,'+base64.b64encode((P/v['file']).read_bytes()).decode() for k,v in parts.items()}
(P/'preview_data.js').write_text('window.RIG='+json.dumps(rig)+';\nwindow.ASSETS='+json.dumps(assets)+';',encoding='utf-8')
print(json.dumps(metrics,indent=2))
