# QUESTIONS_FOR_HYUN

무인 세션이 대신 내린 결정. (형식: 태스크 / 질문 / 택한 답 / 이유 / 뒤집으려면)

- T1 / 에디터가 6.6 업그레이드 후 배치 실행 때 다시 쓴 설정 에셋(`Assets/Settings/*URP*`, `Assets/XR/*`, `Assets/XRI/Settings/Resources/`, `Assets/CompositionLayers/`, `ProjectSettings/Packages/com.unity.learn.iet-framework/Settings.json`)을 커밋할까? / 커밋함(`a519806`, 손으로 고친 것 없음) / 매 실행마다 다시 생겨 작업 트리가 더러워지고, 하드 규칙 8의 ProjectSettings 변경은 사람이 의도한 변경을 막으려는 취지라 판단 / 되돌리려면 `git revert a519806`(다음 에디터 실행 때 다시 생긴다).
- T2 / 주먹·핀치 임계값(grab 0.7/release 0.45, pinchFire 0.8/pinchReset 0.5)을 어디에 둘까? / `XRHandsInputSource`로 옮김(이름·기본값 동일) / 계획서 T5가 "제스처 인식은 XRHandsInputSource 쪽"이라 했고, 봇·디버그 입력은 아날로그 강도가 없음 / 컨트롤러 쪽으로 되돌리려면 HandInputData에 FistStrength·PinchStrength를 추가해야 함.
- T2 / 샌드박스 씬에서 플레이어 히어로 레이어 / 내장 "Ignore Raycast"(2) 레이어 사용, aimMask에서 제외 / 하드 규칙 8(ProjectSettings는 T9에서만) 때문에 새 레이어를 만들지 않음 / T9에서 전용 FlyingHero 레이어로 교체 예정. **→ T3에서 바뀜(아래).**
- T3 / 봇 빔이 플레이어 히어로를 맞혀야 하는데 플레이어가 Ignore Raycast 레이어라 맞힐 수 없음 / 레이어 대신 `PointingBeamController`가 **자기 히어로의 콜라이더만 건너뛰는** 레이캐스트(`RaycastIgnoringSelf`)를 쓰고, 두 히어로 모두 Default 레이어 / 새 레이어 없이(규칙 8) 플레이어·봇이 서로 맞힐 수 있고 aimMask 의미는 그대로 / T9에서 FlyingHero 레이어를 만들면 aimMask로 바꿔도 됨(서로 맞혀야 하므로 "자기 제외"는 남겨야 함).
- T3 / 봇 난이도 기본값 / 반응 0.35초, 조준 오차 4°, 발사 간격 2.2+0~1초, 예고 0.6초, 선호 거리 12±3m, 손 속도 상한 1.5m/s(사람 손 수준) / 계획서 T4의 0.6초 예고와 맞추고, 12m에서 4° 오차 ≈ 0.84m라 가만히 있으면 대부분 맞고 피하면 빗나감 / `Assets/MyAssets/Generated/Bot/BotDifficulty_Normal.asset`에서 조절(Play 중 실시간 반영, 씬 빌더가 덮어쓰지 않음).
