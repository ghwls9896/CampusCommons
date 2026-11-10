\set ON_ERROR_STOP on
BEGIN;
INSERT INTO auth.users(id,email,email_confirmed_at) VALUES
 ('11111111-1111-4111-8111-111111111111','a@example.test',now()),
 ('22222222-2222-4222-8222-222222222222','b@example.test',now()),
 ('33333333-3333-4333-8333-333333333333','unconfirmed@example.test',null);
DO $$ BEGIN
 IF (SELECT count(*) FROM public.player_progress) <> 3 THEN RAISE EXCEPTION 'bootstrap missing'; END IF;
 IF (SELECT array_agg(private.level_for_xp(x) ORDER BY x) FROM unnest(ARRAY[0,99,100,299,300,600]::bigint[])x) <> ARRAY[1,1,2,2,3,4] THEN RAISE EXCEPTION 'level thresholds'; END IF;
END $$;
SET LOCAL ROLE authenticated;
SELECT set_config('request.jwt.claim.sub','11111111-1111-4111-8111-111111111111',true);
DO $$ BEGIN
 IF (SELECT count(*) FROM public.player_progress) <> 1 THEN RAISE EXCEPTION 'RLS ownership'; END IF;
 BEGIN UPDATE public.player_progress SET total_xp=999; RAISE EXCEPTION 'XP write allowed'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 BEGIN INSERT INTO public.xp_events(user_id,source,source_event_id,delta) VALUES(auth.uid(),'quiz',gen_random_uuid(),500); RAISE EXCEPTION 'event write allowed'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 BEGIN PERFORM public.account_enter(auth.uid(),'테스트'); RAISE EXCEPTION 'privileged RPC allowed'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 BEGIN PERFORM private.award_activity(auth.uid(),gen_random_uuid(),'quiz'); RAISE EXCEPTION 'private RPC allowed'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;
SELECT set_config('request.jwt.claim.sub','33333333-3333-4333-8333-333333333333',true);
DO $$ BEGIN IF (SELECT count(*) FROM public.player_progress) <> 0 THEN RAISE EXCEPTION 'unconfirmed read'; END IF; END $$;
SET LOCAL ROLE anon;
DO $$ BEGIN
 BEGIN PERFORM * FROM public.profiles; RAISE EXCEPTION 'anon read allowed'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;
RESET ROLE;
SET LOCAL ROLE service_role;
SELECT public.account_enter('11111111-1111-4111-8111-111111111111','새닉네임');
DO $$ BEGIN
 BEGIN PERFORM public.account_enter('11111111-1111-4111-8111-111111111111','<b>가</b>'); RAISE EXCEPTION 'markup allowed'; EXCEPTION WHEN invalid_parameter_value THEN NULL; END;
 BEGIN PERFORM public.account_enter('33333333-3333-4333-8333-333333333333','미확인'); RAISE EXCEPTION 'unconfirmed enter'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;
INSERT INTO private.activity_runs(id,user_id,source,status,reward_xp) VALUES
 ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','11111111-1111-4111-8111-111111111111','quiz','verified',150),
 ('bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb','22222222-2222-4222-8222-222222222222','quiz','verified',150),
 ('cccccccc-cccc-4ccc-8ccc-cccccccccccc','11111111-1111-4111-8111-111111111111','quiz','pending',150);
DO $$ DECLARE r jsonb; BEGIN
 BEGIN PERFORM private.award_activity('11111111-1111-4111-8111-111111111111','bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb','quiz'); RAISE EXCEPTION 'stolen activity'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 BEGIN PERFORM private.award_activity('11111111-1111-4111-8111-111111111111','cccccccc-cccc-4ccc-8ccc-cccccccccccc','quiz'); RAISE EXCEPTION 'pending activity'; EXCEPTION WHEN invalid_parameter_value THEN NULL; END;
 r:=private.award_activity('11111111-1111-4111-8111-111111111111','aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','quiz');
 IF (r->>'total_xp')::bigint<>150 OR (r->>'level')::int<>2 OR (r->>'awarded_xp')::int<>150 THEN RAISE EXCEPTION 'award result %',r; END IF;
 r:=private.award_activity('11111111-1111-4111-8111-111111111111','aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','quiz');
 IF (r->>'total_xp')::bigint<>150 OR (r->>'awarded_xp')::int<>0 OR NOT (r->>'already_awarded')::boolean THEN RAISE EXCEPTION 'duplicate %',r; END IF;
 IF (SELECT count(*) FROM public.xp_events) <> 1 THEN RAISE EXCEPTION 'event duplication'; END IF;
END $$;
RESET ROLE;
ROLLBACK;
\echo ACCOUNT_SQL_PASS
