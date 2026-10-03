"use client";
import {usePathname} from 'next/navigation';
import type {ReactNode} from 'react';
import {SessionProvider} from './session';
import {Shell} from './shell';
export function AppFrame({children}:{children:ReactNode}){
 const pathname=usePathname();
 const family=['/ganhos','/analise','/tarefas','/configuracoes','/membros'].some(route=>pathname===route||pathname.startsWith(route+'/'));
 return family?<SessionProvider><Shell>{children}</Shell></SessionProvider>:children;
}
