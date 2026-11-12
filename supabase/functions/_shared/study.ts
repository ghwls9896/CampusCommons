import type { Dependencies } from './handler.ts';

const states = new Set(['studying', 'phone', 'other', 'away', 'drowsy']);
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const maxBodyBytes = 65536;
const maxSegments = 128;
const maxSegmentMs = 120000;
const maxSequence = 2147483647;

class StudyInputError extends Error {
 constructor(readonly status = 400) { super('invalid study request'); }
}

function hasFields(value: unknown, fields: string[]): value is Record<string, unknown> {
 if (!value || typeof value !== 'object' || Array.isArray(value)) return false;
 return Object.keys(value).length === fields.length && fields.every(key => Object.hasOwn(value, key));
}

function integer(value: unknown): value is number {
 return typeof value === 'number' && Number.isSafeInteger(value) && value >= 0;
}

function reply(body: unknown, status = 200) {
 return new Response(JSON.stringify(body), {
  status,
  headers: { 'content-type': 'application/json', 'cache-control': 'no-store' },
 });
}

async function readBody(request: Request): Promise<unknown> {
 const reader = request.body?.getReader();
 if (!reader) throw new StudyInputError();

 const chunks: Uint8Array[] = [];
 let size = 0;
 while (true) {
  const part = await reader.read();
  if (part.done) break;
  size += part.value.length;
  if (size > maxBodyBytes) {
   await reader.cancel();
   throw new StudyInputError(413);
  }
  chunks.push(part.value);
 }

 const bytes = new Uint8Array(size);
 let offset = 0;
 for (const chunk of chunks) {
  bytes.set(chunk, offset);
  offset += chunk.length;
 }
 try {
  return JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(bytes));
 } catch {
  throw new StudyInputError();
 }
}

export function studyHandler(deps: Dependencies) {
 return async (request: Request): Promise<Response> => {
  if (request.method !== 'POST') return reply({ error: { code: 'method_not_allowed' } }, 405);
  let userId: string;
  try {
   userId = await deps.authenticate(request);
  } catch {
   return reply({ error: { code: 'unauthorized' } }, 401);
  }

  try {
   const body = await readBody(request);
   if (hasFields(body, ['action']) && body.action === 'summary') {
    return reply(await deps.rpc('study_summary', { p_user_id: userId }));
   }

   if (!hasFields(body, ['action', 'session_id', 'started_ms', 'ended_ms', 'segments']) ||
       body.action !== 'sync' || typeof body.session_id !== 'string' || !uuid.test(body.session_id) ||
       !integer(body.started_ms) || body.started_ms === 0 || !integer(body.ended_ms) ||
       !Array.isArray(body.segments) || body.segments.length > maxSegments) {
    throw new StudyInputError();
   }
   for (const segment of body.segments) {
    if (!hasFields(segment, ['sequence', 'start_ms', 'end_ms', 'state']) ||
        !integer(segment.sequence) || segment.sequence > maxSequence ||
        !integer(segment.start_ms) || !integer(segment.end_ms) ||
        segment.start_ms < body.started_ms || segment.end_ms <= segment.start_ms ||
        segment.end_ms - segment.start_ms > maxSegmentMs ||
        typeof segment.state !== 'string' || !states.has(segment.state)) {
     throw new StudyInputError();
    }
   }

   return reply(await deps.rpc('study_sync', {
    p_user_id: userId,
    p_session_id: body.session_id,
    p_started_ms: body.started_ms,
    p_ended_ms: body.ended_ms,
    p_segments: body.segments,
   }));
  } catch (error) {
   const code = (error as { code?: string })?.code;
   if (error instanceof StudyInputError && error.status === 413)
    return reply({ error: { code: 'request_too_large' } }, 413);
   if (error instanceof StudyInputError || code === '22023')
    return reply({ error: { code: 'invalid_record' } }, 400);
   if (code === '42501') return reply({ error: { code: 'forbidden' } }, 403);
   if (code === '23505') return reply({ error: { code: 'record_conflict' } }, 409);
   return reply({ error: { code: 'service_unavailable' } }, 503);
  }
 };
}
