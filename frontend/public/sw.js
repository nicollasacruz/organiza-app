const CACHE='organiza-static-v1';
const SHELL=['/','/ganhos/','/tarefas/','/configuracoes/','/membros/','/convite/','/manifest.webmanifest','/icon.svg'];
self.addEventListener('install',event=>event.waitUntil(caches.open(CACHE).then(cache=>cache.addAll(SHELL))));
self.addEventListener('activate',event=>event.waitUntil(caches.keys().then(keys=>Promise.all(keys.filter(key=>key!==CACHE).map(key=>caches.delete(key)))).then(()=>self.clients.claim())));
self.addEventListener('fetch',event=>{
 const request=event.request,url=new URL(request.url);
 if(request.method!=='GET'||url.origin!==self.location.origin||url.pathname.startsWith('/api/'))return;
 const staticAsset=url.pathname.startsWith('/_next/static/')||url.pathname.startsWith('/icons/')||url.pathname==='/icon.svg'||url.pathname==='/manifest.webmanifest';
 const route=SHELL.includes(url.pathname);
 if(!staticAsset&&!route)return;
 event.respondWith(fetch(request).then(response=>{if(response.ok){const clean=new Request(url.origin+url.pathname);void caches.open(CACHE).then(cache=>cache.put(clean,response.clone()));}return response;}).catch(async()=>{const cached=await caches.match(url.origin+url.pathname);return cached||Response.error();}));
});
