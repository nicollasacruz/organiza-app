"use client";
import {useEffect,useRef,useState,type Dispatch,type SetStateAction} from 'react';
import {api} from '@/lib/api';
import {dateLabel,addDays} from '@/lib/types';
import {Button,Modal,Icon,LoadingPulse} from './ui';
import {Select} from './select';

type Message={role:'user'|'assistant';content:string;at?:string};
type Window={memberId:string;date:string;start:string;end:string};
type Estimate={taskId:string;minutes:number};
type Choice={taskId:string;memberId:string|null;allowReassignment:boolean;reason:string};
type State={windows:Window[];estimates:Estimate[];choices:Choice[]};
type Planned={taskId:string;title:string;memberId:string;start:string;end:string;minutes:number;reason:string};
type Response={stage:string;message:string;state:State;plan:{items:Planned[];unplaced:{taskId:string;title:string;reason:string}[]}|null;members:{id:string;name:string}[];tasks:{id:string;title:string;assigneeId:string|null;due:string|null}[];now:string;configured:boolean;source:string};
export type SuggestionSession={messages:Message[];response:Response|null;busy:boolean;error:string};
export const emptySuggestionSession=():SuggestionSession=>({messages:[],response:null,busy:false,error:''});
const emptyState=():State=>({windows:[],estimates:[],choices:[]});
const clock=(value:string)=>new Date(value).toLocaleTimeString('pt-PT',{timeZone:'Europe/Lisbon',hour:'2-digit',minute:'2-digit'});
const localDate=(value:string)=>new Intl.DateTimeFormat('sv-SE',{timeZone:'Europe/Lisbon'}).format(new Date(value));

export function Suggestions({session,setSession,onClose}:{session:SuggestionSession;setSession:Dispatch<SetStateAction<SuggestionSession>>;onClose:()=>void}){
 const [draft,setDraft]=useState(''),[editing,setEditing]=useState(false);const end=useRef<HTMLDivElement>(null),started=useRef(false);
 const response=session.response,state=response?.state??emptyState();
 async function run(action:string,messages=session.messages){
  setSession(current=>({...current,busy:true,error:''}));
  try{
   const next=await api<Response>('/tasks/suggestions','POST',{action,messages,state:action==='start'?emptyState():state});
   setSession(current=>({...current,busy:false,response:next,messages:next.stage==='unavailable'?messages:[...messages,{role:'assistant',content:next.message,at:next.now}]}));
   if(next.stage==='confirm')setEditing(false);
  }catch(error){setSession(current=>({...current,busy:false,error:(error as Error).message}));}
 }
 useEffect(()=>{if(!started.current&&!session.response&&!session.busy&&!session.error){started.current=true;void run('start');}},[]);
 useEffect(()=>{end.current?.scrollIntoView({block:'nearest'});},[session.messages.length,session.busy]);
 function update(next:State){setSession(current=>({...current,response:current.response?{...current.response,state:next,plan:null,stage:current.response.stage==='plan'?'confirm':current.response.stage}:null}));}
 async function send(event:React.FormEvent){event.preventDefault();if(!draft.trim()||session.busy)return;const messages=[...session.messages,{role:'user' as const,content:draft.trim(),at:new Date().toISOString()}];setDraft('');setSession(current=>({...current,messages}));await run('chat',messages);}
 const day=response?localDate(response.now):'';
 const canPlan=state.windows.length>0&&state.estimates.length>0&&!session.busy;
 return <Modal title="Hoje e amanhã" onClose={onClose}><div className="suggestion-dialog">
  <p className="suggestion-privacy">As tarefas, nomes dos participantes e esta conversa são enviados ao OpenRouter e ao fornecedor do modelo. Ganhos, emails e credenciais ficam fora. Esta conversa não é guardada.</p>
  <div className="suggestion-history" role="log" aria-label="Conversa de planeamento" aria-live="polite">
   {session.messages.map((message,index)=><div key={index} className={'suggestion-message '+message.role}><strong>{message.role==='user'?'Você':'Organiza'}</strong><p>{message.content}</p></div>)}
   {session.busy&&<div className="suggestion-message assistant" aria-busy="true"><LoadingPulse className="wide"/><LoadingPulse className="wide"/></div>}
   {session.error&&<p className="error" role="alert">{session.error}</p>}
   {!response&&session.error&&<Button disabled={session.busy} onClick={()=>void run('start')}>Tentar novamente</Button>}
   {response?.stage==='unavailable'&&<p className="notice" role="status">{response.message}</p>}
   {response&&response.stage!=='empty'&&<>
    {response.stage!=='plan'&&(state.windows.length>0||state.estimates.length>0||editing||response.stage==='unavailable')&&<section className="suggestion-summary"><h3>Resumo para confirmar</h3><p className="muted">Confira os horários e as durações antes de gerar. Pode corrigir aqui ou continuar a conversa.</p>
     <h4>Tempo livre da família</h4>{state.windows.map((window,index)=><div className="availability-row" key={index}>
      <Select aria-label="Participante" value={window.memberId} disabled={session.busy} onChange={event=>update({...state,windows:state.windows.map((w,i)=>i===index?{...w,memberId:event.target.value}:w)})}>{response.members.map(member=><option key={member.id} value={member.id}>{member.name}</option>)}</Select>
      <Select aria-label="Dia disponível" value={window.date} disabled={session.busy} onChange={event=>update({...state,windows:state.windows.map((w,i)=>i===index?{...w,date:event.target.value}:w)})}><option value={day}>Hoje · {dateLabel(day).slice(0,5)}</option><option value={addDays(day,1)}>Amanhã · {dateLabel(addDays(day,1)).slice(0,5)}</option></Select>
      <label>Das<input aria-label="Início do intervalo" type="time" value={window.start.slice(0,5)} disabled={session.busy} onChange={event=>update({...state,windows:state.windows.map((w,i)=>i===index?{...w,start:event.target.value+':00'}:w)})}/></label>
      <label>Às<input aria-label="Fim do intervalo" type="time" value={window.end.slice(0,5)} disabled={session.busy} onChange={event=>update({...state,windows:state.windows.map((w,i)=>i===index?{...w,end:event.target.value+':00'}:w)})}/></label>
      <button className="icon-button" aria-label="Remover intervalo" disabled={session.busy} onClick={()=>update({...state,windows:state.windows.filter((_,i)=>i!==index)})}><Icon name="close"/></button>
     </div>)}
     <Button disabled={session.busy||!response.members.length} onClick={()=>update({...state,windows:[...state.windows,{memberId:response.members[0].id,date:addDays(day,1),start:'09:00:00',end:'10:00:00'}]})}><Icon name="plus" size={16}/>Adicionar intervalo</Button>
     <h4>Durações estimadas</h4>{response.tasks.map(task=>{const estimate=state.estimates.find(e=>e.taskId===task.id),choice=state.choices.find(c=>c.taskId===task.id);return <label className="estimate-row" key={task.id}><span>{task.title}<small>{choice?.memberId?response.members.find(m=>m.id===choice.memberId)?.name+(task.assigneeId&&task.assigneeId!==choice.memberId?' · Responsável alternativo sugerido':''):task.assigneeId?response.members.find(m=>m.id===task.assigneeId)?.name:'Qualquer participante disponível'}</small></span><input aria-label={'Minutos para '+task.title} type="number" min="1" max="720" inputMode="numeric" placeholder="—" value={estimate?.minutes??''} disabled={session.busy} onChange={event=>update({...state,estimates:[...state.estimates.filter(e=>e.taskId!==task.id),...(event.target.value?[{taskId:task.id,minutes:Number(event.target.value)}]:[])]})}/><span>min</span></label>;})}
     <p className="muted">Sem duração, a tarefa fica como “Duração por confirmar”.</p>
     <div className="suggestion-actions"><Button className="primary" disabled={!canPlan} onClick={()=>void run(response.stage==='unavailable'?'local':'plan')}>{response.stage==='unavailable'?'Confirmar e gerar proposta local':'Confirmar e gerar plano'}</Button></div>
    </section>}
    {response.plan&&<section className="suggestion-plan"><span className="type-pill">{response.source==='local'?'Proposta por regras locais':'Proposta assistida por IA'}</span>{[day,addDays(day,1)].map((date,index)=><div key={date}><h3>{index===0?'Hoje':'Amanhã'} · {dateLabel(date).slice(0,5)}</h3>{response.plan!.items.filter(item=>localDate(item.start)===date).map(item=><article className="suggested-task" key={item.taskId}><strong>{item.title}</strong><p>{response.members.find(member=>member.id===item.memberId)?.name} · {clock(item.start)}–{clock(item.end)} · {item.minutes} min</p><small>{item.reason}</small></article>)}{!response.plan!.items.some(item=>localDate(item.start)===date)&&<p className="muted">Sem tarefas encaixadas neste dia.</p>}</div>)}{response.plan.unplaced.length>0&&<><h3>Ficam por encaixar</h3>{response.plan.unplaced.map(task=><p className="unplaced-task" key={task.taskId}><strong>{task.title}</strong><span>{task.reason}</span></p>)}</>}<p className="notice">É uma sugestão. As datas, responsáveis, recorrências e Google Agenda mantêm-se.</p></section>}
    <div className="suggestion-actions">{(response.stage==='unavailable'||session.error)&&<Button disabled={session.busy||session.messages.at(-1)?.role!=='user'} onClick={()=>void run('chat')}>Tentar novamente</Button>}<Button disabled={session.busy} onClick={()=>{setEditing(true);if(response.stage==='plan')update(state);}}>Ajustar horários e durações</Button><Button disabled={session.busy} onClick={()=>{setSession(emptySuggestionSession());setEditing(false);setDraft('');void run('start',[]);}}>Nova conversa</Button></div>
   </>}
   <div ref={end}/>
  </div>
  <form className="suggestion-compose" onSubmit={send}><label className="sr-only" htmlFor="suggestion-message">Mensagem para a Organiza</label><textarea id="suggestion-message" value={draft} onChange={event=>setDraft(event.target.value)} rows={2} maxLength={2000} placeholder="Ex.: hoje tenho tempo das 20h às 21h; a Sarah amanhã das 10h às 12h…" disabled={session.busy}/><Button className="primary" disabled={session.busy||!draft.trim()||response?.stage==='empty'} aria-label="Enviar mensagem"><Icon name="arrow"/></Button></form>
 </div></Modal>;
}
