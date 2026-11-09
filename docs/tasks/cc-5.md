# CC-5 승인 담당 변경 인수인계

- 담당: HIGHCHOI / 브랜치: codex/review-routing / Issue: https://github.com/ghwls9896/CampusCommons/issues/5
- 사용자 결정: 다른 작성자의 PR은 HIGHCHOI가 승인, HIGHCHOI의 PR은 ghwls9896가 승인. 전원 승인은 요구하지 않음.
- 변경: CODEOWNERS의 기능별 승인자 목록을 두 최종 리뷰어로 통일, AGENTS/CONTRIBUTING/담당표/시작 안내 갱신, 작성자별 자동 리뷰 요청 workflow 추가.
- 구현 담당과 공유 파일 기술 조율은 유지. 기존 팀원 작업이나 게임 코드는 변경하지 않음.
- 검증: 기존 협업 tests16개 통과, 실제 workflow의 JavaScript를 모의 GitHub API로 실행한 8개 사례 통과(HIGHCHOI/소문자 표기/세 팀원/중복 요청/Draft/closed), YAML 구문과 권한 검사 통과.
- 별도 코드 리뷰 완료. actions/github-script v9의 annotated tag와 실제 commit을 구분해 검증한 commit SHA(3a2844b)로 고정함.
- 자동화는 pull_request_target에서 신뢰된 base workflow만 실행하며 PR 코드 checkout, 제목/본문 실행, 자동 승인/merge, 다른 리뷰어 제거를 하지 않음. 요청 권한만 사용.
- 한계: CODEOWNERS는 정적 OR 규칙이므로 두 Code Owner 중 하나의 승인은 기술적으로 허용됨. 작성자별 구분은 팀 규칙 및 자동 리뷰 요청이며, 별도 강제 승인 gate가 아님.
- 실제 자동 요청 이벤트는 이 PR이 main에 통합된 뒤에만 동작함. 현재 PR은 작성자가 HIGHCHOI이므로 ghwls9896에게 수동 리뷰 요청.
- 현재 저장소 상태(2026-11-09 확인): ghwls9896가 PR #3을 main b415cc0으로 merge. branch API protected=true, team-checks 필수. 상세 보호 GET은 현 Write 계정에서404여서 승인 수/관리자 우회 등은 확인하지 못함. Yeni-sjs는Read. 퀴즈 PR #4는Draft/미통합.
- 다음: ghwls9896가 본 PR 리뷰/merge → 이후 Ready 상태 PR에서 지정 리뷰 요청 확인. 관리자에게 기존 main 보호의 승인1명/Code Owner/우회 제한 설정을 비교 확인받기. 기존 PR #4는 새 규칙에 따라 ghwls9896가 최종 승인 담당.
