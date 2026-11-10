# 계정 모듈과 경험치 연동 계약

`AccountGate.Instance`는 실행 시 생성되고 SceneLoaded에서 기존/생성된 Player의 이동·컨트롤러·QuizRoom 입력을 로그인 완료 전 차단합니다. 기존 GameStart/Player/씬 파일을 수정하지 않습니다. 새 캐릭터/방 모듈을 도입할 때는 AccountWorldGate 차단 대상도 함께 검증합니다. 인증은 클라이언트 조작 방지의 서버 경계가 아니며 모든 데이터 요청은 별도 검증합니다.

- `EntryReady(AccountProfile)`: 이메일 인증·닉네임 서버 저장 완료 후 발생
- `EntryRevoked`: 로그아웃 또는 인증 갱신 거부로 입장 해제. 로컬 퀴즈를 정리하고 탈출 게임을 종료한 후 입력 차단
- `AccountSession.ReadProfileAsync()`: 본인 UUID의 최신 닉네임/누적XP/레벨/다음 누적XP 조회
- `AccountSession.EnterAsync(nickname)`: 본인 닉네임을 저장하고 최신 성장 데이터 반환
- 현재 계정 외 공개 프로필 조회/랭킹은 구현 범위에 포함하지 않음

API 계약:

```text
POST /functions/v1/account  Authorization: Bearer <user access token>
{"action":"read"}
또는 {"action":"enter","nickname":"학생"}
→ {user_id,nickname,total_xp,level,next_level_total_xp}

POST /functions/v1/progression  Authorization: Bearer <user access token>
{"source":"quiz","activity_id":"<서버에서 발급한 UUID>"}
→ {user_id,nickname,total_xp,level,next_level_total_xp,awarded_xp,already_awarded}
```

user_id/xp/delta를 요청으로 받지 않습니다. 보상 source는 quiz/escape/study 중 서버 설정으로 허용한 기능만 가능하며 기본값은 모두 비활성입니다. RPC 호출의 대상 UUID는 검증된 Auth 사용자 ID만 사용합니다.

후속 보상 생산자는 활동 시작 시 서버에서 private.activity_runs를 만들고 실제 완료 근거를 서버에서 검증한 다음 status=verified와 서버 보상표의 reward_xp/policy_version을 기록해야 합니다. pending·타인·다른 source 활동은 지급 거절됩니다. 이 테이블은 Data API에 노출하지 않고 신뢰된 서버에서만 갱신합니다. 클라이언트가 보내는 정답 인덱스/성공 플래그/웹캠 판정만으로 verified를 설정하지 않습니다.

반영은 한 트랜잭션 안에서 계정 행과 활동 행 잠금 → 유일한 xp_event 삽입 → total_xp 원자 증가 순서로 수행합니다. 응답 유실 후 동일 activity_id 재전송은 추가 지급 없이 최신 성장 데이터를 반환합니다. 실패 시 이벤트 삽입까지 롤백합니다. 보상 상한은 이벤트당10000XP, 누적XP는10^12입니다.

level은 DB generated column입니다. 최소 누적XP=50*(L-1)*L. 예를 들어 150XP는 레벨2이며 UI는 해당 레벨 안의50/200XP를 표시합니다. 성장 곡선 변경은 migration과 기존 generated 값 재계산·호환성 확인을 포함하는 별도 작업입니다.

보상 생산자와 지급 API를 연결할 때 source별 상한·정책 버전·반복 학습 제한·활동 유효기간을 합의한 뒤 활성화합니다. 현재 PR은 안전한 지급 저장소와 API를 제공하지만 실제 퀴즈/탈출/웹캠 결과의 신뢰 경로를 구현했다고 주장하지 않습니다.
