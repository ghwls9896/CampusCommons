"""Creates/removes one disposable DB on an explicitly local PostgreSQL test server.
ACCOUNT_TEST_DATABASE_URL supplies the local maintenance connection, never hosted Supabase.
"""
import json
import os
from pathlib import Path
import subprocess
from urllib.parse import urlsplit, urlunsplit
import uuid

ROOT = Path(__file__).resolve().parents[2]
ADMIN = os.environ['ACCOUNT_TEST_DATABASE_URL']
parts = urlsplit(ADMIN)
if parts.hostname not in ('localhost', '127.0.0.1', '::1'):
    raise SystemExit('Only a local disposable PostgreSQL server is accepted')
name = 'campus_account_test_' + uuid.uuid4().hex
DSN = urlunsplit(parts._replace(path='/' + name))

def sql(command, expect_ok=True, admin=False):
    result = subprocess.run(['psql', '--dbname', ADMIN if admin else DSN, '-X', '-qAt', '-v', 'ON_ERROR_STOP=1'],
                            input=command, text=True, capture_output=True)
    if expect_ok and result.returncode:
        raise AssertionError(result.stderr)
    if not expect_ok and not result.returncode:
        raise AssertionError('Expected transaction to fail')
    return result.stdout.strip()

def snapshot():
    return sql("SELECT jsonb_build_object('progress',(SELECT jsonb_agg(to_jsonb(p) ORDER BY user_id) FROM public.player_progress p),'events',(SELECT jsonb_agg(to_jsonb(e) ORDER BY id) FROM public.xp_events e),'profiles',(SELECT jsonb_agg(to_jsonb(p) ORDER BY user_id) FROM public.profiles p))")

def main():
    sql('CREATE DATABASE '+name, admin=True)
    try:
        bootstrap=(ROOT/'Tools/account/bootstrap_test_db.sql').read_text()
        for role in ['anon','authenticated','service_role']:
            if sql("SELECT 1 FROM pg_roles WHERE rolname='"+role+"'",admin=True)=='1':
                bootstrap='\n'.join(line for line in bootstrap.splitlines() if not line.startswith('CREATE ROLE '+role+' '))
        sql(bootstrap)
        sql((ROOT/'supabase/migrations/20261109081917_account_progression.sql').read_text())
        migration=(ROOT/'supabase/migrations/20261112013634_standalone_account_level.sql').read_text()
        if not migration.strip():
            sql((ROOT/'supabase/tests/database/account_progression.test.sql').read_text())
        first=str(uuid.uuid4());second=str(uuid.uuid4());invalid=str(uuid.uuid4())
        for user in [first,second,invalid]:
            sql(f"INSERT INTO auth.users(id,email,email_confirmed_at) VALUES('{user}','test@example.test',now())")
        sql(f"UPDATE public.player_progress SET total_xp=1000 WHERE user_id='{first}'; UPDATE public.player_progress SET total_xp=10500 WHERE user_id='{second}'; UPDATE public.player_progress SET total_xp=12000 WHERE user_id='{invalid}';")
        before=snapshot()
        sql('BEGIN;'+migration+'COMMIT;',expect_ok=False)
        assert snapshot()==before, 'failed migration changed existing values'
        assert sql("SELECT is_generated FROM information_schema.columns WHERE table_schema='public' AND table_name='player_progress' AND column_name='level'")=='ALWAYS', 'failed migration changed schema'
        sql(f"DELETE FROM auth.users WHERE id='{invalid}'; INSERT INTO public.xp_events(user_id,source,source_event_id,delta) VALUES('{first}','quiz',gen_random_uuid(),150)")
        before=snapshot()
        sql('BEGIN;'+migration+'COMMIT;')
        assert snapshot()==before, 'migration lost existing level/XP/profile/event data'
        assert sql("SELECT is_generated FROM information_schema.columns WHERE table_schema='public' AND table_name='player_progress' AND column_name='level'")=='NEVER', 'level is still XP-generated'
        assert sql(f"SELECT level FROM public.player_progress WHERE user_id='{first}'")=='5'
        assert sql(f"SELECT level FROM public.player_progress WHERE user_id='{second}'")=='15'
        sql((ROOT/'supabase/tests/database/account_progression.test.sql').read_text())
        print('ACCOUNT_DATABASE_PASS: migration preservation/rollback, independent level1-15, ownership, default, nickname, disabled rewards')
    finally:
        sql('DROP DATABASE '+name,admin=True)

if __name__=='__main__':
    main()
