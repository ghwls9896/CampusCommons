import type { Dependencies } from './handler.ts';
const states=new Set(['studying','phone','other','away','drowsy']);
const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
function exact(b:Record<string,unknown>,ks:string[]){return Object.keys(b).length===ks.length&&ks.every(k=>Object.hasOwn(b,k));}
function integer(x:unknown){return typeof x==='number'&&Number.isSafeInteger(x)&&x>=0;}
function reply(body:unknown,status=200){return new Response(JSON.stringify(body),{status,headers:{'content-type':'application/json','cache-control':'no-store'}});}
export function studyHandler(deps:Dependencies){return async(req:Request)=>{
 if(req.method!=='POST')return reply({error:{code:'method_not_allowed'}},405);
 let user:string;try{user=await deps.authenticate(req);}catch{return reply({error:{code:'unauthorized'}},401);}
 try{
  const reader=req.body?.getReader();if(!reader)return reply({error:{code:'invalid_request'}},400);
  const chunks:Uint8Array[]=[];let size=0;while(true){const r=await reader.read();if(r.done)break;size+=r.value.length;if(size>65536){await reader.cancel();return reply({error:{code:'request_too_large'}},413);}chunks.push(r.value);}
  const bytes=new Uint8Array(size);let at=0;for(const c of chunks){bytes.set(c,at);at+=c.length;}
  const b=JSON.parse(new TextDecoder('utf-8',{fatal:true}).decode(bytes));
  if(!b||typeof b!=='object'||Array.isArray(b))throw new TypeError();
  if(b.action==='summary'&&exact(b,['action']))return reply(await deps.rpc('study_summary',{p_user_id:user}));
  if(b.action!=='sync'||!exact(b,['action','session_id','started_ms','ended_ms','segments'])||!uuid.test(b.session_id)||!integer(b.started_ms)||!integer(b.ended_ms)||!Array.isArray(b.segments)||b.segments.length>128)throw new TypeError();
  for(const s of b.segments){if(!s||typeof s!=='object'||!exact(s,['sequence','start_ms','end_ms','state'])||!integer(s.sequence)||!integer(s.start_ms)||!integer(s.end_ms)||s.start_ms<b.started_ms||s.end_ms<=s.start_ms||s.end_ms-s.start_ms>120000||!states.has(s.state))throw new TypeError();}
  return reply(await deps.rpc('study_sync',{p_user_id:user,p_session_id:b.session_id,p_started_ms:b.started_ms,p_ended_ms:b.ended_ms,p_segments:b.segments}));
 }catch(e){const code=(e as {code?:string})?.code;const status=e instanceof SyntaxError||e instanceof TypeError||code==='22023'?400:code==='42501'?403:code==='23505'?409:503;
  return reply({error:{code:status===400?'invalid_record':status===403?'forbidden':status===409?'record_conflict':'service_unavailable'}},status);}
};}
