# PROGRESS — Round 2 (RUN mode)

상태 값: TODO / IN_PROGRESS / DONE / BLOCKED(<이유>)

| 태스크 | 상태 | 메모 |
|---|---|---|
| R1 테이블탑 보정 원뿔 | DONE | `aea6422`. 테이블탑(WorldScale > 1.5)에서 보정 원뿔 6°/8°, VR 아레나는 4°/6° 그대로. 봇(보정 0)은 계속 꺼짐. 순수 로직 `AimAssist.Cone` + 테스트 3개, 173/173 통과. 기기 확인 필요: 테이블탑에서 조준이 너무 끈적이지 않은지. |
| R2 아이템 기반 (Core) | DONE | `a7d0aac`. `ItemDefinition`·`StatEffect`·`Stat` enum, `Scaling.Evaluate`(4종, Hyperbolic 기반값은 0.99로 제한), `Scaling.DamageTakenMultiplier`(하한 25%), `Inventory`(중복=레벨업, 유니크·MaxLevel 거부, 보유 태그), `ItemCatalog` 19개. 테스트 26개 추가, 199/199 통과. 효과 수치를 실제 스탯으로 바꾸는 일은 R3. |
| R3 인벤토리 → 히어로 스탯 (Core) | DONE | `628550c`. `HeroStats.From(Inventory)`(카탈로그는 인벤토리가 이미 들고 있어 인자에서 뺐다). 곱연산 스탯은 아이템끼리 곱하고, 확률·감소류는 1−∏(1−x)로 합쳐 100%에 닿지 않는다. 치명타 = ×2 × `CritDamageMult`(`CritHitMultiplier`), `MaxHealth(base)` = (base+add)×mult. 테스트 21개, 220/220 통과. |
| R4 상자·포털 롤링 (Core) | DONE | `7c0096c`. `ChestType`·`ChestRoller`(풀 필터 → 보유 ×1.75 → 가중 비복원 추출, `System.Random` 주입), `IslandType`·`Portal`·`PortalRoller`(2개, 섬 3부터 50%로 3개, 중복 없음, 빈 상자 금지, 엘리트 = Epic 상자, 섬 1 = Arena + Damage 상자 고정). 테스트 31개, 251/251 통과. 상자·섬 가중치는 첫 추정값이라 기기에서 밸런스 확인 필요. |
| R5 런 상태 머신 (Core) | DONE | `70d34fe`. `RunStateMachine`(Idle → Intro 3초 → Island → Cleared 2초 → OpenChest → 섬 4 뒤 Shop → ChoosePortal, 섬 8 뒤 바로 Boss, 보스 클리어 = Victory, 부활 없는 사망 = Defeat), `RunRules.Island`(봇 수·동시 최대 2·적 체력 ×(1+0.15×(섬−1))·엘리트·보스 배수), `WantsSpawn`, `HealAfterClear`, 부활. 테스트 18개, 269/269 통과. **시간 추정**(아래 R5 메모) 약 9분 — 10분 한도에 여유가 적어 기기에서 꼭 재 볼 것. |
| R6 경제·상점 (Core) | DONE | `34589b2`. `Economy`(기준 가격 100, 등급 배수 1/3/8/2, 인플레이션 1+0.05×섬 인덱스, 리롤 25×인플레이션×1.5^n, 처치 10×`CrystalGainMult`, 클리어 25+`CrystalPerClear`+이자, Spiked = 최대 체력 33%·HP 하한 1), `CrystalWallet`, `Shop`(Random 풀에서 4칸, 구매 = 레벨업, 리롤 = 4칸 전부 새로). 런에 연결: 처치·클리어 때 크리스탈, 섬 4 상자 뒤 `CurrentShop`, `BuyShopItem`/`RerollShop`. `ChestRoller.Roll`에 개수 지정 오버로드 추가. 테스트 14개, 283/283 통과. Spiked 체력 차감은 R8(RunDirector)이 `Economy.SpikedHealthAfter`로 적용해야 한다. |
| R7 스탯을 전투에 연결 | DONE | `dc4bac4`. 순수 로직 `CombatMath`(데미지·치명타·차지 데미지/시간·쿨다운·받는 피해·최대 체력·속도·쇼크웨이브 반경/스턴) + `HeroHealthModel.ChangeMaxHealth`(최대 체력이 오르면 오른 만큼 회복, 내리면 잘라냄)·`Heal`·`SetHealth`(최소 1). `RunHeroStats`(플레이어 히어로에 붙여 런 인벤토리를 `Bind`)를 4개 컨트롤러가 읽는다. 연결 안 됨/없음 = 중립이라 퀵 매치·봇은 그대로(치명타 확률 0이면 난수도 뽑지 않음). 치명타 빔은 노란색. 테스트 15개, 298/298 통과. **R8이 할 일**: 런 시작 때 `Bind(run.Inventory)`, 런이 끝나면 `Unbind()`. 씬에 `RunHeroStats` 추가는 R11. |
| R8 섬·봇 런타임 (RunDirector) | DONE | `b3ddd35`. `MatchPhase.Run` 추가(메뉴에서만 시작, 시간·KO 무시, 일시정지·메뉴 복귀는 그대로 — 테스트 3개, 301/301 통과). `RunDirector`가 `RunStateMachine`을 돌린다: 빌더가 만드는 `Generated/Bot/RunBot.prefab`에서 봇 생성(체력·데미지·발사 간격 스케일), 처치 → 크리스탈, 클리어 때 회복, Spiked 상자 체력 차감, 부활/패배, 섬마다 플레이어를 출발점으로. 런 중에는 퀵 매치 봇을 숨긴다. 씬 배선(RunDirector·`RunHeroStats`·스폰 지점 3개)과 디버그 키 R·1–3·B·N도 미리 넣었다(R11 일부). **헤드셋·플레이 모드 확인은 아직 없음**(컴파일·씬 빌드·테스트만). 알려진 점: 봇 빔이 다른 봇에 맞으면 그 처치도 플레이어 처치로 센다. |
| R9 선택 UI (포털·상자·상점) | DONE | `f2cbc5e`. 메뉴 자리에 포털(2–3개)·상자(카드 3–4장, 이름·`Lv n`·등급 단어·설명, 등급 색)·상점(2×2 + REROLL·LEAVE, 가격·잔액, 살 수 없으면 어둡게) 패널. `HandMenuButton`에 `SetCustomAction`/`SetLabel`/`SetIdleColor` 추가(기존 메뉴 버튼은 그대로), `HandMenu`가 선택 패널이 뜨면 포인터를 켠다. 글자·배치는 순수 로직 `RunChoiceText` + 테스트 12개, 313/313 통과. 씬 재생성 완료(배선 오류 없음). **플레이 모드·헤드셋 확인 없음**: 카드 글자 크기와 패널 폭(3개 = 약 ±29°)을 기기에서 볼 것. |
| R10 HUD·메뉴 | DONE | `e3da4a4`. 메인 메뉴 3열×2행(QUICK MATCH·RUN·TUTORIAL / MR TABLE·AIM), START → QUICK MATCH. RUN도 튜토리얼 우선(`MatchStateMachine.StartRun(withTutorial)` — 튜토리얼이 끝나면 Run으로, 테스트 2개). `RunDirector`는 매치가 Run 단계에 들어오면 런을 시작한다. 런 HUD(섬 n/9·종류·목표·HP·크리스탈)와 카운트다운·FIGHT!·CLEARED·VICTORY/DEFEAT(섬 수·아이템 수·런 시간) 배너는 `MatchHud`가 `RunHudText`(테스트 6개)로 쓴다. 런이 끝나면 기존 MENU 끝 패널이 뜬다. 321/321 통과, 씬 재생성(배선 오류 없음). **플레이 모드·헤드셋 확인 없음.** |
| R11 씬·디버그 키·APK | DONE | `9b38020`. 두 씬 모두 RunDirector·`RunHeroStats`·스폰 지점·선택 패널·런 HUD 배선 확인(R8–R10에서 이미 들어감, 배선 오류 0). 디버그 키 R·1–3·B·N은 `RunDirector`에 있고 두 씬 모두 켜져 있다(기기에는 키보드가 없어 영향 없음). 테스트 321/321, 씬 재생성(fileID만 바뀜), APK `MetaAwards\Build\HandHero_20261008.apk`(약 55 MB, 2.9분) 빌드 성공. **헤드셋 확인 없음.** |
| R12 최종 리뷰·리포트 | IN_PROGRESS | |

## 세션 로그

- 2026-10-08 Phase 0 (대화형): 브랜치 `auto/2026-10-08-run`을 만들고, 1차 무인 작업 기록은 `AUTO/archive/2026-10-07/`로 옮겼다. 설계 문서는 `docs/crab-champions-systems-analysis.md`로 복사했다. 시작 시점 테스트 170/170.
- 2026-10-08 무인 세션 1: R1, R2 완료(테스트 170 → 199, 모두 통과). 결정 3건은 `QUESTIONS_FOR_HYUN.md`에 기록. 다음 세션은 R3부터.
- 2026-10-08 무인 세션 2: R3, R4 완료(테스트 199 → 251, 모두 통과). 결정 3건 추가(`QUESTIONS_FOR_HYUN.md`). 다음 세션은 R5(런 상태 머신)부터.
- 2026-10-08 무인 세션 3: R5, R6 완료(테스트 251 → 283, 모두 통과). 결정 5건 추가(`QUESTIONS_FOR_HYUN.md`). 다음 세션은 R7(스탯을 전투에 연결)부터.
- 2026-10-08 무인 세션 4: R7, R8 완료(테스트 283 → 301, 모두 통과). 씬 재생성·RunBot 프리팹 커밋. 결정 6건 추가(`QUESTIONS_FOR_HYUN.md`). 다음 세션은 R9(선택 UI)부터. R11에서는 이미 들어간 디버그 키·RunDirector 배선을 다시 만들지 말고 확인만 할 것.
- 2026-10-08 무인 세션 5: R9, R10 완료(테스트 301 → 321, 모두 통과). 씬 재생성 커밋. 결정 7건 추가(`QUESTIONS_FOR_HYUN.md`). 다음 세션은 R11(씬·디버그 키·APK)부터 — 씬 배선(선택 패널·HUD·메뉴)과 디버그 키는 이미 들어가 있으니 확인 후 APK 빌드가 주 작업이다.

## R5 시간 추정 (헤드셋 없이 계산한 값)

- 고정 시간: 섬 9개 × (카운트다운 3초 + CLEARED 2초) = 45초.
- 선택 시간: 상자 8번 + 포털 7번 × 약 6초 = 약 90초, 상점 약 30초.
- 전투(봇 1마리 = 명중 5번, 명중 약 1.5초에 1번이라고 가정): 섬 1–2 약 20초씩, 섬 3–5 약 40초, 섬 6–8 약 55초(Horde는 45초 고정), 보스 약 70초 → 약 395초.
- 합계 약 560초 ≈ **9.3분**. 아이템으로 데미지가 오르면 8–9분으로 줄 것으로 본다. 10분을 넘으면 먼저 줄일 값: `RunParams.EnemyHealthPerIsland`(0.15), `BossHealthMult`(6), Arena 봇 수.
