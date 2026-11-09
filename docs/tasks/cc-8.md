# CC-8 계정·성장 데이터 인수인계

- Issue: https://github.com/ghwls9896/CampusCommons/issues/8
- 담당 HIGHCHOI / 최종 리뷰 ghwls9896
- 브랜치 codex/cc-8-account-progression / 기준 origin/main fc5d8c7
- 작업 폴더: CampusCommons-account-progression (기존 퀴즈/웹캠과 분리)
- 확정: 이메일+비밀번호, 매 입장 닉네임, 계정별 XP·레벨 저장, Supabase Auth+PostgreSQL.
- 이번 산출물: 설계와 단계별 구현 계획. Auth/DB/Unity 게임 코드는 아직 구현하지 않음.
- 설계: docs/superpowers/specs/2026-11-09-account-progression-design.md
- 작업안: docs/superpowers/plans/2026-11-09-account-progression.md
- 제안: 닉네임2~12자·중복허용, 직전 값 미리 채움, 메모리 세션, 최소 누적XP=50*(L-1)*L. 사용자 검토 전 정책 확정 아님.
- 선행 설정: 일반 사용자 이메일 확인/복구용 SMTP. 무료 DB와 이메일 발신·도메인 비용은 구분.
- XP 신뢰: 현재 로컬 게임 결과는 보상 증명이 아님. 서버 검증 결과가 생긴 기능만 실제 지급 활성화.
- 공유 파일 수정 없음. 실제 통합은 별도 작업 등록/조율. .meta 및 Unity6000.4.5f1 유지.
- 검사: 신규 worktree의 협업 테스트16개 통과. 문서 범위 검사는 커밋 후 실행.
- 미검증: 실제 Supabase 프로젝트/SMTP/DB/Unity, 지급 원천별 계약 및 복구 리허설.
- 다음: 설계 세부 정책 검토 → DB/권한 모듈 → 인증/독립 Unity UI → 별도 메인 씬 통합 → 보상 원천별 연결.
