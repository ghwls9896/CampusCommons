# CampusCommons — 모든 AI 에이전트의 공통 작업 규칙

이 파일을 먼저 읽고 `CONTRIBUTING.md`, `docs/team/OWNERSHIP.md`, 해당 `docs/tasks/cc-번호.json`을 읽는다. Codex/Claude/Gemini/Copilot/Cursor 모두 같은 규칙을 따른다. 도구별 파일에 다른 팀 규칙을 복제하지 않는다. 사람의 명시적 요청과 도구의 상위 안전 지침이 우선이며, 예외는 작업 Issue/PR에 기록한다.

## 시작 전

1. `git status --short`, `git branch --show-current`, `git remote -v` 확인. 다른 사람의 수정·미추적 파일·스테이징을 덮거나 지우거나 자동 stash하지 않는다.
2. `git fetch origin` 후 최신 main의 이 파일/담당표, 열린 Issue와 Draft PR을 확인한다. 오프라인이면 공유 파일의 새 작업을 시작하지 말고 독립적인 조사만 계속한다.
3. 작업 Issue 하나에 사람 담당자 한 명, 목표/완료 조건, 수정할 파일/폴더, 공통 파일, 의존 작업을 기록한다. 에이전트는 사람 담당자의 작업자이며 별도 가상 팀원이 아니다.
4. 작업 하나당 브랜치 하나, 실행 중 에이전트 하나당 작업 폴더(worktree 또는 별도 clone) 하나. 다른 에이전트가 쓰는 폴더에서 checkout/rebase/merge하지 않는다. `main`에서 구현하거나 직접 push하지 않는다.
5. 새 작업은 최신 `origin/main`에서 `feat/cc-번호-설명`, `fix/cc-번호-설명`, `chore/cc-번호-설명` 등으로 시작한다. Codex의 `codex/설명`도 허용한다. 사람별 영구 브랜치나 모든 기능이 쌓이는 장기 브랜치를 만들지 않는다.
6. `docs/tasks/cc-번호.json`을 만들어 실제 branch, owner, Issue, scope, shared_files, 검증 계획을 선언한다. 템플릿은 `docs/team/task.example.json`. 선언은 승인이나 잠금이 아니다.

## 구현 중

- 담당 범위 밖 리팩터링·이름 변경·에셋 재생성·일괄 포맷은 하지 않는다. 범위 확대가 필요하면 Issue/작업 명세/PR을 갱신하고 영향을 받는 담당자와 조율한다.
- 공유 씬, Player/GameStart/Portal/LobbyDoor, Packages/ProjectSettings, backend/app.py는 `OWNERSHIP.md`의 절차로 조율한다. 이미 다른 작업이 편집 중이면 그 파일은 건드리지 않고 자신의 모듈을 먼저 만든다. Git 브랜치/JSON 파일은 원격 분산 잠금이 아니다.
- API·JSON 필드·이벤트·프리팹 연결을 바꾸면 생산자와 소비자 경로, 호환성, 적용 순서를 작업 Issue에 남긴다. 서로 다른 에이전트가 같은 인터페이스를 추측해 구현하지 않는다.
- Unity 버전은 `ProjectSettings/ProjectVersion.txt`의 **6000.4.5f1**. 버전 변경은 별도 합의된 작업. 다른 에디터 검증은 임시 복사본에서 하고 실제 버전을 보고한다.
- 현재 메인 씬은 `Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity`. 씬/프리팹은 Unity 에디터로 편집하고 공유 씬 전체 재생성 메뉴를 임의 실행하지 않는다. 충돌 시 YAML을 추측하거나 파일 전체를 ours/theirs로 선택하지 않는다.
- 경로 대소문자를 정확히 지킨다. 공용 도구 디렉터리는 `Tools/`이며 `tools/`를 따로 만들지 않는다. Windows/macOS에서 같은 이름이 되는 경로를 추가하지 않는다.
- 에셋 생성/이동/삭제에는 `.meta`가 따라야 한다. 기존 GUID를 재발급하지 않는다. Force Text / Visible Meta Files를 유지한다. Library/Temp/Logs/Builds, 개인 경로, 키, 토큰, 실제 학습 PDF/사용자 DB는 커밋하지 않는다.
- 변경 파일만 명시적으로 stage한다. 기존 CRLF 정규화 차이는 `git diff --ignore-space-at-eol`로 구분하고 무관한 파일을 포함하지 않는다. repo-wide `git add --renormalize .`는 별도 작업이다.

## 검증·공유·완료

- 커밋 전 변경에 맞는 테스트를 실행한다. 커밋 후 `python Tools/team/check.py --base origin/main`으로 제출할 커밋을 확인한다. 이 검사는 작업 폴더의 미커밋 수정은 검사하지 않는다.
- 게임 코드 변경은 정확한 Unity 버전에서 컴파일/관련 Play Mode 검증 결과를 PR에 붙인다. 못 돌린 검사, 다른 버전, 가짜 API 응답 사용은 정확히 적는다. CI 통과를 Unity 실행 성공으로 말하지 않는다.
- 작업이 시작되면 범위/작업 명세가 들어간 작은 커밋을 push하고 Draft PR을 만든다. 완료 시 같은 PR을 갱신한다. PR마다 목표 하나, 연결 Issue, task JSON, 실제 검증, 미검증, 공통 파일 조율 및 의존 PR을 적는다.
- 최종 승인 담당은 PR 작성자가 HIGHCHOI이면 ghwls9896, 그 외이면 HIGHCHOI다. 지정된 사람 1명의 승인을 받으며 전원 승인은 필요하지 않다. 공유 파일은 기존 기능 담당자와 기술적으로 조율하되 그 담당자 전원의 GitHub Approve를 요구하지 않는다. AI 리뷰는 보조이며 사람 승인을 대신하지 않는다. 검사/대화/지정 승인이 충족되면 HIGHCHOI가 통합을 관리하고, HIGHCHOI 본인 PR은 ghwls9896가 검토·통합한다. 에이전트가 자기 PR을 승인/자동 merge하지 않는다.
- 넘길 때 Issue/PR 및 `docs/tasks/cc-번호.md`에 마지막 커밋, 완료/미완료, 실행한 검사, 다음 행동, 수정 중인 공유 파일과 해제 여부를 남긴다. 한 파일짜리 전체 팀 진행표를 모든 브랜치가 동시에 고치지 않는다.
- 실제 공유 상태의 기준은 GitHub Issue/PR이다. 로컬 완료, push 완료, CI 완료, merge 완료, 배포 완료는 서로 다르다. 외부 권한이 없으면 가능한 파일/검증까지 끝내고 필요한 관리자 행동을 구체적으로 보고한다.
