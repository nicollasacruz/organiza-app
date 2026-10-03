"use client";
import {Children,isValidElement,useEffect,useId,useRef,useState,type ReactNode,type KeyboardEvent} from 'react';

type Option={value?:string;children?:ReactNode;disabled?:boolean};
type Props={children:ReactNode;value?:string;defaultValue?:string;name?:string;required?:boolean;disabled?:boolean;'aria-label'?:string;onChange?:(event:{target:{value:string}})=>void};

export function Select({children,value,defaultValue='',name,required,disabled,'aria-label':label,onChange}:Props){
 const id=useId(),trigger=useRef<HTMLButtonElement>(null),menu=useRef<HTMLDivElement>(null);
 const [internal,setInternal]=useState(defaultValue),[open,setOpen]=useState(false);
 const [fieldLabel,setFieldLabel]=useState<string>();
 useEffect(()=>{setFieldLabel(trigger.current?.closest('label')?.querySelector('span')?.textContent??undefined);},[]);
 const selected=value??internal;
 const options=Children.toArray(children).filter(isValidElement<Option>).map(child=>child.props);
 const current=options.find(option=>String(option.value??'')===selected);
 function close(){menu.current?.hidePopover();trigger.current?.focus();}
 function choose(option:Option){const next=String(option.value??'');setInternal(next);onChange?.({target:{value:next}});close();}
 function show(last=false){
  const button=trigger.current,popup=menu.current;if(!button||!popup||disabled)return;
  const rect=button.getBoundingClientRect(),height=Math.min(options.length*44+12,280);
  const availableBelow=window.innerHeight-rect.bottom-12,availableAbove=rect.top-12;
  const above=availableBelow<height&&availableAbove>availableBelow;
  const maxHeight=Math.min(height,above?availableAbove:availableBelow);
  popup.style.width=Math.min(Math.max(rect.width,180),window.innerWidth-24)+'px';
  popup.style.left=Math.max(12,Math.min(rect.left,window.innerWidth-parseFloat(popup.style.width)-12))+'px';
  popup.style.top=(above?rect.top-maxHeight-6:rect.bottom+6)+'px';popup.style.maxHeight=maxHeight+'px';
  popup.showPopover();
  const buttons=Array.from(popup.querySelectorAll<HTMLButtonElement>('button:not(:disabled)'));
  (buttons.find(button=>button.getAttribute('aria-selected')==='true')??(last?buttons.at(-1):buttons[0]))?.focus();
 }
 function keyboard(event:KeyboardEvent<HTMLDivElement>){
  const buttons=Array.from(menu.current?.querySelectorAll<HTMLButtonElement>('button:not(:disabled)')??[]);
  const index=buttons.indexOf(document.activeElement as HTMLButtonElement);
  let next:HTMLButtonElement|undefined;
  if(event.key==='ArrowDown')next=buttons[(index+1)%buttons.length];
  if(event.key==='ArrowUp')next=buttons[(index-1+buttons.length)%buttons.length];
  if(event.key==='Home')next=buttons[0];if(event.key==='End')next=buttons.at(-1);
  if(next){event.preventDefault();next.focus();}
  if(event.key==='Escape'){event.preventDefault();event.stopPropagation();close();}
  if(event.key==='Tab')menu.current?.hidePopover();
  if(event.key.length===1&&!event.ctrlKey&&!event.metaKey){const match=buttons.slice(index+1).concat(buttons.slice(0,index+1)).find(button=>button.textContent?.trim().toLocaleLowerCase().startsWith(event.key.toLocaleLowerCase()));if(match){event.preventDefault();match.focus();}}
 }
 return <span className="custom-select">
  <input className="select-value" aria-hidden="true" tabIndex={-1} name={name} value={selected} onChange={()=>{}} onFocus={()=>trigger.current?.focus()} required={required} disabled={disabled} onInvalid={event=>{event.preventDefault();trigger.current?.focus();show();}}/>
  <button ref={trigger} type="button" className="select-trigger" disabled={disabled} aria-label={label??fieldLabel} aria-haspopup="listbox" aria-expanded={open} aria-controls={id} onClick={()=>open?close():show()} onKeyDown={event=>{if(['ArrowDown','ArrowUp'].includes(event.key)){event.preventDefault();show(event.key==='ArrowUp');}}}>
   <span>{current?.children??'Escolher…'}</span><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg>
  </button>
  <div ref={menu} id={id} popover="auto" role="listbox" aria-label={label??fieldLabel} className="select-menu" onKeyDown={keyboard} onToggle={event=>setOpen(event.newState==='open')}>
   {options.map(option=><button type="button" role="option" tabIndex={-1} aria-selected={String(option.value??'')===selected} disabled={option.disabled} key={String(option.value??'')} onClick={()=>choose(option)}><span>{option.children}</span><span aria-hidden="true">{String(option.value??'')===selected?'✓':''}</span></button>)}
  </div>
 </span>;
}
