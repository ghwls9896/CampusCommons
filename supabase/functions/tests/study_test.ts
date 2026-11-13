import { studyHandler } from '../_shared/study.ts';
import type { Dependencies } from '../_shared/handler.ts';
function eq(a:unknown,b:unknown){if(JSON.stringify(a)!==JSON.stringify(b))throw Error(JSON.stringify([a,b]));}
const uid='11111111-1111-4111-8111-111111111111',sid='22222222-2222-4222-8222-222222222222';
const body=()=>({action:'sync',session_id:sid,started_ms:1000,ended_ms:0,segments:[{sequence:0,start_ms:1000,end_ms:2000,state:'studying'}]});
function fixture(){const calls:unknown[]=[];const deps:Dependencies={authenticate:async()=>uid,rpc:async(n,a)=>{calls.push([n,a]);return {};},rewardSources:new Set()};return{calls,deps};}
function req(b:unknown){return new Request('https://example.test',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify(b)});}
Deno.test('study binds owner to verified identity, never body',async()=>{const f=fixture();eq((await studyHandler(f.deps)(req(body()))).status,200);eq((f.calls[0] as any)[1].p_user_id,uid);eq((await studyHandler(f.deps)(req({...body(),user_id:sid}))).status,400);});
Deno.test('study auth failure and methods fail closed',async()=>{const f=fixture();f.deps.authenticate=async()=>{throw Error();};eq((await studyHandler(f.deps)(req(body()))).status,401);eq(f.calls.length,0);eq((await studyHandler(f.deps)(new Request('https://e.test'))).status,405);});
Deno.test('study rejects malformed intervals and unknown states',async()=>{for(const s of [{sequence:0,start_ms:0,end_ms:2000,state:'studying'},{sequence:-1,start_ms:1000,end_ms:2000,state:'studying'},{sequence:0,start_ms:2000,end_ms:1000,state:'studying'},{sequence:0,start_ms:1000,end_ms:2000,state:'unknown'},{sequence:0,start_ms:1000,end_ms:2000,state:'studying',reward:99}]){const f=fixture();eq((await studyHandler(f.deps)(req({...body(),segments:[s]}))).status,400);eq(f.calls.length,0);}});
Deno.test('study summary has no target and errors hide internals',async()=>{const f=fixture();eq((await studyHandler(f.deps)(req({action:'summary'}))).status,200);f.deps.rpc=async()=>{throw {code:'23505',message:'private'};};const r=await studyHandler(f.deps)(req(body()));eq(r.status,409);eq((await r.text()).includes('private'),false);});
Deno.test('study enforces byte and segment limits',async()=>{const f=fixture();eq((await studyHandler(f.deps)(req({...body(),segments:Array(129).fill(body().segments[0])}))).status,400);eq((await studyHandler(f.deps)(req({x:'a'.repeat(70000)}))).status,413);});
Deno.test('network type error stays retryable',async()=>{
 const f=fixture();f.deps.rpc=async()=>{throw new TypeError('fetch failed');};
 eq((await studyHandler(f.deps)(req(body()))).status,503);
});
Deno.test('study session and sequence types match database columns',async()=>{
 const f=fixture();eq((await studyHandler(f.deps)(req({...body(),session_id:[sid]}))).status,400);
 eq((await studyHandler(f.deps)(req({...body(),segments:[{...body().segments[0],sequence:2147483648}]}))).status,400);
 eq(f.calls.length,0);
});
