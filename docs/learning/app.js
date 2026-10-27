/* =================== STATE =================== */
const KEY='phonestore-toan-learn-v1';
let S={fc:{},qz:{},lab:{},ck:{}};
try{const r=localStorage.getItem(KEY);if(r)S=Object.assign(S,JSON.parse(r));}catch(e){}
function save(){try{localStorage.setItem(KEY,JSON.stringify(S));}catch(e){}}
const CK_TOTAL=CHECKS.reduce((a,c)=>a+c.li.length,0);
function calc(){
  const fcT=Object.keys(S.fc).length, qzT=Object.keys(S.qz).length, labT=Object.keys(S.lab).length, ckT=Object.keys(S.ck).length;
  document.getElementById('s-card').textContent=fcT+'/'+CARDS.length;
  document.getElementById('s-quiz').textContent=qzT+'/'+QUIZ.length;
  document.getElementById('s-lab').textContent=labT+'/'+LAB.length;
  document.getElementById('s-ck').textContent=ckT+'/'+CK_TOTAL;
  const tot=CARDS.length+QUIZ.length+LAB.length+CK_TOTAL;
  const done=fcT+qzT+labT+ckT;
  const p=tot?done/tot:0;
  document.getElementById('ring').setAttribute('stroke-dashoffset',106.8*(1-p));
}

/* =================== DASHBOARD =================== */
document.getElementById('taskcards').innerHTML=TASKS.map(t=>`
<div class="tcard" data-k="${t.k}">
  <div class="k">${t.k}</div>
  <h4>${t.n}</h4>
  <p>${t.d}</p>
  <div class="meta">
    <span class="pill acc">${t.st}</span>
    ${t.fr!=='—'?`<span class="pill">${t.fr}</span>`:''}
    <span class="pill ok">✅ Đã triển khai</span>
  </div>
</div>`).join('');
document.getElementById('timeline').innerHTML=TIMELINE.map(t=>`
<div class="tl-item">
  <div class="d">${t.d}</div>
  <h4>${t.h}</h4>
  <ul>${t.li.map(l=>`<li>${l}</li>`).join('')}</ul>
</div>`).join('');

/* =================== FLOWS =================== */
function renderFlow(k){
  const f=FLOWS[k];
  document.getElementById('flowbody').innerHTML=`
  <h3>${f.title}</h3>
  <div class="grid g2">${f.steps.map((s,i)=>`
    <div class="card">
      <div style="font-family:var(--mono);font-size:11px;color:var(--acc);font-weight:700;margin-bottom:6px">BƯỚC ${i+1}</div>
      <b style="font-size:14px;display:block;margin-bottom:6px">${s.n}</b>
      <p style="font-size:13px;color:var(--fg2);line-height:1.65">${s.d}</p>
    </div>`).join('')}</div>`;
}
document.querySelectorAll('#flowtabs button').forEach(b=>b.onclick=()=>{
  document.querySelectorAll('#flowtabs button').forEach(x=>x.classList.remove('on'));
  b.classList.add('on'); renderFlow(b.dataset.f);
});
renderFlow('cart');

/* =================== FLASHCARD =================== */
let fcList=[],fcIdx=0;
function fcPool(){
  const f=document.getElementById('fcFilter').value, st=document.getElementById('fcState').value;
  let l=CARDS.map((c,i)=>({c,i}));
  if(f!=='all')l=l.filter(x=>x.c.t===f);
  if(st!=='all')l=l.filter(x=>{
    const s=S.fc['c'+x.i];
    if(st==='new')return !s; if(st==='known')return s==='k'; return s==='w';
  });
  return l;
}
function renderFC(){
  fcList=fcPool();
  document.getElementById('fcPool').textContent=fcList.length+' thẻ trong bộ lọc';
  document.getElementById('fcCount').textContent=fcList.length?`Thẻ ${fcIdx+1} / ${fcList.length}`:'Không có thẻ nào khớp bộ lọc';
  const F=document.getElementById('fcFront'),B=document.getElementById('fcBack');
  if(!fcList.length){F.innerHTML='<div class="fc-q">🎉 Không còn thẻ nào<br><small style="color:var(--fg2);font-weight:400">Thử đổi bộ lọc hoặc bấm "Reset thẻ này"</small></div>';B.innerHTML='';return;}
  const c=fcList[fcIdx].c, st=S.fc['c'+fcList[fcIdx].i];
  const badge=st==='k'?'<span style="color:var(--ok)">✓ đã biết</span>':st==='w'?'<span style="color:var(--err)">✗ chưa biết</span>':'';
  F.innerHTML='<div class="fc-tag">TASK '+c.t.toUpperCase()+'</div><div class="fc-q">'+c.q+'</div><div class="fc-hint">Bấm để lật · '+badge+'</div>';
  B.innerHTML='<div class="fc-tag">ĐÁP ÁN</div><div class="fc-a">'+c.a+'</div>'+(c.e?'<div class="fc-exp"><b>Nguồn:</b> '+c.e+'</div>':'')+'<div class="fc-hint">'+badge+'</div>';
}
document.getElementById('fcCard').onclick=()=>document.getElementById('fcCard').classList.toggle('flip');
document.getElementById('fcPrev').onclick=()=>{if(fcList.length){fcIdx=(fcIdx-1+fcList.length)%fcList.length;document.getElementById('fcCard').classList.remove('flip');renderFC();}};
document.getElementById('fcNext').onclick=()=>{if(fcList.length){fcIdx=(fcIdx+1)%fcList.length;document.getElementById('fcCard').classList.remove('flip');renderFC();}};
function fcMark(s){if(!fcList.length)return;S.fc['c'+fcList[fcIdx].i]=s;save();calc();renderFC();}
document.getElementById('fcKnown').onclick=()=>fcMark('k');
document.getElementById('fcWeak').onclick=()=>fcMark('w');
document.getElementById('fcFilter').onchange=()=>{fcIdx=0;renderFC();};
document.getElementById('fcState').onchange=()=>{fcIdx=0;renderFC();};
document.getElementById('fcShuffle').onclick=()=>{
  const f=document.getElementById('fcFilter').value;
  let l=CARDS.map((c,i)=>({c,i}));if(f!=='all')l=l.filter(x=>x.c.t===f);
  for(let i=l.length-1;i>0;i--){const j=Math.floor(Math.random()*(i+1));const tmp=l[i];l[i]=l[j];l[j]=tmp;}
  fcList=l;fcIdx=0;document.getElementById('fcCard').classList.remove('flip');renderFC();
};
document.getElementById('fcReset').onclick=()=>{if(fcList.length){delete S.fc['c'+fcList[fcIdx].i];save();calc();renderFC();}};

/* =================== QUIZ =================== */
function renderQuiz(){
  const f=document.getElementById('qzFilter').value;
  const list=QUIZ.map((q,i)=>({q,i})).filter(x=>f==='all'||x.q.t===f);
  document.getElementById('quizBody').innerHTML=list.map(function(x){
    const q=x.q,i=x.i;
    return '<div class="qz" data-i="'+i+'">'+
    '<div class="qz-q"><span style="color:var(--acc);font-family:var(--mono);font-size:12px">TASK '+q.t.toUpperCase()+'</span><br>'+q.q+'</div>'+
    q.o.map(function(o,j){return '<div class="opt" data-j="'+j+'"><div class="m">'+'ABCD'[j]+'</div><div>'+o+'</div></div>';}).join('')+
    '<div class="exp"><b>Giải thích:</b> '+q.e+'</div>'+
    '<div style="margin-top:11px"><button class="btn p" data-check="'+i+'">Đáp lời</button></div>'+
    '</div>';
  }).join('');
  document.querySelectorAll('#quizBody .opt').forEach(o=>o.onclick=function(){
    if(o.closest('.qz').dataset.done)return;
    o.closest('.qz').querySelectorAll('.opt').forEach(function(x){x.classList.remove('sel');});
    o.classList.add('sel');
  });
  document.querySelectorAll('#quizBody [data-check]').forEach(b=>b.onclick=function(){
    const qz=b.closest('.qz'),i=+b.dataset.check;
    const sel=qz.querySelector('.opt.sel');
    if(!sel){alert('Hãy chọn một đáp án trước nhé!');return;}
    if(qz.dataset.done)return;
    qz.dataset.done='1';
    const ok=+sel.dataset.j===QUIZ[i].c;
    qz.querySelectorAll('.opt').forEach(function(x){
      x.classList.remove('sel');
      if(+x.dataset.j===QUIZ[i].c)x.classList.add('ok');
      else if(x===sel)x.classList.add('no');
    });
    qz.querySelector('.exp').classList.add('show');
    S.qz['q'+i]=ok?1:0;save();calc();updQzScore();
  });
  updQzScore();
}
function updQzScore(){
  const ans=Object.values(S.qz);
  const ok=ans.filter(x=>x===1).length;
  document.getElementById('qzScore').textContent=ans.length?('Điểm: '+ok+'/'+ans.length+' ('+Math.round(ok/ans.length*100)+'%)'):'Chưa làm câu nào';
}
document.getElementById('qzFilter').onchange=renderQuiz;
document.getElementById('qzCheck').onclick=()=>{
  document.querySelectorAll('#quizBody .qz').forEach(q=>{
    if(!q.dataset.done){
      const b=q.querySelector('[data-check]');
      if(b && !q.querySelector('.opt.sel')){
        const first=q.querySelector('.opt');
        if(first)first.classList.add('sel');
      }
      if(b)b.click();
    }
  });
};
document.getElementById('qzReset').onclick=()=>{S.qz={};save();calc();renderQuiz();};

/* =================== CODE LAB =================== */
function renderLab(){
  document.getElementById('labBody').innerHTML=LAB.map(function(l){
    const code=l.code.replace(/\[A(\d+)\]/g,function(m,n){
      const opts=l.blanks[+n-1][1];
      const sel='<select class="blank" data-b="'+n+'" data-ans="'+l.blanks[+n-1][0]+'"><option value="">— chọn —</option>'+opts.map(o=>'<option>'+o+'</option>').join('')+'</select>';
      return sel;
    });
    return '<div class="lab" data-l="'+l.id+'">'+
      '<div class="lab-h"><b>'+l.t+'</b><span class="pill">'+l.f+'</span><span class="pill acc" style="margin-left:auto">'+l.blanks.length+' chỗ trống</span></div>'+
      '<div class="lab-b"><pre>'+code+'</pre><div class="exp" style="margin-top:12px"><b>Giải thích:</b> '+l.why+'</div></div>'+
      '<div class="lab-f"><button class="btn p" data-lc="'+l.id+'">✓ Kiểm tra</button><span class="res"></span></div>'+
    '</div>';
  }).join('');
  document.querySelectorAll('#labBody [data-lc]').forEach(b=>b.onclick=function(){
    const lab=b.closest('.lab');let ok=0;
    const sels=lab.querySelectorAll('select.blank');
    sels.forEach(function(s){
      s.disabled=true;
      if(s.value===s.dataset.ans){s.classList.add('good');ok++;}
      else s.classList.add('bad');
    });
    const res=lab.querySelector('.res');
    const perfect=ok===sels.length;
    res.textContent=perfect?'✅ Chính xác!':('❌ '+ok+'/'+sels.length+' đúng');
    res.style.color=perfect?'var(--ok)':'var(--err)';
    lab.querySelector('.exp').classList.add('show');
    S.lab['l'+lab.dataset.l]=ok;save();calc();updLabScore();
  });
}
function updLabScore(){
  const v=Object.keys(S.lab).length;
  document.getElementById('labScore').textContent=v?(v+'/'+LAB.length+' bài đã làm'):'Chưa làm bài nào';
}
document.getElementById('labCheck').onclick=()=>{
  document.querySelectorAll('#labBody .lab').forEach(l=>{
    if(!l.dataset.done){
      l.dataset.done='1';
      l.querySelectorAll('select.blank').forEach(s=>{if(!s.value)s.options[1].selected=true;});
      l.querySelector('[data-lc]').click();
    }
  });
};
document.getElementById('labReset').onclick=()=>{S.lab={};save();calc();renderLab();};

/* =================== CHEATSHEET =================== */
function renderCheat(k){document.getElementById('cheatBody').innerHTML=CHEAT[k];}
document.querySelectorAll('#cheattabs button').forEach(b=>b.onclick=()=>{
  document.querySelectorAll('#cheattabs button').forEach(x=>x.classList.remove('on'));
  b.classList.add('on');renderCheat(b.dataset.c);
});
renderCheat('api');

/* =================== CHECKLIST =================== */
function renderCheck(){
  document.getElementById('checkBody').innerHTML=CHECKS.map(function(c,ci){
    const total=c.li.length;
    const done=c.li.filter(function(_,i){return S.ck['c'+ci+'_'+i];}).length;
    return '<div class="ck" data-c="'+ci+'">'+
      '<h4>'+c.h+' <span class="pill '+(done===total?'ok':'')+'" style="margin-left:auto">'+done+'/'+total+'</span></h4>'+
      c.li.map(function(l,i){
        return '<label><input type="checkbox" data-k="c'+ci+'_'+i+'" '+(S.ck['c'+ci+'_'+i]?'checked':'')+'><span>'+l+'</span></label>';
      }).join('')+
      '<div class="bar"><i style="width:'+(done/total*100)+'%"></i></div>'+
    '</div>';
  }).join('');
  document.querySelectorAll('#checkBody input[type=checkbox]').forEach(cb=>cb.onchange=function(){
    S.ck[cb.dataset.k]=cb.checked?1:0;save();calc();
    const ck=cb.closest('.ck');const boxes=ck.querySelectorAll('input');
    let d=0;boxes.forEach(b=>{if(b.checked)d++;});
    ck.querySelector('.bar i').style.width=(d/boxes.length*100)+'%';
    const pill=ck.querySelector('.pill');
    pill.textContent=d+'/'+boxes.length;
    pill.className='pill '+(d===boxes.length?'ok':'');
  });
}
renderCheck();

/* =================== NAV =================== */
document.querySelectorAll('#nav button').forEach(b=>b.onclick=function(){
  document.querySelectorAll('#nav button').forEach(x=>x.classList.remove('on'));
  b.classList.add('on');
  document.querySelectorAll('main section').forEach(s=>s.classList.remove('on'));
  document.getElementById('s-'+b.dataset.s).classList.add('on');
  window.scrollTo({top:0,behavior:'smooth'});
});
document.addEventListener('keydown',function(e){
  if(/input|select|textarea/i.test(e.target.tagName))return;
  const on=document.querySelector('#nav button.on').dataset.s;
  if(e.key==='ArrowLeft'&&on==='flash'){document.getElementById('fcPrev').click();e.preventDefault();}
  if(e.key==='ArrowRight'&&on==='flash'){document.getElementById('fcNext').click();e.preventDefault();}
  if(e.key===' '&&on==='flash'){document.getElementById('fcCard').click();e.preventDefault();}
  if(e.key==='1'&&on==='flash'){fcMark('k');}
  if(e.key==='2'&&on==='flash'){fcMark('w');}
  const idx=parseInt(e.key);
  if(idx>=1&&idx<=9){const btns=document.querySelectorAll('#nav button');if(btns[idx-1])btns[idx-1].click();}
});

/* =================== THEME =================== */
if(localStorage.getItem('phonestore-theme')==='light')document.documentElement.setAttribute('data-light','');
document.getElementById('theme').onclick=function(){
  const on=document.documentElement.hasAttribute('data-light');
  if(on){document.documentElement.removeAttribute('data-light');localStorage.setItem('phonestore-theme','dark');}
  else{document.documentElement.setAttribute('data-light','');localStorage.setItem('phonestore-theme','light');}
};
document.getElementById('reset').onclick=function(){
  if(!confirm('Xoá toàn bộ tiến độ học tập?'))return;
  S={fc:{},qz:{},lab:{},ck:{}};save();renderFC();renderQuiz();renderLab();renderCheck();calc();
};

/* =================== INIT =================== */
renderFC();renderQuiz();renderLab();calc();
