# 실행본·검증·정리 현황

기능 기준: 2026-11-17 main `38a8568f79f2de92d43f5745276073ad1bb4a76c` (#39 포함). 작업 Issue #42, 문서/배포 workflow PR #43. #33은 보류 상태 그대로이며 #41은 병합 없이 닫혔습니다.

## 실행본

- macOS ARM64 / macOS 15 이상: Unity 6000.4.5f1 빌드 통과. 최종 폴더에서 실행해 한글 로그인·회원가입 화면 표시 확인. 앱 로그의 `STUDY_RUNTIME_READY platform=macos-arm64 packaged=true` 확인.
- Mac 동봉 모델: 현재 main과 런타임 app 소스 일치 확인, 실제 MPS 오프라인 추론 통과. 개인 카메라 사용 없음.
- Windows x64: Windows runner의 동봉 Python·QEV 패키지 제작 및 오프라인 추론 통과. Unity 실행본 빌드 진행 중이며 최종 결과를 아래에 갱신합니다.
- Mac/Windows의 개인 연결 설정 및 Sunbox 원본·파생 에셋은 Git에 올리지 않습니다. 실행본에는 로컬 설치 에셋과 운영용 클라이언트 연결 설정이 포함됩니다. 서비스 비밀키를 포함하지 않습니다.
- GitHub 공개 Release 업로드·macOS Developer ID 서명/공증은 수행하지 않았습니다. 로컬 팀 시험용 배포본입니다.

Windows 런타임 검증: https://github.com/ghwls9896/CampusCommons/actions/runs/44074768791

## 검증의 경계

- 최신 main CI: 팀 도구 16개, 퀴즈 서버 52개 통과. CI는 Unity 실행 검사가 아닙니다.
- #31/#37/#39: HIGHCHOI가 관련 수동 검사를 확인하고 승인·병합했습니다.
- 이번 Mac 기동은 시작 화면·런타임 준비 검사입니다. 실제 계정 로그인·메일 발송·개인 PDF 전송·카메라 분석은 자동으로 수행하지 않았습니다.
- 최신 Windows 게임의 실제 GUI·카메라·다른 PC 실행은 사람 확인이 필요합니다. Windows runner의 모델 검증으로 이를 대체하지 않습니다.
- 과거 Unity SearchDatabase 시작 예외와 EscapeOpenDoorRouteVerification의 기존 기대값 실패는 당시 PR 기록에 유지합니다.

## 남은 이슈

| 이슈 | 현재 남은 일 |
|---|---|
| #1 | main의 protected=true 확인. 관리자 세부 보호 규칙·팀원 권한/작업 등록 확인 |
| #7 | 실제 카메라 정확도·Windows 카메라/게임 동시 사용 |
| #8 | 실제 두 계정·다른 PC·비밀번호 복구 흐름 |
| #24 | 실제 계정/카메라에서 창 전환·시간 증가·재로그인 기록 동기화 |
| #32 / PR #33 | 사용자 요청으로 보류, 수정·병합하지 않음 |
| #40 / PR #41 | PR은 닫혔고 미통합 코드는 보존. 재개 여부 미정 |
| #42 / PR #43 | 실행본·문서·정리 결과의 인계와 ghwls9896 최종 문서 리뷰 |

완료 종료한 이슈: #2, #17, #19, #21, #26, #28, #30, #34, #36, #38. 기존 테스트 한계는 남은 이슈와 원 PR에 보존합니다. #26의 포획 밸런스·헤드폰 좌우 소리는 HIGHCHOI가 직접 정상 확인 후 종료를 요청했습니다.

## 정리 결과

- main 직접 병합, 현재 branch SHA와 PR 최종 SHA 일치, 활성 작업 폴더에서 미사용을 확인한 원격 브랜치 9개 삭제: `codex/cc-15-item-icons`, `codex/cc-17-npc-sounds`, `codex/cc-26-escape-followup`, `codex/cc-30-excuse-fix`, `codex/cc-36-escape-final`, `codex/cc-38-escape-scene`, `codex/local-integration`, `feat/cc-19-avatar-customization`, `feat/cc-21-inventory-ui`.
- 이전 Mac 실행본 2개와 ZIP 1개(약 6.2GiB)는 최신 실행본 기동 확인 후 휴지통으로 이동. 휴지통을 비우지 않았으므로 복구 가능하며 디스크 용량이 즉시 확보됐다는 뜻은 아닙니다.
- 이번 분석에서 만든 임시 main 소스 사본 삭제.
- 보존: #33 브랜치, #41의 미통합 마피아 코드, 원본/통합 프로젝트의 미커밋 작업, 개인 연결 설정, Sunbox 에셋, 모델 캐시, 다른 프로젝트, 팀 전달용 설정 파일.
- 오래된 씬/에셋/검증 코드의 참조를 임의로 끊지 않았습니다. 작업 인계는 현재 상태를 상단에 추가하고 과거 검증 이력을 유지했습니다.
