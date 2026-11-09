# CC-1 협업 설정 인수인계

- 담당: HIGHCHOI / 브랜치: codex/team-collaboration
- Issue: https://github.com/ghwls9896/CampusCommons/issues/1
- PR: https://github.com/ghwls9896/CampusCommons/pull/3
- 구현/검증 기준 커밋: fb67dee (이 문서 정리 직전).
- 완료: 공통 AGENTS, 네 사람 담당표와 CODEOWNERS, 도구별 시작 파일, 작업 JSON/PR/인수인계 템플릿, 독립 worktree 절차, 수정 범위 및 Unity 파일 검사, 비밀키 없는 CI.
- 최신 main eb786c2 반영. 원격에 새로 추가된 Tools/와 충돌하지 않도록 폴더 대소문자를 통일했습니다. 공간/씬/기존 코드의 upstream 변경을 보존했고 이 PR의 main 대비 diff는 협업 설정뿐입니다.
- 검증: `python -m unittest discover -s Tools/team/tests -v` 16개 통과. `python Tools/team/check.py --base origin/main` 통과. 독립 리뷰의 폴더 meta/숨김파일/부모 폴더 문제는 재현 테스트 후 수정했습니다. GitHub Actions Team checks 실행 42572707302 성공: https://github.com/ghwls9896/CampusCommons/actions/runs/42572707302
- 게임 코드는 변경하지 않았으므로 이 PR에서 Unity 런타임/빌드 검증을 수행했다고 주장하지 않습니다. 기존 퀴즈 테스트는 별도 PR #4의 증거입니다.
- 파일 조율: 이 PR은 팀 규칙·CI·도구만 수정. 다른 담당자의 미공유 작업을 확인하거나 파일 잠금을 획득했다는 의미가 아닙니다.
- 다음 행동: ghwls9896가 PR #3 검토/merge → main 보호 적용 → Yeni-sjs Write 권한 확인 → 각자 현재 작업 Issue/Draft PR 등록 → 각 기능 브랜치에 최신 main 반영.
- 아직 적용하지 않은 것: main 보호(관리자 권한 필요), 팀원 초대/역할 변경, 자기 PR의 승인/merge, 배포. API 확인상 HIGHCHOI는 Write이고 main은 아직 protected=false였습니다.
- 기존 퀴즈는 독립 브랜치/인수인계/Issue #2/Draft PR #4로 공유했습니다. 두 기능을 한 PR로 합치지 않았습니다.
