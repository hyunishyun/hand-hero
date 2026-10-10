# CLAUDE_AUTONOMOUS_PLAN — Round 5: ASSIST pinch release, minors, enemy movement, terrain stage 2, demo mode

## 한국어 요약

- **목표:**
  - ASSIST 모드에서 핀치를 놓아도 "아직 쥐고 있다"고 읽는 문제를 고친다. 이 문제 때문에 차지 오인이 생기고, 연사가 끊긴다.
  - 남은 Minor 13건을 정리한다.
  - 적과 지형의 2단계를 만든다.
  - 링크드인 영상을 찍을 데모 모드를 넣는다.
- **근거:** `AUTO/device_logs/2026-10-09-r4/run_log.jsonl`. 핀치를 쥔 시간의 중앙값이 CURSOR는 0.125초인데, ASSIST는 0.19–7.9초였고 최대 22초였다. 3차 APK에서도 똑같아서 4차에서 생긴 회귀가 아니다. 가리키는 자세에서는 엄지와 검지 사이가 고정 기준(약 3.3 cm) 밖으로 거의 벗어나지 않는다.
- **바뀌는 것(파일):**
  - 핀치 판정: `PinchTrigger`(또는 새 Core 규칙), `HandGestureTracker`(원시·Meta 핀치 값 노출), `XRHandsInputSource`, `RunRecord`(핀치 진단 값)
  - 적 움직임: `BotBrain`·`BotArchetype`
  - 지형: `ArenaLayout`·`ArenaLayoutApplier`·`HandHeroSceneBuilder`(새 조각·테마)
  - 데모: 새 `DemoDirector`·`GhostHands`·`GestureCaptions`, 메뉴
  - Minor 13건에 해당하는 파일들
- **결정:**
  - **D1** 핀치 놓기 판정
    - 1순위: Meta Hand Tracking Aim의 검지 핀치 신호를 쓴다(플랫폼이 보정한 값).
    - 보조: 쥔 동안 가장 강했던 값에서 일정량 이상 떨어지면 놓은 것으로 본다(상대 기준).
    - 핀치를 누르는 판정은 지금처럼 0.8이다. 차지 대기 0.25초는 그대로 둔다.
  - **D2** 핀치마다 최소 거리, 놓은 순간의 값, 어느 판정이 놓았는지를 런 기록에 남긴다(기기에서 다시 맞추기 위해).
  - **D3** Minor 13건을 모두 고친다.
    - Gunner 색은 조준 표시 주황과 구별되게 바꾼다.
    - 지느러미는 결정대로 빨간 계열을 유지한다.
    - 런 중에 RESET을 누르면 그 런은 메타에 기록하지 않는다.
    - STARTING RELIC 화면에 머문 시간은 런 시간에서 뺀다.
  - **D4** 적 2단계: 움직임 성격만 추가하고, 이동 규칙(비행 모델·속도 상한)은 플레이어와 같다.
    - Sniper는 멀리 머문다.
    - Lancer는 돌진한 뒤 멈춰서 조준한다.
    - Gunner는 가까이서 좌우로 움직인다.
    - Striker는 지금 그대로다.
  - **D5** 지형 2단계
    - 새 조각: 낮은 벽, 떠 있는 발판, 가는 기둥
    - 섬 테마 3종: 바닥·벽 색과 조명 색만 바꾼다. 실제 아트는 아트 패스 때 한다.
    - 퀵 매치와 튜토리얼은 지금 그대로다.
  - **D6** 데모 모드
    - 메인 메뉴에 DEMO 버튼을 둔다.
    - 죽지 않는 연습장: 느린 Striker 1–2마리와 과녁이 계속 다시 나온다. 타이머와 점수 HUD는 없다.
    - 반투명 손(관절 점과 뼈 선)을 실제 손 위치에 그린다.
    - 동작 캡션(GRAB / PINCH = FIRE / HOLD = CHARGE / PUSH = SHOCKWAVE)이 동작이 감지될 때 손 옆에 잠깐 뜬다.
    - 손목 일시정지로 메뉴에 돌아간다.
    - 휴대폰 촬영·편집 가이드를 보고서에 쓴다.
  - **D7** 실행: 이 데스크탑 채팅(학교 계정)에서 울트라코드 워크플로로 한다. 사용 한도에 걸리면 이 세션의 크론 감시가 초기화 뒤 자동으로 이어서 한다.
  - **D8** 브랜치 `auto/2026-10-09-r5`(4차 `a8cd3c2` 위), 커밋 `[auto] T<n>: …`, push 금지.
- **테스트·검증:**
  - 핀치 판정은 기기 기록을 본뜬 입력 시퀀스로 TDD한다. 엄지가 2.8 cm에 머무는 경우, 빠른 탭, 1초 차지, Meta 신호 유무, 추적 끊김을 다룬다.
  - 적 움직임과 지형 규칙도 TDD한다. Striker는 프레임 단위로 지금과 같아야 하고, 기존 601개 테스트는 유지한다.
  - 컴파일, 씬 재생성, APK 2종, 단계별 리뷰와 최종 리뷰를 한다.
  - **헤드셋 확인은 없다.**
- **기기에서 확인할 것:**
  - ASSIST로 30초 연사 → 핀치 수와 발사 수가 비슷하고, 차지샷은 의도한 것만 나오는지(`run_summary.py`의 오인률)
  - 적 움직임이 원형마다 다르게 느껴지는지
  - 새 지형 조각과 테마
  - 데모 모드에서 손과 캡션이 녹화에 잘 보이는지
  - MR TABLE 메뉴에서 10–20초 기다린 뒤 방 스캔 결과
- **위험:**
  - Meta 핀치 신호가 이 펌웨어에서 오지 않을 수 있다. 그래서 보조 판정을 둔다.
  - 손 그리기는 프레임 비용이 든다. 데모 모드에서만 켠다.
  - 떠 있는 발판이 비행 경로를 막을 수 있다. 배치 규칙에 높이 여유를 넣는다.

---

> Operating instructions for the build agents of round 5. Read this file top to bottom before every task.
> Work can stop at any time (usage limits); a watchdog resumes it. **State lives only in files and git**: every task checks `AUTO/PROGRESS.md` first and returns at once if it is already DONE.
>
> Background:
> - `AUTO/device_logs/2026-10-09-r4/` — round-4 device logs (run_log.jsonl with `hold_s` / `hold_started` / `hold_charged` per pinch; perf_log.txt).
> - `AUTO/archive/2026-10-09-r4/REPORT_FOR_HYUN.md` section 7 — the 13 leftover minors (IDs S7-1-1, S7-1-2, S8-1-2, S8-1-3, S8-2-1/F2-3/F4-2, F2-2, F1-2, F3-3, F3-4, F4-1, F4-3).
> - Round-4 plan: `AUTO/archive/2026-10-09-r4/CLAUDE_AUTONOMOUS_PLAN.md` (S6 archetypes, S7 layout).
>
> Reports and decision notes for Hyun are in Korean. Code, comments and commit messages are in English.

## 0. Facts

- Unity **6000.6.4f1**. Branch **`auto/2026-10-09-r5`** (from `auto/2026-10-09-run` @ `a8cd3c2`). Remote `origin` exists; never push.
- Device evidence (Hyun, 2026-10-09, Quest 3, standalone):
  - Pinch hold seconds per run — CURSOR (12:49): median 0.125, 12 of 508 holds over 1 s. ASSIST (17:56, round-3 APK): 8 holds, median 7.9 s, max 22 s, 15 shots in 100 s. ASSIST (18:07, round-3 APK): 103 holds, 22 over 1 s, max 8 s. ASSIST (23:02, round-4 APK): 20 holds, median 0.94 s, 15 started a charge.
  - So in ASSIST the release edge is missed: `HandGestureTracker` smooths `PinchStrength = InverseLerp(0.06, 0.015, thumb–index distance)` and `PinchTrigger` releases only below `pinchResetThreshold` 0.6 (≈ 3.3 cm). A pointing hand rests the thumb closer than that. Not a round-4 regression.
  - Gameplay perf: no spike in fights with the round-4 APK; warmup 44 ms. Room scan: permission granted, plane + box subsystems started, the menu was left after 6 s (no results yet).
- XR Hands 1.9 (`Library/PackageCache/com.unity.xr.hands@*`): `XRHandSubsystem.TryGetAimState(handedness, out XRHandAimState)`, `XRHandAimState.pinchStrengthIndex`, `MetaAimHandState(in aim).aimFlags` with `MetaAimFlags` (verify the exact `IndexPinching` member name in the package source before use). `HandGestureTracker` already reads `SystemGesture` this way.
- Tests: 601 EditMode tests pass at the start. They must all stay green (update a test only where a decision deliberately changes behaviour; say which in the commit).

## 1. Mode

**Unattended.** Never ask questions. When a choice is needed, take the most reasonable default that fits section 4 and log it in `AUTO/QUESTIONS_FOR_HYUN.md` (task / question / choice / why / how to undo). When stuck, log it in `AUTO/BLOCKERS.md`, mark the task `BLOCKED(<why>)` and stop; don't spend more than 30 minutes on one problem.

## 2. Task protocol

1. Read this file, `AUTO/PROGRESS.md`, `AUTO/DECISIONS.md`, `AUTO/BLOCKERS.md`, `git log --oneline -15`, `git status`.
2. If your task is already `DONE`, return at once. If it is `IN_PROGRESS`, a previous run was cut off: compile-check, keep what is salvageable, otherwise `git stash push -m "abandoned-<task>"` and restart it.
3. Mark it `IN_PROGRESS`, do it, then: compile check → EditMode tests → (scene build if serialized fields or the builder changed) → commit `[auto] T<n>: <summary>` → mark it `DONE` in `PROGRESS.md` with 2–4 Korean lines (results, caveats, audit IDs) → commit.

## 3. Verification (no headset)

- Commands (Unity must not be running; exit 4 = Unity open → blocker):
  - Compile: `powershell -NoProfile -ExecutionPolicy Bypass -File "AUTO\tools\compile_check.ps1"`
  - Tests: the same + `-Tests`
  - Scenes: the same + `-ExecuteMethod HandHero.EditorTools.HandHeroSceneBuilder.BuildAll` (throws on a missing serialized field).
  - APK: the same + `-BuildTarget Android -TimeoutMinutes 120 -ExecuteMethod HandHero.EditorTools.BuildScript.BuildQuestApkDevClean` (and `BuildQuestApkRelease`). Commit the regenerated scenes. Always `git checkout -- ProjectSettings/UnityConnectSettings.asset` before committing.
- Never run two Unity commands at once. Never hand-edit `.unity` YAML. `git add <paths>`, never `-A`.
- **Pure logic in `HandHero.Core` with EditMode tests first (TDD: red, then green).** MonoBehaviour wiring = compile + scene build + a device-checklist item.

## 4. Decisions (approval and details: `AUTO/DECISIONS.md`)

D1 pinch release: Meta aim index pinch first, relative-drop fallback · D2 per-pinch diagnostics in the run log · D3 fix all 13 minors · D4 enemy movement personalities, same flight rules · D5 terrain stage 2: low wall, floating platform, thin pillar + 3 colour/light themes · D6 demo mode with ghost hands and gesture captions · D7 workflow + watchdog · D8 branch/commit rules.

## 5. Task queue (in order)

### T0. ASSIST pinch release (D1, D2) — TDD
- `HandGestureTracker`: per hand also expose the **unsmoothed** pinch strength and, when `TryGetAimState` succeeds, the Meta aim `pinchStrengthIndex` and the index-pinching flag (+ a `HasMetaPinch` bool). Read them where `SystemGesture` is read today; no allocation.
- Core: a pinch release rule used by `PinchTrigger` (extend it or add `PinchReleaseRule`; keep the public behaviour for CURSOR's trigger path unchanged). Press stays at `pinchFireThreshold` 0.8 (or the Meta flag's rising edge when present — choose one, log it). Release when ANY of: (a) Meta flag present and off (with a short debounce, e.g. 2 frames); (b) the raw strength has fallen by at least `relativeRelease` (tunable, start 0.2 ≈ 1 cm) below the peak reached during this press; (c) the existing absolute `pinchResetThreshold`. Tracking loss still drops the pinch and requires a reopen (existing behaviour).
- Tests built from the device numbers: thumb settles at ~2.8 cm after a pinch (old rule never releases, new rule releases within ≤ 3 frames); 10 quick taps → 10 fires, 0 charge starts; a deliberate 1 s hold → charge starts after `HoldDelay` and fires on release; Meta flag path; no flag (fallback only); jitter around the release point does not double-fire; tracking loss mid-hold.
- `XRHandsInputSource` uses it for ASSIST; CURSOR (trigger gesture) untouched.
- Run log: per pinch add `min_strength`, `release_strength`, `release_by` (meta / relative / absolute / lost); update `run_summary.py` to print the release-by mix and the share of holds over 1 s.
- DONE: tests; compile; scenes rebuilt if fields changed; device item "30 s of rapid ASSIST fire → shots ≈ pinches, no unintended charge".

### T1. Leftover minors (D3)
Fix every item from the round-4 report section 7, each with a test where the logic lives in Core:
- S7-1-1: a Generate-level test that forces the sight-line rule to reject layouts (tall piece / low spawn area) and checks the result stays open and not a fallback; optionally require the sight line to spawn index 0.
- S7-1-2: with no fallback arrays, `Generate` returns the input defaults or null arrays — never a half-placed mix; document + test.
- S8-1-2: handle `PermissionRequestDismissed` (treat as denied for this session, log it).
- S8-1-3: a failed `SetManagers(true)` logs UNAVAILABLE once and nothing else.
- S8-2-1 / F2-3 / F4-2: stop polling the permission (JNI) every frame once it is decided; poll at most every 0.5 s before that.
- F2-2: leaving MR TABLE menu: build the summary without `Debug.Log` stack traces in release (`HHLog` or `LogType.Log` with `StackTraceLogType.None`), and defer manager shutdown off the match-start frame if cheap.
- F1-2: time spent on the STARTING RELIC screen is excluded from run time and best-time records (TDD in `RunStateMachine`).
- F3-3: a RESET PROGRESS during a run marks that run as not recorded to meta (TDD in `MetaProgress`/`RunDirector` rule).
- F3-4: Gunner colour clearly different from the target / CURSOR orange (pick a hue ≥ 40° away, log it).
- F4-1: fins keep the red family (builder: remove `Fin_L`/`Fin_R` from `baseColorRenderers`).
- F4-3: add `[Tooltip]` to every listed field.
- Also fix `AUTO/SECOND_PC_SETUP.md` section 0 only if it still says the CLI and the desktop app use different accounts (it was corrected by hand already if not).
- DONE: tests; compile; scenes rebuilt.

### T2. Enemy movement personalities (D4) — TDD
- Add movement parameters to `BotArchetype` (preferred distance to the player, strafe amplitude/frequency, a dash-then-hold pattern) and read them in `BotBrain`'s movement part only. The bot still drives the same `HandInputData` path, the same flight model and the same speed caps as the player (ADR fairness).
  - Sniper: keeps far (preferred distance ~1.5× today's), small strafe, repositions after each shot.
  - Lancer: dashes to a new spot before its telegraph, holds still while telegraphing and firing, then dashes again.
  - Gunner: keeps closer (~0.7×), wider strafe.
  - Striker: exactly today's behaviour (frame-for-frame tests stay green unchanged). Boss: Gunner movement during Gunner pattern, Lancer during Lancer pattern.
- Movement must respect arena bounds and the T3 pieces (bots already fly with the clamp; add a cheap avoidance only if a test shows bots pinned inside pieces — log the choice).
- DONE: tests; compile; scenes/RunBot prefab rebuilt; device item (each archetype moves differently; fights stay fair).

### T3. Terrain stage 2 (D5) — TDD for the layout rules
- New piece kinds built by `HandHeroSceneBuilder` (simple primitives, a small pool per kind, all disabled by default): low wall (≈ 6 × 3 × 1), floating platform (≈ 4 × 0.6 × 4 at y between 2 and 8), thin pillar (≈ 1 × 14 × 1). Today's two pillars stay as they are.
- `ArenaLayout`: heterogeneous pieces with per-kind counts per island (an island-depth table, seeded); floating platforms are not on the floor and keep a vertical clearance band for flight lanes; every existing rule (bounds, gaps, start/spawn clearance, sight line, keep-clear boxes) still holds; deterministic per seed; fallback = today's two pillars only.
- Island themes (3, e.g. "Dusk", "Frost", "Ember"): floor/wall/piece colours via MaterialPropertyBlock and the directional light colour/intensity; chosen per island from the run seed; Quick Match / tutorial / menu restore the default look.
- Run log: theme and piece counts per island.
- DONE: tests; compile; scenes rebuilt; device item (islands look and play differently; no piece blocks the start or a spawn; tabletop scale OK).

### T4. Demo mode (D6)
- Main menu: a `DEMO` button (fits the existing grid; adjust the layout cleanly). Demo = a match phase or a director of its own that reuses the arena: player invulnerable (health never drops, no death), 1–2 slow Strikers (low damage, long telegraph) and the practice targets respawning, no timer/score HUD, run systems off. Wrist pause → menu works as today.
- `GhostHands`: draws both tracked hands as translucent joint spheres + bone lines at the real hand poses (world space from `HandGestureTracker`; correct in VR arena and tabletop scale), one material, MaterialPropertyBlock colour per hand, no per-frame allocation, only active in demo mode. Clutch hand tints when GRAB is held; aim hand tints when PINCH is held.
- `GestureCaptions`: small world-space TMP labels beside each hand: `GRAB` (clutch rising edge), `PINCH = FIRE` (shot), `HOLD = CHARGE` (charge start), `PUSH = SHOCKWAVE` (shockwave); fade after ~0.8 s; readable in a headset recording (size, contrast), never covering the hero.
- Keep the hero, beam, impacts and sounds exactly as in the game.
- Report: a Korean recording guide — Quest built-in recording steps, recommended mode (VR arena vs MR TABLE), 30–60 s shot list (grab-fly → aim → fire → charge → shockwave → dodge), phone filming of the real hands from the side, side-by-side edit for LinkedIn (1:1 or 4:5).
- DONE: compile; scenes rebuilt; device item.

### T5. Scenes, tests, APKs, final review, report
- Rebuild scenes, run all tests, build `BuildQuestApkDevClean` and `BuildQuestApkRelease`; record paths and sizes in `PROGRESS.md`.
- Final review fixes (the workflow runs the reviewers), then write `AUTO/REPORT_FOR_HYUN.md` (section 6) and put `STATUS: ALL_DONE` on the first line of `PROGRESS.md`.

## 5b. Hard rules

1. **The XR Origin never moves or rotates.** No follow camera.
2. Don't delete the 20 legacy scripts in the `Scripts\` root or `Networking~`. Don't touch the scenes in `Assets/Scenes/`.
3. Every gesture threshold uses hysteresis. Every tunable is `[SerializeField]` + `[Tooltip]`. Don't change existing gameplay tuning defaults except as a decision above says (new tunables are fine).
4. **Quick Match, the tutorial, both aim modes and RUN must keep working.** All existing tests stay green.
5. No account creation, login, payments or sign-ups.
6. **No `git push`.** No force commands. Never `git reset --hard` committed work.
7. Don't modify files outside the project folder and `MetaAwards\Build\`.
8. No new packages. No `ProjectSettings/` changes unless a build needs them; list any in `PROGRESS.md`.
9. Never commit a broken compile.
10. In-game text in English. Reports for Hyun in Korean.
11. No XR/render setting changes (FFR, MSAA, latency mode, depth submission, refresh rate).
12. Build for players, not for a judging booth (the demo mode is Hyun's recording tool and a practice mode, not a judge-only flow).

## 6. Report (`AUTO/REPORT_FOR_HYUN.md`, Korean)

1. Task table (done / blocked / not started) with commits.
2. Headset checklist by priority: ASSIST rapid fire and charge (with `run_summary.py`), demo mode recording, enemy movement, terrain pieces/themes, room scan (wait 10–20 s in MR TABLE menu), regressions (Quick Match, tutorial, CURSOR, a full RUN, meta progression).
3. The recording guide (T4).
4. Decisions taken for Hyun (summary of `QUESTIONS_FOR_HYUN.md`), each reversible; changed values and how to undo each.
5. Review results (fixed / minor left).
6. Three game-first next steps (e.g. art direction, FIT TO TABLE, balance from data).
