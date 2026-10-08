# PROGRESS — Round 2 (RUN mode)

상태 값: TODO / IN_PROGRESS / DONE / BLOCKED(<이유>)

| 태스크 | 상태 | 메모 |
|---|---|---|
| R1 테이블탑 보정 원뿔 | DONE | `aea6422`. 테이블탑(WorldScale > 1.5)에서 보정 원뿔 6°/8°, VR 아레나는 4°/6° 그대로. 봇(보정 0)은 계속 꺼짐. 순수 로직 `AimAssist.Cone` + 테스트 3개, 173/173 통과. 기기 확인 필요: 테이블탑에서 조준이 너무 끈적이지 않은지. |
| R2 아이템 기반 (Core) | DONE | `a7d0aac`. `ItemDefinition`·`StatEffect`·`Stat` enum, `Scaling.Evaluate`(4종, Hyperbolic 기반값은 0.99로 제한), `Scaling.DamageTakenMultiplier`(하한 25%), `Inventory`(중복=레벨업, 유니크·MaxLevel 거부, 보유 태그), `ItemCatalog` 19개. 테스트 26개 추가, 199/199 통과. 효과 수치를 실제 스탯으로 바꾸는 일은 R3. |
| R3 인벤토리 → 히어로 스탯 (Core) | DONE | `628550c`. `HeroStats.From(Inventory)`(카탈로그는 인벤토리가 이미 들고 있어 인자에서 뺐다). 곱연산 스탯은 아이템끼리 곱하고, 확률·감소류는 1−∏(1−x)로 합쳐 100%에 닿지 않는다. 치명타 = ×2 × `CritDamageMult`(`CritHitMultiplier`), `MaxHealth(base)` = (base+add)×mult. 테스트 21개, 220/220 통과. |
| R4 상자·포털 롤링 (Core) | DONE | `7c0096c`. `ChestType`·`ChestRoller`(풀 필터 → 보유 ×1.75 → 가중 비복원 추출, `System.Random` 주입), `IslandType`·`Portal`·`PortalRoller`(2개, 섬 3부터 50%로 3개, 중복 없음, 빈 상자 금지, 엘리트 = Epic 상자, 섬 1 = Arena + Damage 상자 고정). 테스트 31개, 251/251 통과. 상자·섬 가중치는 첫 추정값이라 기기에서 밸런스 확인 필요. |
| R5 런 상태 머신 (Core) | TODO | |
| R6 경제·상점 (Core) | TODO | |
| R7 스탯을 전투에 연결 | TODO | |
| R8 섬·봇 런타임 (RunDirector) | TODO | |
| R9 선택 UI (포털·상자·상점) | TODO | |
| R10 HUD·메뉴 | TODO | |
| R11 씬·디버그 키·APK | TODO | |
| R12 최종 리뷰·리포트 | TODO | |

## 세션 로그

- 2026-10-08 Phase 0 (대화형): 브랜치 `auto/2026-10-08-run`을 만들고, 1차 무인 작업 기록은 `AUTO/archive/2026-10-07/`로 옮겼다. 설계 문서는 `docs/crab-champions-systems-analysis.md`로 복사했다. 시작 시점 테스트 170/170.
- 2026-10-08 무인 세션 1: R1, R2 완료(테스트 170 → 199, 모두 통과). 결정 3건은 `QUESTIONS_FOR_HYUN.md`에 기록. 다음 세션은 R3부터.
- 2026-10-08 무인 세션 2: R3, R4 완료(테스트 199 → 251, 모두 통과). 결정 3건 추가(`QUESTIONS_FOR_HYUN.md`). 다음 세션은 R5(런 상태 머신)부터.
