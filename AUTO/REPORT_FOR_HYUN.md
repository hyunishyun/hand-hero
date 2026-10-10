# REPORT_FOR_HYUN — 5차 (ASSIST 핀치 놓기, 남은 Minor, 적 움직임, 지형 2단계, 데모 모드)

- **브랜치:** `auto/2026-10-09-r5`(4차 `a8cd3c2` 위, Phase 0 `ae71d0f`). push는 하지 않았다.
- **테스트:** EditMode 766/766 통과. 시작할 때는 601개였다(새 165개).
- **씬:** `HandHeroSceneBuilder.BuildAll`로 다시 만들었다. 배선 오류는 0이다.
- **APK:** 2종을 빌드했다. 최종 리뷰에서 코드가 바뀌지 않았으므로 이 APK가 마지막 코드다.
- **헤드셋 확인은 하지 않았다.** 아래 체크리스트는 모두 기기에서 확인해야 한다.
- 막힌 것(`AUTO/BLOCKERS.md`)은 없다. ProjectSettings, 패키지, XR·렌더 설정은 바꾸지 않았다.

**설치할 APK** (`MetaAwards\Build\`)
- `HandHero_20261010_0203_release.apk` (55.5 MB): 체감, 성능, 녹화용이다.
- `HandHero_20261010_0156_dev.apk` (126.7 MB, 클린 개발 빌드): 방 스캔 와이어프레임과 Profiler 확인용이다.
- 4차 APK(`HandHero_20261009_*`)는 쓰지 않는다.

**이번 라운드를 돌린 방식 (D7, 울트라코드 워크플로)**
- T0은 메인 체크아웃에서 TDD로 끝까지 했다. 같은 시간에 T1–T4 초안을 git 워크트리 4개(`draft/r5b-T1`–`T4`)에서 따로 썼다(10-09 23:42–23:55, `draft (uncompiled)` 커밋 4개).
- 그다음 초안을 하나씩 메인 브랜치에 통합했다. 순서는 T1 00:24 → T2 00:48 → T3 01:02 → T4 01:14다. 통합할 때마다 사전 리뷰(P)를 반영하고, Unity 컴파일, 테스트, 씬 빌드를 했다.
- 리뷰는 태스크마다 리뷰어 2명(R1·R2)이 봤고, 최종 리뷰는 4개 관점(F1–F4)으로 봤다.
- Unity 배치 명령은 한 번에 하나만 돌릴 수 있다(계획 3절). 그래서 통합, 테스트, 씬·APK 빌드, 이 보고서는 에이전트 하나가 차례로 한다. 이 구간에 에이전트가 하나만 보이는 것은 정상이다.
- 통합이 끝난 초안 브랜치 `draft/r5b-T1`–`T4`와 워크트리는 이 커밋에서 정리했다(결과는 `PROGRESS.md`).

## 1. 태스크 표

| 태스크 | 상태 | 커밋 | 한 줄 |
|---|---|---|---|
| T0 ASSIST 핀치 놓기 | 완료 | `38ca4b6`, `e1ee7aa` + 리뷰 수정 `deb63c5` | 놓기 = Meta 검지 핀치 플래그(2프레임) / 최고값 대비 0.2 하락 / 바닥 0.75 / 예전 절대 0.6 중 먼저 오는 것. 원시 강도를 쓴다. 핀치마다 진단값을 런 기록에 남기고 `run_summary.py`가 요약한다. |
| T1 남은 Minor 13건 | 완료 | 초안 `6ab52a4` → `9acd761`, `0712939` | 13건 모두 고쳤다. 배치 대체 규칙, 방 스캔 프로브 4건, STARTING RELIC 시간 제외, 런 중 RESET은 미기록, Gunner 연두, 지느러미 빨강, 툴팁. |
| T2 적 움직임 성격 | 완료 | 초안 `086cb40` → `207a8a9`, `f475c11` | Sniper는 멀리(18 m), 쏜 뒤 자리를 옮긴다. Gunner는 가까이(8.4 m) 넓게 좌우로 움직인다. Lancer는 돌진 → 멈춰 예고·발사 → 다시 돌진한다. 보스는 패턴별로 움직이고, Striker는 4차와 비트까지 같다. |
| T3 지형 2단계 | 완료 | 초안 `28d4a2e` → `35a2b6e`, `3f5e2a4` + 리뷰 수정 `deb63c5` | 섬 깊이별로 낮은 벽·떠 있는 발판·가는 기둥을 놓는다. 테마는 Dusk/Frost/Ember 3종이고, 섬별 지형이 런 기록에 남는다. 조각 안에서 쏘는 빔은 막힌다. |
| T4 데모 모드 | 완료 | 초안 `5f3e272` → `a5691a5`, `617b778` + 리뷰 수정 `deb63c5` | 메인 메뉴 DEMO, 죽지 않는 연습장(느린 Striker 2마리, 과녁 4개), 반투명 손, 동작 캡션, 녹화 가이드(`AUTO/RECORDING_GUIDE.md`). |
| T5 씬·테스트·APK·리뷰·보고서 | 완료 | `34514c0` + 이 커밋 | 씬 재생성, 테스트 766/766, APK 2종, 최종 리뷰(고칠 것 없음), 이 보고서. |

## 2. 헤드셋 체크리스트 (우선순위 순)

**릴리스** APK로 A–D와 F를 확인하고, E만 **개발** APK로 확인한다. 패키지 이름은 `com.hyun.handhero`다. PowerShell에서 한 줄씩 실행한다.

설치하기 전에 기기에 남은 예전 로그를 PC로 받아 두고, 기기에서는 지운다(딥 리뷰 DR-7).
- `run_log.jsonl`은 `adb install -r`로 다시 설치해도 지워지지 않고 계속 쌓인다. 4차 때 받은 파일에도 3차 APK 런 3개와 4차 APK 런 1개가 같이 들어 있었다.
- 딥 리뷰 DR-5 전의 `run_summary.py`는 예전 런과 새 런을 한 통에 더했다. 예를 들어 4차 파일에 새 런 하나(10번 중 1번이 1초 넘음)를 붙이면 "141번 중 38번(27%)"으로 나와서, ASSIST 버그가 그대로인 것처럼 보였다.
- 지금은 아래처럼 나눠서 보여 준다. 그래도 지워 두면 파일에 이번 런만 남아서 읽기 쉽다.

```powershell
$adb = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"
& $adb pull /sdcard/Android/data/com.hyun.handhero/files/run_log.jsonl .\run_log_before_r5.jsonl
& $adb pull /sdcard/Android/data/com.hyun.handhero/files/perf_log.txt .\perf_log_before_r5.txt
& $adb shell rm -f /sdcard/Android/data/com.hyun.handhero/files/run_log.jsonl /sdcard/Android/data/com.hyun.handhero/files/perf_log.txt
& $adb install -r "C:\Users\AISTUDIO\Desktop\Hyun's Playground\MetaAwards\Build\HandHero_20261010_0203_release.apk"
& $adb shell dumpsys package com.hyun.handhero | Select-String versionName
```

- 두 `pull` 줄은 기기에 파일이 없으면 오류를 내지만 괜찮다.
- 마지막 줄은 지금 설치된 APK를 보여 준다.
  - 딥 리뷰 DR-7 뒤에 빌드한 APK는 `versionName=9.2.0+20261010_1042_release`처럼 APK 파일 이름과 같은 날짜·시각·종류를 단다.
  - 같은 값이 런 기록마다 `build`로, `perf_log` 머리줄에는 `app`으로 남는다.
  - 그 전에 빌드한 APK(위의 `0203`, `0156` 포함)는 `9.2.0`만 나온다.

로그는 플레이가 끝난 뒤(메뉴로 한 번 돌아간 뒤) 가져온다.

```powershell
& $adb pull /sdcard/Android/data/com.hyun.handhero/files/run_log.jsonl .\run_log.jsonl
& $adb pull /sdcard/Android/data/com.hyun.handhero/files/perf_log.txt .\perf_log.txt
python AUTO\tools\run_summary.py .\run_log.jsonl
```

- `run_summary.py`는 가장 최근에 시작한 런의 빌드만 요약한다(딥 리뷰 DR-7).
  - 파일에 빌드가 여럿이면 맨 위 "Builds in the log"에 빌드마다 런 수와 시작 시각이 나온다.
  - 모두 합치려면 `--all`을 붙인다. 다른 빌드 하나만 보려면 `--build 1042`처럼 이름 일부를 준다.
  - 도장이 없는 런은 두 묶음이다(딥 리뷰 DR-5).
    - `no build stamp (round 5)`: 도장 전에 빌드한 5차 APK(위의 `0203`, `0156` 포함)의 런이다. 5차 APK는 런마다 `release_by`를 남기므로 이것으로 가른다.
    - `no build stamp (rounds 3-4)`: 3·4차 APK의 런이다.
    - `--build none`은 둘 다, `--build "round 5"`는 5차 쪽만 고른다.
    - 그래서 예전 로그를 지우지 못했어도, `0203` APK로 RUN을 한 뒤의 기본 요약은 5차 런만 센다.
  - 설치한 뒤의 런만 보려면 `--since "2026-10-10 14:00"`처럼 헤드셋 시각을 준다. `--all`·`--build`보다 먼저 걸린다.
- "Pinch holds by aim mode"의 모드별 첫 줄(쥐기 수, 중앙값, 1초 넘는 비율)과 `released by`·`strength` 줄은 진단값이 있는 런(5차 이후 APK)만 센다(딥 리뷰 DR-5).
  - `--all`로 예전 런을 합치면, 예전 쥐기는 그 아래 `older APKs (rounds 3-4, not in the numbers above)` 줄에 따로 나온다.
  - 예전 런만 있는 모드는 첫 줄에 예전 숫자를 쓰고 `released by: not recorded (logs before round 5)`라고 적는다.

**A. ASSIST 연사와 차지 (가장 먼저)**

1. 메인 메뉴에서 AIM을 **ASSIST**로 두고 **RUN**을 시작한다.
   - 핀치 기록은 RUN에서만 쌓인다. DEMO와 퀵 매치는 아무것도 기록하지 않는다(리뷰 F4-3).
2. 섬 1에서 30초 동안 짧은 핀치로 연사한다.
   - 쏘는 수가 핀치 수와 비슷해야 한다. 4차처럼 "쥔 채로 남아" 연사가 끊기면 안 된다.
   - 발사 쿨다운(0.35초, 한 발 버퍼) 때문에 1초에 3번보다 빨리 탭하면 발사 수가 핀치 수보다 적다. 이것은 정상이다(리뷰 T0-R2-6).
3. 핀치를 1초쯤 쥐었다 놓기를 3번 한다. 0.25초 뒤 차지가 시작되고, 놓을 때 차지샷이 나가야 한다.
   - 쥐고 있는데 차지가 끊기거나 차지샷이 일찍 나가는지 본다.
4. 런을 끝내거나 포기한 뒤 위 명령으로 `run_summary.py`를 돌린다. "Pinch holds by aim mode"에서 볼 것:
   - Assist의 1초 넘는 쥐기 비율. 3차 APK로 한 18:07 런은 103번 중 22번이었다(4차 APK의 23:02 런은 20번 중 15번이 차지를 시작했다). 이번에는 일부러 한 차지 3번 정도만 남아야 한다.
   - 맨 위 "Build:" 또는 "Summarizing the newest build only:" 줄이 방금 설치한 APK인지 먼저 본다. `0203` APK는 `no build stamp (round 5)`로 나온다.
   - `meta_seen`: Meta 검지 핀치 신호가 이 펌웨어에서 실제로 오는지 보여 준다.
   - `release_by` 분포를 읽을 때 주의할 점이 두 가지다(리뷰 Minor, 고치지 않음).
     - `relative`가 거의 전부로 나와도 정상이다. 이 라벨은 "새 규칙만 놓았다"는 뜻이 아니다. 손을 보통 속도로 펴면 상대 기준이 먼저 걸린다(T0-R2-5, F1-1).
     - `lost`는 항상 0이다. 추적이 끊기거나 시스템 제스처로 끝난 쥐기는 `hold_s`에서도 빠진다. 그래서 1초 넘는 비율이 실제보다 조금 낮게 나올 수 있다(T0-R1-3 외).
5. 쉬는 엄지가 남보다 가까운 편이면 연사가 끊길 수 있다. 그때는 `release_strength`·`min_strength`를 보고 `XRHandsInputSource`의 `pinchReleaseFloorMargin`을 0.03으로 줄인다.
   - 인스펙터에서 바꾸고 `Arena_Main` 씬을 저장한 뒤 **`HandHero > Build Quest APK (release, keep scene edits)`**로 빌드한다. PowerShell이면 `-ExecuteMethod HandHero.EditorTools.BuildScript.BuildQuestApkReleaseKeepScenes`다(4절 첫머리).
   - 다른 빌드 메뉴와 `compile_check`의 `BuildQuestApkRelease`는 빌드 전에 씬을 코드로 다시 만든다. 그러면 이 값이 0.05로 돌아가서, 다시 해 봐도 똑같이 느껴진다(딥 리뷰 DR-4).
   - 0.03을 계속 쓰기로 하면 `XRHandsInputSource.cs`의 기본값(`pinchReleaseFloorMargin = 0.05f`)을 바꾸고 씬을 다시 만든다.
6. CURSOR로도 한 판 쏴 본다. CURSOR의 검지 방아쇠는 바뀌지 않았다.

**B. 데모 모드와 녹화 (3절 가이드와 같이)**

7. DEMO를 누른다.
   - 점수와 타이머 없이 느린 Striker 2마리와 주황 과녁 4개가 나와야 한다.
   - 데모 Striker가 RUN의 Striker보다 눈에 띄게 느리게 나는지 본다. 시뮬레이션으로는 평균 약 0.63배이고, 멀리서 다가올 때 최고 15 m/s다(딥 리뷰 DR-1, 4.1의 54번).
   - 맞아도 체력이 줄지 않는다. 번쩍임, 소리, 감속은 나온다.
   - 쓰러진 봇은 3초, 과녁은 2초 뒤 다시 나온다.
8. 반투명 손이 VR ARENA와 MR TABLE 모두에서 실제 손에 붙어 있는지 본다. 주먹을 쥐면 초록, 핀치를 하면 노랑이다.
9. 캡션 4종(GRAB / PINCH = FIRE / HOLD = CHARGE / PUSH = SHOCKWAVE)이 동작할 때 손 바깥쪽에 뜨는지 본다.
   - Quest 녹화에서 검은 외곽선과 함께 읽혀야 한다.
   - 가까운 영웅의 날개를 가리지 않아야 한다.
   - PUSH = SHOCKWAVE가 민 손이 아니라 주먹 쥔 손 옆에 뜨는 일이 있는지도 본다(리뷰 T4-R1-2).
10. 앱을 켜고 처음 고른 모드가 DEMO일 때 `perf_log`에 데모 시작 `HITCH`가 없는지 본다.
    - 왼손 GRAB 캡션이 처음 뜨는 순간도 본다. 워밍업이 왼쪽 캡션을 미리 켜지 않는다(리뷰 F2-2).
11. `perf_log`에서 데모 구간의 프레임 시간을 본다. 반투명 손이 렌더러 64개를 더 그린다(리뷰 F2-1).
12. 손목 일시정지 → MENU 뒤 퀵 매치와 RUN을 한다. 체력이 정상으로 줄고 퀵 매치 봇이 다시 보여야 한다.
13. 10초 시험 녹화를 한다(3절).
    - Quest 메뉴 단계가 맞는지 본다.
    - 두 손과 가장 긴 캡션 `PUSH = SHOCKWAVE`가 녹화 화면 안에 들어오는지 본다(리뷰 T4-R2-3).

**C. 적 움직임**

14. RUN에서 섬 3부터 Gunner, 5부터 Sniper, 7부터 Lancer가 나온다. 원형마다 움직임이 다르게 느껴지는지 본다.
    - Sniper는 멀리서 쏘고 자리를 옮긴다.
    - Gunner는 가까이서 좌우로 크게 움직인다.
    - Lancer는 돌진한 뒤 멈춰서 쏜다.
15. 싸움이 공정한지 본다.
    - Lancer의 공격 빈도가 4차와 비슷한지, 멈춘 Lancer를 맞히기 쉬운지 본다.
    - 멀리서 들어온 Lancer가 예고 중에 미끄러지는지 본다(리뷰 T2-R1-2).
    - Gunner의 빨간 예고선이 예고 동안 크게 돌아가서 피하기 어려운지 본다(리뷰 T2-R2-2).
16. 기둥이나 벽 근처에서 Lancer와 보스가 조각 안에 멈춰 쏘지 않는지 본다. Sniper가 경기장 벽에 붙어 있는 시간이 거슬리는지도 본다.
    - Sniper, Gunner, Lancer, 보스가 플레이어 옆(정면에서 45° 넘게)이나 얼굴 앞(8 m 안)으로 오지 않는지 본다. 영웅을 좌석 쪽으로 몰아도 마찬가지여야 한다(딥 리뷰 DR-2, 4.1의 55번).
    - 시야 끝에서 되돌아갈 때 멈칫하는 모습이 거슬리는지, Sniper가 늘 영웅 뒤쪽에만 있어서 단조로운지 본다. 거슬리면 `RunBot` 프리팹 `BotInputSource`의 `Seat Max Yaw`를 50–55로 넓힌다. 프리팹을 저장한 뒤 keep-scene-edits 빌드로 만든다(4절 첫머리).
17. Gunner의 연두가 과녁·CURSOR 주황과 헷갈리지 않는지 본다.
    - **ASSIST 조준점(노랑)이 Gunner 몸 위에서 잘 보이는지** 특히 본다. 밝기 차이가 작다(리뷰 T1-R2-1). Dusk·Ember 섬에서는 색상도 더 가까워진다(F3-2).
    - 모든 원형의 지느러미는 빨강이어야 한다.

**D. 지형 조각과 테마**

18. 섬마다 조각 수와 색·빛이 달라 보이는지 본다. 연속한 두 섬은 테마가 같지 않다.
19. 어떤 조각도 시작점과 스폰을 막지 않는지 본다. 발판 밑과 위로 날아서 지나갈 수 있어야 한다.
20. 영웅을 발판 속에 두고 쏘면 영웅 자리에서 이펙트만 나와야 한다. 발판 속 봇도 플레이어를 맞히지 못한다(리뷰 수정 T3-R2-1).
21. 섬이 시작될 때 색과 빛이 한 프레임에 바뀐다. 눈이 불편한지 본다(리뷰 F3-3).
22. 보스 섬이 Dusk나 Ember일 때 보스(짙은 빨강)가 배경에서 잘 보이는지 본다(리뷰 F3-2).
23. MR TABLE에서 일시정지, 상자, 포털, 상점 패널의 글자를 조각이 가리는지 본다.
    - 버튼은 가려져도 눌린다.
    - 가리면 어느 패널, 어느 조각인지 적어 둔다. `PROGRESS.md`와 `QUESTIONS_FOR_HYUN.md` 29번의 "`Platform Max Y` 6"은 효과가 없다. 리뷰가 시뮬레이션으로 확인했다(T3-R2-2, F3-4). 다음 라운드에서 고친다.
24. 런을 끝내거나 그만두면 퀵 매치와 튜토리얼에서 기둥 2개와 원래 색만 남는지 본다.
25. `perf_log`에서 테마 섬과 퀵 매치의 프레임 시간을 비교한다. `run_summary.py`의 "Terrain per island" 줄도 본다.

**E. MR TABLE 방 스캔 (개발 APK)**

26. 개발 APK를 설치하고 MR TABLE을 누른다. 메인 메뉴에서 **10–20초 기다린 뒤** 매치를 시작한다.
    - `perf_log`의 `ROOM_SCAN` 줄에서 STARTED, FIRST_PLANES·FIRST_BOXES, SUMMARY(`near_table`)를 본다.
    - `SUMMARY`·`STOPPED`가 2프레임 뒤에 남는지 본다. 매치 첫 프레임에 프로브 때문인 `HITCH`가 없어야 한다.
27. 권한 대화상자를 바깥을 눌러 닫아 본다(다시 설치하면 다시 묻는다).
    - `ROOM_SCAN scan=PERMISSION_DISMISSED`가 남아야 한다.
    - 그 실행 동안 다시 묻지 않고, 게임은 평소대로 돌아야 한다.

**F. 회귀**

28. 퀵 매치, 튜토리얼, ASSIST·CURSOR, VR ARENA·MR TABLE이 4차처럼 동작하는지 본다.
29. RUN을 끝까지 한 판 한다. 목표 시간은 8–9분이다.
30. 메타 진행을 확인한다.
    - NEW BEST와 UNLOCKED가 뜨는지 본다.
    - 런 중에 일시정지 > RESET PROGRESS를 두 번 누른 뒤 런을 끝낸다. NEW BEST·UNLOCKED가 없어야 하고, 다음 런은 평소대로 기록되어야 한다.
    - 유물이 해금된 상태에서 STARTING RELIC 화면에 30초 머문 뒤 런을 한다. 끝 화면과 `run_log`의 시간에 그 30초가 들어가지 않아야 한다.
31. 빌드 도장과 빌드 메뉴를 확인한다(딥 리뷰 DR-7, DR-4). DR-7 뒤에 빌드한 APK에서 본다.
    - 설치 뒤 `dumpsys`의 `versionName`이 APK 파일 이름의 도장(예: `20261010_1042_release`)과 같아야 한다.
    - RUN 한 판 뒤 `run_log.jsonl` 마지막 줄의 `"build"`와 `perf_log.txt` 머리줄의 `app`도 같은 값이어야 한다.
    - Unity 에디터에서 `Arena_Main`의 값 하나를 바꿔 저장하고 `HandHero > Build Quest APK (release)`를 누른다. "scene edits will be reset" 창이 떠야 한다. Cancel을 누르면 빌드하지 않는다. 시험한 값은 되돌린다.

## 3. 녹화 가이드 (T4, 원본 `AUTO/RECORDING_GUIDE.md`)

> 주의: 3.2의 Quest 메뉴 단계(오른손 손바닥 핀치로 유니버설 메뉴, 카메라 → 동영상 녹화, `/sdcard/Oculus/VideoShots` 경로, 녹화에 패스스루가 찍히는지)는 기억으로 쓴 것이다. 기기에서 확인하지 않았다. 첫 녹화는 10초짜리로 시험한다.

목표는 링크드인에 올릴 30–60초 영상이다. 헤드셋 화면(게임 + 반투명 손 + 동작 캡션)과 휴대폰으로 찍은 실제 손을 한 화면에 함께 보여 준다.

**3.1 준비**
- 밝은 방에서 찍는다. 손 추적도 휴대폰 화면도 밝을수록 좋다. 손 뒤 배경은 단색이면 좋다.
- 헤드셋 렌즈를 닦고 배터리를 50% 이상으로 맞춘다.
- 게임 메인 메뉴에서 AIM을 **ASSIST**로 둔다. 그러면 캡션이 `PINCH = FIRE`로 나온다(CURSOR는 `TRIGGER = FIRE`).
- 보기 모드는 **VR ARENA**를 기본으로 한다(3.3).

**3.2 Quest 기본 녹화**
1. 오른손 손바닥을 얼굴 쪽으로 돌리고 엄지와 검지를 짧게 집었다 놓는다. Meta 메뉴(유니버설 메뉴)가 열린다.
   - 왼손으로 같은 동작을 하면 게임의 일시정지 버튼이 된다. 헷갈리지 않게 주의한다.
2. 메뉴 아래쪽 바에서 **카메라** 아이콘 → **동영상 녹화**를 고른다. 녹화가 시작되면 빨간 점이 보인다.
   - 마이크 소리를 넣는 항목이 있으면 켠다. 휴대폰 영상과 맞출 때 쓴다.
3. 게임으로 돌아가 DEMO를 고르고 3.4 순서대로 찍는다.
4. 끝낼 때는 Meta 메뉴를 다시 열고 녹화 중지를 누른다.
5. 영상은 휴대폰 Meta Horizon 앱의 갤러리로 옮기거나, USB로 PC에 받는다(PowerShell, 한 줄씩).
   ```powershell
   & "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" devices
   & "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" pull /sdcard/Oculus/VideoShots .\QuestVideos
   ```
- 메뉴 이름과 위치는 Horizon OS 버전에 따라 조금 다를 수 있다.

**3.3 VR ARENA와 MR TABLE**
- **추천: VR ARENA.** 반투명 손과 캡션이 어두운 아레나 위에서 또렷하다. 영웅, 봇, 빔이 크게 보이고, 녹화에 무엇이 찍히는지 확실하다.
- **MR TABLE은 보너스 컷으로 찍는다.** 책상 위 작은 아레나와 실제 방이 함께 보여서 눈길을 끈다.
  - 패스스루가 녹화에 찍히는지는 OS 버전과 설정에 따라 다르다. 찍히지 않으면 배경이 검게 나온다. 먼저 10초 시험 녹화로 확인한다.
  - 보기 모드는 메인 메뉴에서만 바꿀 수 있다(MR TABLE 버튼).

**3.4 촬영 순서 (30–60초)**

| 시간 | 동작 | 화면에 뜨는 캡션 |
|---|---|---|
| 0–5초 | 메뉴에서 DEMO를 가리키고 핀치한다(손만으로 메뉴를 고르는 장면) | — |
| 5–15초 | 왼손 주먹으로 영웅을 잡고 끌어서 날린다. 손을 펴면 미끄러지듯 날아간다 | GRAB |
| 15–25초 | 오른손으로 과녁이나 봇을 가리키고 짧게 핀치해 연사한다. 과녁이 깨진다 | PINCH = FIRE |
| 25–35초 | 핀치를 1초쯤 유지해 차지를 모았다가 놓아, 큰 빔을 봇에 맞힌다 | HOLD = CHARGE |
| 35–45초 | 봇이 가까이 올 때 손바닥을 앞으로 민다. 충격파 고리가 나오고 봇이 보라색으로 느려진다 | PUSH = SHOCKWAVE |
| 45–55초 | 빨간 예고선이 굵어지면 왼손으로 끌어서 피한다 | GRAB |
| 55–60초 | 왼손 손바닥 핀치로 일시정지 → MENU | — |

- 데모에서는 죽지 않으니 실수해도 계속 찍는다.
- 각 동작을 2–3번씩 찍어 두고 편집에서 좋은 컷을 고른다.
- 첫 3초용 "훅" 컷(차지샷이 봇을 맞히는 장면)을 따로 하나 찍어 둔다.
- 손은 가슴 앞, 헤드셋 시야 안에서 움직인다. 시야 밖으로 나가면 추적이 끊겨 반투명 손이 사라진다.

**3.5 휴대폰으로 실제 손 찍기**
- 삼각대에 휴대폰을 **가로**로 세운다. 플레이어 옆 앞쪽 45°, 1–1.5 m 거리, 가슴 높이에 둔다.
- 두 손과 헤드셋이 한 화면에 다 들어오게 한다.
- 1080p 60fps(가능하면 4K 60fps)로 찍는다. 노출과 초점은 손에 고정한다.
- 두 녹화를 모두 켠 뒤 카메라 앞에서 박수를 한 번 친다. 편집할 때 이 소리와 장면으로 두 영상을 맞춘다.
- 다른 사람의 얼굴이나 화면 속 개인 정보가 찍히지 않게 한다.

**3.6 링크드인용 편집**
- **추천: 4:5 세로(1080×1350), 위아래 배치.** 위에 헤드셋 화면, 아래에 휴대폰 손 영상을 둔다. 휴대폰 피드에서 가장 크게 보인다.
- **대안: 1:1 정사각(1080×1080), 좌우 배치.** 왼쪽에 휴대폰 손, 오른쪽에 헤드셋 화면을 둔다.
- 동작과 캡션이 같은 순간에 보이게 맞춘다.
- 링크드인 영상은 소리 없이 자동 재생된다. 큰 자막을 넣는다. 예: "Hands only — no controllers".
- 첫 3초에 훅 컷을 넣고, 전체 길이는 60초 안으로 한다.
- 도구는 CapCut(무료), iMovie, Premiere 중 편한 것을 쓴다.

**3.7 올릴 때 쓰는 말 (정직하게)**
- 써도 되는 말: "Quest 3 hand-tracking prototype", "practice/demo mode: invulnerable, slowed bots", "the translucent hands and captions are drawn by the game's demo mode".
- 쓰면 안 되는 말: 아직 없는 기능(멀티플레이, 정식 출시 등), 측정하지 않은 숫자(정확도 %, 지연 ms).
- 데모 모드는 연습장이다. 실제 게임(RUN, 퀵 매치)이 더 어렵다고 한 줄 덧붙이면 좋다.

**3.8 찍기 전에 알아 둘 것 (최종 리뷰 Minor, 가이드 파일은 고치지 않았다)**
- **크롭(T4-R2-3):** 캡션은 손 바깥쪽으로 뻗는다. 그래서 헤드셋 화면 가운데를 좁게 크롭하면 캡션이 잘릴 수 있다. 1:1 좌우 배치는 특히 좁다. 10초 시험 녹화에서 두 손과 `PUSH = SHOCKWAVE`가 들어오는지 보고 크롭 폭을 정한다. Quest 녹화는 헤드셋에서 보이는 것보다 시야가 좁다.
- **박수 맞추기(T4-R2-4):** VR ARENA 헤드셋 화면에는 박수 치는 손이 보이지 않는다. 반투명 손은 DEMO 안에서만 그려진다. 헤드셋 마이크가 녹화되지 않으면 맞출 단서가 없다. 그때는 DEMO에 들어간 뒤 첫 GRAB 캡션이 뜨는 순간을 휴대폰 영상의 주먹 쥐는 순간에 맞춘다.
- **조준 보조(F4-4):** 데모에서는 과녁 4개에 ASSIST 조준 보조가 붙는다. 퀵 매치와 RUN에서는 붙지 않는다. 그래서 과녁 사격 장면은 실제 게임보다 쉽게 맞는다. 글에 "aim assist on practice targets"도 함께 쓰면 정직하다.

## 4. Hyun 대신 내린 결정과 바뀐 값

**씬과 인스펙터에서 값을 바꿀 때 (딥 리뷰 DR-4, 먼저 읽기)**
- 아래 표에서 "씬", "인스펙터", "프리팹"으로 적은 값은 `Arena_Main` 씬과 `RunBot` 프리팹에 저장된다. `XRHandsInputSource`, `RunDirector`, `ArenaLayoutApplier`, `ArenaThemeApplier`, `DemoDirector`, `GestureCaptions`, `RenderWarmup`, `BotInputSource` 같은 것들이다.
- 보통 APK 빌드는 빌드하기 전에 씬과 프리팹을 코드로 다시 만든다. 그래서 인스펙터에서 바꾼 값이 C# 기본값으로 돌아간다.
  - 해당하는 빌드: `HandHero > Build Quest APK (release)`·`(dev)`·`(… clean)` 메뉴, `compile_check`의 `BuildQuestApkRelease`·`BuildQuestApkDev`·`BuildQuestApkDevClean`.
  - 다시 만들어도 남는 것은 `BotDifficulty_Normal.asset`뿐이다.
- **인스펙터 값으로 시험하는 방법**
  1. 값을 바꾸고 씬(또는 프리팹)을 저장한다.
  2. `HandHero > Build Quest APK (release, keep scene edits)`로 빌드한다. 개발 APK는 `(dev, keep scene edits)`다.
  3. PowerShell이면 Unity를 닫고 아래 명령을 쓴다(개발 APK는 끝을 `BuildQuestApkDevKeepScenes`로 바꾼다).
     ```powershell
     powershell -NoProfile -ExecutionPolicy Bypass -File "AUTO\tools\compile_check.ps1" -BuildTarget Android -TimeoutMinutes 120 -ExecuteMethod HandHero.EditorTools.BuildScript.BuildQuestApkReleaseKeepScenes
     ```
  - 이 빌드는 저장된 씬을 그대로 쓴다. 그래서 그 뒤에 빌더나 직렬화 필드를 바꾼 코드(다음 라운드 등)는 씬을 다시 만들기 전까지 APK에 들어가지 않는다.
- **값을 계속 쓰기로 했으면** C# 기본값을 바꾼다(필드 초기값, `*.Default`, `Defaults()`, 빌더). 그다음 `HandHero > Build All Scenes`로 씬을 다시 만든다. 다음 라운드의 씬 재생성에도 남는 방법은 이것뿐이다.
- 보통 빌드 메뉴는 저장된 씬이나 프리팹이 빌더가 마지막으로 쓴 것과 다르면 먼저 묻는다(Rebuild scenes / Cancel / Keep my scene edits).
  - `compile_check` 빌드는 묻지 않는다. `[BuildScript] NOTE:` 줄을 출력하고 다시 만든다.
  - 미리 보려면 `HandHero > Check Scene Edits`를 쓴다(PowerShell: `-ExecuteMethod HandHero.EditorTools.BuildScript.CheckSceneEdits`).
  - 이 PC에서 씬을 만든 기록과 비교한다. 그래서 git으로 다른 커밋을 받은 뒤에도 "EDITED"로 나올 수 있다.
- 에디터 Play(Link)는 인스펙터 값을 그대로 쓴다.

### 4.1 대신 내린 결정 (모두 되돌릴 수 있음, 자세한 내용은 `QUESTIONS_FOR_HYUN.md` 1–57번)

| # | 결정 | 되돌리려면 |
|---|---|---|
| 1 | 누르기는 강도 0.8 그대로다. Meta 플래그는 놓기에만 쓴다. | `PinchTrigger.Step`의 누르기 조건에 `MetaPinching` 상승 에지를 넣는다. |
| 2 | ASSIST 누르기·놓기는 부드럽게 하기 전 강도(`RawPinchStrength`)를 쓴다. 누르기가 1–2프레임 빨라진다. 메뉴 포인터와 손목 메뉴는 그대로다. | `XRHandsInputSource.Sample()`에서 `Strength = aimHand.PinchStrength` |
| 3, 51 | Meta 플래그로 놓으려면 이번 누름에서 플래그가 한 번 켜진 뒤 2프레임 꺼져야 하고, 강도도 최고값보다 0.05 낮아야 한다. | `metaPinchReleaseFrames` = 0(Meta 끔), `metaPinchReleaseDrop` = 0(플래그만으로 놓음) |
| 4, 48 | 놓인 뒤 다시 누르려면 가장 낮았던 값 + 0.2가 필요하고, 상한은 0.85다. 가벼운 핀치도 0.75 바닥 아래에서 놓인다. | `pinchRelativeRelease`, `pinchRearmMargin`, `pinchReleaseFloorMargin` |
| 5 | 시스템 제스처는 `PinchTrigger` 안에서 추적 끊김처럼 다룬다. 쥔 차지는 취소되고, 다시 열어야 쏠 수 있다. | git에서 `XRHandsInputSource`의 예전 게이트 체인을 되살린다. |
| 6–7 | 런 기록에 `min_strength`, `release_strength`, `release_by`, `peak_strength`, `meta_seen`을 넣었다. 라벨은 meta → absolute → relative 순으로 붙인다. | `RunRecordJson.ToJson`, `PinchTrigger.ReleaseRule` |
| 49 | 강도로 놓기와 다시 열기는 2프레임 연속일 때만 센다. 놓기가 약 14 ms 늦어진다. | `pinchConfirmFrames` = 1 |
| 50 | Meta 플래그가 켜진 프레임에는 상대·바닥 기준을 세지 않는다. 절대 0.6은 계속 센다. | `PinchTrigger.ReleaseRule`의 `!metaPinching` 조건 |
| 8–9 | 시야 규칙은 테스트만 더했다("스폰 하나라도 보이면 됨" 그대로). 대체 배열이 없으면 `null`을 돌려준다. | `ArenaLayout.HasOpeningSightLine`, `Generate`의 마지막 `return` |
| 10–13 | 방 스캔: 대화상자를 닫으면 거부로 보고 `PERMISSION_DISMISSED`를 남긴다. 실패는 `UNAVAILABLE` 한 줄만 남긴다. 권한 확인은 0.5초 간격이고 답을 알면 멈춘다. 콘솔 로그는 개발 빌드에서만 남기고, 요약과 종료는 2프레임 뒤에 한다. | `RoomScanProbe`의 `permissionPollInterval` = 0, `stopDelayFrames` = 0 |
| 14 | STARTING RELIC 화면 시간은 런 시간과 최단 기록에서 뺀다(4차 S5-2와 반대). | `RunStateMachine.Tick`의 `if (Phase != RunPhase.StartRelic)`를 지우고 테스트를 되돌린다. |
| 15 | 런 중에 RESET PROGRESS를 누르면 그 런은 메타에 쓰지 않는다. `run_log`에는 남는다. | `RunDirector.BeginRun`의 `_meta.OnRunStarted();` |
| 16 | Gunner 몸통은 연두 (0.6, 1, 0.1), 빔은 (0.6, 1, 0.2)다. | `BotArchetypes.Get` 또는 씬 `RunDirector` > Archetypes |
| 17–19 | 지느러미는 빨강을 유지한다. 툴팁을 더했다. `SECOND_PC_SETUP.md`는 이미 고쳐져 있었다. | 빌더 `ArchetypeShapes`에 `"Fin_L", "Fin_R"`을 다시 넣는다. |
| 20–22 | 움직임 값은 원형에 둔다(`BotMovement`, 공격과 짝). Sniper ×1.5(18 m), Gunner ×0.7(8.4 m)·옆 이동 ×1.6, Lancer 10 m 돌진. Striker가 아닌 원형은 사거리 구를 지킨다(`KeepRange`). | 씬 `RunDirector` > Enemy archetypes > `Movement`를 모두 0으로 두면 Striker처럼 움직인다. |
| 23 | Lancer는 공격 0.9초 전에 돌진을 시작한다. 그래서 발사 빈도가 4차와 같다. | `Movement.DashLeadTime` = 0 |
| 24 | 돌진할 자리와 멈출 자리는 지형 조각을 피한다(히어로 반지름 1 m). | `RunDirector.SpawnBot`의 `bot.SetObstacles(...)`를 지운다. |
| 25 | Sniper의 18 m 구가 경기장 밖으로 나가도 그대로 둔다(목표점이 안쪽으로 잘린다). | Sniper `RangeMult`를 1.3으로 |
| 54 | 딥 리뷰 DR-1: 데모 속도 배수는 옆으로 끌기, 피하기 거리, 상하 흔들기, 손 속도 상한, 히어로 최고 속도를 함께 줄인다. 손 속도 상한만 줄이던 예전 값은 봇을 느리게 하지 못했다(9.44 대 9.49 m/s). | `DemoDirector` > `Rules` > `Bot Speed Scale` 1 |
| 55 | 딥 리뷰 DR-2: 사거리를 지키는 원형(Sniper, Gunner, Lancer, 보스)은 좌석 정면 45° 안, 눈에서 수평 8 m 밖에서만 돈다. 예전에는 Sniper가 시간의 39%를 45° 밖에 있었고 눈에서 2.5 m까지 왔다. Striker는 그대로다. | `BotInputSource` > `Keep In Seat View`, `Seat Max Yaw`, `Seat Min Distance` |
| 26–27 | Striker는 4차 골든 기록과 비교하는 테스트로 지킨다. 보스 테스트는 패턴별 거리를 본다. | (테스트만 해당) |
| 28–29 | 발판은 다른 조각 위에 걸쳐도 된다. 단, 위아래 2.5 m 통로가 있어야 한다. 발판 중심은 2–8 m(실제 상한 7.2 m)다. | `PlacePieces`의 `VerticalGap` 조건, `Rules`의 `Platform Min/Max Y`·`Flight Lane Clearance` |
| 30–31 | 섬 깊이별 조각 표(섬 1–2 최대 1/1/1 … 섬 7–8 최대 2/2/2, 보스 1/1/1). 낮은 벽은 돌리지 않는다. | `ArenaLayoutApplier` > `Piece Table`의 모든 `Max`를 0으로 두면 4차 아레나다. |
| 32–34 | 테마는 Dusk/Frost/Ember이고 채도를 낮게 둔다. 런 시드로 순서를 섞어 연속한 섬이 같지 않다. MaterialPropertyBlock을 쓴다. | `ArenaThemeApplier` > `Themes` 배열을 비운다. |
| 35 | 섬마다 `theme`, `low_walls`, `platforms`, `thin_pillars`를 런 기록에 남긴다. | `RunRecordJson.ToJson`의 `HasTerrain` 블록 |
| 36 | 발판은 VR 좌석 눈과 TABLE 눈에서도 스폰 하나 이상을 가리지 않아야 배치가 통과한다. | `Rules` > `Viewer Eyes`를 비운다. |
| 52 | 영웅이 단단한 콜라이더(조각, 기둥, 과녁) 안에서 쏘면 빔이 그 자리에서 막힌다. 플레이어와 봇이 같다. | `PointingBeamController.Fire`의 `OriginInsideCover` 분기를 지운다. |
| 37, 46 | 데모는 새 단계 `MatchPhase.Demo`다. 메뉴에서만 들어간다. DEMO 버튼은 아랫줄 왼쪽에 있다. | 빌더에서 DEMO 버튼을 뺀다. |
| 38–39 | 데모 봇은 Striker 2마리다. 속도 ×0.6(54번), 예고 ×1.8, 발사 간격 ×1.6, 피해 ×0.4. 무적이어도 피격 반응은 그대로 나온다. | `Demo` > `DemoDirector` > `Rules`, `HeroHealthModel.ApplyDamage`의 `Invulnerable` 블록 |
| 40 | 연습 과녁은 기존 주황 과녁 4개다. ASSIST 조준 보조는 데모에서만 붙는다. | 빌더 데모 블록의 `demoAssistTargets`를 빈 배열로 |
| 41–43, 53 | 캡션은 0.8초 + 0.3초 동안 뜬다. 손 바깥쪽 7 cm, 위 4 cm에 둔다. 영웅을 가리면 영웅 반대쪽으로 먼저 비킨다. 외곽선 재질 `DemoCaption.mat`을 쓴다. | `GestureCaptions` > `Timing`·`Layout`, 빌더의 `captionMat` 줄 |
| 44–45 | 반투명 손은 손마다 관절 구 26개와 뼈 선 6개다. 로드 워밍업에 손과 캡션을 더했다. | `GhostHands` 필드, 씬 `RenderWarmup`의 `Ghost Hands`·`Captions` |
| 47 | 디버그 키 `G` = 데모 시작. 데모 중에는 퀵 매치 봇을 숨긴다. 잡는 손은 왼손으로 둔다. | `GhostHands`·`GestureCaptions`의 `Clutch Is Left` 등 |
| 56 | 딥 리뷰 DR-4: 보통 APK 빌드는 그대로 씬을 다시 만든다(씬이 늘 코드와 같다). 인스펙터 값을 쓰는 빌드 `…KeepScenes`와 `Check Scene Edits`를 더했다. 저장된 씬이 빌더가 쓴 것과 다르면 메뉴는 묻고, 배치 빌드는 NOTE를 남긴다. | 새 빌드를 쓰지 않으면 예전과 같다. 묻는 창은 `BuildScript.PrepareScenes`, 기록은 `Library/HandHero/SceneBuildFingerprints.txt`다. |
| 57 | 딥 리뷰 DR-7: APK 버전 이름에 빌드 도장(`9.2.0+날짜_시각_종류`)을 빌드하는 동안만 단다. 런 기록에 `build`를 넣었다. `run_summary.py`는 가장 최근 빌드만 요약한다. | 도장: `BuildScript.BuildQuestApk`의 `bundleVersion` 두 줄. 요약: `--all` |

### 4.2 바뀐 값과 되돌리는 법

| 값 | 전 → 후 | 어디서 | 되돌리려면 |
|---|---|---|---|
| ASSIST 핀치 입력 | 부드럽게 한 강도 → 원시 강도 | `XRHandsInputSource.Sample()` | `Strength = aimHand.PinchStrength` |
| ASSIST 놓기 규칙 | 절대 0.6만 → 절대 0.6 + 상대 0.2 + 바닥 0.75 + Meta 플래그 | 씬 `XRHandsInputSource`: `pinchRelativeRelease` 0.2, `pinchReleaseFloorMargin` 0.05, `pinchRearmMargin` 0.05, `pinchConfirmFrames` 2, `metaPinchReleaseFrames` 2, `metaPinchReleaseDrop` 0.05 | 4차와 거의 같게: `pinchRelativeRelease` 0, `pinchReleaseFloorMargin` 0, `pinchConfirmFrames` 1, `metaPinchReleaseFrames` 0. 원시 강도와 시스템 제스처 처리는 코드에서 되돌린다(4.1의 2·5번). |
| `pinchFireThreshold` / `pinchResetThreshold` / 차지 대기 | 0.8 / 0.6 / 0.25초 그대로 | — | — |
| Gunner 몸통 | (1, 0.5, 0.1) 주황 → (0.6, 1, 0.1) 연두 | `BotArchetypes.Get` + 씬 `RunDirector` > Archetypes | 예전 값 |
| Gunner 빔(보스 Gunner 패턴 포함) | (1, 0.6, 0.15) → (0.6, 1, 0.2) | 위와 같음 | 예전 값 |
| 지느러미 색 | 원형 색 → 빨강 유지 | 빌더 `ArchetypeShapes` | 4.1의 17–19번 |
| 런 시간 | STARTING RELIC 포함 → 제외 | `RunStateMachine.Tick` | 4.1의 14번 |
| 방 스캔 권한 확인 | 매 프레임 → 0.5초 간격, 답을 알면 멈춤 | `RoomScanProbe.permissionPollInterval` | 0 |
| 방 스캔 종료 | 메뉴를 떠나는 프레임 → 2프레임 뒤 | `RoomScanProbe.stopDelayFrames` | 0 |
| 원형 움직임 | 모두 Striker → Sniper 18 m · Gunner 8.4 m · Lancer 10 m 돌진 | 씬 `RunDirector` > Enemy archetypes > `Movement`·`AltMovement` | 모든 값 0 = Striker |
| 지형 조각 | 기둥 2개 → 기둥 2개 + 섬별 낮은 벽·발판·가는 기둥(종류마다 풀 2개) | `ArenaLayoutApplier` > `Piece Table`, `Rules` | 모든 `Max` 0 |
| 섬 테마 | 없음(빛 1.2, 흰색) → Dusk 1.05 / Frost 1.15 / Ember 1.1 | `ArenaThemeApplier` > `Themes` | 배열을 비운다. |
| 조각 안에서 쏘는 빔 | 밖으로 나감 → 그 자리에서 막힘 | `PointingBeamController.Fire` | `OriginInsideCover` 분기를 지운다. |
| 데모 | 새 값: 봇 2마리, 속도 ×0.6, 예고 ×1.8, 간격 ×1.6, 피해 ×0.4, 캡션 0.8 + 0.3초 | `DemoDirector` > `Rules`, `GestureCaptions` | 4.1의 37–47번 |
| 데모 속도 배수(딥 리뷰 DR-1) | 손 속도 상한만 → 옆으로 끌기·피하기·상하 흔들기·손 속도 상한·히어로 최고 속도 | `BotParams.Paced`, `BotInputSource.SetPace`, `FlyingCharacter.SetPaceSpeedMultiplier` | 4.1의 54번 |
| 봇 좌석 시야(딥 리뷰 DR-2) | 없음 → 사거리를 지키는 원형은 좌석 정면 45° 안, 눈에서 8 m 밖 | `RunBot` 프리팹 `BotInputSource` > Seat view(빌더 기본값) | `keepInSeatView` 끄기, 4.1의 55번 |
| 새 재질 | `Generated/Materials/GhostHands.mat`, `DemoCaption.mat` | 빌더가 만든다. | 빌더 데모 블록 |
| APK 버전 이름(딥 리뷰 DR-7) | `9.2.0` → `9.2.0+<yyyyMMdd_HHmm>_<release·dev>`(APK 파일 이름과 같음). ProjectSettings의 `9.2.0`은 그대로 | `BuildScript.BuildQuestApk`(빌드 동안만 바꾸고 되돌림) | 4.1의 57번 |
| 런 기록(딥 리뷰 DR-7) | 새 필드 `build`(APK 버전 이름, 에디터는 `editor`). `v`는 1 그대로 | `RunRecordJson.ToJson`, `RunLogFile.BuildId` | 필드를 지운다. |
| `run_summary.py` 기본 범위(딥 리뷰 DR-7) | 파일의 모든 런 → 가장 최근 빌드의 런 | `select_runs` | `--all` |
| `run_summary.py` 핀치 요약과 묶음(딥 리뷰 DR-5) | 모드별 첫 줄이 파일의 모든 런 → 진단값이 있는 런(5차 이후)만, 예전 런은 `older APKs` 줄. 도장 없는 런 한 묶음 → `(round 5)`·`(rounds 3-4)` 두 묶음. 새 옵션 `--since` | `pinch_holds_by_mode`, `build_of`, `runs_since`, 테스트 `AUTO/tools/test_run_summary.py` | `--build none`은 예전처럼 도장 없는 런을 모두 고른다. |
| APK 빌드 메뉴(딥 리뷰 DR-4) | 4개 → 6개(`keep scene edits` 2개), `Check Scene Edits` | `BuildScript` | 4.1의 56번 |

ProjectSettings, 매니페스트, 패키지, XR·렌더 설정은 바뀌지 않았다(APK 버전 이름의 도장은 빌드하는 동안만 단다, DR-7). 퀵 매치와 튜토리얼의 수치와 배치도 그대로다. 4차 게임 수치(`HordeTime`, 보스 체력, `EnemyDamageMult`, 원형 공격 숫자)도 그대로다.

## 5. 리뷰 결과

리뷰는 세 겹으로 했다.
- 통합 전 사전 리뷰(P): T1-P1–P3, T2-P1–P4, T3-P1–P3, T4-P1–P4를 통합 커밋에 반영했다(`PROGRESS.md`).
- 태스크 리뷰(R1·R2): 아래 "고친 것" 6건을 고쳤다.
- 최종 리뷰(F1–F4): 고칠 것은 없었다. 지적은 모두 Minor로 기록했다.

Critical은 0건이다.

**고친 것** (`deb63c5`, 테스트 747 → 766, 새 19개는 고치기 전에 모두 실패하는 것을 확인했다, 씬 재생성)

- **T0-R1-1 · T0-R2-1 가벼운 핀치가 놓이지 않음**
  - 문제: 최고값이 0.91보다 낮은 핀치(엄지가 1.9 cm보다 덜 닫힘)는 쉬는 엄지(0.71)에서 놓이지 않았다. 4차 버그와 같은 증상이다.
  - 수정: 바닥 0.75(약 2.6 cm)를 넣었다. 다시 누르기 상한은 0.85다.
- **T0-R1-2 · T0-R2-3 한 프레임 튐과 Meta 깜빡임**
  - 수정 1: 강도로 놓기와 다시 열기는 2프레임 연속일 때만 센다.
  - 수정 2: Meta 플래그가 켜진 동안에는 상대 기준을 세지 않는다.
  - 수정 3: Meta 플래그로 놓으려면 강도가 0.05 떨어져야 한다.
- **T3-R2-1 조각 안의 영웅이 밖으로 쏨:** 이제 빔이 그 자리에서 막힌다. 플레이어와 봇이 같은 규칙을 쓴다.
- **T4-R2-1 캡션이 아래로만 비킴:** 이제 영웅 반대쪽으로 먼저 비키고, 안 되면 반대로 간다.

**남은 Minor (기록만 함, 고치지 않음)**

| ID | 파일 | 내용 |
|---|---|---|
| T0-R1-3 · T0-R2-4 · F1-2 · F4-2 | `PointingBeamController.cs:276`, `Core/ChargeInputRule.cs:21` | `release_by`의 `lost`가 런 기록에 절대 남지 않는다. 추적이 끊기거나 시스템 제스처가 나오면 `ChargeInputRule`이 Cancel을 돌려주고, 그 쥐기는 통째로 버려진다. 그렇게 끝난 긴 쥐기는 `hold_s`에도 없어서, 1초 넘는 비율이 낮게 나온다. |
| T0-R2-5 · F1-1 | `Core/PinchTrigger.cs:20, 315` | `relative`/`absolute` 라벨 설명이 코드와 다르다. 손을 보통 속도로 펴면 상대 기준이 먼저 걸려서 거의 모두 `relative`가 된다. `absolute`는 한 프레임에 0.2 넘게 떨어질 때만 나온다. 그래서 "새 규칙이 얼마나 필요했나"(D2)에 답하지 못한다. |
| T0-R2-6 | `Tests/EditMode/PinchReleaseTests.cs:82` | 빠른 탭 테스트는 누르기 에지를 "발사"로 센다. 실제 발사 쿨다운 0.35초는 거치지 않는다. 그래서 기기에서 초당 약 2.9번보다 빨리 탭하면 발사 수가 핀치 수보다 적은 것이 정상이다(2절 A2). |
| T0-R1-4 | `Core/PinchTrigger.cs:209` | Meta 플래그가 2프레임 깜빡이면 강도가 최고값 그대로여도 쥐기가 끝난다는 지적이다. **T0-R2-3 수정(`metaPinchReleaseDrop` 0.05)으로 해결됐다.** 지금은 강도가 최고값에서 0.05 떨어지지 않으면 놓지 않는다. 기록만 남긴다. |
| T1-R2-1 · F3-2(2) | `Core/BotArchetype.cs:160`, `Core/ArenaTheme.cs:53` | Gunner 연두 몸통과 ASSIST 조준점 노랑의 밝기 대비가 약 1.2:1이다(4차 주황은 2.4:1). 조준점이 Gunner 위에 그려지면 잘 안 보일 수 있다. Dusk·Ember의 따뜻한 빛 아래에서는 색상각 차이도 18–20°로 줄어 테스트의 25° 기준을 넘지 못한다. 테스트는 빛 없는 색만 본다. |
| F3-2(1) | `Core/ArenaTheme.cs:53` | 보스 몸통(짙은 빨강)이 Ember 뒷벽, Dusk 바닥과 밝기가 거의 같다(1.0:1). 기본 벽은 2.6:1이다. |
| T2-R1-2 | `Core/BotBrain.cs:440` | Lancer의 돌진 시간 제한은 벽 때문이 아니라 약 26 m 넘는 긴 이동에서만 걸린다. 그때는 빠르게 날던 자리에서 멈춤과 예고가 같은 프레임에 시작되어, 예고 중에 미끄러진다(D4는 "예고 중 멈춤"). |
| T2-R2-2 | `Core/BotBrain.cs:400` | Gunner가 예고와 연발 중에도 넓게 돈다. 그래서 빨간 예고선이 0.48초 동안 평균 34°(p90 43°) 돌아간다. 4차는 19°였다. 처음 본 선으로 피하면 덜 피한 셈이 된다. |
| T3-R2-2 · F3-4 | `HandProto/Match/ArenaLayoutApplier.cs:111` | MR TABLE에서 좌석 패널(일시정지, 상자, 포털, 상점, 끝)은 약 2.5 m에, 작은 아레나는 0.2–1.2 m에 있다. 그래서 섬의 조각이 패널 글자를 가릴 수 있다. 시뮬레이션(섬 27,000개)에서 버튼 가운데를 가린 비율은 일시정지/상점 기준으로 가는 기둥 8.5%/6.8%, 낮은 벽 5.0%/16.7%, 기존 기둥 2개 25%/27%였다. 버튼은 계속 눌린다. `PROGRESS.md`·`QUESTIONS_FOR_HYUN.md` 29번의 "`Platform Max Y` 6" 처방은 효과가 없다. 6으로 낮춰도 12.9%/22.2%다. |
| F3-3 | `HandProto/Match/ArenaThemeApplier.cs:37` | 섬 인트로에서 테마 색과 빛이 한 프레임에 바뀐다. 런이 끝날 때도 그렇다. 시야 전체의 밝기가 갑자기 바뀌는 것은 VR 편안함 문제다. |
| T4-R1-1 | `Core/DemoMode.cs:289` | 캡션이 아래로만 비켜서 아래쪽 영웅을 가린다는 지적이다. **T4-R2-1 수정으로 해결됐다.** 남는 경우는 6걸음 안에 빈 자리가 없을 때뿐이다. 그때는 손바닥 옆 원래 자리에 남는다(의도). |
| T4-R1-2 · T4-R2-2 | `HandProto/GestureCaptions.cs:138` | PUSH = SHOCKWAVE 캡션의 손은 한 프레임 손바닥 속도로 고른다. 그래서 충격파를 낼 수 없는 주먹 쥔 손 옆에 뜰 수 있다. `XRHandsInputSource`는 어느 인식기가 발동했는지 이미 안다. `QUESTIONS_FOR_HYUN.md` 41번의 "입력에 어느 손인지 없다"는 틀린 설명이다. |
| T4-R2-3 | `AUTO/RECORDING_GUIDE.md:68` | 편집 가이드는 헤드셋 화면 가운데를 크롭한다. 그런데 캡션은 손 바깥쪽으로 30–55° 떨어져 뜬다. 그래서 크롭하면 잘릴 수 있다(3.8). |
| T4-R2-4 | `AUTO/RECORDING_GUIDE.md:60` | 두 영상을 박수로 맞추는데, 박수는 DEMO에 들어가기 전이라 VR 헤드셋 화면에는 보이지 않는다(3.8). |
| F2-1 | `HandProto/GhostHands.cs:194` | 반투명 손의 렌더러 64개가 MaterialPropertyBlock 때문에 SRP Batcher 밖에서 그려진다. 관절 구는 Unity 기본 구(768 삼각형)라서 합치면 약 4만 개의 투명 삼각형이다. 데모에서만 드는 비용이고, 기기에서 재지 않았다(2절 B11). |
| F2-2 | `HandProto/GestureCaptions.cs:238` | 로드 워밍업이 오른쪽 캡션과 왼손만 켠다. 그래서 왼쪽 캡션 TMP의 첫 Awake와 오른손의 첫 활성화는 데모 중에 일어난다. 재질은 데워져 있어 파이프라인 끊김은 아니고, 메인 스레드 일과 GC 할당만 남는다(2절 B10). |
| F4-3 | `AUTO/PROGRESS.md:21` | T0 기기 확인에 장소가 빠져 있다. 핀치 기록은 RUN에서만 쌓인다. 이 보고서 2절 A1에 적었다. |
| F4-4 | `HandProto/Match/DemoDirector.cs:91` | 데모에서만 과녁에 ASSIST 조준 보조가 붙어서, 데모의 사격은 게임과 다르게 맞는다. 녹화 가이드의 "써도 되는 말"에 이것이 빠져 있었다(3.8). |

## 6. 다음 단계 3가지 (게임 우선)

1. **기기 데이터로 ASSIST 감각과 밸런스를 맞춘다.**
   - 먼저 진단 Minor 2건을 고친다. `lost`가 기록되게 하고(T0-R1-3 외), `release_by` 라벨이 "예전 규칙도 놓았을까"에 답하게 한다(T0-R2-5). 그래야 `run_summary.py` 숫자를 믿을 수 있다.
   - 그 숫자로 `pinchReleaseFloorMargin`·`pinchRelativeRelease`를 맞춘다. Meta 신호가 오면(`meta_seen`) 그쪽 비중을 높인다.
   - 적 움직임 숫자(Gunner 좌우 폭, Lancer 돌진 거리)와 원형별 피해를 다시 맞춘다. Gunner 예고선 선회(T2-R2-2)와 Lancer 미끄러짐(T2-R1-2)도 같이 본다.
2. **아트 방향을 정한다(아트 패스).**
   - 테마 3종을 실제 아트(재질, 하늘, 조각 모양)로 바꾼다. 테마가 바뀔 때는 페이드를 넣는다(F3-3).
   - 적과 조준 표시의 색 규칙에 밝기 대비를 넣는다. Gunner와 조준점, 보스와 따뜻한 테마가 대상이다(T1-R2-1, F3-2).
   - MR TABLE에서는 패널을 아레나 앞에 두거나, 패널이 떠 있는 동안 조각을 숨긴다(F3-4).
   - 데모 영상에서 반응이 좋은 장면을 아트 방향의 기준으로 삼는다.
3. **MR TABLE "FIT TO TABLE"을 만든다.**
   - 2절 E의 방 스캔 결과(`near_table`)가 쓸 만하면 시작한다.
   - 아레나를 실제 테이블에 맞춘다. XR Origin은 그대로 두고 뷰어 스케일과 오프셋만 바꾼다. 테이블 위 가구 박스는 엄폐물로 쓴다.
   - 실패하면 지금 시드 배치로 돌아간다. 데모 영상의 MR 컷도 이것으로 좋아진다.
