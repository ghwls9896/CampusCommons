\set ON_ERROR_STOP on
BEGIN;
INSERT INTO auth.users(id,email,email_confirmed_at) VALUES
 ('11111111-1111-4111-8111-111111111111','a@example.test',now()),
 ('22222222-2222-4222-8222-222222222222','b@example.test',now()),
 ('33333333-3333-4333-8333-333333333333','unconfirmed@example.test',null);
DO $$ DECLARE target integer; r jsonb; BEGIN
 IF (SELECT level FROM public.player_progress WHERE user_id='11111111-1111-4111-8111-111111111111')<>1 THEN RAISE EXCEPTION 'new account not level1'; END IF;
 FOREACH target IN ARRAY ARRAY[5,10,15] LOOP
  UPDATE public.player_progress SET level=target WHERE user_id='11111111-1111-4111-8111-111111111111';
  r:=public.account_enter('11111111-1111-4111-8111-111111111111','새닉네임');
  IF (r->>'level')::int<>target OR r->>'nickname'<>'새닉네임' OR r ? 'total_xp' OR r ? 'next_level_total_xp' THEN RAISE EXCEPTION 'invalid level-only profile %',r; END IF;
 END LOOP;
 UPDATE public.player_progress SET total_xp=999999 WHERE user_id='11111111-1111-4111-8111-111111111111';
 IF (SELECT level FROM public.player_progress WHERE user_id='11111111-1111-4111-8111-111111111111')<>15 THEN RAISE EXCEPTION 'XP changed level'; END IF;
 IF (SELECT level FROM public.player_progress WHERE user_id='22222222-2222-4222-8222-222222222222')<>1 THEN RAISE EXCEPTION 'other account changed'; END IF;
 FOREACH target IN ARRAY ARRAY[-1,0,16,100] LOOP
  BEGIN UPDATE public.player_progress SET level=target WHERE user_id='11111111-1111-4111-8111-111111111111'; RAISE EXCEPTION 'invalid level accepted'; EXCEPTION WHEN check_violation THEN NULL; END;
 END LOOP;
 BEGIN UPDATE public.player_progress SET level=NULL WHERE user_id='11111111-1111-4111-8111-111111111111'; RAISE EXCEPTION 'null level accepted'; EXCEPTION WHEN not_null_violation THEN NULL; END;
END $$;
SET LOCAL ROLE authenticated;
SELECT set_config('request.jwt.claim.sub','11111111-1111-4111-8111-111111111111',true);
DO $$ BEGIN
 IF (SELECT count(*) FROM public.player_progress)<>1 THEN RAISE EXCEPTION 'RLS ownership'; END IF;
 IF (SELECT level FROM public.player_progress)<>15 THEN RAISE EXCEPTION 'own level lost'; END IF;
 BEGIN UPDATE public.player_progress SET level=1; RAISE EXCEPTION 'client level write allowed'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 BEGIN UPDATE public.player_progress SET total_xp=999; RAISE EXCEPTION 'client XP write allowed'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 BEGIN PERFORM public.account_enter(auth.uid(),'테스트'); RAISE EXCEPTION 'client privileged RPC allowed'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 BEGIN INSERT INTO public.xp_events(user_id,source,source_event_id,delta) VALUES(auth.uid(),'quiz',gen_random_uuid(),100); RAISE EXCEPTION 'client event insert allowed'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;
SELECT set_config('request.jwt.claim.sub','22222222-2222-4222-8222-222222222222',true);
DO $$ BEGIN IF (SELECT count(*) FROM public.player_progress)<>1 OR (SELECT level FROM public.player_progress)<>1 THEN RAISE EXCEPTION 'second account isolation'; END IF; END $$;
SELECT set_config('request.jwt.claim.sub','33333333-3333-4333-8333-333333333333',true);
DO $$ BEGIN IF (SELECT count(*) FROM public.player_progress)<>0 THEN RAISE EXCEPTION 'unconfirmed read'; END IF; END $$;
SET LOCAL ROLE anon;
DO $$ BEGIN BEGIN PERFORM * FROM public.player_progress; RAISE EXCEPTION 'anon read allowed'; EXCEPTION WHEN insufficient_privilege THEN NULL; END; END $$;
RESET ROLE;
SET LOCAL ROLE service_role;
DO $$ BEGIN
 BEGIN PERFORM public.account_enter('33333333-3333-4333-8333-333333333333','미확인'); RAISE EXCEPTION 'unconfirmed enter'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 BEGIN PERFORM public.account_award('11111111-1111-4111-8111-111111111111',gen_random_uuid(),'quiz'); RAISE EXCEPTION 'public reward allowed'; EXCEPTION WHEN object_not_in_prerequisite_state THEN NULL; END;
 BEGIN PERFORM private.award_activity('11111111-1111-4111-8111-111111111111',gen_random_uuid(),'quiz'); RAISE EXCEPTION 'private reward allowed'; EXCEPTION WHEN object_not_in_prerequisite_state THEN NULL; END;
END $$;
RESET ROLE;
ROLLBACK;
\echo ACCOUNT_SQL_PASS
