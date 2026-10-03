import type {Earning} from './types';

export const opening='2026-08-01';
export type Grain='month'|'week';
export type KindFilter='all'|Earning['kind'];
export type Period={from:string;through:string;partial:boolean};
export type Metrics={earnedCents:number;records:number;classes:number;attendances:number;average:number|null;occupancy:number|null;hours:number;byKind:Record<Earning['kind'],number>};
export type Point=Period&{key:string;metrics:Metrics};

const day=(date:string)=>new Date(date+'T12:00:00Z');
export function shiftDays(date:string,offset:number){const d=day(date);d.setUTCDate(d.getUTCDate()+offset);return d.toISOString().slice(0,10);}
export function periodStart(date:string,grain:Grain){return grain==='month'?date.slice(0,7)+'-01':shiftDays(date,-((day(date).getUTCDay()+6)%7));}
export function shiftPeriod(date:string,grain:Grain,offset:number){const start=periodStart(date,grain);if(grain==='week')return shiftDays(start,offset*7);const d=day(start);d.setUTCMonth(d.getUTCMonth()+offset);return d.toISOString().slice(0,10);}
export function periodEnd(date:string,grain:Grain){return shiftDays(shiftPeriod(date,grain,1),-1);}

export function range(anchor:string,grain:Grain,asOf:string):Period{
 const start=periodStart(anchor,grain),end=periodEnd(anchor,grain);
 return {from:start<opening?opening:start,through:end>asOf?asOf:end,partial:start<opening||end>asOf};
}
export function previousRange(anchor:string,grain:Grain,asOf:string):Period|null{
 const previous=shiftPeriod(anchor,grain,-1);
 if(previous<opening)return null;
 const end=periodEnd(previous,grain);
 const through=periodEnd(anchor,grain)>asOf
  ?grain==='week'?shiftDays(previous,(day(asOf).getUTCDay()+6)%7):previous.slice(0,7)+'-'+String(Math.min(Number(asOf.slice(8)),Number(end.slice(8)))).padStart(2,'0')
  :end;
 return {from:previous,through,partial:through!==end};
}
export function aggregate(entries:Earning[],period:Period,kind:KindFilter='all'):Metrics{
 const result:Metrics={earnedCents:0,records:0,classes:0,attendances:0,average:null,occupancy:null,hours:0,byKind:{Sala:0,Aula30:0,Aula50:0}};
 let capacity=0,hundredths=0;
 for(const entry of entries){
  if(entry.date<period.from||entry.date>period.through||(kind!=='all'&&entry.kind!==kind))continue;
  result.records++;result.earnedCents+=entry.cents;result.byKind[entry.kind]+=entry.cents;
  if(entry.kind==='Sala')hundredths+=Math.round(entry.quantity*100);
  else{result.classes++;result.attendances+=entry.quantity;capacity+=entry.capacity??0;}
 }
 result.hours=hundredths/100;
 if(result.classes){result.average=result.attendances/result.classes;result.occupancy=capacity>0?result.attendances/capacity*100:null;}
 return result;
}
export function series(entries:Earning[],anchor:string,grain:Grain,asOf:string,kind:KindFilter='all'):Point[]{
 const points:Point[]=[];
 for(let offset=-(grain==='month'?5:7);offset<=0;offset++){
  const key=shiftPeriod(anchor,grain,offset);
  if(periodEnd(key,grain)<opening||key>asOf)continue;
  const period=range(key,grain,asOf);points.push({...period,key,metrics:aggregate(entries,period,kind)});
 }
 return points;
}
export function change(current:number|null,previous:number|null){
 if(current===null||previous===null)return null;
 return {absolute:current-previous,percent:previous===0?null:(current-previous)/previous*100};
}
export function forecast(entries:Earning[],asOf:string):number|null{
 const current=aggregate(entries,range(asOf,'month',asOf));
 return current.records?Math.round(current.earnedCents*Number(periodEnd(asOf,'month').slice(8))/Number(asOf.slice(8))):null;
}
