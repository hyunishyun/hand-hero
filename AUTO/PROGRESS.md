STATUS: ALL_DONE

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
| S7 지형 다양성 1단계(위치 무작위) | DONE | `5483222`. Core `ArenaLayout`: 섬마다 시드로 `Pillar_L`/`Pillar_R`와 `RunBotSpawn_1..3` 위치를 정한다(벽 여백, 기둥 간격, 시작점·스폰 주변 비우기, 시작점에서 스폰 하나 이상 보임, 과녁과 안 겹침, 기둥은 플레이어 영웅 앞쪽). 200번 안에 못 찾으면 지금 배치. `ArenaLayoutApplier`가 섬 Intro(카운트다운, 봇 스폰 전)에 옮기고 `Physics.SyncTransforms()` 한 번, 런이 끝나면 원래 자리로(퀵 매치·튜토리얼은 고정). `run_log`의 섬마다 `layout_seed`. 새 테스트 15개, 576/576. 씬 재생성(배선 오류 0). 결정은 `QUESTIONS_FOR_HYUN.md` S7-1–S7-3. 기기 확인: 섬마다 기둥 위치가 다르고, 시작점을 막거나 봇과 겹치지 않으며, 기둥이 여전히 빔을 막는지. |
| S8 MR 방 스캔 스파이크 | DONE | Core `RoomScan`(권한·매니저 흐름, 첫 결과 시간, 요약 한 줄) + `RoomScanProbe`: MR TABLE이면서 메인 메뉴일 때만 `USE_SCENE`을 한 번 묻고, 허용되면 `ARPlaneManager`·`ARBoundingBoxManager`(XR Origin, 씬에서는 꺼짐)를 켠다. 거부·기능 없음·서브시스템 실패면 아무것도 켜지 않는다(MR TABLE 그대로). `perf_log`에 `ROOM_SCAN` 줄(STARTED, FIRST_PLANES/BOXES 시간, SUMMARY: 개수·분류·크기·테이블 근처 가장 큰 수평면), 개발 빌드 와이어프레임. 메시는 끔. **바뀐 설정 키(에디터 스크립트 `RoomScanXRSettings.Apply`로만):** `Assets/XR/Settings/OpenXRPackageSettings.asset`의 `ARPlaneFeature Android`(`com.unity.openxr.feature.arfoundation-meta-plane`) `m_enabled` 0→1, `ARBoundingBoxFeature Android`(`com.unity.openxr.feature.arfoundation-meta-bounding-boxes`) `m_enabled` 0→1. 그 밖의 ProjectSettings·매니페스트 변경 없음(`USE_SCENE`·`USE_ANCHOR_API`는 Meta 패키지가 빌드 때 넣음). 새 테스트 22개, 598/598. 씬 재생성(배선 확인). 연구 문서 `docs/superpowers/specs/2026-10-09-room-terrain-research.md`. 결정 `QUESTIONS_FOR_HYUN.md` S8-1–S8-5. 기기 확인: 공간 설정 후 MR TABLE → 권한 허용 → 와이어프레임·`ROOM_SCAN`; 거부해도 MR TABLE 정상. |
| S9 씬·테스트·APK 2종 | DONE | `cbb19d0`. 씬 재생성(배선 오류 0, fileID만 바뀜), 테스트 598/598. APK: `MetaAwards\Build\HandHero_20261009_1923_dev.apk`(126.5 MB, 클린 빌드 6.4분), `HandHero_20261009_1930_release.apk`(55.4 MB, 3.6분). 첫 개발 빌드(증분, `_1917_dev`)는 207.6 MB로 또 커져서(항목 합 126.4 MB) `BuildQuestApkDevClean`(`BuildOptions.CleanBuildCache`)을 새로 만들어 다시 빌드했다(`QUESTIONS_FOR_HYUN.md` S9-1). 두 APK 모두 매니페스트에 `com.oculus.permission.USE_SCENE`이 있고, 릴리스는 debuggable이 아니다. OpenXR 검증 경고 1건(패스스루 카메라 clear flags, 이전과 같음). |
| S10 최종 리뷰·보고서 | DONE | `b49af99` + 보고서 커밋. 리뷰 수정 F1-1: 보스 패턴 전환이 공격을 **시작할 때** 센다(피격으로 예고·연발이 끊겨도 Gunner 2번↔Lancer 2번이 계속 바뀜). 피격 뒤 재발사 하한이 공격 간격 배수를 따른다(`FireInterval × 0.5 × IntervalMult`, Striker는 그대로). 실패 테스트 3개 먼저(빨강 확인) → 수정, 601/601. 씬 재생성(fileID만 바뀜). **최신 APK:** `MetaAwards\Build\HandHero_20261009_1944_dev.apk`(126.5 MB, `BuildQuestApkDev` 증분인데 커지지 않음), `HandHero_20261009_1951_release.apk`(55.4 MB). 둘 다 `USE_SCENE` 있음, 릴리스는 debuggable 아님. 결정 `QUESTIONS_FOR_HYUN.md` S10-1. 남은 Minor 13건은 고치지 않고 기록만 했다. 보고서 `AUTO/REPORT_FOR_HYUN.md`(7절 리뷰 결과). |

## 세션 로그

- 2026-10-09 Phase 0 (대화형): 브랜치 `auto/2026-10-09-run`(`perf/freeze-hunt` `a3b1dd1`에서). 3차 기기 로그를 `AUTO/device_logs/2026-10-09/`에 커밋, 3차 AUTO 파일은 `AUTO/archive/2026-10-09/`로. 시작 시점 테스트 461/461.
- 2026-10-09 세션 1 (무인): S1, S2 완료. 테스트 461 → 472, 모두 통과. 씬 두 번 재생성. 헤드셋 확인 없음. 다음 세션은 S3부터.
- 2026-10-09 세션 2 (무인): S3, S4 완료. 테스트 472 → 507, 모두 통과. 씬 변경 없음(직렬화 필드 변화 없음). 헤드셋 확인 없음. 다음 세션은 S5(메타 진행 연결)부터: `MetaProgress`를 `RunDirector.FinishRecord`에서 `PlayerPrefsKeyValueStore.Instance`로 부른다.
- 2026-10-09 세션 3 (무인): S5, S6 완료. 테스트 507 → 561, 모두 통과. 씬 두 번, RunBot 프리팹 한 번 재생성. 헤드셋 확인 없음. 다음 세션은 S7(지형 위치 무작위)부터: 기둥은 씬 빌더의 `Pillar_L`/`Pillar_R`, 봇 스폰 지점은 `RunBotSpawn_1..3`(`RunDirector.botSpawnPoints`); 원형 선택이 `_spawnSeeds`에서 난수를 하나 더 쓰므로 배치 시드는 별도 스트림으로 둔다.
- 2026-10-09 세션 4 (무인): S7 완료. 테스트 561 → 576, 모두 통과. 씬 한 번 재생성. 헤드셋 확인 없음. 다음 세션은 S8(MR 방 스캔 스파이크)부터.
- 2026-10-09 세션 5 (무인): S8 완료. 테스트 576 → 598, 모두 통과. OpenXR Android 기능 2개 켬, 씬 한 번 재생성. 헤드셋 확인 없음. 다음 세션은 S9(씬·테스트·APK 2종)부터. 에디터의 활성 타깃이 Android라 `RoomScanProbe`의 권한 코드(`UNITY_ANDROID`)도 이미 컴파일됐다. APK에 `USE_SCENE`이 들어갔는지 빌드 매니페스트에서 확인할 것.
- 2026-10-09 세션 6 (무인): S9 완료. 씬 재생성, 테스트 598/598, 개발 APK(클린) 126.5 MB, 릴리스 APK 55.4 MB. 헤드셋 확인 없음. 다음 세션은 S10(최종 리뷰·보고서)부터.
- 2026-10-09 세션 7 (무인): S10 완료. 리뷰 Important F1-1 수정(`b49af99`), 테스트 601/601, 씬 재생성, APK 2종 다시 빌드. `AUTO/REPORT_FOR_HYUN.md` 작성. 헤드셋 확인 없음. 모든 태스크 DONE, 막힌 것 없음.

## S2 시간 추정 (3차 기기 런 기준, 헤드셋 없이 계산)

- 3차 런: 587.9초 = 전투 445.5초 + 전투 밖 142.4초(카운트다운·CLEARED 45초 + 상자·포털·상점 약 97초).
- Horde 3개: 45 → 30초, **−45초**.
- 보스: 102.5초 × 5/6 ≈ 85.4초, **약 −17초**.
- 봇 데미지 ×1.15는 시간을 직접 바꾸지 않는다. 받은 피해는 166 → 약 190으로 예상한다(부활이 생기면 길어질 수 있다).
- 합계 약 **526초 ≈ 8분 46초**(목표 8–9분 안). S5의 시작 유물 상자(약 5초)와 S6의 적 종류가 더해지면 기기에서 다시 잰다: `python AUTO\tools\run_summary.py <run_log.jsonl>`.
