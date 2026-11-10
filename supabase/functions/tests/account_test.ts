import { handler, type Dependencies } from '../_shared/handler.ts';
import { supabaseDependencies, configurationFromEnvironment } from '../_shared/supabase.ts';
function eq(a: unknown,b: unknown) { if(JSON.stringify(a)!==JSON.stringify(b)) throw Error(`${JSON.stringify(a)} != ${JSON.stringify(b)}`); }
const uid='11111111-1111-4111-8111-111111111111';
function fixture() {
 const calls: unknown[]=[];
 const deps:Dependencies={ authenticate:async()=>uid, rpc:async(name,args)=>{calls.push({name,args});return {user_id:uid,total_xp:0,level:1};}, rewardSources:new Set() };
 return {calls,deps};
}
function req(body:unknown,token='valid') { return new Request('https://example.test/functions/v1/account',{method:'POST',headers:{authorization:`Bearer ${token}`,'content-type':'application/json'},body:JSON.stringify(body)}); }
Deno.test('enter uses verified identity and normalizes nickname',async()=>{
 const {deps,calls}=fixture(); const res=await handler('account',deps)(req({action:'enter',nickname:'  가나다  '}));eq(res.status,200);
 eq(calls,[{name:'account_enter',args:{p_user_id:uid,p_nickname:'가나다'}}]);eq(res.headers.get('cache-control'),'no-store');
});
Deno.test('read has no caller supplied target',async()=>{
 const {deps,calls}=fixture();eq((await handler('account',deps)(req({action:'read'}))).status,200);
 eq(calls,[{name:'account_read',args:{p_user_id:uid}}]);
});
Deno.test('invalid nicknames and excess fields never write',async()=>{
 for(const b of [{action:'enter',nickname:'a'},{action:'enter',nickname:'<b>닉</b>'},{action:'enter',nickname:'닉\n네임'},{action:'enter',nickname:'가'.repeat(13)},{action:'enter',nickname:'닉네임',user_id:uid},{action:'read',delta:999},{action:'enter',nickname:'닉\u202E네임'}]) {
  const {deps,calls}=fixture();eq((await handler('account',deps)(req(b))).status,400);eq(calls.length,0);
 }
});
Deno.test('no auth, malformed JSON, oversized body and wrong method fail closed',async()=>{
 const {deps,calls}=fixture();deps.authenticate=async()=>{throw Error('auth');};
 eq((await handler('account',deps)(req({action:'read'}))).status,401);eq(calls.length,0);
 const f=fixture();eq((await handler('account',f.deps)(new Request('https://e.test',{method:'GET'}))).status,405);
 eq((await handler('account',f.deps)(new Request('https://e.test',{method:'POST',body:'{'}))).status,400);
 eq((await handler('account',f.deps)(req({action:'enter',nickname:'가'.repeat(5000)}))).status,413);
});
Deno.test('production has no test reward route; sources disabled by default',async()=>{
 const {deps,calls}=fixture();eq((await handler('progression',deps)(req({source:'quiz',activity_id:uid}))).status,409);eq(calls.length,0);
 eq((await handler('progression',deps)(req({source:'test',activity_id:uid,delta:100}))).status,400);
 deps.rewardSources=new Set(['quiz']);eq((await handler('progression',deps)(req({source:'quiz',activity_id:uid}))).status,200);
 eq(calls,[{name:'account_award',args:{p_user_id:uid,p_activity_id:uid,p_source:'quiz'}}]);
});
Deno.test('database ownership and pending rejection are stable; internal errors are hidden',async()=>{
 const f=fixture();f.deps.rpc=async()=>{throw {code:'42501'};};eq((await handler('account',f.deps)(req({action:'read'}))).status,403);
 f.deps.rpc=async()=>{throw {code:'22023'};};eq((await handler('account',f.deps)(req({action:'read'}))).status,400);
 f.deps.rpc=async()=>{throw Error('secret database connection string');};const r=await handler('account',f.deps)(req({action:'read'}));eq(r.status,503);eq((await r.text()).includes('secret'),false);
});
Deno.test('auth transport rejects invalid, unconfirmed and anonymous users',async()=>{
 for(const user of [{id:uid,email_confirmed_at:null},{id:uid,email_confirmed_at:'date',is_anonymous:true},{id:'invalid',email_confirmed_at:'date'}]) {
  const deps=supabaseDependencies({url:'https://project.supabase.co',publicKey:'public',serverKey:'server'},async()=>Response.json(user));
  let rejected=false;try{await deps.authenticate(req({}));}catch{rejected=true;}eq(rejected,true);
 }
});
Deno.test('auth sends bearer to Auth, service key only to RPC, no untrusted host',async()=>{
 const calls:{url:string,headers:Headers}[]=[];
 const deps=supabaseDependencies({url:'https://project.supabase.co',publicKey:'public',serverKey:'server'},async(input,init)=>{
  calls.push({url:String(input),headers:new Headers(init?.headers)});
  return Response.json(String(input).includes('/auth/')?{id:uid,email_confirmed_at:'date',is_anonymous:false}:{level:1});
 });
 eq(await deps.authenticate(req({})),uid);await deps.rpc('account_read',{p_user_id:uid});
 eq(calls[0].url,'https://project.supabase.co/auth/v1/user');eq(calls[0].headers.get('authorization'),'Bearer valid');eq(calls[0].headers.get('apikey'),'public');
 eq(calls[1].headers.get('authorization'),'Bearer server');eq(calls[1].headers.get('apikey'),'server');
});

Deno.test('current named publishable/secret keys work without treating secret as a JWT',async()=>{
 const env:Record<string,string>={SUPABASE_URL:'https://project.supabase.co',SUPABASE_PUBLISHABLE_KEYS:'{"default":"sb_publishable_test"}',SUPABASE_SECRET_KEYS:'{"default":"sb_secret_test"}'};
 const config=configurationFromEnvironment(k=>env[k]);eq(config.publicKey,'sb_publishable_test');eq(config.serverKey,'sb_secret_test');
 const deps=supabaseDependencies(config,async(_input,init)=>{const h=new Headers(init?.headers);eq(h.get('apikey'),'sb_secret_test');eq(h.has('authorization'),false);return Response.json({level:1});});
 await deps.rpc('account_read',{p_user_id:uid});
 const legacy=configurationFromEnvironment(k=>({SUPABASE_ANON_KEY:'anon',SUPABASE_SERVICE_ROLE_KEY:'server'} as Record<string,string>)[k]);eq(legacy.publicKey,'anon');eq(legacy.serverKey,'server');
});
