-- Account data. Passwords remain exclusively in Supabase Auth.
CREATE SCHEMA IF NOT EXISTS private;
REVOKE ALL ON SCHEMA private FROM PUBLIC, anon;
GRANT USAGE ON SCHEMA private TO authenticated, service_role;

CREATE FUNCTION private.level_for_xp(xp bigint) RETURNS integer
LANGUAGE sql IMMUTABLE STRICT SET search_path = '' AS $$
 SELECT floor((1 + sqrt(1 + xp::numeric / 12.5)) / 2)::integer
$$;
REVOKE ALL ON FUNCTION private.level_for_xp(bigint) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION private.level_for_xp(bigint) TO service_role;

CREATE TABLE public.profiles (
 user_id uuid PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
 last_nickname text NOT NULL DEFAULT '',
 created_at timestamptz NOT NULL DEFAULT now(),
 updated_at timestamptz NOT NULL DEFAULT now(),
 CONSTRAINT nickname_valid CHECK (last_nickname = '' OR
  (char_length(last_nickname) BETWEEN 2 AND 12 AND last_nickname = normalize(btrim(last_nickname), NFC)
   AND last_nickname !~ '[[:cntrl:]<>]'))
);
CREATE TABLE public.player_progress (
 user_id uuid PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
 total_xp bigint NOT NULL DEFAULT 0 CHECK (total_xp BETWEEN 0 AND 1000000000000),
 level integer GENERATED ALWAYS AS (private.level_for_xp(total_xp)) STORED,
 updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE public.xp_events (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
 source text NOT NULL CHECK (source IN ('quiz','escape','study')),
 source_event_id uuid NOT NULL,
 delta bigint NOT NULL CHECK (delta BETWEEN 1 AND 10000),
 policy_version integer NOT NULL DEFAULT 1 CHECK (policy_version > 0),
 created_at timestamptz NOT NULL DEFAULT now(),
 UNIQUE(user_id,source,source_event_id)
);
-- Only trusted server integrations can create/finalize these records.
CREATE TABLE private.activity_runs (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
 source text NOT NULL CHECK (source IN ('quiz','escape','study')),
 status text NOT NULL DEFAULT 'pending' CHECK (status IN ('pending','verified')),
 reward_xp bigint NOT NULL CHECK (reward_xp BETWEEN 1 AND 10000),
 policy_version integer NOT NULL DEFAULT 1 CHECK (policy_version > 0),
 created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX activity_runs_user_id ON private.activity_runs(user_id);
CREATE INDEX xp_events_user_created ON public.xp_events(user_id,created_at DESC);
ALTER TABLE public.profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.player_progress ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.xp_events ENABLE ROW LEVEL SECURITY;
ALTER TABLE private.activity_runs ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON public.profiles, public.player_progress, public.xp_events FROM PUBLIC, anon, authenticated;
REVOKE ALL ON private.activity_runs FROM PUBLIC, anon, authenticated;
GRANT SELECT ON public.profiles, public.player_progress, public.xp_events TO authenticated;
GRANT SELECT, INSERT, UPDATE ON public.profiles, public.player_progress TO service_role;
GRANT SELECT, INSERT ON public.xp_events TO service_role;
GRANT SELECT, INSERT, UPDATE ON private.activity_runs TO service_role;
GRANT SELECT(id,email_confirmed_at,is_anonymous) ON auth.users TO service_role;

-- Narrow RLS helper: only checks the current JWT user, never arbitrary IDs.
CREATE FUNCTION private.current_user_confirmed() RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER SET search_path = '' AS $$
 SELECT EXISTS(SELECT 1 FROM auth.users
 WHERE id = (SELECT auth.uid()) AND email_confirmed_at IS NOT NULL AND NOT coalesce(is_anonymous,false))
$$;
REVOKE ALL ON FUNCTION private.current_user_confirmed() FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION private.current_user_confirmed() TO authenticated;
CREATE POLICY own_profile ON public.profiles FOR SELECT TO authenticated
 USING ((SELECT auth.uid()) = user_id AND (SELECT private.current_user_confirmed()));
CREATE POLICY own_progress ON public.player_progress FOR SELECT TO authenticated
 USING ((SELECT auth.uid()) = user_id AND (SELECT private.current_user_confirmed()));
CREATE POLICY own_events ON public.xp_events FOR SELECT TO authenticated
 USING ((SELECT auth.uid()) = user_id AND (SELECT private.current_user_confirmed()));

-- Trigger-only function, not an RPC. The auth service inserts the source row.
CREATE FUNCTION private.bootstrap_account() RETURNS trigger
LANGUAGE plpgsql SECURITY DEFINER SET search_path = '' AS $$
BEGIN
 INSERT INTO public.profiles(user_id) VALUES(NEW.id) ON CONFLICT DO NOTHING;
 INSERT INTO public.player_progress(user_id) VALUES(NEW.id) ON CONFLICT DO NOTHING;
 RETURN NEW;
END $$;
REVOKE ALL ON FUNCTION private.bootstrap_account() FROM PUBLIC, anon, authenticated, service_role;
CREATE TRIGGER account_created AFTER INSERT ON auth.users
 FOR EACH ROW EXECUTE FUNCTION private.bootstrap_account();
-- Also supports accounts created before this migration.
INSERT INTO public.profiles(user_id) SELECT id FROM auth.users ON CONFLICT DO NOTHING;
INSERT INTO public.player_progress(user_id) SELECT id FROM auth.users ON CONFLICT DO NOTHING;

CREATE FUNCTION private.ensure_account(p_user_id uuid) RETURNS void
LANGUAGE plpgsql SECURITY INVOKER SET search_path = '' AS $$
BEGIN
 IF NOT EXISTS(SELECT 1 FROM auth.users WHERE id=p_user_id AND email_confirmed_at IS NOT NULL AND NOT coalesce(is_anonymous,false))
 THEN RAISE insufficient_privilege USING MESSAGE='confirmed account required'; END IF;
 INSERT INTO public.profiles(user_id) VALUES(p_user_id) ON CONFLICT DO NOTHING;
 INSERT INTO public.player_progress(user_id) VALUES(p_user_id) ON CONFLICT DO NOTHING;
END $$;
CREATE FUNCTION private.progress_json(p_user_id uuid) RETURNS jsonb
LANGUAGE sql STABLE SECURITY INVOKER SET search_path = '' AS $$
 SELECT jsonb_build_object('user_id',p.user_id,'nickname',p.last_nickname,
 'total_xp',g.total_xp,'level',g.level,'next_level_total_xp',50::bigint*g.level*(g.level+1))
 FROM public.profiles p JOIN public.player_progress g USING(user_id) WHERE p.user_id=p_user_id
$$;
CREATE FUNCTION public.account_read(p_user_id uuid) RETURNS jsonb
LANGUAGE plpgsql SECURITY INVOKER SET search_path = '' AS $$
BEGIN
 PERFORM private.ensure_account(p_user_id);
 RETURN private.progress_json(p_user_id);
END $$;
CREATE FUNCTION public.account_enter(p_user_id uuid,p_nickname text) RETURNS jsonb
LANGUAGE plpgsql SECURITY INVOKER SET search_path = '' AS $$
BEGIN
 IF p_nickname IS NULL OR char_length(p_nickname) NOT BETWEEN 2 AND 12 OR
 p_nickname <> normalize(btrim(p_nickname),NFC) OR p_nickname ~ '[[:cntrl:]<>]'
 THEN RAISE invalid_parameter_value USING MESSAGE='invalid nickname'; END IF;
 PERFORM private.ensure_account(p_user_id);
 UPDATE public.profiles SET last_nickname=p_nickname,updated_at=now() WHERE user_id=p_user_id;
 RETURN private.progress_json(p_user_id);
END $$;

CREATE FUNCTION private.award_activity(p_user_id uuid,p_activity_id uuid,p_source text) RETURNS jsonb
LANGUAGE plpgsql SECURITY INVOKER SET search_path = '' AS $$
DECLARE run private.activity_runs; inserted_count integer; awarded bigint:=0;
BEGIN
 PERFORM private.ensure_account(p_user_id);
 -- Lock per-user FIRST, then activity: stable order also prevents lost updates.
 PERFORM 1 FROM public.player_progress WHERE user_id=p_user_id FOR UPDATE;
 SELECT * INTO run FROM private.activity_runs WHERE id=p_activity_id FOR UPDATE;
 IF NOT FOUND OR run.user_id<>p_user_id OR run.source<>p_source
 THEN RAISE insufficient_privilege USING MESSAGE='activity not owned'; END IF;
 IF run.status<>'verified' THEN RAISE invalid_parameter_value USING MESSAGE='activity not verified'; END IF;
 INSERT INTO public.xp_events(user_id,source,source_event_id,delta,policy_version)
 VALUES(p_user_id,run.source,run.id,run.reward_xp,run.policy_version) ON CONFLICT DO NOTHING;
 GET DIAGNOSTICS inserted_count=ROW_COUNT;
 IF inserted_count=1 THEN
  awarded:=run.reward_xp;
  UPDATE public.player_progress SET total_xp=total_xp+awarded,updated_at=now() WHERE user_id=p_user_id;
 END IF;
 RETURN private.progress_json(p_user_id) || jsonb_build_object('awarded_xp',awarded,'already_awarded',inserted_count=0);
END $$;
CREATE FUNCTION public.account_award(p_user_id uuid,p_activity_id uuid,p_source text) RETURNS jsonb
LANGUAGE sql SECURITY INVOKER SET search_path = '' AS $$
 SELECT private.award_activity(p_user_id,p_activity_id,p_source)
$$;
-- Public-schema wrappers exist for PostgREST but are callable ONLY by the server.
REVOKE ALL ON FUNCTION private.ensure_account(uuid), private.progress_json(uuid), private.award_activity(uuid,uuid,text),
 public.account_read(uuid), public.account_enter(uuid,text), public.account_award(uuid,uuid,text)
 FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION private.ensure_account(uuid), private.progress_json(uuid), private.award_activity(uuid,uuid,text),
 public.account_read(uuid), public.account_enter(uuid,text), public.account_award(uuid,uuid,text) TO service_role;
