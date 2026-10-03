"use client";
import {useEffect,useState} from 'react';
import {api} from '@/lib/api';
import {useData} from '@/lib/use-data';
import {type EarningTarget,monthLabel} from '@/lib/types';
import {Button,ErrorBox,Field,Icon} from './ui';

export function EarningTargetSettings(){
 const target=useData<EarningTarget|null>('/earnings/target',null);
 const [amount,setAmount]=useState('');
 const [busy,setBusy]=useState(false);
 const [error,setError]=useState('');
 const [message,setMessage]=useState('');
 useEffect(()=>{if(target.data)setAmount((target.data.targetCents/100).toFixed(2));},[target.data]);
 async function save(e:React.FormEvent){
  e.preventDefault();if(!target.data)return;
  setBusy(true);setError('');setMessage('');
  try{
   const saved=await api<EarningTarget>('/earnings/target','PUT',{targetCents:Math.round(Number(amount)*100),revision:target.data.revision});
   await target.refresh();
   setMessage('Meta guardada a partir de '+monthLabel(saved.effectiveMonth.slice(0,7))+'. Os meses anteriores mantêm-se.');
  }catch(e){setError((e as Error).message);await target.refresh();}
  finally{setBusy(false);}
 }
 return <section className="panel settings-panel">
  <div className="section-heading"><Icon name="target"/><div><h2>Meta mensal de ganhos</h2><p>Partilhada por toda a família.</p></div></div>
  <form onSubmit={save}>
   <Field label="Meta mensal (€)"><input type="number" min="0.01" max="21474836.47" step="0.01" inputMode="decimal" value={amount} onChange={e=>{setAmount(e.target.value);setMessage('');}} required disabled={target.readonly||target.loading||busy}/></Field>
   <p className="notice">A alteração aplica-se ao mês atual{target.data?' ('+monthLabel(target.data.effectiveMonth.slice(0,7))+')':''} e aos seguintes. As metas e o saldo dos meses anteriores não são alterados.</p>
   <ErrorBox error={target.error||error}/>
   {message&&<p className="success" role="status">{message}</p>}
   <div className="form-actions"><Button className="primary" disabled={target.readonly||target.loading||busy||!target.data}>{busy?'A guardar…':'Guardar meta'}</Button></div>
  </form>
 </section>;
}
