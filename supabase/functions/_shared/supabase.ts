import type { Dependencies } from './handler.ts';
export interface Configuration {url:string;publicKey:string;serverKey:string;rewardSources?:string;}
export function supabaseDependencies(config:Configuration,fetcher:typeof fetch=fetch):Dependencies {
 const base=config.url.replace(/\/$/,'');
 async function request(path:string,key:string,token:string|undefined,body?:unknown) {
  if(!base||!key)throw Error('server configuration');
  const res=await fetcher(base+path,{method:body===undefined?'GET':'POST',headers:{apikey:key,...(token?{authorization:'Bearer '+token}:{}),'content-type':'application/json'},body:body===undefined?undefined:JSON.stringify(body),signal:AbortSignal.timeout(15000)});
  const data=await res.json();if(!res.ok)throw data;return data;
 }
 return {
  rewardSources:new Set((config.rewardSources??'').split(',').map(x=>x.trim()).filter(Boolean)),
  async authenticate(req) {
   const header=req.headers.get('authorization')??'';if(!/^Bearer \S+$/i.test(header))throw Error('auth');
   // Remote Auth verifies signature, expiry, deleted/banned users; never trust decoded JWT claims alone.
   const user=await request('/auth/v1/user',config.publicKey,header.slice(7));
   if(!user||typeof user.id!=='string'||!/^[0-9a-f-]{36}$/i.test(user.id)||!user.email_confirmed_at||user.is_anonymous)throw Error('auth');
   return user.id;
  },
  rpc:(name,args)=>request('/rest/v1/rpc/'+encodeURIComponent(name),config.serverKey,config.serverKey.startsWith('sb_secret_')?undefined:config.serverKey,args),
 };
}
export function configurationFromEnvironment(read:(key:string)=>string|undefined):Configuration {
 function named(key:string):string {try{const value=JSON.parse(read(key)??'{}')?.default;return typeof value==='string'?value:'';}catch{return '';}}
 return {url:read('SUPABASE_URL')??'',publicKey:named('SUPABASE_PUBLISHABLE_KEYS')||read('SUPABASE_ANON_KEY')||'',serverKey:named('SUPABASE_SECRET_KEYS')||read('SUPABASE_SERVICE_ROLE_KEY')||'',rewardSources:read('ACCOUNT_REWARD_SOURCES')??''};
}
export function environmentConfiguration():Configuration {return configurationFromEnvironment(key=>Deno.env.get(key));}
