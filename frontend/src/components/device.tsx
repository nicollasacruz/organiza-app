"use client";

import {useEffect,useRef,useState} from 'react';
import {toDataURL} from 'qrcode';
import {api,resetCsrf} from '@/lib/api';
import {SessionProvider,useSession} from './session';
import {Button,ErrorBox,Icon} from './ui';

type Ticket = {token:string;code:string;expires:string};
type Details = {code:string;expires:string};

export function DeviceScreen(){
 const [token,setToken] = useState<string|null>(null);
 useEffect(()=>{
  const readToken=()=>setToken(new URLSearchParams(window.location.hash.slice(1)).get('token')||'');
  readToken();window.addEventListener('hashchange',readToken);
  return()=>window.removeEventListener('hashchange',readToken);
 },[]);
 if(token===null)return <p className="loading-inline">A preparar a ligação…</p>;
 if(token&&!/^[A-Za-z0-9_-]{43}$/.test(token))return <main className="device-page"><section className="device-card"><h1>QR code inválido</h1><p>Gere um novo QR code na TV e volte a ler com o telemóvel.</p></section></main>;
 if(token)return <SessionProvider><ApproveDevice key={token} token={token}/></SessionProvider>;
 return <ConnectDevice/>;
}

function ConnectDevice(){
 const [generation,setGeneration] = useState(0);
 const request = useRef<{generation:number;promise:Promise<Ticket>}|null>(null);
 const [ticket,setTicket] = useState<Ticket|null>(null);
 const [qr,setQr] = useState('');
 const [error,setError] = useState('');
 const [expired,setExpired] = useState(false);
 const [seconds,setSeconds] = useState(300);
 const [loading,setLoading] = useState(true);

 useEffect(()=>{
  let active = true;
  setLoading(true);setError('');setExpired(false);setTicket(null);setQr('');
  // Reutilizar o pedido quando o Strict Mode volta a montar o efeito.
  if(request.current?.generation!==generation){
   request.current = {generation,promise:api<Ticket>('/auth/device/start','POST')};
  }
  void request.current.promise.then(async result=>{
   const url = window.location.origin+'/device/#token='+result.token;
   const image = await toDataURL(url,{width:400,margin:4,errorCorrectionLevel:'M',color:{dark:'#000000',light:'#ffffff'}});
   if(active){setTicket(result);setQr(image);setLoading(false);}
  }).catch(e=>{if(active){setError((e as Error).message);setLoading(false);}});
  return()=>{active=false;};
 },[generation]);

 useEffect(()=>{
  if(!ticket)return;
  let active = true;
  let timeout:ReturnType<typeof setTimeout>;
  async function poll(){
   if(Date.now()>=Date.parse(ticket!.expires)){if(active)setExpired(true);return;}
   try{
    const result = await api<{status:string}>('/auth/device/poll','POST');
    if(!active)return;
    if(result.status==='approved'){resetCsrf();window.location.assign('/tarefas/');return;}
    if(result.status==='expired'){setExpired(true);return;}
    setError('');
   }catch(e){if(active)setError((e as Error).message);}
   if(active)timeout=setTimeout(()=>void poll(),3000);
  }
  timeout=setTimeout(()=>void poll(),3000);
  function tick(){
   const remaining = Math.max(0,Math.ceil((Date.parse(ticket!.expires)-Date.now())/1000));
   setSeconds(remaining);
   if(remaining===0)setExpired(true);
  }
  tick();
  const timer=setInterval(tick,1000);
  return()=>{active=false;clearTimeout(timeout);clearInterval(timer);};
 },[ticket]);

 return <main className="device-page"><section className="device-card device-connect">
  <a className="brand" href="/ganhos/"><Icon name="home" size={30}/>Organiza</a>
  <div className="device-layout"><div className="device-instructions">
   <span className="eyebrow">O KANBAN NA SUA TV</span><h1>Entre com o telemóvel</h1>
   <ol><li>Abra a câmara do telemóvel e leia o QR code.</li><li>Entre na sua conta, se necessário.</li><li>Confira o código e autorize a ligação.</li></ol>
   <p className="muted">O Kanban abre automaticamente nesta TV após a confirmação.</p>
   <a className="button" href="/tarefas/">Entrar com palavra-passe ou passkey</a>
  </div><div className="device-qr-panel">
   {loading?<p role="status">A gerar QR code…</p>:expired?<div role="status"><h2>O QR code expirou</h2><p>Gere outro para continuar.</p></div>:qr&&ticket?<>
    <img className="device-qr" src={qr} width="400" height="400" alt="QR code para autorizar a ligação desta TV"/>
    <span className="muted">Código de confirmação</span><strong className="device-code">{ticket.code}</strong>
    <p className="muted">Expira em {Math.floor(seconds/60)}:{String(seconds%60).padStart(2,'0')}</p>
    <p role="status">À espera de autorização no telemóvel…</p>
   </>:null}
   <ErrorBox error={error}/>
   {!loading&&(expired||!ticket)&&<Button className="primary" onClick={()=>setGeneration(generation+1)}>Gerar novo QR code</Button>}
  </div></div>
 </section></main>;
}

function ApproveDevice({token}:{token:string}){
 const {user,offline} = useSession();
 const [details,setDetails] = useState<Details|null>(null);
 const [error,setError] = useState('');
 const [confirmed,setConfirmed] = useState(false);
 const [busy,setBusy] = useState(false);
 const [done,setDone] = useState(false);
 const [expired,setExpired] = useState(false);

 useEffect(()=>{
  let active=true;
  if(!offline)void api<Details>('/auth/device/inspect','POST',{token}).then(result=>{if(active)setDetails(result);}).catch(e=>{if(active)setError((e as Error).message);});
  return()=>{active=false;};
 },[token,offline]);
 useEffect(()=>{
  if(!details)return;
  const tick=()=>setExpired(Date.now()>=Date.parse(details.expires));
  tick();const timer=setInterval(tick,1000);return()=>clearInterval(timer);
 },[details]);

 async function approve(){
  setBusy(true);setError('');
  try{await api('/auth/device/approve','POST',{token});setDone(true);window.history.replaceState(null,'','/device/');}
  catch(e){setError((e as Error).message);}
  finally{setBusy(false);}
 }

 return <main className="device-page"><section className="device-card device-approval">
  <span className="brand"><Icon name="home" size={30}/>Organiza</span>
  {done?<div role="status"><h1>TV autorizada</h1><p>O Kanban vai abrir na TV. Pode fechar esta página.</p><a className="button" href="/tarefas/">Abrir as minhas tarefas</a></div>:<>
   <h1>Autorizar esta TV?</h1><p>A TV terá acesso à conta de <strong>{user.displayName}</strong>, com as mesmas permissões. Autorize apenas a TV que está à sua frente.</p>
   {!details&&!error&&!offline&&<p role="status">A verificar o QR code…</p>}
   {details&&<><p>Confirme que este código é igual ao da TV:</p><strong className="device-code">{details.code}</strong>
    <label className="checkbox"><input type="checkbox" checked={confirmed} onChange={e=>setConfirmed(e.target.checked)}/>O código coincide com o da minha TV.</label></>}
   {offline&&<p className="notice">Ligue o telemóvel à internet para autorizar a TV.</p>}
   {expired&&<p className="notice">Este QR code expirou. Gere outro na TV.</p>}
   <ErrorBox error={error}/>
   <Button className="primary full" disabled={!details||!confirmed||busy||offline||expired} onClick={()=>void approve()}>{busy?'A autorizar…':'Autorizar TV e abrir Kanban'}</Button>
   <a className="button full" href="/tarefas/">Cancelar</a>
  </>}
 </section></main>;
}
