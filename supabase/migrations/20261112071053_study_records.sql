CREATE TABLE public.study_sessions (
 id uuid PRIMARY KEY,
 user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
 started_ms bigint NOT NULL CHECK (started_ms > 0),
 ended_ms bigint CHECK (ended_ms >= started_ms),
 last_sequence integer NOT NULL DEFAULT -1,
 created_at timestamptz NOT NULL DEFAULT now(),
 UNIQUE(id,user_id)
);
CREATE TABLE public.study_segments (
 session_id uuid NOT NULL,
 user_id uuid NOT NULL,
 sequence integer NOT NULL CHECK(sequence >= 0),
 start_ms bigint NOT NULL,
 end_ms bigint NOT NULL CHECK(end_ms > start_ms AND end_ms-start_ms <= 120000),
 state text NOT NULL CHECK(state IN ('studying','phone','other','away','drowsy')),
 PRIMARY KEY(session_id,sequence),
 FOREIGN KEY(session_id,user_id) REFERENCES public.study_sessions(id,user_id) ON DELETE CASCADE
);
CREATE INDEX study_sessions_owner ON public.study_sessions(user_id,started_ms DESC);
CREATE INDEX study_segments_owner_time ON public.study_segments(user_id,end_ms,start_ms);
ALTER TABLE public.study_sessions ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.study_segments ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON public.study_sessions,public.study_segments FROM PUBLIC,anon,authenticated;
GRANT SELECT ON public.study_sessions,public.study_segments TO authenticated;
GRANT SELECT,INSERT,UPDATE ON public.study_sessions TO service_role;
GRANT SELECT,INSERT ON public.study_segments TO service_role;
CREATE POLICY own_study_sessions ON public.study_sessions FOR SELECT TO authenticated
 USING ((SELECT auth.uid())=user_id AND (SELECT private.current_user_confirmed()));
CREATE POLICY own_study_segments ON public.study_segments FOR SELECT TO authenticated
 USING ((SELECT auth.uid())=user_id AND (SELECT private.current_user_confirmed()));

CREATE FUNCTION public.study_summary(p_user_id uuid) RETURNS jsonb
LANGUAGE plpgsql SECURITY INVOKER SET search_path='' AS $$
DECLARE day_start bigint; total bigint; history jsonb;
BEGIN
 PERFORM private.ensure_account(p_user_id);
 day_start := (floor((extract(epoch from now())+32400)/86400)*86400000-32400000)::bigint;
 SELECT coalesce(sum(greatest(0,least(end_ms,day_start+86400000)-greatest(start_ms,day_start))),0)
 INTO total FROM public.study_segments WHERE user_id=p_user_id AND state='studying' AND end_ms>day_start AND start_ms<day_start+86400000;
 SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY started_ms DESC),'[]'::jsonb) INTO history FROM (
  SELECT s.id::text AS session_id,s.started_ms,coalesce(s.ended_ms,0) AS ended_ms,
   (SELECT coalesce(sum(end_ms-start_ms),0) FROM public.study_segments g WHERE g.session_id=s.id AND g.state='studying') AS study_ms,
   (SELECT coalesce(sum(end_ms-start_ms),0) FROM public.study_segments g WHERE g.session_id=s.id AND g.state='phone') AS phone_ms,
   (SELECT coalesce(sum(end_ms-start_ms),0) FROM public.study_segments g WHERE g.session_id=s.id AND g.state='other') AS other_ms,
   (SELECT coalesce(sum(end_ms-start_ms),0) FROM public.study_segments g WHERE g.session_id=s.id AND g.state='away') AS away_ms,
   (SELECT coalesce(sum(end_ms-start_ms),0) FROM public.study_segments g WHERE g.session_id=s.id AND g.state='drowsy') AS drowsy_ms
  FROM public.study_sessions s WHERE s.user_id=p_user_id ORDER BY s.started_ms DESC LIMIT 20
 )t;
 RETURN jsonb_build_object('user_id',p_user_id,'day_ms',day_start,'today_ms',total,'sessions',history);
END $$;
CREATE FUNCTION public.study_sync(p_user_id uuid,p_session_id uuid,p_started_ms bigint,p_segments jsonb,p_ended_ms bigint DEFAULT 0) RETURNS jsonb
LANGUAGE plpgsql SECURITY INVOKER SET search_path='' AS $$
DECLARE r public.study_sessions; item jsonb; old public.study_segments; seq integer; a bigint; b bigint; kind text; last_end bigint; now_ms bigint;
BEGIN
 PERFORM private.ensure_account(p_user_id);
 -- Serialize all this user's sessions: two devices cannot credit overlapping wall time.
 PERFORM 1 FROM public.profiles WHERE user_id=p_user_id FOR UPDATE;
 now_ms := (extract(epoch from now())*1000)::bigint;
 IF p_session_id IS NULL OR p_started_ms IS NULL OR p_started_ms<=0 OR p_started_ms>now_ms+300000
 OR p_ended_ms IS NULL OR p_ended_ms<0 OR jsonb_typeof(p_segments) IS DISTINCT FROM 'array' OR jsonb_array_length(p_segments)>128
 THEN RAISE invalid_parameter_value USING MESSAGE='invalid study record'; END IF;
 INSERT INTO public.study_sessions(id,user_id,started_ms) VALUES(p_session_id,p_user_id,p_started_ms) ON CONFLICT DO NOTHING;
 SELECT * INTO r FROM public.study_sessions WHERE id=p_session_id FOR UPDATE;
 IF r.user_id<>p_user_id THEN RAISE insufficient_privilege USING MESSAGE='session not owned'; END IF;
 IF r.started_ms<>p_started_ms THEN RAISE unique_violation USING MESSAGE='session conflict'; END IF;
 SELECT coalesce(max(end_ms),p_started_ms) INTO last_end FROM public.study_segments WHERE session_id=p_session_id;
 FOR item IN SELECT value FROM jsonb_array_elements(p_segments) LOOP
  IF jsonb_typeof(item) IS DISTINCT FROM 'object' OR (SELECT count(*) FROM jsonb_object_keys(item))<>4 OR NOT item ?& ARRAY['sequence','start_ms','end_ms','state'] THEN RAISE invalid_parameter_value; END IF;
  IF (item->>'sequence') !~ '^[0-9]+$' OR (item->>'start_ms') !~ '^[0-9]+$' OR (item->>'end_ms') !~ '^[0-9]+$' THEN RAISE invalid_parameter_value; END IF;
  seq := (item->>'sequence')::integer; a := (item->>'start_ms')::bigint; b := (item->>'end_ms')::bigint; kind := item->>'state';
  IF seq IS NULL OR a IS NULL OR b IS NULL OR kind IS NULL OR a<p_started_ms OR b<=a OR b-a>120000 OR b>now_ms+300000 OR kind NOT IN ('studying','phone','other','away','drowsy') THEN RAISE invalid_parameter_value; END IF;
  SELECT * INTO old FROM public.study_segments WHERE session_id=p_session_id AND sequence=seq;
  IF FOUND THEN
   IF old.start_ms<>a OR old.end_ms<>b OR old.state<>kind THEN RAISE unique_violation USING MESSAGE='segment conflict'; END IF;
   CONTINUE;
  END IF;
  IF r.ended_ms IS NOT NULL OR seq<>r.last_sequence+1 OR a<last_end THEN RAISE invalid_parameter_value USING MESSAGE='segment order'; END IF;
  IF EXISTS(SELECT 1 FROM public.study_segments WHERE user_id=p_user_id AND session_id<>p_session_id AND start_ms<b AND end_ms>a) THEN RAISE unique_violation USING MESSAGE='overlapping sessions'; END IF;
  INSERT INTO public.study_segments VALUES(p_session_id,p_user_id,seq,a,b,kind);
  r.last_sequence:=seq;last_end:=b;
 END LOOP;
 IF p_ended_ms>0 THEN
  IF p_ended_ms<last_end OR p_ended_ms>now_ms+300000 OR (r.ended_ms IS NOT NULL AND r.ended_ms<>p_ended_ms) THEN RAISE invalid_parameter_value USING MESSAGE='invalid end'; END IF;
  r.ended_ms:=p_ended_ms;
 END IF;
 UPDATE public.study_sessions SET last_sequence=r.last_sequence,ended_ms=r.ended_ms WHERE id=p_session_id;
 RETURN public.study_summary(p_user_id)||jsonb_build_object('session_id',p_session_id,'ack_sequence',r.last_sequence);
END $$;
REVOKE ALL ON FUNCTION public.study_summary(uuid),public.study_sync(uuid,uuid,bigint,jsonb,bigint) FROM PUBLIC,anon,authenticated;
GRANT EXECUTE ON FUNCTION public.study_summary(uuid),public.study_sync(uuid,uuid,bigint,jsonb,bigint) TO service_role;
