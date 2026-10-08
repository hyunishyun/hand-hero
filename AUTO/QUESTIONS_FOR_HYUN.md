# QUESTIONS_FOR_HYUN — Round 2

무인 세션이 대신 내린 결정. (형식: 태스크 / 질문 / 택한 답 / 이유 / 뒤집으려면)

- **R1 / 테이블탑 감지 방법** — `HandGestureTracker.Instance.WorldScale > 1.5`(새 튜닝값 `tabletopWorldScaleThreshold`)로 감지했다. `ArenaViewMode` 참조 방식은 고르지 않았다. 이유: 씬 배선이 필요 없고, 손 임계값들이 이미 같은 WorldScale을 쓴다. 보정이 꺼진 히어로(봇, `assistAngle` 0)는 테이블탑에서도 꺼진 채로 둔다(`AimAssist.Cone`). 되돌리려면: `PointingBeamController`에서 `tabletopAssistAngle`/`tabletopAssistReleaseAngle`을 4/6으로 두면 된다.
- **R2 / 테마가 없는 렐릭** — 계획서 표에서 `big_chests`의 테마가 "—"였다. 테마 enum에 None을 넣지 않고 Economy로 두었다. 이유: 렐릭은 상자에서 카테고리(Relic)로 고르므로 테마가 풀에 영향을 주지 않고, 선택지가 늘어나는 효과는 보상·경제 쪽에 가깝다. 되돌리려면: `ItemCatalog.cs`의 BigChests 줄에서 테마만 바꾸면 된다.
- **R2 / Greed 단점의 레벨 스케일링** — `glass_cannon`의 최대 체력 −30%는 Hyperbolic(레벨이 올라도 100%에 닿지 않음), `blood_price`의 받는 피해 +25%는 Linear로 했다. 이유: 체력 감소를 Linear로 두면 레벨 4에서 체력이 0 이하가 된다. 되돌리려면: `ItemCatalog.cs`의 Greed 두 줄.
- **R3 / 같은 스탯을 올리는 아이템끼리 합치는 방법** — 곱연산 계수(데미지·속도·크리스탈 등)는 서로 곱한다(예: Power Cell 1.15 × Glass Cannon 1.6 = 1.84). 치명타 확률·쿨다운 감소·받는 피해 감소는 1−∏(1−x)로 합친다. 이유: 설계 문서 §1.5가 곱연산 계수를 "다른 데미지 버프와 곱해져 시너지"로 설명하고, 확률류는 100%를 넘으면 안 된다. 되돌리려면(합연산으로): `HeroStats.Apply`의 `*=`를 합산으로 바꾸면 된다.
