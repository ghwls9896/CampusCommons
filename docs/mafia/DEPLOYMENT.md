# 교수를 찾아라 배포와 확인

Unity는 기존 `QuizConnection`의 Render 주소와 팀 참가 코드를 사용한다. Render의 `/v1/mafia/rooms`가 방 코드, 발언 순서, 밤 보호 결과와 최종 지목을 관리한다. 같은 방의 Unity 클라이언트는 각자 참가 토큰으로 약 1.25초마다 공개 상태를 읽는다. 참가자 채팅은 공개 목록에 표시되며 AI에는 지정한 캐릭터에 대한 질문만 전달한다. 모델 입력에는 발언자 자신의 역할, 말투, 기억과 최근 공개 NPC 대사가 들어간다. 전체 역할표와 정답 공개 시점은 Render가 관리한다.

## Modal 비용 설정

1. [Modal Starter](https://modal.com/signup)에 로그인하고 GPU 접근에 필요한 결제수단을 등록한다.
2. 운영할 Workspace의 Usage & Billing에서 월 **Workspace budget $25**, **Spend limit $0**을 저장하고 실제 적용된 값을 확인한다. $25는 $30 월 컴퓨팅 크레딧 아래에 여유를 둔 값이다. $0을 설정할 수 없다면 GPU 배포를 연기하고 준비 대사 모드를 사용한다. [Modal 예산](https://modal.com/docs/guide/budgets) · [가격](https://modal.com/pricing)
3. 프로젝트 루트에서 Modal CLI를 준비하고 대시보드에서 선택한 Workspace로 연결한다.

   ```sh
   python -m pip install modal==1.6.4
   python -m modal setup
   python -m modal deploy backend/modal_mafia.py
   ```

4. 대시보드의 **Proxy Tokens**에서 서버용 토큰을 만들고, 출력된 함수 주소와 함께 기존 Render 서비스의 Environment에만 등록한다. [Modal Proxy Tokens](https://modal.com/docs/guide/webhook-proxy-auth)

   | 변수 | 값 |
   | --- | --- |
   | `MAFIA_MODAL_URL` | 배포한 `say` Web Function의 HTTPS 주소 |
   | `MAFIA_MODAL_KEY` | Proxy Token의 Key |
   | `MAFIA_MODAL_SECRET` | Proxy Token의 Secret |
   | `MAFIA_MODAL_BUDGET_VERIFIED` | Workspace 예산과 지출 한도를 확인한 뒤 `true` |
   | `MAFIA_MODAL_DAILY_LIMIT` | 선택. UTC 날짜 기준 기본값과 최대값은 24회 |
   | `MAFIA_MODAL_MONTHLY_LIMIT` | 선택. UTC 달력 월 기준 기본값과 최대값은 180회 |

이 프로젝트는 GPU 한 장을 배정하는 자체 Web Function을 배포한다. Modal의 토큰 단가 Shared Endpoint는 사용하지 않는다. 컨테이너는 최대 1개이며 유휴 12초 후 종료 대상으로 전환한다. Render의 요청 제한은 재시작하면 초기화되므로 실제 요금 차단은 Modal Workspace 예산과 지출 한도가 담당한다. Qwen 호출이 비활성화됐거나 한도·오류에 걸리면 캐릭터별 준비 대사를 사용한다. 즉시 Qwen을 내리려면 Render의 `MAFIA_MODAL_BUDGET_VERIFIED`를 지우고 해당 Modal 앱을 중지한다.

## 플레이 확인

1. 두 Unity 클라이언트에서 각각 로그인해 로비의 마피아룸 문을 통과한다.
2. 한 명이 방을 만들고 코드를 공유한다. 다른 참가자는 그 코드로 입장한다.
3. 진행자가 보호 대상과 다음 발언을 선택한다. 다른 참가자는 채팅을 보내거나 지정한 AI에게 질문한다. 두 클라이언트에서 대사와 순서를 비교한다.
4. 진행자가 교수를 최종 지목하면 양쪽에 같은 결과가 표시된다. 패널을 닫으면 이동이 복구되고, 기존 Esc 퇴장 확인으로 라운지에 돌아간다.

방과 대화는 기존 단일 Render 워커의 메모리에 있다. 서버가 종료되거나 재배포되면 새 방을 열어야 한다.

```sh
cd backend
python -m pytest -q
```

Unity 6000.4.5f1 자동 씬 검사 진입점은 `CampusCommons.Mafia.Editor.MafiaRoomSmoke.Run`이다. 백엔드 테스트의 모델 응답과 Unity 씬 검사의 로그인은 시험용 응답이다.
