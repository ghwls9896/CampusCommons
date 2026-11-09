# 지금 팀에 적용하기

## 현재 확정한 역할

- **Yeni-sjs**: 교수를 찾아라룸(마피아룸)
- **HIGHCHOI**: 웹캠, 퀴즈룸, 회원가입/로그인, 개인 DB
- **ghwls9896**: 전체 공간, 탈출룸, 공통 씬 통합
- **GongBeak**: 캐릭터

세부 파일 경계는 [OWNERSHIP.md](OWNERSHIP.md)에 있습니다. 담당 기능을 위한 영구 브랜치를 미리 만들지 않고, **작업 하나마다 짧은 브랜치**를 만듭니다. 예: `feat/cc-12-mafia-vote`, `feat/cc-13-avatar-animation`, `feat/cc-14-login` (예시 번호이며 실제 Issue가 아님).

## 각자 AI에게 그대로 전달할 시작 프롬프트

```text
이 저장소는 ghwls9896/CampusCommons의 팀 프로젝트다.
먼저 AGENTS.md → CONTRIBUTING.md → docs/team/OWNERSHIP.md를 읽어라.
내 GitHub 아이디는 [아이디], 맡은 작업은 [기능/Issue URL]이다.
git status/현재 브랜치/원격을 확인하고 최신 origin/main 및 열린 Issue와 Draft PR을 읽어라.
다른 작업과 수정 경로가 겹치는지 확인하고, 이번 작업의 목표/완료 조건과 scope를 제시해라.
작업 하나당 독립 브랜치와 작업 폴더를 사용하고 docs/tasks/cc-번호.json을 작성해라.
공통 씬·Player·GameStart·Packages는 변경 전에 담당자와 조율하고 그 기록을 Issue에 남겨라.
main 직접 push, 다른 작업 덮어쓰기, 범위 밖 리팩터링, 임의 Unity 업그레이드는 하지 마라.
작은 초기 커밋과 Draft PR로 시작해 진행을 공유하고, 실제 테스트 결과와 미검증 항목을 기록해라.
인수인계는 개인 채팅 대신 해당 Issue/PR 및 docs/tasks/cc-번호.md에 남겨라.
```

AGENTS.md를 자동으로 읽는 도구도 작업 시작 시 읽었는지 확인합니다. Claude/Gemini/Copilot/Cursor용 진입 파일도 같은 문서로 연결되어 있습니다. 특정 도구의 개인 메모리만으로 팀 규칙이 공유됐다고 생각하지 않습니다.

## 이미 각자 만들고 있던 작업 옮기기

1. **먼저 작업을 보존합니다.** 현재 브랜치·미커밋 파일을 확인하고 자기 변경만 자신의 기능 브랜치에 커밋하거나 별도 백업합니다. 다른 에이전트 작업이 섞여 있으면 임의 stash/reset/clean하지 않습니다.
2. 이번 협업 PR을 관리자가 먼저 검토·통합합니다. 팀원은 `git fetch origin` 후 자기 브랜치에 `git merge origin/main`으로 공통 규칙을 받습니다. main을 다른 기능 브랜치로 덮어쓰지 않습니다.
3. 자기 기능에 Issue를 연결하고 task JSON에 **이미 수정한 파일까지** 기록합니다. 미공유 변경이 공유 파일을 건드렸다면 여기서 충돌을 먼저 공개합니다.
4. 여러 기능이 섞인 커밋은 새 작업 폴더에서 기능별로 분리해 옮깁니다. 기존 브랜치/커밋은 보존합니다. 자기 코드만 정확히 나눌 수 없으면 전체 cherry-pick하지 말고 통합 담당자와 변경 덩어리를 검토합니다.
5. 브랜치를 push하고 Draft PR을 열어 실제 진행 상태를 보여줍니다. 팀원에게서 받은 패치/파일도 같은 리뷰 경로를 거칩니다.

현재 알려진 작업은 [협업 설정 #1](https://github.com/ghwls9896/CampusCommons/issues/1), [PDF 퀴즈룸 #2](https://github.com/ghwls9896/CampusCommons/issues/2)입니다. 다른 팀원의 로컬 구현은 아직 이 저장소에서 확인되지 않았으므로 완료 상태를 임의로 기록하지 않았습니다. 각자 현재 작업 Issue/초기 Draft PR을 등록하면 전체 작업이 보입니다.

## 저장소 관리자 ghwls9896가 한 번 할 설정

파일만 올려서는 main 보호가 켜지지 않습니다. 현재 HIGHCHOI는 Write로 push/PR 가능하지만 보호 설정을 바꿀 Admin 권한은 없습니다.

1. **Settings → Collaborators**에서 네 계정의 초대 수락 및 Write 이상 권한을 확인합니다. 2026-11-09 확인 시 HIGHCHOI/GongBeak는 Write, Yeni-sjs는 Read였습니다. 최신 상태를 다시 확인해야 합니다. Write가 없으면 CODEOWNERS의 해당 담당자가 리뷰 소유자로 동작하지 않습니다.
2. 이 협업 PR을 먼저 리뷰하고 merge합니다. `team-checks`가 한 번 실행되어 표시되는지 확인합니다. bootstrap PR은 수동 리뷰가 필요하며 이후 PR부터 자동 체크를 요구합니다.
3. **Settings → Branches → Add classic branch protection rule**, 대상 `main`에 다음을 설정합니다. 이미 Ruleset/보호 규칙이 있으면 중복 생성하지 말고 기존 규칙을 확인하고 보완합니다.
   - Require a pull request before merging; 승인 1명 이상
   - Dismiss stale approvals; Require review from Code Owners
   - Require approval of the most recent reviewable push
   - Require status checks: **team-checks**; 최신 main 반영 요구
   - Require conversation resolution; Require linear history
   - 관리자 우회도 금지; force push/branch deletion 허용하지 않기
4. **Settings → General → Pull Requests**에서 Squash merge를 팀 기본으로 사용합니다. branch cleanup은 merge 후 사람이 확인합니다.

[보호 설정 payload](../../.github/main-protection.json)를 제공합니다. 관리자가 GitHub CLI로 적용하려면 **현재 보호 규칙을 먼저 조회/검토**합니다. 이 PUT은 기존 classic 보호 설정을 대체하므로 기존 설정이 있다면 그대로 실행하지 않습니다. ruleset은 별도 항목이며 보호 규칙 GET의 404만으로 규칙이 없다고 단정하지 않습니다.

```sh
gh api repos/ghwls9896/CampusCommons/branches/main/protection
gh api repos/ghwls9896/CampusCommons/rulesets
# 관리자 확인 후, 기존 규칙과 충돌하지 않을 때만:
gh api --method PUT repos/ghwls9896/CampusCommons/branches/main/protection --input .github/main-protection.json
# 적용 후 위 GET으로 필수 검사/승인/force-push 금지 값을 다시 확인
```

공개 저장소의 보호 기능은 GitHub Free에서 지원됩니다. 실제 제한을 강제하는 것은 서버에 적용한 보호 설정이며, 로컬 지침은 협업 규칙입니다. [GitHub 보호 브랜치](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches), [CODEOWNERS](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners)

## 팀이 공유할 상태

GitHub **열린 Issues + Draft 포함 PR 목록**이 실시간 작업 목록입니다. 매번 수정하는 단일 Markdown 팀 보드 대신, Issue별 사람 담당/범위/의존성과 PR별 변경 파일/검증을 봅니다. 에이전트의 채팅과 브랜치 안의 JSON만으로 다른 컴퓨터의 작업을 알 수는 없습니다.

작업을 넘길 때는 [인수인계 템플릿](HANDOFF_TEMPLATE.md)을 사용합니다. 실제 사용 중인 공유 파일 조율 기록은 해당 Issue에 남기고, 다른 작업은 그 기록에 링크합니다. 같은 파일에서 충돌하면 담당자/통합 담당자가 순서를 결정합니다. 저장소 내 자동 검사는 GitHub의 사람 승인이나 작업 잠금을 대신하지 않습니다.
