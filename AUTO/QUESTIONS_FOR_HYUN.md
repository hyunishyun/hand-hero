# QUESTIONS_FOR_HYUN

무인 세션이 대신 내린 결정. (형식: 태스크 / 질문 / 택한 답 / 이유 / 뒤집으려면)

- T1 / 에디터가 6.6 업그레이드 후 배치 실행 때 다시 쓴 설정 에셋(`Assets/Settings/*URP*`, `Assets/XR/*`, `Assets/XRI/Settings/Resources/`, `Assets/CompositionLayers/`, `ProjectSettings/Packages/com.unity.learn.iet-framework/Settings.json`)을 커밋할까? / 커밋함(`a519806`, 손으로 고친 것 없음) / 매 실행마다 다시 생겨 작업 트리가 더러워지고, 하드 규칙 8의 ProjectSettings 변경은 사람이 의도한 변경을 막으려는 취지라 판단 / 되돌리려면 `git revert a519806`(다음 에디터 실행 때 다시 생긴다).
