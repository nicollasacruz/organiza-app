"use client";
import {useCallback,useEffect,useState} from 'react';
import {api,ApiError} from './api';
import {readSnapshot,saveSnapshot} from './offline';
import {useSession} from '@/components/session';
export function useData<T>(path:string,initial:T){
 const {user,offline,dataCache}=useSession();
 const [data,setData]=useState<T>(()=>dataCache.entries.has(path)?dataCache.entries.get(path) as T:initial);
 const [error,setError]=useState('');const [loading,setLoading]=useState(()=>!dataCache.entries.has(path));const [readonly,setReadonly]=useState(offline);
 const [hasData,setHasData]=useState(()=>dataCache.entries.has(path));const [synced,setSynced]=useState('');
 const refresh=useCallback(async()=>{
  setError('');
  setHasData(dataCache.entries.has(path));
  if(dataCache.entries.has(path)){setData(dataCache.entries.get(path) as T);setLoading(false);}
  function accept(result:T,stamp:string){if(dataCache.userId!==user.id)return false;dataCache.entries.set(path,result);setData(result);setHasData(true);setSynced(stamp);return true;}
  try{
   if(offline){const cached=await readSnapshot<T>(user.id,path);if(cached)accept(cached.data,cached.synced);else setError('Este ecrã ainda não tem dados guardados neste dispositivo.');setReadonly(true);}
   else{const result=await api<T>(path);if(accept(result,new Date().toISOString())){setReadonly(false);await saveSnapshot(user.id,path,result).catch(()=>{});}}
  }catch(e){setError((e as Error).message);if(e instanceof ApiError&&e.status===0){setReadonly(true);const cached=await readSnapshot<T>(user.id,path).catch(()=>undefined);if(cached)accept(cached.data,cached.synced);}}
  finally{setLoading(false);}
 },[path,user.id,offline,dataCache]);
 useEffect(()=>{void refresh();},[refresh]);
 return{data,error,loading,hasData,synced,readonly:readonly||offline,refresh};
}
