# PROGRESS — Round 2 (RUN mode)

상태 값: TODO / IN_PROGRESS / DONE / BLOCKED(<이유>)

| 태스크 | 상태 | 메모 |
|---|---|---|
| R1 테이블탑 보정 원뿔 | DONE | `aea6422`. 테이블탑(WorldScale > 1.5)에서 보정 원뿔 6°/8°, VR 아레나는 4°/6° 그대로. 봇(보정 0)은 계속 꺼짐. 순수 로직 `AimAssist.Cone` + 테스트 3개, 173/173 통과. 기기 확인 필요: 테이블탑에서 조준이 너무 끈적이지 않은지. |
| R2 아이템 기반 (Core) | IN_PROGRESS | |
| R3 인벤토리 → 히어로 스탯 (Core) | TODO | |
| R4 상자·포털 롤링 (Core) | TODO | |
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
