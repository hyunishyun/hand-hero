# PROGRESS — Round 4 (first-run hitch, balance, leftover fixes, meta progression A)

상태 값: TODO / IN_PROGRESS / DONE / BLOCKED(<이유>)

| 태스크 | 상태 | 메모 |
|---|---|---|
| S1 첫 런 끊김 | DONE | `c119152`. 런 봇 풀을 씬 로드 때 미리 만들고, `RenderWarmup`이 로드 직후 3프레임 동안 봇·빔 3색·피격·처치 폭발·비네트·차지 구슬을 작게 그린다. 로거에 25 ms 작은 끊김 카운터(`HITCH`, `hitches=`, `worst_hitch=`)와 헤더의 warmup 시간. 테스트 469/469, 씬 재생성. **주의:** 3차 로그를 다시 보니 30초 Stale은 시스템 프로세스의 레이어와 겹친다(`QUESTIONS_FOR_HYUN.md` S1-2). |
| S2 밸런스 | DONE | `aaee943`. Horde 45→30초, `BossHealthMult` 6→5, 새 `EnemyDamageMult` 1.15(Elite는 1.5×1.15). 옛 값을 고정하던 `IslandSpec_CountsAndEnemyScaling`을 D2에 맞게 고쳤다. 새 테스트 3개, 472/472. 씬이 `RunParams`를 직렬화하므로 씬도 다시 만들었다. `run_summary.py`는 이미 10분 제한 대비 시간을 출력한다(변경 없음). 시간 추정은 아래. |
| S3 남은 Minor 4건 | DONE | `d7d363b`. 조준 손의 시스템 제스처가 켜지면 차지를 쏘지 않고 취소한다(ASSIST·CURSOR, 게이트→핀치→규칙→차지 연결 테스트로 버그 재현 후 수정). 로거의 머리 장치 조회 0.25초 + 캐시. 핀치 기록은 런 중에만(`QUESTIONS_FOR_HYUN.md` S3-1). 봇은 Core `SeededRandom` 하나를 스폰마다 다시 시드한다(시드별 난수열은 바뀜, 고정한 테스트 없음). 새 테스트 10개, 482/482. 직렬화 필드 변화 없음 → 씬 재생성 불필요. |
| S4 메타 진행 Core | DONE | `3221b3d`. Core `IKeyValueStore` + `MetaProgress`(키 `hh.meta.*`, 버전 1, 버전이 다르면 초기화, 런 끝마다 Save 한 번), HandProto `PlayerPrefsKeyValueStore`. 해금: 섬 5 도달(포기 포함) → Second Wind, 첫 승리 → Big Chests, 첫 승리와 다른 조준 모드로 승리 → Dividends. 최고 기록은 모드별, 나빠지지 않음, 시간은 승리만. 키 표 변경은 `QUESTIONS_FOR_HYUN.md` S4-1. 새 테스트 25개, 507/507. 아직 연결 안 됨(S5). |
| S5 메타 진행 연결 | DONE | `873b58f`. 해금된 유물이 있으면 섬 1 전에 STARTING RELIC 상자(유물 카드 + NONE, 상자 화면 재사용, 상자 소리). 런 끝(끝 화면·포기)에 런 기록과 함께 메타 저장. 메뉴 배너의 안내 문구 자리에 조준 모드별 `BEST  ISLAND n  -  WIN m:ss`, 끝 배너에 `NEW BEST`·`UNLOCKED: … START`. 일시정지 패널 RESET PROGRESS(3초 안에 두 번). `run_log`에 `start_relic`. 새 테스트 30개, 537/537. 씬 재생성(배선 오류 0). 결정은 `QUESTIONS_FOR_HYUN.md` S5-1–S5-4. |
| S6 적 다양성 1단계(생김새·공격) | DONE | `c430f3d`. Striker(지금 봇)·Sniper(보라, 바늘, 예고 ×1.6·간격 ×1.8·피해 ×1.8, 가늘고 오래 남는 빔)·Gunner(주황, 블록, 3연발 ×0.45, 2·3발은 재조준)·Lancer(분홍, 창, 예고 ×1.8·간격 ×2.5·피해 ×2.2, 굵은 빔)·Boss(진홍, 블록+창, Gunner 2번↔Lancer 2번). 섬 1–2 Striker, 3 Gunner, 5 Sniper, 7 Lancer부터. 이동·체력 규칙은 그대로이고 Striker는 프레임 단위로 지금과 같다(기존 BotBrain 테스트 그대로 통과). `run_log`에 `kills_by`·`damage_by`. 새 테스트 24개, 561/561. 씬·RunBot 프리팹 재생성. 결정은 `QUESTIONS_FOR_HYUN.md` S6-1–S6-6(연발 간격 0.36초 등). |
| S7 지형 다양성 1단계(위치 무작위) | TODO | |
| S8 MR 방 스캔 스파이크 | TODO | |
| S9 씬·테스트·APK 2종 | TODO | |
| S10 최종 리뷰·보고서 | TODO | |

## 세션 로그

- 2026-10-09 Phase 0 (대화형): 브랜치 `auto/2026-10-09-run`(`perf/freeze-hunt` `a3b1dd1`에서). 3차 기기 로그를 `AUTO/device_logs/2026-10-09/`에 커밋, 3차 AUTO 파일은 `AUTO/archive/2026-10-09/`로. 시작 시점 테스트 461/461.
- 2026-10-09 세션 1 (무인): S1, S2 완료. 테스트 461 → 472, 모두 통과. 씬 두 번 재생성. 헤드셋 확인 없음. 다음 세션은 S3부터.
- 2026-10-09 세션 2 (무인): S3, S4 완료. 테스트 472 → 507, 모두 통과. 씬 변경 없음(직렬화 필드 변화 없음). 헤드셋 확인 없음. 다음 세션은 S5(메타 진행 연결)부터: `MetaProgress`를 `RunDirector.FinishRecord`에서 `PlayerPrefsKeyValueStore.Instance`로 부른다.
- 2026-10-09 세션 3 (무인): S5, S6 완료. 테스트 507 → 561, 모두 통과. 씬 두 번, RunBot 프리팹 한 번 재생성. 헤드셋 확인 없음. 다음 세션은 S7(지형 위치 무작위)부터: 기둥은 씬 빌더의 `Pillar_L`/`Pillar_R`, 봇 스폰 지점은 `RunBotSpawn_1..3`(`RunDirector.botSpawnPoints`); 원형 선택이 `_spawnSeeds`에서 난수를 하나 더 쓰므로 배치 시드는 별도 스트림으로 둔다.

## S2 시간 추정 (3차 기기 런 기준, 헤드셋 없이 계산)

- 3차 런: 587.9초 = 전투 445.5초 + 전투 밖 142.4초(카운트다운·CLEARED 45초 + 상자·포털·상점 약 97초).
- Horde 3개: 45 → 30초, **−45초**.
- 보스: 102.5초 × 5/6 ≈ 85.4초, **약 −17초**.
- 봇 데미지 ×1.15는 시간을 직접 바꾸지 않는다. 받은 피해는 166 → 약 190으로 예상한다(부활이 생기면 길어질 수 있다).
- 합계 약 **526초 ≈ 8분 46초**(목표 8–9분 안). S5의 시작 유물 상자(약 5초)와 S6의 적 종류가 더해지면 기기에서 다시 잰다: `python AUTO\tools\run_summary.py <run_log.jsonl>`.
