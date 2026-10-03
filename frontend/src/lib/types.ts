export type Member = {id:string;displayName:string;email?:string;isAdmin:boolean;accent:string;theme:'light'|'dark'|'system'};
export type Earning = {id:string;date:string;kind:'Sala'|'Aula30'|'Aula50';quantity:number;capacity:number|null;cents:number;revision:number};
export type Balance = {month:string;earnedCents:number;incomingCents:number;availableCents:number;outgoingCents:number;shortfallCents:number;targetCents:number};
export type TaskItem = {id:string;seriesId:string;previousId:string|null;title:string;description:string;assigneeId:string|null;due:string|null;status:'todo'|'doing'|'done';intervalDays:number|null;intervalCount:number|null;intervalUnit:string|null;completedAt:string|null;completedBy:string|null;archived:boolean;revision:number};
export type Prediction = {sourceId:string;seriesId:string;title:string;description:string;assigneeId:string|null;due:string;predicted:true};
export type GoogleStatus = {configured:boolean;connected:boolean};
export type Calendar = {id:string;name:string};
export type CalendarCopy = {taskId:string;calendarId:string;url:string};
export const money=(cents:number)=>new Intl.NumberFormat('pt-PT',{style:'currency',currency:'EUR'}).format(cents/100);
export const today=()=>new Intl.DateTimeFormat('sv-SE',{timeZone:'Europe/Lisbon',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());
export const dateLabel=(date:string)=>new Intl.DateTimeFormat('pt-PT',{day:'2-digit',month:'2-digit',year:'numeric'}).format(new Date(date+'T12:00:00'));
export const addDays=(date:string,days:number)=>{const d=new Date(date+'T12:00:00Z');d.setUTCDate(d.getUTCDate()+days);return d.toISOString().slice(0,10);};
export const monthLabel=(month:string)=>new Intl.DateTimeFormat('pt-PT',{month:'long',year:'numeric'}).format(new Date(month+'-01T12:00:00'));
export const kindLabel=(kind:string)=>({Sala:'SALA',Aula30:'AULA 30M',Aula50:'AULA 50M'}[kind]||kind);

export type EarningTarget = {targetCents:number;effectiveMonth:string;revision:number};
