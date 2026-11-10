import { handler } from '../_shared/handler.ts';
import { environmentConfiguration, supabaseDependencies } from '../_shared/supabase.ts';
Deno.serve(handler('account',supabaseDependencies(environmentConfiguration())));
