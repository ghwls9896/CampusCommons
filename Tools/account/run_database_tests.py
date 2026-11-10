"""Real PostgreSQL role/transaction tests, against a DISPOSABLE local test DB.

First apply bootstrap_test_db.sql (native PG only), then the migration.
Set ACCOUNT_TEST_DATABASE_URL; credentials are never printed.
"""
import concurrent.futures
import os
from pathlib import Path
import subprocess
import uuid

ROOT = Path(__file__).resolve().parents[2]
DSN = os.environ['ACCOUNT_TEST_DATABASE_URL']


def sql(command, expect_ok=True):
    env = {**os.environ, 'PGDATABASE': DSN}
    p = subprocess.run(['psql', '--dbname', DSN, '-X', '-qAt', '-v', 'ON_ERROR_STOP=1'], input=command,
                       text=True, capture_output=True, env=env)
    if expect_ok and p.returncode:
        raise AssertionError(p.stderr)
    if not expect_ok and not p.returncode:
        raise AssertionError('Expected transaction to fail')
    return p.stdout.strip()


def main():
    sql((ROOT / 'supabase/tests/database/account_progression.test.sql').read_text())
    user = str(uuid.uuid4())
    events = [str(uuid.uuid4()) for _ in range(5)]
    try:
        sql(f"INSERT INTO auth.users(id,email,email_confirmed_at) VALUES('{user}','concurrency@example.test',now());")
        for event in events:
            sql(f"INSERT INTO private.activity_runs(id,user_id,source,status,reward_xp) VALUES('{event}','{user}','quiz','verified',100);")

        def award(event):
            return sql(f"SET ROLE service_role; SELECT public.account_award('{user}','{event}','quiz');")

        with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
            list(pool.map(award, [events[0]] * 16))
            list(pool.map(award, events[1:4]))
        assert sql(f"SELECT total_xp FROM public.player_progress WHERE user_id='{user}'") == '400'
        assert sql(f"SELECT count(*) FROM public.xp_events WHERE user_id='{user}'") == '4'
        award(events[0])  # a response lost after commit can be retried
        assert sql(f"SELECT total_xp FROM public.player_progress WHERE user_id='{user}'") == '400'
        sql(f"UPDATE public.player_progress SET total_xp=1000000000000 WHERE user_id='{user}';")
        sql(f"SET ROLE service_role; SELECT public.account_award('{user}','{events[4]}','quiz');", expect_ok=False)
        assert sql(f"SELECT count(*) FROM public.xp_events WHERE source_event_id='{events[4]}'") == '0'
        assert sql(f"SELECT total_xp FROM public.player_progress WHERE user_id='{user}'") == '1000000000000'
    finally:
        sql(f"DELETE FROM auth.users WHERE id='{user}';")
    print('ACCOUNT_DATABASE_PASS: RLS, confirmation, bootstrap, levels, duplicate/concurrent awards, rollback')


if __name__ == '__main__':
    main()
