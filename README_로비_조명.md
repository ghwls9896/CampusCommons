# 로비와 시간별 조명

Unity 6000.4.5f1에서 `CampusCommons` 프로젝트의 `Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity` 씬을 열고 Play를 누르세요.

- 로비와 마피아·퀴즈방의 책장을 벽 방향에 맞추고 책도 함께 옮겼습니다. 상층 화분은 계단과 발코니 통로에서 바깥쪽으로 옮겼습니다.
- 로비 바닥 색과 공부용 테이블·의자 비율을 조정하고, 퀴즈 좌석 아래 러그와 로비 중앙 샹들리에를 추가했습니다.
- 곡선 계단의 안쪽 난간을 따라 충돌체를 배치했습니다. 계단의 이동 경로와 상층 출구는 열려 있습니다.
- Play 중 컴퓨터의 **현지 시각**을 기준으로 하늘과 햇빛이 바뀌고, 밤에는 실내 조명이 강해집니다. 학교 탈출방에 들어가면 학교의 원래 조명으로 돌아갑니다. 서버 시간 동기화 기능은 아직 없습니다.

시간대를 미리 보려면 Hierarchy의 `Library interior update` 오브젝트에서 `Local Time Lighting`의 `Use Local Clock`을 끄고 `Preview Hour`를 조절한 뒤 Play 하세요. 실제 시각으로 사용하려면 다시 켜세요. 오전 5:30~7:30과 오후 5:00~7:00에 점진적으로 전환됩니다.

변경 전 씬 백업은 `Assets/LibraryCharacter/Backups/GrandLibrary_before_interior_update.unity`에 있습니다.
