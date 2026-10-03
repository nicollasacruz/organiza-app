"use client";
import {useState} from 'react';
import {useData} from '@/lib/use-data';
import {aggregate,change,forecast,opening,periodEnd,periodStart,previousRange,range,series,shiftPeriod,type Grain,type KindFilter,type Point} from '@/lib/analytics';
import {type Earning,type Balance,dateLabel,kindLabel,money,monthLabel,today} from '@/lib/types';
import {Button,Empty,ErrorBox,Icon,LoadingPulse} from './ui';
import {Select} from './select';

const number=(value:number)=>new Intl.NumberFormat('pt-PT',{maximumFractionDigits:1}).format(value);
const signed=(value:number)=>new Intl.NumberFormat('pt-PT',{maximumFractionDigits:1,signDisplay:'exceptZero'}).format(value);
const percent=(value:number|null)=>value===null?'—':number(value)+'%';
const dates=(from:string,through:string)=>dateLabel(from)+' — '+dateLabel(through);
const kinds=['Sala','Aula30','Aula50'] as const;

export function AnalyticsScreen(){
 const now=today();
 const records=useData<Earning[]>('/earnings',[]);
 const balances=useData<Balance[]>('/earnings/balances?through='+now.slice(0,7),[]);
 const asOf=records.readonly&&records.synced?new Intl.DateTimeFormat('sv-SE',{timeZone:'Europe/Lisbon',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date(records.synced)):now;
 return <><header className="page-header"><div><h1>Análise</h1><p>O ritmo dos ganhos e das presenças nas aulas.</p></div><Button disabled={records.loading||balances.loading||records.readonly} onClick={()=>void Promise.all([records.refresh(),balances.refresh()])}><Icon name="repeat"/>Atualizar</Button></header>
  <ErrorBox error={records.error||balances.error}/>
  {records.readonly&&records.synced&&<p className="notice" role="status">Ganhos guardados em {new Date(records.synced).toLocaleString('pt-PT',{timeZone:'Europe/Lisbon'})}. A análise usa os registos guardados, até {dateLabel(asOf)}.{balances.synced&&' Saldos guardados em '+new Date(balances.synced).toLocaleString('pt-PT',{timeZone:'Europe/Lisbon'})+'.'}</p>}
  {records.loading&&!records.hasData?<section className="panel" aria-busy="true"><div className="loading-rows"><LoadingPulse className="wide"/><LoadingPulse className="wide"/><LoadingPulse className="wide"/></div></section>:records.hasData?<AnalyticsDashboard records={records.data} balance={balances.hasData?balances.data.find(b=>b.month===now.slice(0,7)):undefined} now={now} asOf={asOf} balanceLoading={balances.loading}/>:<Empty title="Análise indisponível" description={records.readonly?'Ligue-se à internet para carregar os ganhos. Os dados ainda não foram guardados neste dispositivo.':'Não foi possível carregar os ganhos. Tente atualizar a análise.'}/>}
 </>;
}

export function AnalyticsDashboard({records,balance,now,asOf=now,balanceLoading=false}:{records:Earning[];balance?:Balance;now:string;asOf?:string;balanceLoading?:boolean}){
 const [grain,setGrain]=useState<Grain>('month');
 const [anchor,setAnchor]=useState(asOf);
 const [kind,setKind]=useState<KindFilter>('all');
 const [attendanceMode,setAttendanceMode]=useState<'attendances'|'average'>('attendances');
 const selectedStart=periodStart(anchor,grain)>periodStart(asOf,grain)?periodStart(asOf,grain):periodStart(anchor,grain);
 const currentRange=range(selectedStart,grain,asOf),previous=previousRange(selectedStart,grain,asOf);
 const metrics=aggregate(records,currentRange,kind),before=previous?aggregate(records,previous,kind):null;
 const points=series(records,selectedStart,grain,asOf,kind);
 const caption=grain==='month'?monthLabel(selectedStart.slice(0,7)):dates(currentRange.from,currentRange.through);
 function changeGrain(value:Grain){setGrain(value);setAnchor(asOf);}
 const cards=kind==='Sala'
  ?[{label:'Ganhos reais',value:money(metrics.earnedCents),current:metrics.earnedCents,prior:before?.earnedCents??null,unit:'money'},{label:'Horas de sala',value:number(metrics.hours),current:metrics.hours,prior:before?.hours??null,unit:'hours'}]
  :[{label:'Ganhos reais',value:money(metrics.earnedCents),current:metrics.earnedCents,prior:before?.earnedCents??null,unit:'money'},
    {label:'Aulas registadas',value:number(metrics.classes),current:metrics.classes,prior:before?.classes??null,unit:'count'},
    {label:'Presenças',value:number(metrics.attendances),current:metrics.attendances,prior:before?.attendances??null,unit:'count'},
    {label:'Pessoas por aula',value:metrics.average===null?'—':number(metrics.average),current:metrics.average,prior:before?.average??null,unit:'average'},
    {label:'Ocupação',value:percent(metrics.occupancy),current:metrics.occupancy,prior:before?.occupancy??null,unit:'points'}];
 return <div className="analytics-screen">
  <CurrentMonth records={records} balance={balance} now={now} asOf={asOf} loading={balanceLoading}/>
  <section className="analytics-controls" aria-label="Filtros de análise">
   <div className="segmented" aria-label="Agrupamento"><button aria-pressed={grain==='month'} className={grain==='month'?'active':''} onClick={()=>changeGrain('month')}>Mensal</button><button aria-pressed={grain==='week'} className={grain==='week'?'active':''} onClick={()=>changeGrain('week')}>Semanal</button></div>
   <label className="analytics-kind"><span>Tipo de trabalho</span><Select value={kind} onChange={e=>setKind(e.target.value as KindFilter)}><option value="all">Todos</option>{kinds.map(k=><option key={k} value={k}>{kindLabel(k)}</option>)}</Select></label>
   <div className="month-bar"><button className="icon-button" aria-label="Período anterior" disabled={periodEnd(shiftPeriod(selectedStart,grain,-1),grain)<opening} onClick={()=>setAnchor(shiftPeriod(selectedStart,grain,-1))}><Icon name="left"/></button><strong className="analytics-period">{caption}</strong><button className="icon-button" aria-label="Período seguinte" disabled={selectedStart>=periodStart(asOf,grain)} onClick={()=>setAnchor(shiftPeriod(selectedStart,grain,1))}><Icon name="right"/></button><Button className="today-button" onClick={()=>setAnchor(asOf)}>Atual</Button></div>
  </section>
  <p className="analytics-comparison">{currentRange.partial?'Período parcial · ':''}{dates(currentRange.from,currentRange.through)}{previous?' · Comparação: '+dates(previous.from,previous.through):' · Sem período anterior completo no histórico.'}</p>
  <section className="analytics-stats" aria-label="Indicadores do período">{cards.map(card=><article className="panel analytics-stat" key={card.label}><p>{card.label}</p><strong>{card.value}</strong><Delta current={card.current} previous={card.prior} unit={card.unit}/></article>)}</section>
  {!metrics.records&&<p className="notice">Sem registos neste período para {kind==='all'?'os tipos selecionados':kindLabel(kind)}. Os totais refletem apenas o trabalho registado.</p>}
  <div className="analytics-charts">
   <Chart key={'earnings'+grain+kind+selectedStart} title="Evolução dos ganhos" points={points} metric="earnedCents" grain={grain}/>
   {kind==='Sala'?<section className="panel analytics-class-empty"><Empty title="Presenças e ocupação" description="Escolha Todos, AULA 30M ou AULA 50M para acompanhar as aulas."/></section>:<>
    <Chart key={'attendance'+grain+kind+selectedStart+attendanceMode} title="Presenças nas aulas" points={points} metric={attendanceMode} grain={grain} controls={<div className="segmented" aria-label="Indicador de presenças"><button className={attendanceMode==='attendances'?'active':''} aria-pressed={attendanceMode==='attendances'} onClick={()=>setAttendanceMode('attendances')}>Total</button><button className={attendanceMode==='average'?'active':''} aria-pressed={attendanceMode==='average'} onClick={()=>setAttendanceMode('average')}>Média por aula</button></div>}/>
    <Chart key={'occupancy'+grain+kind+selectedStart} title="Ocupação das aulas" points={points} metric="occupancy" grain={grain}/>
   </>}
  </div>
  {kind!=='Sala'&&<section className="panel analytics-breakdown"><div className="panel-head"><h2>Comparação entre aulas</h2><span className="muted">{caption}</span></div><div className="table-wrap"><table><caption className="sr-only">Desempenho por duração no período selecionado</caption><thead><tr><th scope="col">Tipo</th><th scope="col" className="numeric">Ganhos</th><th scope="col" className="numeric">Aulas</th><th scope="col" className="numeric">Presenças</th><th scope="col" className="numeric">Pessoas / aula</th><th scope="col" className="numeric">Ocupação</th><th scope="col">Evolução da média</th></tr></thead><tbody>{(['Aula30','Aula50'] as const).filter(k=>kind==='all'||kind===k).map(k=>{
   const m=aggregate(records,currentRange,k),p=previous?aggregate(records,previous,k):null;
   return <tr key={k}><th scope="row"><span className={'type-pill '+k}>{kindLabel(k)}</span></th><td className="numeric">{money(m.earnedCents)}</td><td className="numeric">{m.classes}</td><td className="numeric">{m.attendances}</td><td className="numeric">{m.average===null?'—':number(m.average)}</td><td className="numeric">{percent(m.occupancy)}</td><td><Delta current={m.average} previous={p?.average??null} unit="average"/></td></tr>;
  })}</tbody></table></div><p className="analytics-footnote">Mais presenças podem resultar de mais aulas. A média por aula e a ocupação ajudam a distinguir os dois efeitos.</p></section>}
  <details className="panel analytics-method"><summary>Como ler esta análise</summary><p>Cada registo de AULA 30M ou AULA 50M conta como uma sessão, incluindo sessões com zero pessoas. Presenças são participações em sessões, não alunos únicos.</p><p>A média divide presenças pelo número de aulas. A ocupação divide o total de presenças pelo total de lugares disponíveis, ponderando aulas com diferentes lotações.</p><p>Ganhos são a soma dos valores registados, sem saldo transportado e sem datas futuras. Períodos sem registos não confirmam ausência de trabalho. Uma variação percentual precisa de uma base anterior superior a zero.</p><p>O mês e a semana em curso comparam apenas os dias decorridos. Se o mês anterior for mais curto, a comparação termina no último dia desse mês. Os intervalos exatos aparecem acima dos indicadores. O histórico começa em agosto de 2026.</p></details>
 </div>;
}

function Delta({current,previous,unit}:{current:number|null;previous:number|null;unit:string}){
 const delta=change(current,previous);
 if(!delta)return <small className="analytics-delta">Sem base de comparação</small>;
 const absolute=unit==='money'?(delta.absolute>0?'+':'')+money(delta.absolute):signed(delta.absolute)+(unit==='points'?' p.p.':unit==='hours'?' h':unit==='average'?' pessoas / aula':'');
 return <small className="analytics-delta">{absolute}{unit!=='points'&&(delta.percent===null?' · Sem base percentual':' · '+signed(delta.percent)+'%')}</small>;
}

function CurrentMonth({records,balance,now,asOf,loading}:{records:Earning[];balance?:Balance;now:string;asOf:string;loading:boolean}){
 const current=aggregate(records,{from:now.slice(0,7)+'-01',through:asOf,partial:true});
 const estimate=asOf.slice(0,7)===now.slice(0,7)?forecast(records,asOf):null;
 const available=balance?current.earnedCents+balance.incomingCents:null;
 const estimatedAvailable=balance&&estimate!==null?estimate+balance.incomingCents:null;
 const shortfall=balance&&available!==null?Math.max(0,balance.targetCents-available):null;
 return <section className="panel analytics-month" aria-label="Resumo global do mês atual" aria-busy={loading}>
  <div className="analytics-month-heading"><div><span className="analytics-eyebrow">MÊS ATUAL · TODOS OS TIPOS</span><h2>{monthLabel(now.slice(0,7))}</h2></div><span className="stat-icon sage"><Icon name="target" size={26}/></span></div>
  <div className="analytics-month-columns"><div>
   <dl className="analytics-month-values"><div><dt>Ganho real</dt><dd>{money(current.earnedCents)}</dd></div><div><dt>Saldo anterior</dt><dd>{balance?money(balance.incomingCents):loading?<LoadingPulse/>:'—'}</dd></div><div><dt>Disponível</dt><dd>{available===null?loading?<LoadingPulse/>:'—':money(available)}</dd></div></dl>
   {balance&&available!==null?<div className="goal-progress"><div><strong>{percent(available/balance.targetCents*100)}</strong><span>{shortfall?'Faltam '+money(shortfall):'Meta atingida'} · Meta {money(balance.targetCents)}</span></div><progress aria-label="Progresso da meta do mês atual" max={balance.targetCents} value={available}/></div>:<p className="muted">{loading?'A carregar a meta…':'Meta e saldo indisponíveis. Sincronize os dados deste mês.'}</p>}
  </div><div className="analytics-estimate"><span className="analytics-eyebrow">ESTIMATIVA DE FECHO</span><strong>{estimate===null?'Sem dados para estimar':money(estimate)}</strong><p>Ganho estimado{estimatedAvailable!==null&&' · Disponível estimado '+money(estimatedAvailable)}</p>{balance&&estimatedAvailable!==null&&<p>{estimatedAvailable>=balance.targetCents?'Acima da meta por '+money(estimatedAvailable-balance.targetCents):'Abaixo da meta por '+money(balance.targetCents-estimatedAvailable)}</p>}<small>Ritmo até {dateLabel(asOf)}: ganho ÷ dias decorridos × dias do mês. Pressupõe manter esse ritmo; não é uma garantia.</small></div></div>
 </section>;
}

type ChartMetric='earnedCents'|'attendances'|'average'|'occupancy';
function Chart({title,points,metric,grain,controls}:{title:string;points:Point[];metric:ChartMetric;grain:Grain;controls?:React.ReactNode}){
 const [selected,setSelected]=useState(points.length-1);
 const values=points.map(p=>p.metrics[metric]);
 const maximum=metric==='occupancy'?100:Math.max(1,...values.map(v=>v??0));
 const format=(value:number|null)=>value===null?'Sem aulas':metric==='earnedCents'?money(value):metric==='occupancy'?percent(value):number(value)+(metric==='average'?' pessoas / aula':' presenças');
 const label=(point:Point)=>grain==='month'?new Intl.DateTimeFormat('pt-PT',{month:'short',year:'2-digit',timeZone:'UTC'}).format(new Date(point.key+'T12:00:00Z')):point.from.slice(8)+'/'+point.from.slice(5,7);
 const x=(index:number)=>65+(index+.5)*535/Math.max(1,points.length);
 const y=(value:number)=>180-value/maximum*145;
 let line='';
 let contiguous=false;
 points.forEach((point,i)=>{const value=point.metrics[metric];if(value===null){contiguous=false;return;}line+=(contiguous?'L':'M')+x(i)+','+y(value)+' ';contiguous=true;});
 const active=points[selected]??points.at(-1);
 const hasValues=values.some(v=>v!==null&&(metric==='earnedCents'?points.some(p=>p.metrics.records>0):points.some(p=>p.metrics.classes>0)));
 return <section className={'panel analytics-chart '+(metric==='earnedCents'?'analytics-chart-wide':'')}>
  <div className="analytics-chart-heading"><h2>{title}</h2>{controls}</div>
  {metric==='earnedCents'&&<div className="analytics-chart-legend">{kinds.map(kind=><span key={kind}><i className={'analytics-key '+kind}/>{kindLabel(kind)}</span>)}</div>}
  {hasValues?<>
   <div className="analytics-chart-scroll"><svg viewBox="0 0 620 225" role="group" aria-label={title+' · use as setas para percorrer os períodos'}>
    {[0,.5,1].map(tick=><g key={tick} className="analytics-grid"><line x1="65" x2="600" y1={y(maximum*tick)} y2={y(maximum*tick)}/><text x="56" y={y(maximum*tick)+4} textAnchor="end">{metric==='earnedCents'?money(Math.round(maximum*tick)):number(maximum*tick)+(metric==='occupancy'?'%':'')}</text></g>)}
    {(metric==='occupancy'||metric==='average')&&<path d={line} className="analytics-line"/>}
    {points.map((point,i)=>{const value=point.metrics[metric],width=Math.min(48,535/points.length*.55);let stacked=0;
     return <g key={point.key} data-point tabIndex={0} role="button" aria-label={dates(point.from,point.through)+': '+format(value)+(point.partial?' · Parcial':'')} aria-pressed={selected===i} onFocus={()=>setSelected(i)} onMouseEnter={()=>setSelected(i)} onClick={()=>setSelected(i)} onKeyDown={event=>{if(event.key==='ArrowRight'||event.key==='ArrowLeft'||event.key==='Home'||event.key==='End'){event.preventDefault();const next=event.key==='Home'?0:event.key==='End'?points.length-1:Math.max(0,Math.min(points.length-1,i+(event.key==='ArrowRight'?1:-1)));const elements=event.currentTarget.parentElement?.querySelectorAll<SVGElement>('[data-point]');elements?.[next]?.focus();}else if(event.key==='Enter'||event.key===' '){event.preventDefault();setSelected(i);}}}>
      <rect className="analytics-hit" x={x(i)-535/points.length/2} y="25" width={535/points.length} height="190" fill="transparent"/>
      {metric==='earnedCents'?kinds.map(kind=>{const amount=point.metrics.byKind[kind],height=amount/maximum*145;stacked+=height;return <rect key={kind} className={'analytics-bar '+kind} x={x(i)-width/2} y={180-stacked} width={width} height={height}/>;}):metric==='attendances'?<rect className="analytics-bar attendance" x={x(i)-width/2} y={y(value??0)} width={width} height={180-y(value??0)}/>:value!==null?<circle className="analytics-point" cx={x(i)} cy={y(value)} r={selected===i?6:4}/>:null}
      <text className="analytics-axis" x={x(i)} y="204" textAnchor="middle">{label(point)}{point.partial?' *':''}</text>
      {selected===i&&<line className="analytics-selection" x1={x(i)} x2={x(i)} y1="25" y2="185"/>}
     </g>;
    })}
   </svg></div>
   {active&&<p className="analytics-chart-readout" aria-live="polite"><span>{dates(active.from,active.through)}{active.partial?' · Parcial':''}</span><strong>{format(active.metrics[metric])}</strong></p>}
  </>:<Empty title="Sem registos para o gráfico" description="Os períodos ficam disponíveis na tabela abaixo."/>}
  <details className="analytics-chart-data"><summary>Ver dados em tabela</summary><div className="table-wrap"><table><caption className="sr-only">{title}</caption><thead><tr><th scope="col">Período</th><th scope="col" className="numeric">{metric==='earnedCents'?'Ganhos':metric==='occupancy'?'Ocupação':metric==='average'?'Pessoas / aula':'Presenças'}</th></tr></thead><tbody>{points.map(point=><tr key={point.key}><th scope="row">{dates(point.from,point.through)}{point.partial?' · Parcial':''}</th><td className="numeric">{metric==='earnedCents'&&!point.metrics.records?'Sem registos':format(point.metrics[metric])}</td></tr>)}</tbody></table></div></details>
 </section>;
}
