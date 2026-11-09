import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location('team_check', Path(__file__).parents[1] / 'check.py')
check = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(check)


def task():
    return dict(id='cc-12', title='Quiz feature', owner='HIGHCHOI', branch='feat/cc-12-quiz',
                issue='https://github.com/ghwls9896/CampusCommons/issues/12',
                scope=['Assets/QuizRoom/', 'Assets/QuizRoom.meta', 'backend/'],
                shared_files=[], depends_on=[], validation=['Compile and test quiz'])


class ScopeTests(unittest.TestCase):
    def test_task_schema_and_branch(self):
        self.assertEqual(check.task_errors(task(), 'feat/cc-12-quiz'), [])
        for field, value in [('owner', 'unknown'), ('branch', 'main'), ('scope', ['../outside']),
                             ('scope', ['*']), ('scope', ['Assets/']), ('scope', ['']),
                             ('validation', []), ('issue', 'https://evil.test/issues/12')]:
            with self.subTest(field=field, value=value):
                data = task(); data[field] = value
                self.assertTrue(check.task_errors(data, 'feat/cc-12-quiz'))

    def test_scope_is_path_boundary_not_arbitrary_prefix(self):
        self.assertTrue(check.in_scope('backend/app.py', ['backend/']))
        self.assertFalse(check.in_scope('backend-copy/app.py', ['backend/']))
        self.assertFalse(check.in_scope('backend/app.py.meta', ['backend/app.py']))

    def inspect(self, changes, head, base=None, data=None):
        return check.change_errors(data or task(), changes, head, base or {})

    def test_declared_scope_and_private_handoff_are_accepted(self):
        head={'backend/quiz.py': b'pass\n', 'docs/tasks/cc-12.json': b'{}', 'docs/tasks/cc-12.md': b'notes'}
        self.assertEqual(self.inspect([( 'A', p) for p in head],head), [])

    def test_out_of_scope_deletion_and_rename_source_fail(self):
        errors=self.inspect([('D','Assets/Characters/Player.cs'),('A','backend/player.py')],{'backend/player.py':b'pass'})
        self.assertTrue(any('scope' in e for e in errors))

    def test_shared_file_must_be_explicit(self):
        head={'backend/app.py':b'pass'}
        self.assertTrue(any('shared_files' in e for e in self.inspect([('M','backend/app.py')],head)))
        data=task();data['shared_files']=['backend/app.py']
        self.assertEqual(self.inspect([('M','backend/app.py')],head,data=data),[])

    def test_new_asset_and_new_directory_need_meta(self):
        path='Assets/QuizRoom/Q.cs'
        head={path:b'class Q {}'}
        self.assertTrue(any('.meta' in e for e in self.inspect([('A',path)],head)))
        head[path+'.meta']=b'guid: '+b'a'*32
        head['Assets/QuizRoom.meta']=b'guid: '+b'b'*32
        self.assertEqual(self.inspect([('A',p) for p in head],head),[])

    def test_existing_guid_cannot_change_and_new_guid_cannot_collide(self):
        path='Assets/QuizRoom/Q.cs.meta'
        base={path:b'guid: '+b'a'*32, 'Assets/QuizRoom/Q.cs':b'code'}
        head={**base,path:b'guid: '+b'b'*32}
        self.assertTrue(any('GUID changed' in e for e in self.inspect([('M',path)],head,base)))
        head={**base,'Assets/QuizRoom/R.cs':b'code','Assets/QuizRoom/R.cs.meta':base[path]}
        self.assertTrue(any('duplicate GUID' in e for e in self.inspect([('A','Assets/QuizRoom/R.cs.meta')],head,base)))

    def test_deleting_asset_requires_meta_deletion(self):
        path='Assets/QuizRoom/Q.cs.meta'
        self.assertTrue(any('orphan' in e for e in self.inspect([('D',path[:-5])],{path:b'guid: '+b'a'*32})))

    def test_hidden_assets_follow_unity_streaming_exception(self):
        self.assertTrue(check.ignored_by_unity('Assets/QuizRoom/.gitkeep'))
        self.assertTrue(check.ignored_by_unity('Assets/QuizRoom/backup~/Q.cs'))
        self.assertTrue(check.ignored_by_unity('Assets/QuizRoom/cvs/Q.cs'))
        self.assertTrue(check.ignored_by_unity('Assets/QuizRoom/draft.tmp'))
        self.assertFalse(check.ignored_by_unity('Assets/StreamingAssets/.config'))
        self.assertFalse(check.ignored_by_unity('Assets/QuizRoom/Q.cs'))

    def test_existing_directory_cannot_lose_meta(self):
        path='Assets/QuizRoom.meta'
        head={'Assets/QuizRoom/Q.cs':b'code', 'Assets/QuizRoom/Q.cs.meta':b'guid: '+b'a'*32}
        self.assertTrue(any('lost .meta' in e for e in self.inspect([('D',path)],head)))

    def test_empty_folder_metadata_and_ignored_dotfiles_are_valid(self):
        head={'Assets/QuizRoom.meta':b'folderAsset: yes\nguid: '+b'a'*32}
        self.assertEqual(self.inspect([('A','Assets/QuizRoom.meta')],head),[])
        head['Assets/QuizRoom/.gitkeep']=b''
        self.assertEqual(self.inspect([('A',p) for p in head],head),[])

    def test_conflicts_and_secret_files_fail_but_example_is_allowed(self):
        for name, data in [('backend/a.py',b'<<<<<<< HEAD\na\n=======\nb\n>>>>>>> branch'),
                           ('backend/.env',b'PASSWORD=x'),('backend/account.json',b'{"private_key":"secret"}')]:
            with self.subTest(name=name):
                self.assertTrue(self.inspect([('A',name)],{name:data}))
        self.assertEqual(self.inspect([('A','backend/.env.example')],{'backend/.env.example':b'GEMINI_API_KEY=\n'}),[])

    def test_pr_body_task_must_match(self):
        self.assertEqual(check.pr_errors('Task: docs/tasks/cc-12.json\n\nBody',task()),[])
        self.assertTrue(check.pr_errors('Task: docs/tasks/cc-13.json',task()))

    def test_real_git_diff_handles_unicode_and_spaces(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder)
            def git(*args):
                return subprocess.check_output(['git','-C',folder,*args],stderr=subprocess.STDOUT).decode().strip()
            git('init','-b','main');git('config','user.name','Test');git('config','user.email','test@example.invalid')
            (root/'README.md').write_text('base')
            git('add','.');git('commit','-m','base');base=git('rev-parse','HEAD')
            git('switch','-c','feat/cc-12-quiz')
            (root/'backend').mkdir();(root/'backend'/'한글 file.py').write_text('pass')
            (root/'docs/tasks').mkdir(parents=True)
            (root/'docs/tasks/cc-12.json').write_text(json.dumps(task()))
            git('add','.');git('commit','-m','feature')
            errors, count=check.check_repo(root,base,'feat/cc-12-quiz')
            self.assertEqual(errors,[]);self.assertEqual(count,2)


if __name__=='__main__':
    unittest.main()
