# QUESTIONS_FOR_HYUN — Round 2

무인 세션이 대신 내린 결정. (형식: 태스크 / 질문 / 택한 답 / 이유 / 뒤집으려면)

- **R1 / 테이블탑 감지 방법** — `HandGestureTracker.Instance.WorldScale > 1.5`(새 튜닝값 `tabletopWorldScaleThreshold`)로 감지했다. `ArenaViewMode` 참조 방식은 고르지 않았다. 이유: 씬 배선이 필요 없고, 손 임계값들이 이미 같은 WorldScale을 쓴다. 보정이 꺼진 히어로(봇, `assistAngle` 0)는 테이블탑에서도 꺼진 채로 둔다(`AimAssist.Cone`). 되돌리려면: `PointingBeamController`에서 `tabletopAssistAngle`/`tabletopAssistReleaseAngle`을 4/6으로 두면 된다.
