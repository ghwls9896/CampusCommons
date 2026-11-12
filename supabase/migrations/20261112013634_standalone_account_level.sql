-- Preserve existing levels and history; reject incompatible legacy levels.
LOCK TABLE public.player_progress IN ACCESS EXCLUSIVE MODE;
DO $$ BEGIN
 IF EXISTS(SELECT 1 FROM public.player_progress WHERE level IS NULL OR level NOT BETWEEN 1 AND 15) THEN
  RAISE EXCEPTION 'Existing account levels must be within 1..15 before migration' USING ERRCODE='23514';
 END IF;
END $$;
ALTER TABLE public.player_progress ALTER COLUMN level DROP EXPRESSION;
ALTER TABLE public.player_progress ALTER COLUMN level SET DEFAULT 1;
ALTER TABLE public.player_progress ALTER COLUMN level SET NOT NULL;
ALTER TABLE public.player_progress ADD CONSTRAINT player_progress_level_range CHECK(level BETWEEN 1 AND 15);

CREATE OR REPLACE FUNCTION private.progress_json(p_user_id uuid) RETURNS jsonb
LANGUAGE sql STABLE SECURITY INVOKER SET search_path = '' AS $$
 SELECT jsonb_build_object('user_id',p.user_id,'nickname',p.last_nickname,'level',g.level)
 FROM public.profiles p JOIN public.player_progress g USING(user_id) WHERE p.user_id=p_user_id
$$;

-- Both entry points stay disabled until a future reviewed progression policy.
CREATE OR REPLACE FUNCTION private.award_activity(p_user_id uuid,p_activity_id uuid,p_source text) RETURNS jsonb
LANGUAGE plpgsql SECURITY INVOKER SET search_path = '' AS $$
BEGIN
 RAISE EXCEPTION 'progression_not_enabled' USING ERRCODE='55000';
END $$;
CREATE OR REPLACE FUNCTION public.account_award(p_user_id uuid,p_activity_id uuid,p_source text) RETURNS jsonb
LANGUAGE plpgsql SECURITY INVOKER SET search_path = '' AS $$
BEGIN
 RAISE EXCEPTION 'progression_not_enabled' USING ERRCODE='55000';
END $$;
