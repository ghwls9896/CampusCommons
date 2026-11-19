# 공용 로비 WebSocket

도서관 로비에 접속한 사용자는 서로의 닉네임과 이동 상태를 공유한다. 서버는 공용 로비 한 곳에 계정 8개까지 수용한다. 첫 사용자 혼자서도 입장할 수 있고, 그 사용자가 나간 뒤에도 다른 사람은 계속 접속할 수 있다. 이 문서는 1단계 서버와 후속 Unity 클라이언트 사이의 통신 규격이다. [작업 이슈 #44](https://github.com/ghwls9896/CampusCommons/issues/44)

## 서버 실행

기존 Render 웹 서비스 `campuscommons-quiz`의 FastAPI가 퀴즈와 로비를 함께 처리한다. 참가자와 위치는 프로세스 메모리에 있으므로 인스턴스 한 대에 Uvicorn 워커 한 개로 실행한다. 재시작하면 새 로비가 열린다. [배포 설정](../../render.yaml)

| 서버 환경변수 | 값 |
|---|---|
| `SUPABASE_URL` | 로그인에 사용하는 Supabase 프로젝트의 HTTPS 기본 주소. 경로·쿼리 없이 호스트까지만 지정한다. |
| `SUPABASE_PUBLISHABLE_KEY` | 같은 프로젝트의 `sb_publishable_` 형식 공개 키. |

로컬에서는 두 값을 프로세스 환경에 설정한 뒤 다음과 같이 실행한다. Render에서는 기존 서비스의 **Environment**에 같은 이름으로 설정한다. [환경변수 예제](../../backend/.env.example)

```sh
cd backend
python -m pip install -r requirements.txt
python -m uvicorn app:app --host 127.0.0.1 --port 8000 --workers 1 --ws-max-size 1024
```

무료 시연은 기존 서비스의 Free 인스턴스를 사용한다. Render의 월 750 무료 인스턴스 시간은 워크스페이스 전체에서 공유한다. 전송량과 빌드 시간에는 별도 한도가 있고, 결제수단이 등록돼 있으면 초과 요금이 생길 수 있다. 시연 전 Billing의 **Monthly Included Usage**에서 잔여량을 확인한다. 수신 트래픽이 15분 동안 없으면 무료 서비스가 휴면에 들어가고, 다음 접속 때 시작에 약 1분이 걸릴 수 있다. [Render 무료 서비스 조건](https://render.com/docs/free)

## 입장과 명단 수신

1. Unity가 계정 로그인과 프로필 준비를 마치면 `wss://<Render 서비스 호스트>/v1/lobby/ws`로 연결한다. 로컬 주소는 `ws://127.0.0.1:8000/v1/lobby/ws`다. 데스크톱 클라이언트는 WebSocket 핸드셰이크의 `Authorization` 헤더에 `Bearer <사용자 액세스 토큰>`을 싣는다.
2. 서버는 기존 계정 함수 `POST /functions/v1/account`에 `{"action":"read"}`를 보내 사용자 UUID와 닉네임을 확인하고, 인증된 토큰의 만료 시각까지 연결을 허용한다. WebSocket이 열린 뒤 처음 받은 `welcome`이 입장 성공을 뜻한다. [계정 API](../account/INTEGRATION.md)
3. `welcome.players`는 본인을 포함한 현재 명단이다. `self_id`는 이 연결에서 본인에게 발급한 공개 ID다. 이름이 같은 사람도 있으므로 Unity는 `id`로 아바타를 구분하고, `self_id`와 일치하는 항목에는 기존 로컬 캐릭터를 사용한다.

```json
{"type":"welcome","revision":1,"self_id":"11111111111111111111111111111111","players":[{"id":"11111111111111111111111111111111","nickname":"도서관학생","slot":0,"pose":null}]}
```

`slot`은 0~7 사이의 로비 입장 번호다. 가장 작은 빈 번호를 배정하며 퇴장한 자리는 재사용한다. `pose`는 첫 이동 상태 수신 전까지 `null`이다. 가만히 있는 사용자도 상대에게 현재 위치가 보이도록 클라이언트는 `welcome` 수신 직후 첫 자세를 한 번 전송한다.

## 이동 전송과 화면 반영

클라이언트는 자기 캐릭터의 상태를 UTF-8 JSON 텍스트 메시지 하나로 보낸다. 처음에는 초당 10회 이내에서 이동을 전송하고, 멈췄을 때 마지막 위치와 `idle`을 한 번 보낸다.

```json
{"type":"move","seq":1,"x":2.0,"y":0.08,"z":-3.0,"yaw":90.0,"motion":"walk"}
```

| 필드 | 값 |
|---|---|
| `type` | `move` |
| `seq` | 연결별 0~2147483647 정수. 수신된 마지막 순번보다 큰 값만 반영한다. |
| `x`, `y`, `z` | Unity 월드 좌표. 각 값은 −10000 이상 10000 이하인 유한한 JSON 숫자다. |
| `yaw` | 캐릭터 몸이 향한 Y축 회전각. 0도 이상 360도 미만. |
| `motion` | `idle`, `walk`, `run` 중 하나. |

서버는 위 필드만 받으며 한 메시지는 최대 1024바이트다. 연결별 1초 구간에 30개를 초과하면 연결을 닫는다. 서버는 약 100ms마다 변경을 확인하고, 입장·퇴장·이동이 있었으면 `snapshot`에 **전체 명단**을 `slot` 순으로 전달한다. 아래는 두 번째 참가자가 입장한 뒤의 예다.

```json
{"type":"snapshot","revision":3,"players":[{"id":"11111111111111111111111111111111","nickname":"도서관학생","slot":0,"pose":{"seq":1,"x":2.0,"y":0.08,"z":-3.0,"yaw":90.0,"motion":"walk"}},{"id":"22222222222222222222222222222222","nickname":"열람실학생","slot":1,"pose":null}]}
```

Unity는 명단에 새로 나타난 ID의 아바타를 생성하고, 남은 ID의 자세를 갱신하고, 사라진 ID의 아바타를 제거한다. 변경이 여러 번 모이면 `revision`이 건너뛸 수 있다. 다음 연결에서는 `welcome`을 새 기준으로 사용한다. 같은 계정이 다시 들어오면 새 연결이 기존 슬롯을 이어받고 새 ID와 빈 자세로 시작한다. 개인 공간 입장과 로그아웃 시에는 클라이언트가 로비 소켓을 닫아 퇴장한다.

## 종료 사유

오류는 WebSocket close 프레임의 코드와 영문 사유로 전달한다.

| 코드 | 사유 | 클라이언트 동작 |
|---|---|---|
| 4400 | `replaced` | 같은 계정의 새 연결이 입장했다. 이전 연결의 자동 재시도를 멈춘다. |
| 4401 | `unauthorized`, `token_expired` | 유효한 계정 세션을 확인하거나 갱신한 뒤 다시 연결한다. |
| 4403 | `profile_not_ready` | 닉네임 설정을 끝낸 뒤 입장한다. |
| 4409 | `lobby_full` | 8명 만원을 표시하고 빈자리가 생긴 뒤 재시도한다. |
| 1003 | `text_required` | JSON을 텍스트 메시지로 보낸다. |
| 1009 | `message_too_large` 또는 WebSocket 전송 계층의 사유 | 1024바이트를 초과했다. 크기 초과는 종료 코드로 판별한다. |
| 1008 | `invalid_move`, `rate_limited` | 이동 필드 또는 전송 빈도를 확인한다. |
| 1013 | `account_unavailable`, `slow_connection` | 간격을 늘려 재시도한다. 계정 API의 403도 여기에 해당한다. 반복되면 서버의 계정 함수 접근 권한과 환경변수를 확인한다. |

## 검증

`backend`에서 `python -m pip install -r requirements-dev.txt` 후 `python -m pytest -q`를 실행한다. 로비 테스트는 실제 루프백 Uvicorn 소켓으로 명단·이동·퇴장·8명 정원·재접속을 확인하고 Supabase 계정 함수 응답은 테스트 데이터로 제공한다. 실제 계정 두 개와 macOS·Windows 실행본을 이용한 시연은 Unity 클라이언트 연결 후 검증한다. [로비 테스트](../../backend/tests/test_lobby.py)
