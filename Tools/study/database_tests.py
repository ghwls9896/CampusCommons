import importlib.util, os
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('account_db',ROOT/'Tools/account/run_database_tests.py');db=importlib.util.module_from_spec(spec);spec.loader.exec_module(db)
sql=db.sql
sql('CREATE DATABASE '+db.name,admin=True)
try:
 b=(ROOT/'Tools/account/bootstrap_test_db.sql').read_text()
 for r in ['anon','authenticated','service_role']:
  if sql("select 1 from pg_roles where rolname='"+r+"'",admin=True)=='1':b='\n'.join(x for x in b.splitlines() if not x.startswith('CREATE ROLE '+r+' '))
 sql(b)
 for p in sorted((ROOT/'supabase/migrations').glob('*.sql')):sql('BEGIN;'+p.read_text()+'COMMIT;')
 u='11111111-1111-4111-8111-111111111111';v='22222222-2222-4222-8222-222222222222';s='33333333-3333-4333-8333-333333333333'
 sql(f"insert into auth.users(id,email,email_confirmed_at) values('{u}','a@example.test',now()),('{v}','b@example.test',now())")
 day=int(sql("select (floor((extract(epoch from now())+32400)/86400)*86400000-32400000)::bigint"));start=day-10000
 def sync(user=u,seg=None,sid=s,end=0):
  import json
  seg=seg if seg is not None else [{'sequence':0,'start_ms':start,'end_ms':day+1000,'state':'studying'}]
  return f"set role service_role;select public.study_sync('{user}','{sid}',{start},'{json.dumps(seg)}'::jsonb,{end});"
 sql(sync());sql(sync());assert sql(f"select count(*) from public.study_segments where session_id='{s}'")=='1'
 import json
 summary=json.loads(sql(f"set role service_role; select public.study_summary('{u}')"));assert summary['today_ms']==1000
 sql(sync(user=v),expect_ok=False)
 sql(sync(seg=[{'sequence':0,'start_ms':start,'end_ms':day+2000,'state':'studying'}]),expect_ok=False)
 sql(sync(seg=[{'sequence':2,'start_ms':day+1000,'end_ms':day+2000,'state':'phone'}]),expect_ok=False)
 sql(sync(seg=[{'sequence':1,'start_ms':day,'end_ms':day+2000,'state':'phone'}]),expect_ok=False)
 sql(sync(sid='44444444-4444-4444-8444-444444444444'),expect_ok=False)
 sql(sync(seg=[],end=day+1000));sql(sync(seg=[],end=day+1000))
 sql(sync(seg=[{'sequence':1,'start_ms':day+1000,'end_ms':day+2000,'state':'phone'}],end=day+1000),expect_ok=False)
 assert sql(f"set role authenticated;set request.jwt.claim.sub='{v}';select count(*) from public.study_segments")=='0'
 assert sql(f"set role authenticated;set request.jwt.claim.sub='{u}';select count(*) from public.study_segments")=='1'
 sql(f"set role authenticated;select public.study_summary('{u}')",expect_ok=False)
 assert sql(f"select level from public.player_progress where user_id='{u}'")=='1'
 print('STUDY_DATABASE_PASS: owner, retry, payload conflict, sequence, overlap, multi-session, finalization, midnight, RLS, permissions, levels')
finally:sql('DROP DATABASE '+db.name,admin=True)
