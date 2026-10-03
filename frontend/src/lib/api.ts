let csrf:string|null=null;
export class ApiError extends Error {constructor(message:string,public status:number){super(message);}}
export async function api<T=void>(path:string,method='GET',body?:unknown):Promise<T>{
 const headers:Record<string,string>={};
 if(method!=='GET'){if(!csrf){const r=await fetch('/api/auth/csrf',{credentials:'same-origin',cache:'no-store'});if(!r.ok)throw new ApiError('Não foi possível iniciar a sessão.',r.status);csrf=(await r.json()).token;}headers['X-CSRF']=csrf!;if(body!==undefined)headers['Content-Type']='application/json';}
 let response:Response;
 try{response=await fetch('/api'+path,{method,headers,credentials:'same-origin',cache:'no-store',body:body===undefined?undefined:JSON.stringify(body)});}catch{throw new ApiError('Sem ligação ao servidor. Os dados guardados estão disponíveis apenas para leitura.',0);}
 if(!response.ok){let message='Não foi possível concluir a ação.';try{message=(await response.json()).message||message;}catch{}if((response.status===401||response.status===403)&&!path.startsWith('/auth/login')&&!path.startsWith('/auth/passkey')&&!path.startsWith('/integrations/google'))window.dispatchEvent(new Event('organiza-expired'));throw new ApiError(message,response.status);}
 if(response.status===204)return undefined as T;
 return response.json();
}
export function resetCsrf(){csrf=null;}
