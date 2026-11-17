# 교수를 찾아라 웹 테스트

프로젝트 루트에서 Ollama와 백엔드 의존성을 준비한다. Ollama가 이미 실행 중이면 `ollama serve`를 생략한다.

```sh
ollama serve
```

다른 터미널에서 실행한다.

```sh
ollama pull qwen3.5:2b-q4_K_M
python -m pip install -r backend/requirements.txt
cd backend
python mafia_demo.py
```

[로컬 데모](http://127.0.0.1:8874/)에서 `방 만들기` → `6명 발언 시작`을 누른다. 등장인물을 고르면 질문 또는 최종 지목을 할 수 있다. 다른 탭에서 다른 닉네임과 동일한 방 코드를 입력하면 공개 발언과 채팅을 함께 볼 수 있다. 첫 낮에 여섯 명이 모두 등장하고, 모델 비교를 위해 시작 단서와 정답은 고정되어 있다.

## 모델 바꾸기

```sh
ollama pull <모델태그>
python mafia_demo.py --model <모델태그>
```

Ollama 모델 목록에 표시되는 전체 태그를 사용한다. 기존에 실행한 데모 서버는 먼저 종료한다. 별도 포트를 쓰는 Ollama는 `--ollama-url`로 연결한다. Modal에서 바꿀 때는 기존 배포 명령을 실행하는 환경에 `MAFIA_MODEL`을 지정한다. [Modal 배포 안내](DEPLOYMENT.md)

## 요청과 응답

1. 방 서버가 화자, 그 인물의 말투와 목격담, 최근 공개 발언, 해당 인물에게 온 질문을 모델에 보낸다. 교수 화자의 요청에만 교수의 비공개 행동을 추가한다.
2. 브라우저와 Modal은 `backend/mafia_dialogue.py`의 프롬프트, Ollama 요청 형식, 응답 파서를 공유한다. 모델은 후속 질문과 확인할 주제(`time`, `place`, `object`, `confirm`)를 반환한다.
3. 서버는 고정 증언에 기준을 통과한 생성 질문을 붙인다. 생성 질문을 쓰기 어려우면 모델이 선택한 주제에 맞는 캐릭터별 질문을 붙이고, 유효한 주제도 없으면 기본 증언만 보낸다. 웹 화면에 각각 `AI 질문 작성`, `AI 질문 선택`, `기본 증언`으로 표시된다.
4. 브라우저는 방 서버가 확정한 같은 대화 순서를 읽는다. 웹 데모는 루프백에서만 열리며 로컬 Ollama를 사용한다. Modal이나 Render 인증 정보가 필요 없다.
