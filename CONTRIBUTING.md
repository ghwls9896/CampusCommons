# 팀 개발 절차

**작업 Issue → 독립 브랜치/폴더 → Draft PR → 검사/리뷰 → main 통합.** 모든 에이전트의 시작 문서는 [AGENTS.md](AGENTS.md)이며, [담당표](docs/team/OWNERSHIP.md)와 [처음 적용하기](docs/team/START_HERE.md)를 함께 읽습니다.

## 1. 작업을 잡기

[Task 양식](https://github.com/ghwls9896/CampusCommons/issues/new?template=task.yml)으로 목표 하나를 등록합니다. 사람 담당자, 수정 범위, 완료 조건을 적고 기존 열린 Issue/PR과 겹치는 파일을 확인합니다. 팀원은 도메인을 담당하지만 그 도메인의 모든 파일을 항상 잠그는 것은 아닙니다. 공유 파일은 [조율 절차](docs/team/OWNERSHIP.md#공유-파일-조율)를 따릅니다.

## 2. 브랜치와 폴더 만들기

작업 중인 기존 폴더를 다른 에이전트가 쓰지 않는지 확인합니다. Codex 등의 관리형 worktree 기능을 쓸 수 있으면 그것을 우선합니다. 일반 Git에서는 아래처럼 시작합니다(실제 Issue 번호로 바꿉니다).

```sh
git fetch origin
git worktree add ../CampusCommons-cc-123 -b feat/cc-123-description origin/main
cd ../CampusCommons-cc-123
```

Unity Hub에는 **각 worktree 폴더**를 별도 프로젝트로 추가합니다. Library/Temp는 공유·복사·커밋하지 않습니다. 원래 checkout은 유지합니다. 다른 사람의 브랜치에서 작업하거나 여러 에이전트가 같은 working directory를 사용하지 않습니다.

`docs/team/task.example.json`을 `docs/tasks/cc-123.json`으로 복사하고 실제 값을 넣습니다. `scope`는 정확한 파일 또는 `/`로 끝나는 폴더 경로이며, 폴더는 하위 경로 전체를 뜻합니다. 루트 전체나 wildcard는 허용하지 않습니다. 담당자 개인 경로를 넣지 않습니다.

## 3. 일찍 공유하기

작업 명세를 커밋하고 push한 뒤 Draft PR을 엽니다. **PR의 첫 줄은 `Task: docs/tasks/cc-123.json`**으로 합니다. 이렇게 해야 다른 에이전트가 아직 끝나지 않은 수정 범위를 볼 수 있습니다. 내용 없는 기능 브랜치만 여러 개 만들어 두지 않습니다.

```sh
git add docs/tasks/cc-123.json
git commit -m "docs: register cc-123 task scope"
git push -u origin feat/cc-123-description
gh pr create --draft --base main --title "[영역] 변경 결과" --body-file pr-description.md
```

`pr-description.md`는 `.github/pull_request_template.md`를 참고한 임시 파일로 만들어 실제 검증 내용을 씁니다. 비밀 키와 원문 사용자 데이터는 올리지 않습니다. 이후 작업도 같은 Issue/PR에서 갱신합니다.

## 4. 구현하고 검사하기

- 정확한 Unity 버전에서 컴파일 및 관련 Play Mode 검사를 수행합니다. 씬 편집자는 변경 전/후와 재현 경로를 PR에 남깁니다.
- 백엔드가 있는 브랜치에서는 `cd backend`, 개발 의존성 설치 후 `python -m pytest -q`.
- 협업 도구: `python -m unittest discover -s Tools/team/tests -v`.
- 명시한 파일만 stage/commit 후 저장소 루트에서 `python Tools/team/check.py --base origin/main`.
- PR CI `team-checks`는 작업 명세/수정 범위/공유 파일 선언, 충돌 흔적, 신규 Unity .meta/GUID, 기본 비밀 파일 실수, 도구 테스트를 검사합니다. 백엔드가 있으면 서버 테스트도 실행합니다. **Unity 실행/시각적 품질/진짜 AI 응답/파일 독점권은 검사하지 않습니다.**

CI 실패를 없애려고 검사나 다른 사람 코드를 지우지 않습니다. 범위가 틀렸으면 실제 작업과 맞추고 영향받는 담당자가 검토합니다.

## 5. 최신 main 반영하기

내 작업이 안전하게 커밋된 **내 작업 폴더**에서 `git fetch origin`, `git merge origin/main`을 사용합니다. 공유된 브랜치는 기본적으로 rebase/force-push하지 않습니다. 재검증 후 push합니다. 다른 사람의 미커밋 파일을 reset/clean/stash하지 않습니다.

씬 충돌은 두 변경의 의도를 확인하고 통합 담당자와 Unity에서 재현합니다. [Unity Smart Merge](https://docs.unity3d.com/6000.4/Documentation/Manual/SmartMerge.html)는 로컬 설치 버전에 맞춰 선택적으로 설정할 수 있지만 결과를 자동으로 신뢰하지 않습니다. 기기별 Unity 절대 경로는 저장소 설정에 넣지 않습니다.

## 6. 리뷰와 통합

완료한 PR은 Ready for review로 전환합니다. **PR 작성자가 HIGHCHOI이면 ghwls9896, 그 외이면 HIGHCHOI가 최종 승인합니다. 지정된 사람 1명이면 충분하고 전원 승인은 필요하지 않습니다.** 본인 PR을 본인이 승인할 수 없습니다.

`review-routing.yml`이 Ready 상태의 새 PR/재개/업데이트에 맞춰 지정 리뷰어를 요청합니다. Draft에서는 요청하지 않습니다. 이 workflow가 main에 합쳐진 뒤 발생하는 이벤트부터 동작하므로 이미 열린 PR은 해당 리뷰어를 직접 지정하거나 Ready로 전환합니다. `CODEOWNERS`에는 두 사람이 들어가므로 GitHub가 둘 다 리뷰 대상으로 표시할 수도 있지만 둘 다 승인하라는 뜻은 아닙니다. 기술적으로는 둘 중 한 명의 Code Owner 승인이 충족되므로, 작성자별 정확한 담당은 이 운영 규칙으로 지킵니다. 자동 요청은 자동 승인이나 특정 승인자 강제 검사와 다릅니다.

공통 Player 변경은 캐릭터/공간 담당자에게 필요한 기술 확인을 받습니다. 이 조율은 지정 리뷰어의 최종 승인과 별개이며 담당자의 추가 Approve를 일괄 요구하지 않습니다. HIGHCHOI가 검사·대화·선행 PR·지정 승인을 확인하고 Squash merge하며, HIGHCHOI 본인의 PR은 ghwls9896가 리뷰·통합합니다. 에이전트가 자기 PR을 merge하지 않습니다. Issue는 코드 통합/배포 조건을 구분해 닫습니다.

## 7. 중단·인수인계

[인수인계 템플릿](docs/team/HANDOFF_TEMPLATE.md)을 `docs/tasks/cc-123.md`에 채우고 PR을 갱신합니다. 공유 파일 조율 상태는 해당 Issue에도 남깁니다. agent의 개인 채팅 기록은 팀원이 볼 수 없으므로 인수인계로 간주하지 않습니다. 중단된 작업은 명시적으로 넘겨받기 전 다른 에이전트가 동시에 이어 쓰지 않습니다.
