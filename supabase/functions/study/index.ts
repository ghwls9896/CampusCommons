import { studyHandler } from '../_shared/study.ts';
import { supabaseDependencies, environmentConfiguration } from '../_shared/supabase.ts';
Deno.serve(studyHandler(supabaseDependencies(environmentConfiguration())));
