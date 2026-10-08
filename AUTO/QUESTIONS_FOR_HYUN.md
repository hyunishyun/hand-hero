# QUESTIONS_FOR_HYUN — Round 2

무인 세션이 대신 내린 결정. (형식: 태스크 / 질문 / 택한 답 / 이유 / 뒤집으려면)

- **R1 / 테이블탑 감지 방법** — `HandGestureTracker.Instance.WorldScale > 1.5`(새 튜닝값 `tabletopWorldScaleThreshold`)로 감지했다. `ArenaViewMode` 참조 방식은 고르지 않았다. 이유: 씬 배선이 필요 없고, 손 임계값들이 이미 같은 WorldScale을 쓴다. 보정이 꺼진 히어로(봇, `assistAngle` 0)는 테이블탑에서도 꺼진 채로 둔다(`AimAssist.Cone`). 되돌리려면: `PointingBeamController`에서 `tabletopAssistAngle`/`tabletopAssistReleaseAngle`을 4/6으로 두면 된다.
- **R2 / 테마가 없는 렐릭** — 계획서 표에서 `big_chests`의 테마가 "—"였다. 테마 enum에 None을 넣지 않고 Economy로 두었다. 이유: 렐릭은 상자에서 카테고리(Relic)로 고르므로 테마가 풀에 영향을 주지 않고, 선택지가 늘어나는 효과는 보상·경제 쪽에 가깝다. 되돌리려면: `ItemCatalog.cs`의 BigChests 줄에서 테마만 바꾸면 된다.
- **R2 / Greed 단점의 레벨 스케일링** — `glass_cannon`의 최대 체력 −30%는 Hyperbolic(레벨이 올라도 100%에 닿지 않음), `blood_price`의 받는 피해 +25%는 Linear로 했다. 이유: 체력 감소를 Linear로 두면 레벨 4에서 체력이 0 이하가 된다. 되돌리려면: `ItemCatalog.cs`의 Greed 두 줄.
- **R3 / 같은 스탯을 올리는 아이템끼리 합치는 방법** — 곱연산 계수(데미지·속도·크리스탈 등)는 서로 곱한다(예: Power Cell 1.15 × Glass Cannon 1.6 = 1.84). 치명타 확률·쿨다운 감소·받는 피해 감소는 1−∏(1−x)로 합친다. 이유: 설계 문서 §1.5가 곱연산 계수를 "다른 데미지 버프와 곱해져 시너지"로 설명하고, 확률류는 100%를 넘으면 안 된다. 되돌리려면(합연산으로): `HeroStats.Apply`의 `*=`를 합산으로 바꾸면 된다.
- **R4 / 테마 상자가 고르는 테마** — 테마가 7개인데 테마 상자는 4종이라 묶었다: Damage 상자 = Damage + Critical, Health 상자 = Health + Defense + Speed, Ability = Ability, Economy = Economy. 테마 상자에는 렐릭과 Greed가 나오지 않는다. Random 상자는 Greed만 빼고 전부(렐릭 포함, 가중치 0.2), Epic·Spiked 상자는 Epic + Legendary, Upgrade 상자는 보유 아이템 중 레벨업 가능한 것(Greed 제외). 이유: 설계 문서 §2.3. 되돌리려면: `ChestRoller.InPool`.
- **R4 / Big Chests가 늘리는 선택지** — 모든 상자에 +1(Upgrade 3개, Relic·Greed 2개 포함). 이유: 아이템 설명이 "Every chest offers one more choice". 되돌리려면: `ChestRoller.ChoiceCount`.
- **R4 / 포털 가중치** — 섬: Arena 0.5 / Horde 0.3 / Elite 0.2. 엘리트 섬 보상은 항상 Epic 상자. Arena·Horde 보상: Damage·Ability·Health·Random 1, Economy 0.7, Upgrade 0.6, Spiked 0.4, Greed 0.3, Relic 0.2. 열어도 빈 상자가 될 포털(빈 인벤토리의 Upgrade, 렐릭을 다 가진 뒤의 Relic)은 내지 않는다. 세 번째 포털은 "섬 3으로 가는 포털부터"로 해석했다. 되돌리려면: `PortalRoller`의 표와 `ThirdPortalFromIsland`.
- **R5 / 상자를 여는 순서** — 계획서에는 "IslandCleared → ChoosePortal → OpenChest"로 적혀 있지만, R4에서 포털 = "다음 섬 + 그 섬을 클리어하면 받는 상자"로 정했으므로 클리어 → 그 섬의 상자 열기 → 다음 포털 고르기 순서로 했다. 섬 1은 Damage 상자 고정. 이유: 크랩 챔피언스와 같고, 포털에 적힌 보상을 바로 받는 것보다 "위험을 이기면 보상"이 분명하다. 되돌리려면: `RunStateMachine.Tick`의 IslandCleared 분기와 `ChoosePortal`.
- **R5 / 상점 위치·보스 앞** — 섬 4의 상자 다음에 상점, 상점을 나오면 섬 5 포털을 고른다. 섬 8 다음에는 포털 없이 바로 보스. 보스를 깨면 상자 없이 Victory. 되돌리려면: `RunStateMachine.AfterChest` / `OfferPortals`.
- **R5 / Arena 동시 봇 수** — Arena 봇 3마리(섬 6–8)도 한 번에 최대 2마리만 나오고, 하나가 쓰러지면 다음이 나온다(Horde와 같은 상한 2). 이유: 앉아서 손으로만 하는 게임이라 3마리 동시 사격은 너무 버겁다. 되돌리려면: `RunParams.MaxAlive`.
- **R5 / 적 스케일링 기준** — "1 + 0.15 × islandIndex"의 islandIndex를 0부터 셌다(섬 1 = ×1.0, 보스 섬 9 = ×2.2, 보스 체력 ×6 → 기본의 13.2배). 보스 데미지는 ×1(발사 간격만 ×0.7로 빠르게). 되돌리려면: `RunRules.Island`, `RunParams.BossFireIntervalMult`.
- **R6 / 상점 가격의 섬 인덱스와 리롤** — 상점은 섬 5 앞에 있으므로 섬 5의 인덱스(4, 인플레이션 ×1.2)로 값을 매긴다(커먼 120, 첫 리롤 30). 리롤은 팔린 칸까지 4칸 전부 새로 뽑는다. 이자는 클리어 보상을 받기 전 잔액 기준이다. 처치·클리어 크리스탈은 반올림, 가격은 올림. 되돌리려면: `RunStateMachine.AfterChest`의 `new Shop(Inventory, Island, …)`, `Shop.Restock`, `Economy.ClearReward`.
