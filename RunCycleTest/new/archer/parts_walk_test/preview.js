const $=id=>document.getElementById(id),ctx=$('canvas').getContext('2d'),imgs={};
let playing=true,phase=0,last=0,ready=false;
Promise.all(Object.entries(ASSETS).map(([k,v])=>new Promise((resolve,reject)=>{let im=new Image();im.onload=()=>{imgs[k]=im;resolve()};im.onerror=reject;im.src=v}))).then(()=>{ready=true;requestAnimationFrame(tick)});
function rot(p,a){return [Math.cos(a)*p[0]-Math.sin(a)*p[1],Math.sin(a)*p[0]+Math.cos(a)*p[1]]}
function sample(v){
 let f=v*120,i=Math.floor(f)%120,j=(i+1)%120,u=f-Math.floor(f),a=RIG.samples[i],b=RIG.samples[j],nodes={};
 for(let [k,n] of Object.entries(a.nodes)){let m=b.nodes[k];nodes[k]={...n,xy:n.xy.map((x,h)=>x+(m.xy[h]-x)*u),angle:n.angle+(m.angle-n.angle)*u};
 let mode=$('surface').value;
 if(mode==='legacy'&&['torso','pelvis'].includes(k))nodes[k].art=(Math.cos(2*Math.PI*v)>=0?'front':'rear')+'/'+k;
 else if(mode==='side'&&['torso','pelvis'].includes(k))nodes[k].art='side/'+k;
 else if(['front','rear'].includes(mode)&&!['head','cape','quiver','bow'].includes(k))nodes[k].art=mode+'/'+k}
 return {nodes,surface:a.surface}
}
function draw(){
 let p=sample(phase),world={};
 function solve(k){if(world[k])return world[k];let n=p.nodes[k],xy=n.xy,a=n.angle;if(n.parent){let pa=solve(n.parent),r=rot(xy,pa.a);xy=[pa.xy[0]+r[0],pa.xy[1]+r[1]];a+=pa.a}return world[k]={xy,a}}
 for(let k in p.nodes)solve(k);
 ctx.fillStyle='#181f27';ctx.fillRect(0,0,640,640);ctx.strokeStyle='#586f7d';ctx.beginPath();ctx.moveTo(0,566);ctx.lineTo(640,566);ctx.stroke();
 ctx.strokeStyle='#2a3743';for(let x=-100;x<800;x+=40){let xx=((x-phase*160)%720+720)%720-40;ctx.beginPath();ctx.moveTo(xx,568);ctx.lineTo(xx-30,610);ctx.stroke()}
 for(let [k,n] of Object.entries(p.nodes).sort((a,b)=>a[1].z-b[1].z)){if(k==='cape'&&!$('cape').checked)continue;if(['bow','quiver'].includes(k)&&!$('equipment').checked)continue;let w=world[k],m=RIG.parts[n.art];ctx.save();ctx.translate(...w.xy);ctx.rotate(w.a);ctx.drawImage(imgs[n.art],-m.pivot[0],-m.pivot[1],...m.size);ctx.restore()}
 if($('joints').checked)for(let [k,n] of Object.entries(p.nodes)){let w=world[k];ctx.strokeStyle=ctx.fillStyle=k.startsWith('near')?'#ffb441':'#3ccdF5';if(n.parent){ctx.beginPath();ctx.moveTo(...world[n.parent].xy);ctx.lineTo(...w.xy);ctx.stroke()}ctx.beginPath();ctx.arc(...w.xy,3,0,Math.PI*2);ctx.fill()}
 $('phase').value=Math.floor(phase*120);$('status').textContent='샘플 '+Math.floor(phase*120)+' / 120\n몸통: '+p.nodes.torso.art+' / 골반: '+p.nodes.pelvis.art+'\n활 부모: near_hand\n가까운 쪽 금색 허벅지 장식 고정';
 for(let b of $('keys').children)b.classList.toggle('active',Math.abs(phase-Number(b.dataset.phase))<.005);
 window.previewState={phase,nodes:p.nodes,world,imagesLoaded:Object.keys(imgs).length};
}
function tick(t){if(last&&playing)phase=(phase+(t-last)/1000/1.2*Number($('speed').value))%1;last=t;draw();requestAnimationFrame(tick)}
function pause(){playing=false;$('play').textContent='재생'}
$('play').onclick=()=>{playing=!playing;$('play').textContent=playing?'일시 정지':'재생'};
$('reset').onclick=()=>{phase=0;pause()};
$('phase').oninput=()=>{phase=Number($('phase').value)/120;pause()};
['1 · 접지','2 · 통과','3 · 반대 접지','4 · 반대 통과'].forEach((label,i)=>{let b=document.createElement('button');b.textContent=label;b.dataset.phase=i/4;b.onclick=()=>{phase=i/4;pause()};$('keys').append(b)});

