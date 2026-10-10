STATUS: DONE

# DEEP_REVIEW — Hand Hero, round 5 (branch `auto/2026-10-09-r5`)

## 한국어 요약

**무엇을 봤나**
- 5차가 끝난 뒤의 코드 전체를 다시 봤다. 기준은 5차 diff(`ae71d0f..`)와 4–5차 diff(`a3b1dd1..`)다. 씬·프리팹 YAML은 씬 배선 문제일 때만 봤다.
- 계획(`CLAUDE_AUTONOMOUS_PLAN.md` 5b 규칙), `AUTO/DECISIONS.md`, `PROGRESS.md`, `REPORT_FOR_HYUN.md`, 4차 보관본(`AUTO/archive/2026-10-09-r4/`), 실제 기기 로그(`AUTO/device_logs/`)와 비교했다.

**어떻게 봤나**
- 관점 10개(입력, 봇, 런·메타, 지형·방 스캔, 데모·UI, 성능, 빌드, 테스트 등)로 나눠서, 관점마다 최대 3라운드까지 지적을 모았다.
- 지적마다 회의적인 검토자 3명이 코드와 기기 로그로 따로 확인했다. 그다음 비평가가 한 번 더 걸렀다. 남은 것만 "확인됨"으로 적었다.
- Important는 하위 시스템별로 고치고(TDD: 새 테스트가 고치기 전에 실패하는 것을 먼저 확인), 다시 확인했다. 재확인에서 남은 문제는 2차 수정에서 고쳤다.

**숫자**
- 확인된 지적: 74건. Critical 0, Important 10(DR-1–DR-10), Minor 64.
  - 하위 시스템별: terrain-room 18, input 13, run-meta 13, build 11, tests 7, demo-ui 5, bots 4, perf 3.
  - 같은 문제를 두 관점이 따로 낸 쌍이 5개다(DR-1/DR-3, M-04/M-08, M-07/M-22, M-50/M-53, 일부 겹치는 M-10/M-14). 서로 다른 문제로 세면 69건이다.
- 기각: 2건(아래 목록).

**고친 것**
- Important 10건 중 9건을 고쳤다(DR-1–DR-8, DR-10). DR-9는 일부만 고쳤다(안내와 쉬는 엄지 기록은 고침, 놓기 규칙 변경은 미룸).
  - DR-1·DR-3: 데모 Striker가 실제로 느려졌다(시뮬레이션 평균 약 0.6배). DR-2: 사거리를 지키는 원형이 좌석 정면 45° 안, 눈에서 8 m 밖에서만 돈다.
  - DR-4: APK 빌드가 인스펙터 수정을 말없이 지우지 않는다(keep-scene-edits 빌드, 묻는 창, NOTE). DR-7: APK마다 빌드 도장, 런 기록 `build`. DR-5: `run_summary.py`가 3·4차 런을 5차 숫자에 섞지 않는다.
  - DR-6: Meta 놓기도 2프레임을 본다. DR-8: 메뉴 핀치가 추적 끊김 뒤 두 번 눌리지 않는다. DR-10: 왼손이 0.25초까지 끊겨도 클러치가 이어진다.
  - DR-9: 보고서 A5 안내를 다시 쓰고, 쉬는 엄지 강도 r을 런 기록(`pinch_strength_s`)과 요약(`resting thumb` 줄)으로 읽게 했다.
- Minor는 4건을 고쳤고, 5건은 일부 고쳤다.
  - 고침: M-59(A4 비교 기준, DR-7 커밋에서), 그리고 이 커밋에서 M-07·M-22(`run_summary.py` 제안이 보스 체력 배수를 (6)이 아닌 실제 값 (5)로 적음), M-21(`PROGRESS.md`의 "3프레임"을 테스트와 같은 4프레임으로).
  - 일부: M-18·M-20(`min_strength`는 r이 아니라고 보고서에 적고 r은 새 기록으로 읽음, 필드와 예시 파일은 그대로), M-24(APK 빌드 메뉴는 저장을 묻지만 Build All Scenes 메뉴는 아직 묻지 않음), M-51(DR-6 테스트가 Meta 꺼짐 카운터를 시험, 예전 테스트는 그대로), M-61(versionName 도장은 넣었고 versionCode는 1 그대로).
- 커밋 5개: `23d1ebb`(봇), `30b4450`(빌드), `b3ff613`(런 요약), `b520dbb`(입력), `d4c5f0d`(2차 수정). 그리고 이 커밋(보고서, 테스트, APK).
- 테스트: EditMode 766 → 818(새 52개), Python `test_run_summary.py` 25개(새로 만듦).

**남은 것과 이유**
- Minor 55건은 고치지 않고 기록만 했다.
  - 이유 1: 모두 Minor다. 헤드셋 테스트를 막지 않는다. 기기 테스트 전에 코드를 더 바꾸면 테스트하고 빌드할 범위만 커진다.
  - 이유 2: 많은 것이 기기 데이터나 디자인 결정이 먼저다. 테마 대비와 밝기 변화(M-10, M-14, M-15, M-34), 깊이 단서(M-37, M-58), 엄폐와 조준 보조·예고선(M-04/M-08, M-31, M-33, M-50/M-53), 핀치 다시 누르기(M-01, M-35)가 그렇다. 아트 패스와 다음 라운드의 ASSIST 조정에서 같이 고친다.
  - 이유 3: 체크리스트와 문서의 틀린 설명(M-23, M-32, M-36, M-38, M-41, M-42, M-43, M-44, M-46, M-47, M-48, M-52)은 코드 대신 `REPORT_FOR_HYUN.md`의 "정밀 리뷰 (추가)" 절에서 바로잡았다. 앞 절의 원문은 그대로 두었다.
  - 이유 4: 나머지(테스트 이름·빈약한 테스트 M-16, M-17, M-19, M-39, M-55–M-57, M-64, 빌드·저장소 정리 M-25, M-40, M-62, 런 기록·요약 M-06, M-27, M-29, M-45 등)는 게임 동작을 바꾸지 않거나 다음 라운드의 작은 정리 작업이다. 지금 C#을 바꾸면 새 APK를 다시 빌드해야 한다. M-63(손 추적을 매니페스트에서 필수로)은 설치 조건이 바뀌는 매니페스트 변경이라 Hyun이 정한다.
- DR-9의 규칙 변경(다시 누르기 기준을 바닥에서 잇기, 플레이어별 바닥)은 미뤘다. 이어 붙이면 쉬는 엄지 위 떨림 여유가 0.05(약 2 mm)로 줄어서, `QUESTIONS_FOR_HYUN.md` 48번이 약속한 0.14를 깬다. 플레이어별 바닥은 디자인 변경이다. 둘 다 새 `resting thumb` 기기 데이터가 먼저다(61·62번, 보고서 6절 1번).
- 재확인에서 "의도대로 남김"으로 나온 것: Striker는 좌석 시야 규칙을 따르지 않는다(DR-2). MR TABLE도 VR 좌석 기준을 쓴다(해는 없음). Meta 플래그가 오래 꺼진 뒤에는 최고값보다 0.05 낮은 값 2개로 놓인다(DR-6). DR-10은 끊긴 동안의 손 이동과 부드럽게 하기 지연(10 m/s에서 약 0.4 m)만큼은 잃는다. 씬 빌드 기록이 없을 때 배치 빌드는 NOTE만 남기고 다시 만든다(DR-4).
- 헤드셋 확인은 아직 하지 않았다. 딥 리뷰 수정은 모두 아래 새 APK에 처음 들어간다.

**최신 APK** (`MetaAwards\Build\`, `d4c5f0d`의 C# 코드로 빌드)
- `HandHero_20261010_1202_release.apk` (55.5 MB, 58,176,476 B): 체감, 성능, 녹화용 릴리스 빌드.
- `HandHero_20261010_1155_dev.apk` (126.7 MB, 132,891,684 B, 클린 개발 빌드): 방 스캔 와이어프레임과 Profiler 확인용.
- 예전 APK(`0203`, `0156`)와 검증용 `1042`·`1046`에는 딥 리뷰 수정이 전부 또는 일부 없다. 쓰지 않는다.

**이번 마무리에서 한 확인**
- EditMode 818/818(`AUTO\tools\compile_check.ps1 -Tests`), Python 25/25.
- `HandHeroSceneBuilder.BuildAll` OK. 다시 만든 씬 2개는 ID와 줄 순서만 바뀌었다(ID를 빼고 정렬해 비교하면 차이 0줄).
- APK 2종을 `BuildQuestApkDevClean`, `BuildQuestApkRelease`로 다시 빌드했다. 개발 빌드 6.6분, 릴리스 3.7분. 빌드 전에 씬을 막 다시 만들었기 때문에 `[BuildScript] NOTE:` 줄은 없었다. `aapt`로 본 `versionName`은 `9.2.0+20261010_1202_release`와 `9.2.0+20261010_1155_dev`이고 `versionCode`는 1이다. 빌드가 끝난 뒤 ProjectSettings의 `bundleVersion`은 `9.2.0`으로 돌아왔다. 빌드가 바꾼 `ProjectSettings/UnityConnectSettings.asset`은 되돌렸다. 이 커밋의 다른 변경(`run_summary.py` 문구, 문서)은 APK에 들어가지 않는다.

## Confirmed findings

Keys: `DR-n` are the important findings the fix rounds worked on. `M-nn` numbers the minor findings in the order the review handed them over. "Fixed?" was checked against the code at `d4c5f0d` (the C# these APKs are built from); "this commit" marks the three text fixes made with this report (the `run_summary.py` wording and `PROGRESS.md`, which do not go into the APKs). Line numbers are where the reviewers found the issue; later commits moved some lines.

| Key | Severity | Subsystem | File:line | Title | Fixed? | Commit | Note |
|---|---|---|---|---|---|---|---|
| DR-1 | important | bots | `Assets/MyAssets/Scripts/HandProto/Input/BotInputSource.cs:136` | Demo BotSpeedScale 0.6 does not slow the demo bots: the hand speed cap never binds | yes | 23d1ebb | - |
| DR-2 | important | bots | `Assets/MyAssets/Scripts/Core/BotMovement.cs:74` | Sniper's 18 m orbit ignores the seat, so it flies beside the player and up to 2.6 m in front of their face | yes | 23d1ebb | Striker still ignores the seat (by design) |
| DR-3 | important | demo-ui | `Assets/MyAssets/Scripts/HandProto/Input/BotInputSource.cs:136` | Demo 'slow' Strikers are not slower: BotSpeedScale 0.6 caps a speed the bot never reaches | yes | 23d1ebb | same root cause as DR-1 |
| DR-4 | important | build | `Assets/MyAssets/Editor/BuildScript.cs:74` | APK build regenerates Arena_Main from code, so the report's inspector-based tuning and undo steps are silently reverted | yes | 30b4450, d4c5f0d | no-record case fixed in d4c5f0d |
| DR-5 | important | run-meta | `AUTO/tools/run_summary.py:178` | A4 pinch check will mix in rounds 3-4 runs that are still on the device, so the ASSIST long-hold share looks unfixed | yes | b3ff613 | - |
| DR-6 | important | input | `Assets/MyAssets/Scripts/Core/PinchTrigger.cs:312` | Meta-flag release ends a held ASSIST pinch on one outlier sample, bypassing the 2-frame confirm | yes | b520dbb | - |
| DR-7 | important | build | `AUTO/tools/run_summary.py:178` | Device run_log.jsonl keeps round-3 and round-4 runs across `adb install -r`, and no run says which APK wrote it, so the ASSIST release check reads old data | yes | 30b4450 | - |
| DR-8 | important | input | `Assets/MyAssets/Scripts/HandProto/Match/HandMenuPointer.cs:135` | Menu pinch held through a short tracking dropout presses the button again (RESET PROGRESS confirms itself, REROLL charges twice) | yes | b520dbb, d4c5f0d | closing-pinch side effect fixed in d4c5f0d |
| DR-9 | important | input | `AUTO/REPORT_FOR_HYUN.md:65` | Report's fix for a closer resting thumb (pinchReleaseFloorMargin 0.03) does not fix it; the release thresholds sit 2 mm from one assumed value | partly | b520dbb, d4c5f0d | guide rewritten and resting thumb r now logged; the rule change waits for device data |
| DR-10 | important | input | `Assets/MyAssets/Scripts/Core/HandClutchSampler.cs:27` | A one-frame hand dropout drops the clutch, and the regrab throws away the hero's lead (up to 40% of a drag) | yes | b520dbb | - |
| M-01 | minor | input | `Assets/MyAssets/Scripts/Core/PinchTrigger.cs:278` | After a Meta-flag release, the re-arm level can be above 1.0, so shallow rapid ASSIST taps never fire again | no | - | - |
| M-02 | minor | input | `Assets/MyAssets/Scripts/HandProto/HandGestureTracker.cs:15` | HandGestureTracker updates after XRHandsInputSource, so all hand input is a frame late and the CR-8 stall guard checks the wrong frame | no | - | - |
| M-03 | minor | bots | `Assets/MyAssets/Scripts/Core/BotBrain.cs:203` | Pooled bot keeps the previous spawn's _active attack, so the first-hit refire floor uses the wrong archetype | no | - | - |
| M-04 | minor | bots | `Assets/MyAssets/Scripts/HandProto/Match/ArenaLayoutApplier.cs:187` | Dash and hold spots avoid terrain pieces but not the greybox targets, which also block shots as cover | no | - | same as M-08 |
| M-05 | minor | run-meta | `Assets/MyAssets/Scripts/HandProto/Match/RunDirector.cs:155` | RESET PROGRESS on the STARTING RELIC screen still offers the relics it just wiped | no | - | - |
| M-06 | minor | run-meta | `AUTO/tools/run_summary.py:138` | run_summary charge misfire rate mixes CURSOR and ASSIST, which hides an ASSIST regression | no | - | - |
| M-07 | minor | run-meta | `AUTO/tools/run_summary.py:265` | run_summary suggestions still cite BossHealthMult (6); the shipped value is 5 | yes | this commit | same as M-22; the suggestions now say (5) |
| M-08 | minor | terrain-room | `Assets/MyAssets/Scripts/HandProto/Match/ArenaLayoutApplier.cs:187` | Bot dash/hold spots can land inside the practice targets, which round 5 made solid cover | no | - | same as M-04 |
| M-09 | minor | terrain-room | `Assets/MyAssets/Scripts/HandProto/Match/RoomScanProbe.cs:314` | Room-scan probe logs STARTED and STOPPED with no SUMMARY when no trackable event ever arrived | no | - | - |
| M-10 | minor | terrain-room | `Assets/MyAssets/Scripts/Core/ArenaTheme.cs:44` | Frost theme roughly halves the contrast of the white HUD and the player's beam in the VR arena | no | - | overlaps M-14 |
| M-11 | minor | demo-ui | `Assets/MyAssets/Scripts/HandProto/Match/MatchHud.cs:41` | DEMO skips the tutorial, never ends and shows no exit hint; the wrist-pause exit is never taught in-game | no | - | - |
| M-12 | minor | perf | `Assets/MyAssets/Editor/HandHeroSceneBuilder.cs:772` | Choice panels drop a frame when they open; STARTING RELIC moves that drop onto the run-start frame | no | - | - |
| M-13 | minor | perf | `Assets/MyAssets/Scripts/Core/PerfSample.cs:137` | perf_log cannot measure the demo or theme frame-time costs that checklist items B11 and D25 ask about | no | - | - |
| M-14 | minor | terrain-room | `Assets/MyAssets/Scripts/Core/ArenaTheme.cs:45` | Frost theme roughly halves the contrast of the outline-free white run HUD | no | - | overlaps M-10 |
| M-15 | minor | terrain-room | `Assets/MyAssets/Scripts/HandProto/Match/ArenaThemeApplier.cs:140` | Theme switch is a one-frame 10-17x full-field brightness step in every run (quantifies F3-3) | no | - | - |
| M-16 | minor | input | `Assets/MyAssets/Tests/EditMode/PinchReleaseTests.cs:285` | Release-point jitter tests never release the pinch, so the re-arm that blocks a double shot is untested | no | - | - |
| M-17 | minor | input | `Assets/MyAssets/Tests/EditMode/PinchReleaseTests.cs:242` | Meta 'flag must be on in this press' gating is guarded only by a vacuous test | no | - | - |
| M-18 | minor | run-meta | `Assets/MyAssets/Tests/EditMode/PinchReleaseTests.cs:410` | min_strength always records a release-phase sample; the round-5 fixture holds values the rule cannot produce | partly | d4c5f0d | test_run_summary values fixed and the report says lowest is not r; run_log_round5_sample.jsonl unchanged |
| M-19 | minor | terrain-room | `Assets/MyAssets/Tests/EditMode/RoomScanTests.cs:313` | Room-scan 'Probe' test re-implements the probe and omits the tabletop gate that stops JNI polling in VR ARENA | no | - | - |
| M-20 | minor | input | `Assets/MyAssets/Scripts/Core/PinchTrigger.cs:320` | min_strength now records the first release sample, not the lowest strength during the hold | partly | d4c5f0d | documented (report A5); resting thumb now comes from pinch_strength_s; field unchanged |
| M-21 | minor | tests | `AUTO/PROGRESS.md:20` | T0 DONE criterion 'release within <= 3 frames' relaxed to 4; PROGRESS still claims 3 | yes | this commit | PROGRESS now says 4 frames and why (deb63c5) |
| M-22 | minor | run-meta | `AUTO/tools/run_summary.py:265` | run_summary.py still advises BossHealthMult (6) after round-4 D2 set it to 5 | yes | this commit | same as M-07 |
| M-23 | minor | build | `AUTO/REPORT_FOR_HYUN.md:120` | Checklist step E-27 (dismiss the room-scan permission dialog) cannot happen: USE_SCENE is already granted and `adb install -r` keeps it | no | - | corrected in the report's new section |
| M-24 | minor | build | `Assets/MyAssets/Editor/HandHeroSceneBuilder.cs:113` | Running BuildAll or any APK build from the editor menu silently discards unsaved changes in the open scenes | partly | 30b4450 | APK build menus now offer to save; the Build All Scenes menu still does not |
| M-25 | minor | build | `SETUP_GUIDE.md:5` | Stale root-level guides describe an abandoned project setup that conflicts with the pinned version and the hard rules | no | - | - |
| M-26 | minor | run-meta | `Assets/MyAssets/Scripts/HandProto/Match/RunDirector.cs:534` | Quitting after island 5 unlocks Second Wind silently; UNLOCKED and NEW BEST only exist on the Victory/Defeat banner | no | - | checklist note in the report's new section |
| M-27 | minor | run-meta | `Assets/MyAssets/Scripts/HandProto/Match/RunDirector.cs:538` | A run ended by closing the app is never written: no run_log line and no meta unlock or best | no | - | checklist note in the report's new section |
| M-28 | minor | run-meta | `Assets/MyAssets/Scripts/HandProto/Match/RunDirector.cs:440` | Second Wind revive has no feedback; SfxCues says revives have their own sound, but none exists | no | - | - |
| M-29 | minor | run-meta | `AUTO/tools/run_summary.py:83` | run_summary fight-time medians include unfinished islands, such as 0 s for a quit during the intro | no | - | - |
| M-30 | minor | terrain-room | `Assets/MyAssets/Scripts/Core/ArenaLayout.cs:234` | The opening sight line protects any spawn, but the first (and on elite/boss islands the only) bot always uses spawn 0 | no | - | - |
| M-31 | minor | terrain-room | `Assets/MyAssets/Scripts/HandProto/BotTelegraphLine.cs:52` | Bot telegraph line ignores terrain: the full red 'about to be hit' line runs through platforms and pillars that will stop the shot | no | - | - |
| M-32 | minor | terrain-room | `Assets/MyAssets/Scripts/HandProto/Match/RoomScanProbe.cs:321` | Room-scan SUMMARY logged when leaving MR TABLE via the VR ARENA button measures the table point in the VR layout, so near_table comes out 'none' | no | - | checklist note in the report's new section |
| M-33 | minor | demo-ui | `Assets/MyAssets/Scripts/HandProto/LockOnRing.cs:98` | Lock-on ring is hidden inside the 2 m practice cubes the demo made snappable | no | - | - |
| M-34 | minor | terrain-room | `Assets/MyAssets/Scripts/HandProto/Match/ArenaThemeApplier.cs:42` | Island themes leave the aim and depth markers at default colours: the CURSOR marker is about 1:1 on Frost, the bot disc about 1.1:1 on Dusk | no | - | - |
| M-35 | minor | input | `Assets/MyAssets/Tests/EditMode/PinchReleaseTests.cs:26` | Pinch tests start from a fresh trigger, but the game calls RequireReopen first, so taps that close to 2.2-2.4 cm never fire in ASSIST | no | - | deferred with the DR-9 rule change |
| M-36 | minor | perf | `Assets/MyAssets/Scripts/HandProto/PerfSpikeLogger.cs:119` | The frame that starts a new mode is never logged as HITCH if the menu already had a hitch, so checklist B10 can pass falsely | no | - | checklist note in the report's new section |
| M-37 | minor | terrain-room | `Assets/MyAssets/Scripts/HandProto/HeroGroundMarker.cs:77` | Low walls hide the ground-marker discs, the depth cue, for bots over the spawn band in VR ARENA | no | - | - |
| M-38 | minor | terrain-room | `Assets/MyAssets/Scripts/Core/ArenaPieceTable.cs:133` | Setting every Max in the piece table to 0 does not restore the round-4 arena, as the report claims | no | - | corrected in the report's new section |
| M-39 | minor | tests | `Assets/MyAssets/Tests/EditMode/DemoCaptionTests.cs:253` | Place_StepsDownOffTheHero passes only because two equal float elevations tie toward 'down' | no | - | - |
| M-40 | minor | build | `Assets/MyAssets/Editor/HandHeroSceneBuilder.cs:590` | BuildAll is not idempotent on disk: every rebuild, including the one inside each APK build, rewrites both scenes with new random fileIDs | no | - | - |
| M-41 | minor | input | `Assets/MyAssets/Scripts/Core/PinchTrigger.cs:310` | metaPinchReleaseFrames = 0 is documented as turning the Meta flag off, but the flag still suppresses the relative and floor release | no | - | corrected in the report's new section |
| M-42 | minor | terrain-room | `Assets/MyAssets/Scripts/HandProto/PointingBeamController.cs:500` | The fix that blocks shots fired from inside cover also changes Quick Match and the tutorial, which D5 and the report say are unchanged | no | - | corrected in the report's new section |
| M-43 | minor | build | `Assets/MyAssets/Editor/BuildScript.cs:134` | APKs are signed with the building PC's per-user debug keystore, so an APK from another PC or a reset profile cannot replace the installed one without wiping logs and progress | no | - | checklist note in the report's new section |
| M-44 | minor | build | `AUTO/REPORT_FOR_HYUN.md:119` | Checklist E26 checks the room-scan probe for a match-start hitch on the dev APK, which runs a slower, different probe path than release | no | - | checklist note in the report's new section |
| M-45 | minor | run-meta | `Assets/MyAssets/Scripts/HandProto/Match/RunDirector.cs:98` | run_log 'seed' is fixed per app session while the chest/portal/shop RNG keeps running across runs, so the logged seed neither identifies nor reproduces a run | no | - | - |
| M-46 | minor | run-meta | `AUTO/REPORT_FOR_HYUN.md:54` | Checklist A2 asks for 30 s of rapid fire on island 1, but island 1 is one 100 HP Striker that dies in about 5 hits, after which input is off | no | - | corrected in the report's new section |
| M-47 | minor | run-meta | `AUTO/REPORT_FOR_HYUN.md:130` | Checklist F30's checks for F3-3 (reset voids the run) and F1-2 (relic screen excluded) pass even when those fixes are broken | no | - | checklist note in the report's new section |
| M-48 | minor | terrain-room | `Assets/MyAssets/Scripts/HandProto/Match/RoomScanProbe.cs:230` | Room-scan time-to-first-result is measured from the first MR TABLE visit of the app session, not from the visit that produced the result | no | - | checklist note in the report's new section |
| M-49 | minor | terrain-room | `Assets/MyAssets/Scripts/Core/ArenaLayout.cs:62` | ViewerEyes is a hard-coded copy of ArenaViewMode's layout; retuning the tabletop (or FIT TO TABLE) silently leaves the platform sight-line rule checking an old eye | no | - | - |
| M-50 | minor | terrain-room | `Assets/MyAssets/Scripts/HandProto/PointingBeamController.cs:382` | ASSIST snap ignores cover: it locks onto (and keeps) a bot the hero cannot hit while a hittable bot sits inside the cone | no | - | same as M-53 |
| M-51 | minor | tests | `Assets/MyAssets/Tests/EditMode/PinchReleaseTests.cs:227` | Meta flag 'one-frame flicker' test cannot fail: the strength stays at the peak, so the flag-off counter rules it names are never tested | partly | b520dbb | new DR-6 tests drive the flag-off counter with a strength drop; this test is unchanged |
| M-52 | minor | demo-ui | `AUTO/RECORDING_GUIDE.md:47` | Recording shot list expects a Striker to come within shockwave reach by itself; it holds about 12-13 m and the radius is 6 m | no | - | shot-list note in the report's new section |
| M-53 | minor | terrain-room | `Assets/MyAssets/Scripts/HandProto/PointingBeamController.cs:382` | Aim-assist lock ignores round-5 cover: the ring promises a hit, and the snap sends every shot into the platform or pillar | no | - | same as M-50 |
| M-54 | minor | demo-ui | `Assets/MyAssets/Scripts/HandProto/GestureCaptions.cs:109` | Demo start takes the GRAB baseline from a disabled input, so a fist already closed shows GRAB on the next frame | no | - | - |
| M-55 | minor | tests | `Assets/MyAssets/Tests/EditMode/ArenaLayoutTests.cs:203` | KeepClearPadding is untested: the target tests only check plain overlap | no | - | - |
| M-56 | minor | tests | `Assets/MyAssets/Tests/EditMode/ArenaTerrainTests.cs:62` | Layout test meant to guard round-4 compatibility only compares the new Generate with itself | no | - | - |
| M-57 | minor | tests | `Assets/MyAssets/Tests/EditMode/StartRelicTests.cs:226` | StartRelic test name claims 'after a win' and 'in the choice', but there is no win and no phase check | no | - | - |
| M-58 | minor | terrain-room | `Assets/MyAssets/Editor/HandHeroSceneBuilder.cs:284` | Floating platforms have no depth cue (no shadow, no ground marker), so a player in VR ARENA cannot tell whether one is between the hero and a bot | no | - | - |
| M-59 | minor | input | `AUTO/REPORT_FOR_HYUN.md:60` | Report A4 compares ASSIST against a round-3 APK run labelled as round 4, and leaves out the worse round-4 APK baseline | yes | 30b4450 | fixed with DR-7 |
| M-60 | minor | input | `Assets/MyAssets/Scripts/HandProto/HandGestureTracker.cs:49` | Tunables that round 5's ASSIST mapping and demo targets rely on still have no [Tooltip] (rule 5b.3) | no | - | - |
| M-61 | minor | build | `Assets/MyAssets/Editor/BuildScript.cs:82` | BuildScript never stamps a version: every APK reports 9.2.0 / versionCode 1, so perf_log sessions and the installed APK can't be told apart | partly | 30b4450 | versionName is stamped since DR-7; versionCode is still 1 |
| M-62 | minor | build | `.gitignore:36` | Three tracked .meta files belong to gitignored .unitypackage files, so a fresh clone (second-PC setup) opens with deleted files | no | - | - |
| M-63 | minor | build | `Assets/MyAssets/Editor/BuildScript.cs:29` | Hands-only game ships a manifest that marks hand tracking optional, so Horizon OS never prompts to turn hands on | no | - | - |
| M-64 | minor | tests | `Assets/MyAssets/Scripts/HandProto/PointingBeamController.cs:560` | The round-5 'shots from inside cover are blocked' rule has no test, though the review-fix commit says all its new tests were red first | no | - | - |

### Recheck of the important findings (after the fix rounds)

| Key | Resolved | Notes |
|---|---|---|
| DR-1 | yes | Demo Strikers average 4.7-6.1 m/s against 7.7-9.3 m/s for run Strikers (x0.60-0.67) in a harness built from the shipped Core files. The demo pool never reaches run bots. |
| DR-2 | yes | Sniper 0% of frames over 45.2 deg, closest 21-25 m (was 38% and 2.5-2.9 m). Gunner, Lancer and boss stay at about 45.7 deg and 7.4 m or more. Striker ignores the seat (by design). |
| DR-3 | yes | Same fix as DR-1: demo mean 5.16 m/s against 7.69 m/s with hits every 2 s. |
| DR-4 | yes | The menu asks and batch builds log a NOTE when the saved scene differs from the builder's record. The no-record case (Library deleted, new PC) was fixed in `d4c5f0d`. |
| DR-5 | yes | Default and `--all` summaries keep rounds 3-4 holds on their own line. |
| DR-6 | yes | Replay through the real PinchTrigger: one outlier while the flag is off no longer ends the hold. A real opening still releases 2 frames after it starts. |
| DR-7 | yes | The build stamp groups runs by APK; the default summary uses the newest build only. |
| DR-8 | yes | A held menu pinch through a 1-8 frame dropout gives exactly one press. The closing-pinch side effect was fixed in `d4c5f0d`. |
| DR-9 | partly | The first fix told Hyun to read r from "lowest while held", which is always at or under 0.75. `d4c5f0d` logs `pinch_strength_s` and prints the resting thumb r instead. The rule change itself is deferred (see the Korean summary). |
| DR-10 | yes | 1-frame dropout at 10 m/s: 14.46 m of travel against 15.56 m with no dropout (was 8.20 m). |

## Rejected findings

- `Assets/MyAssets/Tests/EditMode/PinchReleaseTests.cs:105`: Pinch 'device number' tests check only the test's own helper and Default, not the shipping mapping or parameters. **Why rejected:** Verdict 'not/minor': not a defect. The description is accurate: the test has its own Cm() and uses PinchReleaseParams.Default. But the shipping fields (XRHandsInputSource.cs:32-46) default to the same values and lines 145-155 pass them straight to PinchTrigger, so the tests exercise the shipped numbers.
- `Assets/MyAssets/Scripts/HandProto/Match/HandMenuPointer.cs:126`: Menu pointer still re-arms at smoothed 0.5 (about 3.75 cm), the release level round 5 showed a relaxed pointing thumb never reaches. **Why rejected:** Verdict 'not/minor': not real in the shipped game. The code matches the description (HandMenuPointer.cs:126-129 re-arms at smoothed 0.5), but the device logs contradict the premise that a relaxed pointing thumb never gets back below that level.
