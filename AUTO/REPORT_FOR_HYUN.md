# REPORT_FOR_HYUN — 4차 (첫 런 끊김, 밸런스, 메타 진행 A, 적·지형 다양성 1단계, MR 방 스캔 스파이크)

- **브랜치:** `auto/2026-10-09-run`. push는 하지 않았다.
- **테스트:** 601/601 통과. 시작할 때는 461개였다.
- **씬:** `HandHeroSceneBuilder.BuildAll`로 다시 만들었고, 배선 오류는 0이다.
- **APK:** 2종을 빌드했다.
- **헤드셋 확인은 하지 않았다.** 아래 체크리스트는 모두 기기에서 확인해야 한다.
- 막힌 것(`AUTO/BLOCKERS.md`)은 없다.

**설치할 APK** (`MetaAwards\Build\`)
- `HandHero_20261009_1951_release.apk` (55.4 MB): 성능과 체감 확인용이다.
- `HandHero_20261009_1944_dev.apk` (126.5 MB): 방 스캔 와이어프레임과 Profiler 확인용이다.
- 두 APK 모두 매니페스트에 `com.oculus.permission.USE_SCENE`가 있다. 릴리스는 debuggable이 아니다.
- 옛 APK `_1917_dev`(207.6 MB, 증분 빌드로 커진 것), `_1923_dev`, `_1930_release`는 리뷰 수정 전 버전이다. 쓰지 않는다.

## 1. 태스크 표

| 태스크 | 상태 | 커밋 | 한 줄 |
|---|---|---|---|
| S1 첫 런 끊김 | 완료 | `c119152` | 런 봇 풀을 씬 로드 때 만든다. `RenderWarmup`으로 3프레임 GPU 워밍업을 한다. 25 ms 작은 끊김 카운터(`HITCH`, `hitches=`, `worst_hitch=`)를 넣었다. |
| S2 밸런스 | 완료 | `aaee943` | Horde 45→30초, 보스 체력 배수 6→5, 런 봇 데미지 ×1.15. 예상 런 시간은 약 8분 46초다. |
| S3 남은 Minor 4건 | 완료 | `d7d363b` | 시스템 제스처 때 차지 취소, 머리 장치 조회 0.25초, 핀치 기록은 런 중에만, 봇 난수 재사용. |
| S4 메타 진행 Core | 완료 | `3221b3d` | `MetaProgress` + `IKeyValueStore`(키 `hh.meta.*`, 버전 1), PlayerPrefs 어댑터. |
| S5 메타 진행 연결 | 완료 | `873b58f` | STARTING RELIC 상자, 메뉴 최고 기록 줄, NEW BEST·UNLOCKED, RESET PROGRESS(두 번 눌러야 한다). |
| S6 적 다양성 1단계 | 완료 | `c430f3d` | Striker·Sniper·Gunner·Lancer와 Gunner↔Lancer를 번갈아 쓰는 보스. 바뀐 것은 생김새와 공격뿐이다. |
| S7 지형 다양성 1단계 | 완료 | `5483222` | 섬마다 시드로 기둥 2개와 봇 스폰 3곳을 무작위로 배치한다. 퀵 매치와 튜토리얼은 고정 배치다. |
| S8 MR 방 스캔 스파이크 | 완료 | `75ac30f` | `RoomScanProbe`는 MR TABLE의 메인 메뉴에서만 돈다. 평면과 바운딩 박스를 켜고 `ROOM_SCAN` 기록을 남긴다. 연구 문서도 썼다. |
| S9 씬·테스트·APK | 완료 | `cbb19d0` | 클린 빌드 메서드 2개를 추가했다. APK 2종. |
| S10 최종 리뷰·보고서 | 완료 | `b49af99` + 이 커밋 | 리뷰에서 Important 1건(F1-1)을 고쳤다. Minor는 기록만 했다(아래 7절). |

## 2. 헤드셋 체크리스트 (우선순위 순)

먼저 **릴리스** APK로 A–F를 확인하고, G만 **개발** APK로 확인한다. 패키지 이름은 `com.hyun.handhero`다.

**A. RUN 시작 직후 30초가 부드러운가 (가장 먼저)**

1. 설치하고 logcat을 받으면서 RUN을 한 판 한다. PowerShell에서 한 줄씩 실행한다.
   ```powershell
   $adb = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"
   & $adb install -r "C:\Users\AISTUDIO\Desktop\Hyun's Playground\MetaAwards\Build\HandHero_20261009_1951_release.apk"
   & $adb logcat -c
   & $adb logcat -s VrApi:* Unity:* > freeze_session.txt
   ```
   - 마지막 줄은 플레이하는 동안 계속 돈다. 다 하면 Ctrl+C로 멈춘다.
   - 자세한 방법은 `AUTO/archive/2026-10-09/PERF_REPORT.md` 3절에 있다.
2. 메뉴로 한 번 돌아간 뒤 로그를 가져온다.
   ```powershell
   & $adb pull /sdcard/Android/data/com.hyun.handhero/files/perf_log.txt .\perf_log.txt
   ```
3. 볼 것:
   - `VrApi`의 `Stale=`가 RUN 시작 뒤 30초 동안 0에 가까운지 본다.
   - `perf_log.txt`의 flush 줄에 있는 `hitches=`와 `worst_hitch=`(25 ms 넘는 프레임), `HITCH` 줄을 본다.
   - 세션 헤더의 `warmup … ms`를 본다. 앱 시작이 약 1초까지 늘어날 수 있다.
4. **같이 볼 것:** 3차 로그를 다시 보니 그 30초의 Stale은 시스템 프로세스가 레이어를 올렸다 내린 구간과 겹쳤다(`QUESTIONS_FOR_HYUN.md` S1-2).
   - RUN 직후 헤드셋 안에 시스템 알림이나 안내 창이 떴는지 기억해 둔다.
   - logcat에서 `LCnt=`가 1이 아닌 구간과 Stale을 같이 본다.

**B. 런 길이와 난이도**

5. 한 판을 끝까지 하고 다음 명령을 실행한다.
   ```powershell
   & $adb pull /sdcard/Android/data/com.hyun.handhero/files/run_log.jsonl .\run_log.jsonl
   python AUTO\tools\run_summary.py .\run_log.jsonl
   ```
   - 목표는 8–9분이다. 3차에서는 받은 피해가 166이었고, 이번에는 약 190을 예상한다. 체감 난이도도 적어 둔다.
   - 새 출력 "By bot archetype"에서 원형별 처치 수와 받은 피해를 볼 수 있다.

**C. 메타 진행**

6. 첫 런 끝 화면에 `NEW BEST`가 뜨는지 본다. 그 조준 모드의 첫 기록도 NEW BEST로 센다.
7. 섬 5까지 간다(포기해도 된다). 끝 화면에 `UNLOCKED: SECOND WIND START`가 뜨는지 본다.
   - 다음 RUN에서 섬 1 전에 STARTING RELIC 상자(유물 + NONE)가 나와야 한다.
8. 메뉴 배너 아래 `BEST  ISLAND n  -  WIN m:ss`가 보이는지 본다. AIM을 바꾸면 그 모드의 기록으로 바뀌어야 한다.
9. 일시정지 → RESET PROGRESS → `CONFIRM RESET` → `PROGRESS RESET`. 그 뒤 메뉴의 기록 줄이 사라지고 시작 유물 상자도 안 나와야 한다.
   - **주의(Minor F3-3):** 런 중에 초기화하면 진행 중인 그 런이 끝날 때 다시 기록된다. 확인은 퀵 매치의 일시정지에서 한다.

**D. 3차에서 못 해 본 것**

10. 부활 아이템으로 부활한다. 죽은 자리로 다시 끌려가지 않아야 한다.
11. 일부러 져서 DEFEAT 화면을 본다. 그 런도 `run_log`에 기록되는지 확인한다.

**E. 적 4종과 섬별 지형**

12. 섬 3부터 Gunner(주황, 넓은 블록, 3연발), 5부터 Sniper(보라, 바늘, 긴 예고), 7부터 Lancer(분홍, 창, 굵은 빔)가 나온다.
    - 모양과 소리만으로 구별되는지 본다.
    - 데미지가 공정하게 느껴지는지 본다.
    - **Gunner의 주황이 내 CURSOR 조준 표시나 과녁과 헷갈리는지** 특히 본다(Minor F3-4).
13. 보스가 Gunner 연발 2번 → Lancer 2번을 계속 번갈아 쓰는지 본다. 잘 맞혀도 Lancer에만 머물지 않아야 한다(F1-1 수정).
14. 섬마다 기둥 위치가 다른지 본다.
    - 시작할 때 봇 하나는 보여야 한다.
    - 기둥이 봇과 겹치지 않아야 한다.
    - 기둥이 여전히 빔을 막아야 한다.

**F. 회귀**

15. 퀵 매치, 튜토리얼, ASSIST·CURSOR 두 조준 모드, VR ARENA·MR TABLE 모두 3차처럼 동작해야 한다.
    - 퀵 매치와 튜토리얼의 기둥은 예전 자리에 있어야 한다.

**G. MR TABLE 방 스캔 프로브 (개발 APK)**

16. Quest 설정 > 물리적 공간 > 공간 설정(Space Setup)에서 방을 스캔하고, 플레이하는 테이블을 가구(Table)로 표시한다.
17. 개발 APK에서 MR TABLE을 누르고 "공간 데이터" 권한을 허용한다.
    - 평면은 초록 선, 박스는 노랑 선으로 보이는지 본다.
    - 메뉴에서 약 5초 기다린 뒤 VR ARENA로 돌아가거나 매치를 시작한다. 이때 요약이 써진다.
18. `perf_log.txt`에서 `ROOM_SCAN` 줄을 찾는다. STARTED, FIRST_PLANES·FIRST_BOXES 시간, SUMMARY의 개수·분류·`near_table` `dy`·`d`를 본다.
19. 권한을 거부한 경우도 확인한다(설정 > 앱 > Hand Hero > 권한에서 거부하거나 다시 설치한다).
    - MR TABLE, 퀵 매치, RUN이 평소대로 동작해야 한다.
    - 로그에 `PERMISSION_DENIED`가 남아야 한다.
    - 대화상자를 거부하지 않고 바깥을 눌러 닫으면 아무 기록도 남지 않는다(Minor S8-1-2).

## 3. Hyun 대신 내린 결정 (모두 되돌릴 수 있음, 자세한 내용은 `QUESTIONS_FOR_HYUN.md`)

| # | 결정 | 되돌리려면 |
|---|---|---|
| S1-1 | GPU 워밍업은 API가 아니라 실제 물체를 그려서 한다(`GraphicsStateCollection`은 기기 트레이스가 필요하다). | 씬 `Match`의 `RenderWarmup`을 끈다. |
| S1-2 | 30초 Stale은 시스템 오버레이가 원인일 수 있어서 기록해 두었다. | (확인 항목 A4) |
| S3-1 | 핀치 기록은 256개 상한이 아니라 런 중에만 쌓는다. | `PointingBeamController.Awake`의 `Recording = false` 한 줄을 지운다. |
| S3-2 | CURSOR 방아쇠는 시스템 제스처 뒤에도 지금 규칙을 따른다. | `XRHandsInputSource.Sample`에서 `_trigger.RequireReopen()`을 부른다. |
| S4-1 | 메타 키 `firstWinAim`·`aims`를 더하고 `newUnlocks`·`lastStart`는 뺐다. 첫 기록도 NEW BEST로 센다. | `MetaProgress.cs` |
| S5-1 | 최고 기록 줄은 배너의 안내 문구 자리에 둔다(새 줄을 넣으면 겹친다). 가운뎃점 대신 `-`를 쓴다. | `MetaText.MenuBanner` 또는 씬 빌더 |
| S5-2 | 시작 유물을 고르는 시간도 런 시간에 들어간다. | `RunStateMachine.Tick`(테스트도 같이 고친다) |
| S5-3 | RESET PROGRESS는 일시정지 패널에만 두고, 3초 안에 두 번 눌러야 한다. | 씬 빌더 `Button_RESET PROGRESS`, `confirmWindow` |
| S5-4 | `run_log`에 `start_relic` 필드를 넣었다. | `RunRecordJson.ToJson` |
| S6-1 | Gunner 연발 간격은 0.18초가 아니라 0.36초다(봇 발사 쿨다운이 0.35초다). | Archetypes 표의 `BurstGap` + 봇 `fireCooldown` |
| S6-2 | 연발의 2·3번째 발은 다시 조준한다. | `BotBrain.UpdateShooting`의 재조준 한 줄 |
| S6-3 | 생김새는 모양이 먼저고 색이 다음이다. 소리 높이는 Sniper 0.7, Gunner 1.35, Lancer 0.55다. | Archetypes 표, 빌더 `ArchetypeShapes` |
| S6-4 | Lancer의 넓은 샷은 보이는 굵기만 넓고 판정은 같다(플레이어 차지샷과 같은 규칙). | `PointingBeamController.Fire`를 SphereCast로 바꾼다. |
| S6-5 | 출현표는 섬 1–2 Striker, 3 Gunner, 5 Sniper, 7 Lancer부터다. | Rules의 `*FromIsland` |
| S6-6 | `run_log`에 `kills_by`·`damage_by`를 넣었다. | `RunRecordJson` |
| S7-1 | 배치 규칙: 벽 여백 1.5, 기둥 간격 3, 시작점 반경 5, 스폰 반경 3, 스폰끼리 5, 시도 200번. | `ArenaLayoutApplier` > Rules. 끄려면 `RunDirector`의 `Layout`을 비운다. |
| S7-2 | 기둥은 시작점보다 +z 2 m 앞에 둔다. 과녁 4개와 겹치지 않게 한다. | `MinPieceForward` = -100, `Keep Clear`를 비운다. |
| S7-3 | 배치 시드는 런마다 새로 뽑는다(`run_log`의 `layout_seed`). | `RunDirector.BeginRun`의 `_layoutRunSeed` |
| S8-1 | 평면과 바운딩 박스만 켜고 메시는 끈다. | OpenXR(Android) 체크 해제, `RoomScanProbe` Probe Enabled |
| S8-2 | 프로브는 MR TABLE + 메인 메뉴에서만 돈다. | `RoomScanProbe.InMenu()` |
| S8-3 | 권한은 앱 실행당 한 번만 묻는다. | Core `RoomScanFlow` |
| S8-4 | `perf_log`에 ` \| 메모`를 붙이는 `Mark` 오버로드를 더했다. | `PerfSample.Note` |
| S8-5 | "테이블 근처"는 아레나 바닥 중심에서 수평 1.0 m, 높이 0.5 m 안이다. | `RoomScanProbe` Near Table 값 |
| S9-1 | 클린 빌드 메서드 `BuildQuestApkDevClean`·`ReleaseClean`을 추가했다. | `BuildScript.cs` |
| S10-1 | 보스 패턴 전환은 공격을 시작할 때 센다. 피격 뒤 재발사 하한은 `IntervalMult`를 따른다. | `BotBrain.CountAttackForSwitch()`의 위치, `StartEvade` |

## 4. 바뀐 값과 되돌리는 법

| 값 | 전 → 후 | 어디서 | 되돌리려면 |
|---|---|---|---|
| `HordeTime` | 45 → 30초 | `RunStateMachine.cs` 기본값 + 씬 `RunDirector` > Params | 45로 바꾸고 씬을 다시 만든다(테스트 `IslandSpec_CountsAndEnemyScaling`도 같이). |
| `BossHealthMult` | 6 → 5 | 위와 같음 | 6 |
| `EnemyDamageMult` | (없음) → 1.15 | 위와 같음. 런 봇에만 적용되고 Elite는 1.5×1.15다. | 1로 바꾼다. |
| `hitchThresholdMs` | 새 값 25 ms | `PerfSpikeLogger` | 기록만 바뀐다. |
| 원형 숫자 | 새 값: Sniper 예고 ×1.6·간격 ×1.8·피해 ×1.8 / Gunner 3연발·0.36초·×0.45 / Lancer 예고 ×1.8·간격 ×2.5·피해 ×2.2 / 보스 `SwitchEvery` 2 | 씬 `RunDirector` > Archetypes | Striker만 쓰려면 `*FromIsland`를 99로 둔다. |
| 피격 뒤 재발사 하한 | `FireInterval×0.5` → `×0.5×IntervalMult` | `BotBrain.StartEvade` | `* _active.IntervalMult`를 지운다. Striker와 Gunner는 그대로다. |
| OpenXR Android 기능 | `ARPlaneFeature Android`·`ARBoundingBoxFeature Android`의 `m_enabled` 0 → 1 | `Assets/XR/Settings/OpenXRPackageSettings.asset`(`RoomScanXRSettings.Apply`로 바꿨다) | Project Settings > XR Plug-in Management > OpenXR(Android)에서 두 기능을 끈다. 그러면 `USE_SCENE`도 매니페스트에서 빠진다. |
| 메타 저장 | 새 값: PlayerPrefs `hh.meta.*` | `MetaProgress` | 게임 안에서 RESET PROGRESS를 누른다. |

그 밖의 ProjectSettings, 매니페스트 손 편집, 새 패키지, XR·렌더 설정 변경은 없다. 퀵 매치와 튜토리얼의 수치도 바뀌지 않았다.

## 5. 방 기반 지형 연구 결과 (`docs/superpowers/specs/2026-10-09-room-terrain-research.md`)

**알게 된 것 (패키지 소스 기준, 기기 확인 전)**

- 지금 설치된 Meta OpenXR 2.6.1 + AR Foundation 6.6.2만으로 평면(바닥·벽·천장·**테이블**·소파 등)과 바운딩 박스(분류가 붙은 가구)를 받을 수 있다. 새 패키지는 필요 없다.
- **Quest는 실시간으로 스캔하지 않는다.** 공간 설정(Space Setup)에 저장된 장면 모델을 돌려준다. 공간 설정이 없으면 아무것도 나오지 않는다.
  - 앱 안에서 `TryRequestSceneCapture()`로 공간 설정을 띄울 수는 있다. 그동안 앱은 멈춘다.
- 권한 `USE_SCENE`은 Meta 빌드 단계가 매니페스트에 넣는다. 실행 중에 묻는 것은 앱이 한다.
  - 기능이 실패하거나 권한이 거부되면 기능만 꺼지고, OpenXR과 패스스루는 정상이다.
- 데이터는 트래킹 공간의 실제 미터 단위다. 아레나로 바꾸는 식은 `arena = (tracking - 테이블점) × WorldScale`이다.

**지형으로 만드는 방안 (다음 라운드 제안)**

1. 아레나를 실제 테이블에 맞춘다. 바꾸는 것은 `TabletopParams`의 뷰어 스케일과 오프셋뿐이고, XR Origin은 그대로 둔다.
2. 테이블 가장자리를 아레나 경계로 쓴다. 직사각형 테이블이면 직사각형 섬이 된다.
3. 테이블 위의 가구 박스는 엄폐물로 쓴다. 테이블 밖의 가구는 배경의 "먼 섬"으로 쓴다.
4. 방에서 얻은 조각은 S7 `ArenaLayout`에 고정 조각으로 넣는다. 나머지는 시드 배치로 채운다. 공정하지 않으면 시드 배치로 돌아간다.
5. 권한, 공간 설정, 테이블 중 하나라도 없으면 지금 시드 배치를 그대로 쓴다.
6. 개인정보: 장면 데이터는 기기를 떠나지 않는다. 로그에는 개수, 분류, 크기, 시간만 남긴다.

**열린 질문 (추천 답)**

| # | 질문 | 추천 |
|---|---|---|
| Q1 | 아무것도 없을 때 게임이 공간 설정을 띄울까? | 아직은 아니다. 나중에 MR TABLE 메뉴에만 "SCAN ROOM" 버튼을 둔다. |
| Q2 | 평면 제공자는? | `XrFbScene`(기본값). 룸 메시 면은 벽에만 도움이 된다. |
| Q3 | 메시를 쓸까? | 다음 단계에서는 쓰지 않는다. 가림(occlusion)용으로만 다시 검토한다. |
| Q4 | 테이블이 여러 개면? | 앉은 자리에서 손이 닿는 거리(약 1 m) 안에서 가장 넓은 위쪽 `Table` 면을 쓴다. 없으면 시드 배치를 쓴다. |
| Q5 | 자동으로 맞출까, 버튼으로 맞출까? | "FIT TO TABLE" 버튼으로 한다. 아레나가 갑자기 움직이지 않게 한다. |
| Q6 | 릴리스에도 프로브를 둘까? | 로그는 남기고 와이어프레임은 끈다. 진짜 기능이 들어오면 프로브는 지운다. |
| Q7 | 긴 세션에서 박스가 밀리나? | 모른다. 기기에서 세션 시작 때와 런 뒤의 `near_table`을 비교한다. |

**조정 손잡이**

- 적: 씬 `Match` > `RunDirector` > **Archetypes** 표에서 원형마다 `TelegraphMult`, `IntervalMult`, `DamageMult`, `BurstCount`, `BurstGap`, `BeamWidthMult`, `BeamDurationMult`, `TelegraphWidthMult`, `FirePitch`, 색을 바꾼다. 보스는 `SwitchEvery`다. 출현 섬은 Rules의 `GunnerFromIsland`, `SniperFromIsland`, `LancerFromIsland`다.
- 지형: 씬 `Match` > `ArenaLayoutApplier` > **Rules**에서 `Margin`, `MinPieceGap`, `StartClearRadius`, `SpawnClearRadius`, `MinSpawnGap`, `MinPieceForward`, `KeepClearPadding`, `Attempts`, 스폰 영역을 바꾼다.
- 방 스캔: `RoomScanProbe`의 Probe Enabled, wireframe(Off / DevBuildsOnly / Always), Near Table Radius, Near Table Height Gap.

## 6. 다음 단계 3가지 (게임 우선)

1. **기기에서 잰 숫자로 밸런스와 적을 다시 맞춘다.** `run_summary.py`의 런 시간, 원형별 피해·처치, 차지 오인률을 본다.
   - 먼저 손볼 것은 Gunner의 색(F3-4)과 지느러미 색(F4-1)이다.
   - 그다음 `EnemyDamageMult`와 원형 숫자를 조정한다.
   - 런이 여전히 쉬우면 원형이 늘어나는 섬을 앞당긴다.
2. **적 다양성 2단계와 지형 2단계를 만든다.**
   - 원형마다 움직임 성격을 하나씩 준다(Sniper는 멀리 머물고, Lancer는 돌진 뒤 멈춘다). 규칙은 플레이어와 같게 유지한다.
   - 지형에는 새 조각(낮은 벽, 떠 있는 섬)과 섬 테마를 넣는다.
   - 같은 라운드에서 S7 Minor 2건(시야 규칙 테스트, 실패 시 대체 배치)을 함께 정리한다.
3. **MR TABLE "FIT TO TABLE"을 만든다.** 프로브의 `near_table` 결과가 기기에서 쓸 만하면 시작한다. 아레나를 실제 테이블에 맞추고, 테이블 위 가구 박스를 엄폐물로 쓴다.
   - 그 전에 프로브 Minor(권한 대화상자를 닫은 경우, 매 프레임 JNI 호출)를 고친다.

## 7. 리뷰 결과

`a3b1dd1..HEAD`(`.unity` 제외)를 코드 리뷰했다. Critical은 0건이다.

**고친 것** (`b49af99`, 테스트 598 → 601, 씬 재생성, APK 2종을 다시 빌드)

- **F1-1 보스 패턴 전환 (Important)**
  - 문제: 예전에는 공격의 마지막 발에서만 횟수를 셌다. 피격으로 예고나 연발이 끊기면 횟수가 오르지 않아, 잘 맞히는 플레이어 상대로 보스가 Lancer에 머물렀다.
  - 수정: 이제 공격을 **시작할 때**(예고 시작, 예고가 없으면 첫 발) 센다. 끊기지 않은 런에서는 순서가 예전과 똑같다.
  - 함께 고친 것: 피격 뒤 재발사 하한이 `IntervalMult`를 따른다. Striker와 Gunner는 그대로이고, Sniper와 Lancer가 피격 덕분에 더 빨리 돌아오지 않는다.
  - 새 테스트 3개: `Boss_KeepsAlternating_WhenEveryTelegraphIsInterrupted`, `Boss_KeepsAlternating_WhenEveryBurstIsCutAfterItsFirstShot`, `Hit_DuringATelegraph_RefireFloorScalesWithTheAttack`.
- S7·S8 리뷰에서 고칠 것은 없었다.

**남은 Minor (기록만 함, 고치지 않음)**

| ID | 파일 | 내용 |
|---|---|---|
| S7-1-1 | `Core/ArenaLayout.cs:177` | 시작 시야 규칙이 지금 지형에서는 거의 적용되지 않는다. 스폰이 모두 y≥2인데 `Pillar_L` 꼭대기가 y=0이라서 가릴 수 없다. 그래서 다시 시도하는 경로를 검증하는 테스트가 없다. |
| S7-1-2 | `Core/ArenaLayout.cs:98` | 규칙을 만족하는 배치가 없고 대체 배열도 안 주면, 시도 중이던 배열이 섞인 채 나온다. 놓이지 않은 조각은 (0,0,0)에 남는다. 지금 호출부는 대체 배치를 넘긴다. |
| S8-1-2 | `RoomScanProbe.cs:175` | 권한 대화상자를 거부하지 않고 닫으면(`PermissionRequestDismissed`) 답이 오지 않는다. 그러면 그 실행 동안 프로브가 Asked에 머물고 아무것도 기록하지 않는다. MR TABLE 자체는 정상이다. |
| S8-1-3 | `RoomScanProbe.cs:117` | `SetManagers(true)`가 예외를 던지면 `UNAVAILABLE`을 기록한 뒤에도 STARTED(서브시스템 0)·STOPPED·요약이 이어서 기록된다. 로그가 헷갈린다. |
| S8-2-1 · F2-3 · F4-2 | `RoomScanProbe.cs:112` | MR TABLE 메인 메뉴에서 권한 확인(JNI)을 매 프레임 부른다. 권한이 정해진 뒤에도 계속 부른다. 메뉴에서만 생기는 작은 낭비다(리뷰 세 곳에서 같은 지적). |
| F2-2 | `RoomScanProbe.cs:115` | MR TABLE에서 메뉴를 떠나는 프레임에 매니저 두 개를 끄고, 요약 문자열을 만들고, `Debug.Log`(스택 트레이스)를 부른다. 매치 시작 첫 프레임에 작은 끊김이 생길 수 있다. 확인은 A3의 `HITCH` 줄로 한다. |
| F1-2 | `Core/RunStateMachine.cs:370` | STARTING RELIC 화면에 머문 시간도 런 시간과 최단 승리 기록에 들어간다(S5-2에서 일부러 정한 것과 같다). 시간 제한이 없는 화면이라 기록이 늘어날 수 있다. |
| F3-3 | `RunDirector.cs:151` | 런 중에 일시정지 메뉴에서 RESET PROGRESS를 누르면, 그 런이 끝날 때 섬·기록·해금이 다시 저장된다. 고치려면 초기화한 런은 메타에 기록하지 않게 한다. |
| F3-4 | `Core/BotArchetype.cs:152` | Gunner의 주황(1, 0.5, 0.1)이 과녁 주황, 그리고 CURSOR 조준 표시·낙하선 주황(1, 0.6, 0.15)과 거의 같다. 적을 한눈에 구별하기에 좋지 않다. |
| F4-1 | `Editor/HandHeroSceneBuilder.cs:836` | S6-3은 "지느러미는 빨간 계열 그대로"라고 했지만, 빌더가 `Fin_L`·`Fin_R`을 `baseColorRenderers`에 넣어서 지느러미도 원형 색으로 칠해진다. 결정 기록과 코드가 다르다. |
| F4-3 | `ResetProgressButton.cs:16` 외 | 규칙 5b.3(`[Tooltip]`)을 지키지 않은 필드가 있다: `idleText`, `confirmText`, `doneText`, `RenderWarmup.vignette`, `BotArchetype`의 `Id`, `Shapes`, `Attack`. |
