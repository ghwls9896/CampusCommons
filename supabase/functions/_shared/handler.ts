export interface Dependencies {
 authenticate(request: Request): Promise<string>;
 rpc(name: string,args: Record<string,unknown>): Promise<unknown>;
 rewardSources: Set<string>;
}
class InputError extends Error { constructor(public status:number,public code:string){super(code);} }
function reply(data:unknown,status=200):Response {return Response.json(data,{status,headers:{'Cache-Control':'no-store'}});}
async function readBody(req:Request):Promise<Record<string,unknown>> {
 const reader=req.body?.getReader();if(!reader)throw new InputError(400,'invalid_request');
 let size=0;const parts:Uint8Array[]=[];
 try { while(true){const {value,done}=await reader.read();if(done)break;size+=value.length;
  if(size>4096){await reader.cancel();throw new InputError(413,'request_too_large');}parts.push(value);}
 }finally{reader.releaseLock();}
 const bytes=new Uint8Array(size);let offset=0;for(const p of parts){bytes.set(p,offset);offset+=p.length;}
 let data;try{data=JSON.parse(new TextDecoder('utf-8',{fatal:true}).decode(bytes));}catch{throw new InputError(400,'invalid_request');}
 if(!data||typeof data!=='object'||Array.isArray(data))throw new InputError(400,'invalid_request');return data;
}
function keys(data:Record<string,unknown>,expected:string[]) {
 if(Object.keys(data).length!==expected.length||expected.some(k=>!Object.hasOwn(data,k)))throw new InputError(400,'invalid_request');
}
export function handler(kind:'account'|'progression',deps:Dependencies) {
 return async(req:Request):Promise<Response>=>{
  if(req.method!=='POST')return reply({error:{code:'method_not_allowed'}},405);
  let userId:string;try{userId=await deps.authenticate(req);}catch{return reply({error:{code:'unauthorized'}},401);}
  if(kind==='progression')return reply({error:{code:'progression_not_enabled'}},409);
  try {
   const b=await readBody(req);
   {
    if(b.action==='read'){keys(b,['action']);return reply(await deps.rpc('account_read',{p_user_id:userId}));}
    if(b.action!=='enter')throw new InputError(400,'invalid_request');keys(b,['action','nickname']);
    if(typeof b.nickname!=='string')throw new InputError(400,'invalid_nickname');
    const name=b.nickname.normalize('NFC').trim();
    if([...name].length<2||[...name].length>12||/[\p{Cc}\p{Cf}\p{Zl}\p{Zp}<>]/u.test(name))throw new InputError(400,'invalid_nickname');
    return reply(await deps.rpc('account_enter',{p_user_id:userId,p_nickname:name}));
   }

  } catch(e) {
   const code=(e as {code?:string})?.code;
   const status=e instanceof InputError?e.status:code==='42501'?403:code==='22023'?400:503;
   return reply({error:{code:e instanceof InputError?e.code:status===403?'forbidden':status===400?'invalid_activity':'service_unavailable'}},status);
  }
 };
}
