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
- **R7 / 런 도중 최대 체력이 바뀔 때** — 최대 체력이 오르면(Hull Plating) 오른 만큼 현재 체력도 채우고, 내리면(Glass Cannon) 현재 체력을 새 최대치로 자르기만 한다. 이유: 아이템을 고른 직후 체력 바가 비어 보이지 않게. 되돌리려면: `HeroHealthModel.ChangeMaxHealth`.
- **R7 / 치명타 표시** — 치명타 빔은 노란색(`critBeamColor`)으로 나간다. 숫자 팝업은 없다. 되돌리려면: `PointingBeamController`의 `critBeamColor`를 `beamColor`와 같게.
- **R8 / 런과 퀵 매치의 공존 방법** — `MatchPhase.Run`을 새로 넣어 `MatchDirector`가 일시정지·포커스 상실·메뉴 복귀를 그대로 맡고, 섬 진행은 `RunDirector`가 한다. 이유: 일시정지·헤드셋 벗기·메뉴 패널 로직을 두 번 만들지 않으려고. 되돌리려면: `MatchStateMachine.StartRun`과 `RunDirector`.
- **R8 / 봇 재등장 간격** — 봇이 쓰러지면 1.5초 뒤 다음 봇이 나온다(Horde, Arena 3마리 섬). 섬 시작 때는 바로 나온다. 되돌리려면: `RunDirector.respawnInterval`.
- **R8 / 부활·섬 시작 위치** — Second Wind 부활은 그 자리에서 바로(출발점으로 이동, 최대 체력 50%). 매 섬 카운트다운 때 플레이어 히어로를 출발점으로 돌려보낸다(체력은 유지). 되돌리려면: `RunDirector.OnPlayerDied` / `OnRunPhaseChanged`의 Intro 분기.
- **R8 / Spiked 상자 대가 시점** — 상자가 열릴 때(카드가 뜰 때) 최대 체력 33%를 깎는다(최소 1). 선택지가 없어 상자를 건너뛰면 깎지 않는다. 되돌리려면: `RunDirector.OnRunPhaseChanged`의 OpenChest 분기.
- **R9 / 카드 글자 줄바꿈** — 메뉴 버튼은 한 줄(NoWrap)+자동 크기지만, 아이템 카드·상점 칸은 설명(최대 약 42자)이 있어 줄바꿈(Normal)+자동 크기(최소 0.5)로 했다. 이유: 한 줄로 두면 글자가 2.5 m 거리에서 4 cm 정도로 작아진다. 포털·REROLL·LEAVE·제목은 한 줄 그대로. 되돌리려면: 빌더 `RunChoicePanels`에서 `wrap: true`를 지우면 된다.
- **R9 / 화살표 대신 두 줄** — 포털 버튼은 "ARENA → DAMAGE CHEST" 대신 "ARENA" 아래 작은 글씨 "DAMAGE CHEST" 두 줄로 했다. 이유: 기본 TMP 폰트 아틀라스에 → 글자가 있는지 확인할 수 없어 ASCII만 썼다. 되돌리려면: `RunChoiceText.PortalLabel`.
- **R9 / 새 패널 입력 지연 0.4초** — 상자 카드를 고르자마자 같은 자리에 포털 패널이 뜨므로, 새 패널은 0.4초 동안 핀치를 무시한다. 이유: 빠른 두 번째 핀치로 읽지도 않은 포털이 골라지는 것을 막으려고. 되돌리려면: `RunChoiceMenu.armDelay`를 0으로.
- **R9 / 카드 4장(Big Chests)** — 선택지가 4개면 한 줄로 늘이지 않고 2×2로 놓는다. 카드에는 등급 색과 함께 등급 단어(COMMON/EPIC/…)도 쓴다(색약 대비). 살 수 없는 상점 칸(돈 부족·SOLD·OWNED)은 색을 어둡게 한다. 되돌리려면: `RunChoiceText.Layout` / `ItemCard` / `Dimmed`.
