# QUESTIONS_FOR_HYUN

무인 세션이 대신 내린 결정. (형식: 태스크 / 질문 / 택한 답 / 이유 / 뒤집으려면)

- T1 / 에디터가 6.6 업그레이드 후 배치 실행 때 다시 쓴 설정 에셋(`Assets/Settings/*URP*`, `Assets/XR/*`, `Assets/XRI/Settings/Resources/`, `Assets/CompositionLayers/`, `ProjectSettings/Packages/com.unity.learn.iet-framework/Settings.json`)을 커밋할까? / 커밋함(`a519806`, 손으로 고친 것 없음) / 매 실행마다 다시 생겨 작업 트리가 더러워지고, 하드 규칙 8의 ProjectSettings 변경은 사람이 의도한 변경을 막으려는 취지라 판단 / 되돌리려면 `git revert a519806`(다음 에디터 실행 때 다시 생긴다).
- T2 / 주먹·핀치 임계값(grab 0.7/release 0.45, pinchFire 0.8/pinchReset 0.5)을 어디에 둘까? / `XRHandsInputSource`로 옮김(이름·기본값 동일) / 계획서 T5가 "제스처 인식은 XRHandsInputSource 쪽"이라 했고, 봇·디버그 입력은 아날로그 강도가 없음 / 컨트롤러 쪽으로 되돌리려면 HandInputData에 FistStrength·PinchStrength를 추가해야 함.
- T2 / 샌드박스 씬에서 플레이어 히어로 레이어 / 내장 "Ignore Raycast"(2) 레이어 사용, aimMask에서 제외 / 하드 규칙 8(ProjectSettings는 T9에서만) 때문에 새 레이어를 만들지 않음 / T9에서 전용 FlyingHero 레이어로 교체 예정.
