# CLAUDE_AUTONOMOUS_PLAN — Round 3: freeze hunt, performance and bug fixes

## 한국어 요약

- **목표:** RUN 플레이 중 헤드셋 화면이 랜덤하게 멈추는 원인을 **증거로** 잡을 계측을 먼저 넣는다. 감사에서 확인된 스파이크(봇·피격 이펙트 생성/파괴, 머티리얼 누수, 매 프레임 할당)를 없애고, 확인된 버그를 고친다. 리팩토링은 이 일에 필요한 만큼만 한다(Hyun 승인: "추천대로"). 그다음 ASSIST 차지 오인을 고치고, 효과음·타격감·런 기록을 넣고, 메타 진행 설계 문서를 쓴다.
- **근거:** `AUTO/PERF_AUDIT.md` — 에이전트 13개가 읽기 전용으로 감사하고 반박 검증까지 했다. 핵심은 세 가지다.
  - 코드 안에 수백 ms 정지를 혼자 설명하는 경로는 없다.
  - 큰 후보는 봇 생성/파괴(봇 하나에 GameObject 12개, 머티리얼 4개 누수)와 피격마다 이펙트 생성/파괴다.
  - 오늘 저녁 Link 로그에서는 USB 전송이 정상이었고 OpenXR 세션도 계속 FOCUSED였다. 그래서 Link 플레이 중 멈춤은 에디터가 프레임을 제때 못 넘긴 쪽으로 의심한다(아직 미증명).
- **바뀌는 것(파일):**
  - 새 파일: `PerfSpikeLogger`(+ Core 순수 로직), 오브젝트 풀(Core), `BeamImpact` 풀
  - `RunDirector` / `RunBot`(풀링 생명주기), `HeroHealth`(MaterialPropertyBlock, 런 중 자동 부활 끔)
  - `PointingBeamController`, `XRHandsInputSource`, `HandGestureTracker`, `HandPuppeteerController`
  - `SpringFlightModel` / `ClutchMapper` / `HandClutchSampler` / `GestureRecognizers` / `WristMenuGesture` / `RunStateMachine`(Core, TDD)
  - `MatchHud` / `RunHudText`(바뀔 때만 갱신), `RunChoiceMenu`, `MatchDirector`
  - `BuildScript`(개발/릴리스 APK 2종), `HandHeroSceneBuilder`(배선 누락 시 실패)
  - `ProjectSettings`: Frame Timing Stats 켬, 스택 트레이스, Time 설정
- **결정:**
  - **D1** 진단 먼저. `PerfSpikeLogger`를 에디터·개발·릴리스 빌드 모두에 넣는다.
    - 실제 시간 기준으로 50 ms 넘는 프레임을 기록한다(`deltaTime`은 0.333 s에서 잘리고 일시정지 때 0이라 쓰지 않는다).
    - 손·머리 추적 끊김, 포커스, 일시정지, 런 단계, 시스템 제스처도 기록한다.
    - 링 버퍼에 쌓아 두고 안전한 때(일시정지·포커스 잃음·메뉴·전투 밖 10초마다)에만 파일에 쓴다.
    - 대안(개발 빌드에서만 기록)은 버렸다. 실제 성능을 보여 주는 쪽은 릴리스 빌드이기 때문이다.
  - **D2** APK는 `_dev`와 `_release` 2개를 따로 빌드한다. 지금은 같은 날 같은 이름이라 덮어쓴다.
  - **D3** 수다스러운 로그는 개발 빌드·에디터 전용으로 바꾸고, 릴리스 빌드에서는 Log·Warning 스택 트레이스를 끈다(Error는 유지).
  - **D4** 풀링 대상은 RunBot과 BeamImpact 둘뿐이다(런타임에 생성/파괴하는 건 이것뿐). 머티리얼 복제는 MaterialPropertyBlock으로 바꾼다.
  - **D5** 끊김 뒤 튀는 문제를 막는다.
    - Maximum Allowed Timestep 0.333 → 0.1, Fixed Timestep 0.01 → 0.02(강체 없음)
    - 스프링 감쇠를 지수식 + 서브스텝으로 바꾼다. 지금은 0.2초 넘는 프레임에서 히어로가 뒤로 튄다.
  - **D6** 손 입력 버그를 고친다.
    - 입력이 다시 켜질 때 남아 있던 상태를 리셋한다(가짜 쇼크웨이브, 히어로 점프).
    - 추적이 다시 잡힌 첫 프레임은 스냅한다.
    - 클러치 목표를 아레나 안으로 제한한다.
    - 부활할 때 클러치를 리셋한다.
    - 손목 일시정지는 손이 펴져 있을 때만 받는다.
    - Meta 시스템 제스처 중의 핀치는 무시한다.
  - **D7** 손 추적이 끊기면 레티클과 내 바닥 표시를 회색으로 바꾼다. 추적 끊김이 "멈춤"처럼 보이지 않게 하려는 것이다.
  - **D8** 런 사망·상태 버그(기존 Minor 6건 포함)를 고친다.
    - 런 중에는 자동 부활을 끈다.
    - 봇 빔은 다른 봇을 통과한다(아군 사격 없음).
    - 포털이 0개면 대체 포털을 낸다.
  - **D9** 같은 프레임에 나온 봇이 똑같이 움직이지 않게, 봇마다 시드를 다르게 주고 첫 발사 시간도 흩뜨린다.
  - **D10** 리팩토링은 위 작업에 필요한 것만 한다. 큰 클래스 분할은 하지 않는다.
  - **D11** XR·렌더 설정(FFR, MSAA, 지연 모드, 깊이 제출)은 무인으로 바꾸지 않는다. 보고서에 A/B 실험 후보로만 적는다.
  - **D12** 브랜치 `perf/freeze-hunt`, 커밋 `[auto] P<n>: …`, push 금지.
  - **D13** ASSIST 차지 오인을 고친다. 핀치를 0.25초 넘게 쥐고 있어야 차지를 시작하고, 그 전에는 감속도 표시도 없다. 손을 펴는 판정은 덜 굼뜨게 바꾼다. 쥔 시간 기록을 남겨 수치를 데이터로 조정할 수 있게 한다.
  - **D14** 코드로 합성한 효과음을 넣는다(에셋·패키지 없음, 짧고 깔끔한 SF 톤). 효과음은 시작할 때 미리 만들어 둔다. 나중에 실제 음원을 칸에 넣으면 그 음원이 대신 나온다.
  - **D15** 타격감을 넣는다. 피격되면 시야 가장자리가 붉게 번쩍이고(편안함 토글 있음), 봇을 처치하면 작은 폭발이 나온다.
  - **D16** 런마다 기록(`run_log.jsonl`)을 남기고, 요약 스크립트(`AUTO/tools/run_summary.py`)를 만든다.
  - **D17** 메타 진행은 설계 문서만 쓴다(코드 없음).
- **테스트·검증:**
  - 순수 로직은 TDD(EditMode)로 한다. 현재 321개 테스트는 모두 유지한다.
  - 컴파일, 씬 재생성(배선 오류 0), APK 2종 빌드까지 한다.
  - 마지막에 코드 리뷰 서브에이전트를 1회 돌린다.
  - **헤드셋 확인은 없다.**
- **기기에서 확인할 것:**
  - 멈출 때 시각을 메모해 두고 `perf_log.txt`와 대조한다.
  - Link로 할 때는 Unity Profiler를 켜고 한다.
  - 독립형 APK로 할 때는 `adb logcat -s VrApi`를 같이 받는다.
  - 멈출 때 봇과 HUD도 멈추는지 본다(화면 멈춤인지 조작 멈춤인지 구분).
  - 회귀 확인: 퀵 매치, 튜토리얼, 조준 모드 2종.
- **위험:**
  - 원인이 Link나 에디터 쪽이면 코드 수정만으로는 멈춤이 사라지지 않는다. 그 경우 이번 라운드의 성과는 "원인 확정"이다.
  - 풀링 재사용 버그(상태가 남는 문제)를 막으려고 생명주기 리셋 체크리스트를 넣었다.
  - 헤드셋 미검증이다.

---

> Operating instructions for Claude Code while Hyun is away. Read this file top to bottom at the start of every session.
> Sessions can end at any time (usage limits, crashes); `run_autonomous.ps1` starts a new one. **Memory lives only in files and git.**
>
> Background (read once per session, skim later):
> - `AUTO/PERF_AUDIT.md` — the audit this round implements (IDs like GC-1, SP-3, BR-2). `AUTO/perf_audit_raw.json` has the full text of every finding (detail, fix, verifier note). **Line numbers are for `19a5942`; re-read the code before editing.**
> - `HANDOFF_VR_HERO.md` — concept, ADRs. `docs/superpowers/specs/2026-10-08-aim-*.md` — the aim systems.
> - Round 1 and 2 plans and reports: `AUTO/archive/2026-10-07/`, `AUTO/archive/2026-10-08/`.
>
> Logs, reports and decision notes for Hyun are in Korean. Code, comments and commit messages are in English.

## 0. Facts

- Unity **6000.6.4f1**. Branch **`perf/freeze-hunt`** (from `auto/2026-10-08-run` @ `19a5942`). Remote `origin` exists; never push.
- Symptom (Hyun, 2026-10-08, played over **Quest Link** from the editor): at random moments, not tied to an island, the headset image freezes and its edges smear while Unity on the PC keeps running. Standalone APK not yet tried for this.
- Link evidence on this PC (2026-10-08 20:56–21:02): `%LOCALAPPDATA%\Oculus\LinkClient_*.txt` shows Glitches 0 and ~4 ms transmission every 5 s; `Client_*.txt` shows the OpenXR session FOCUSED the whole time. Transport looked healthy; the cause is unproven.
- Already on: incremental GC (`gcIncremental: 1`), IL2CPP ARM64, Vulkan, multiview. Stack traces: ScriptOnly for every log type.
- Tests: 321 EditMode tests pass at the start. They must all stay green.

## 1. Mode

**Unattended.** Never ask questions; nobody answers.
- When a choice is needed, take the most reasonable default that fits section 4's decisions and log it in `AUTO/QUESTIONS_FOR_HYUN.md` (task / question / choice / why / how to undo).
- When stuck, log it in `AUTO/BLOCKERS.md` and move on. Don't spend more than 30 minutes on one problem.

## 2. Session protocol (every session, in this order)

1. Read this file, then `AUTO/PROGRESS.md`, `AUTO/DECISIONS.md`, `AUTO/BLOCKERS.md`, and `git log --oneline -15`.
2. If a task is `IN_PROGRESS`, the previous session was cut off.
   - Run `git status`, then a compile check.
   - If it is salvageable, finish it. Otherwise `git stash push -m "abandoned-<task>"` and restart the task.
3. Otherwise mark the first `TODO` task `IN_PROGRESS` and start it.
4. When a task finishes: compile check → EditMode tests → commit `[auto] P<n>: <summary>` → mark it `DONE` in `PROGRESS.md` with 2–4 lines (results, audit IDs closed, caveats) → commit.
5. **At most 2 tasks per session.** Then tidy `PROGRESS.md` and end the session.
6. When every task is `DONE` or `BLOCKED`: write the two reports (section 6), put `STATUS: ALL_DONE` on the first line of `PROGRESS.md`, commit and end.

## 3. Verification (no headset)

- Commands (Unity must not be running; exit 4 = Unity open → log a blocker, keep coding, mark commits `[unverified]`):
  - Compile: `powershell -NoProfile -ExecutionPolicy Bypass -File "AUTO\tools\compile_check.ps1"`
  - Tests: the same command + `-Tests`
  - Scenes: the same command + `-ExecuteMethod HandHero.EditorTools.HandHeroSceneBuilder.BuildAll`, then grep `AUTO\logs\execute.log` for `has no field` / `has no array field` (must be empty; after P15 the builder fails instead).
  - APK: the same command + `-BuildTarget Android -TimeoutMinutes 120 -ExecuteMethod HandHero.EditorTools.BuildScript.<Method>` (after P2: `BuildQuestApkDev` and `BuildQuestApkRelease`). The APK build regenerates the scenes; commit them. Always `git checkout -- ProjectSettings/UnityConnectSettings.asset` before committing.
- **Pure logic goes in `HandHero.Core` with EditMode tests first (TDD)**: write the test, watch it fail, implement, watch it pass. MonoBehaviour wiring and scenes = compile + scene build + a device-checklist item.
- The test assembly references only `HandHero.Core` (CR-6). So move every rule you want tested (pool reuse contract, spike detection, HUD change keys, spring step, clutch clamp, sampler reset) into Core first; keep MonoBehaviours thin.
- **Never hand-edit `.unity` YAML.** Scenes come only from `HandHeroSceneBuilder.BuildAll`, which must stay idempotent. `ProjectSettings/*.asset` edits are allowed only where a task below names them; prefer editing them through an editor script run with `-ExecuteMethod` (e.g. `PlayerSettings.enableFrameTimingStats = true`), and list every changed key in `PROGRESS.md`.
- Use `git add <paths>`. Never `git add -A`.

## 4. Decisions (approval status and details: `AUTO/DECISIONS.md`)

D1 diagnosis first: logger in every build · D2 dev + release APKs with distinct names · D3 chatty logs dev/editor only, release stack traces off for Log/Warning · D4 pool only RunBot and BeamImpact; MaterialPropertyBlock instead of material clones · D5 Max Allowed Timestep 0.1, Fixed Timestep 0.02, frame-rate-independent spring · D6 hand-input robustness fixes · D7 tracking-lost cue · D8 run death/state fixes incl. no bot friendly fire · D9 per-bot seeds and first-shot jitter · D10 refactor only what these need · D11 no XR/render setting changes · D12 branch/commit rules · D13 charge hold delay · D14 synthesized SFX · D15 hit/kill feel · D16 run telemetry + summary script · D17 meta progression: design doc only.

## 5. Task queue (in order; audit IDs in brackets)

### P1. PerfSpikeLogger (D1) [CR-2, CR-3, RS-1, RS-15]
- Core (TDD): `FrameSpikeDetector` + a fixed-size ring buffer of a `PerfSample` struct, and a formatter that writes one line per record into a reused `StringBuilder`/`char[]` (no per-record allocation after warm-up; test the formatting and the wrap-around).
  - Spike = real frame interval > `spikeThresholdMs` (50). Measure with `Time.realtimeSinceStartupAsDouble` differences, **not** `Time.deltaTime` (capped at maximumDeltaTime, 0 while `timeScale` = 0).
  - Record: wall-clock time (`DateTime.Now` taken only when flushing is fine; store realtime seconds per record), frame ms, CPU/GPU ms from `FrameTimingManager` when available (else -1), `GC.CollectionCount(0)` delta since the previous record, `GC.GetTotalMemory(false)`, match phase, run phase, island index, bots alive, `Time.timeScale`, `Application.isFocused`, left/right hand `IsTracked`, head tracked.
  - Edge events (one record each, no threshold): hand tracking lost/regained per hand, head tracking lost/regained, `OnApplicationFocus`, `OnApplicationPause`, match/run phase changes, system gesture start/end (wired in P9).
- HandProto: `PerfSpikeLogger` MonoBehaviour, compiled in **every** build and the editor (a serialized `enabled` toggle, default on). File: `Path.Combine(Application.persistentDataPath, "perf_log.txt")`, appended, with a session header (app version, build type dev/release, device model, refresh rate, date). Flush the buffer only on `OnApplicationPause(true)`, `OnApplicationFocus(false)`, return to menu, run end, and every 10 s while the match phase is not a fight/run island. Cap the file at ~1 MB (rotate to `perf_log_prev.txt`).
- Scene builder adds it to both scenes. Turn on Frame Timing Stats (`PlayerSettings.enableFrameTimingStats`) via an editor method.
- DONE: tests for detector, ring buffer and formatter; compile; scenes rebuilt without wiring errors.

### P2. Builds and logging (D2, D3) [RS-2, RS-3, CR-4]
- `BuildScript`: `BuildQuestApkDev` (`BuildOptions.Development | AllowDebugging`, optional `ConnectWithProfiler` off) → `HandHero_<yyyyMMdd_HHmm>_dev.apk`; `BuildQuestApkRelease` → `..._release.apk`. Keep `BuildQuestApk` as an alias of release so old commands still work. Both log their output path.
- A tiny `HHLog` helper in HandProto with `[Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]` for chatty logs; route existing per-event `Debug.Log` calls through it (warnings/errors stay as they are).
- Release stack traces: Log and Warning → None; Error, Assert, Exception keep ScriptOnly (editor script, list the change).
- DONE: compile; one dev APK build to prove the path (the final builds happen in P15).

### P3. Per-frame waste (D10) [GC-1, GC-2, GM-1…GM-5, GM-7, GC-M2, GM-9, GM-11, GM-12, GC-6…GC-9]
- `AimAssistTarget.All` loops by index; skip candidate collection when the acquire cone is 0 and no target is held (bots).
- `HandGestureTracker.FindSubsystem`: reuse a static list; build the report string only when the state changes; poll at most every 0.5 s while no subsystem runs.
- HUD: make `RunHudText` produce a change key (ints: island, kind, objective count/seconds, HP, crystals, banner state) and have `MatchHud` rebuild/assign strings only when the key changes (TDD on the key). Same for the Quick Match score line, intro banner and end summary. Cache `IslandName` strings.
- `RunChoiceMenu.ShopKey` by index; `RunStateMachine.Stats` cached by inventory `TotalLevels` (one source of truth, RF-6).
- `MatchDirector` headset-presence check throttled (e.g. every 0.25 s); `HandMenuPointer` raycast with a layer mask and cached component lookups.
- `Camera.main` cached; `LockOnRing`, shockwave ring and `HeroGroundMarker` write only when values change.
- DONE: tests for the HUD change key and stats caching; compile; existing tests green.

### P4. Materials and renderer caching (D4) [GC-4, SP-2, RS-5, BR-11, GC-5, GC-10, GM-13]
- `HeroHealth`: replace `renderer.material` cloning with one `MaterialPropertyBlock` per renderer (the `HandMenuButton` pattern; cache shader property IDs). Cache the renderer and collider arrays once in `Awake` for `SetVisible`.
- `PrototypeTarget` and `HandMenuButton`: property IDs / property blocks, no clones.
- DONE: compile; scenes rebuilt; the hit flash still uses the same colors (device item).

### P5. Pool core + BeamImpact pool (D4) [SP-3, GC-3, RF-2 (impact part only)]
- Core (TDD): a small generic pool (`Get`, `Release`, prewarm, cap, double-release guard, a reset callback contract).
- Extract hit-effect spawning from `PointingBeamController` into a `BeamImpactPool` component (prewarm ~8, scene-level, shared by all heroes); `ImpactFlash` returns itself to the pool instead of `Destroy`. The beam controller only asks the pool for an effect.
- DONE: pool tests; compile; scenes rebuilt.

### P6. RunBot lifecycle and pool (D4, D9) [SP-1, SP-4, SP-6, SP-9, RF-1, BR-4a, GM-10, GM-M1, BC-M1, BC-2]
- `RunBot.Activate(Vector3 at, IslandSpec spec, int seed)` / `Deactivate()`; `RunBot.Died` event subscribed once. Reset checklist on every activate: `HeroHealth.SetSpawnPosition(at)` then health scale/reset, `FlyingCharacter.Respawn`, puppeteer clutch reset (`HandPuppeteerController.OnDisable` resets `_clutch` and clears the target), `BotBrain.Reset` with the new seed, beam/charge/cooldown/telegraph cleared (`PointingBeamController.OnDisable`), aim reset, registry membership in `AimAssistTarget`.
- `RunDirector`: a pool prewarmed at run start with `MaxAlive + 1`; despawn = `Deactivate` immediately (stops updates, colliders and registry at once).
- Seeds: one `System.Random` per run (seeded from the run seed) gives each spawn its seed; `BotBrain.Reset` jitters the first fire timer (e.g. ×U(0.5, 1.0)). TDD the jitter/seed rule in Core.
- DONE: Core tests; compile; scenes rebuilt; `RunBot.prefab` regenerated by the builder if needed.

### P7. Flight and hand-input robustness (D5, D6) [BC-3, BR-12, RS-7, CR-5, BC-4, BR-13, BR-2, BR-3, BR-1, CR-8]
- Core (TDD): `SpringFlightModel` frame-rate-independent damping (`exp(-damping·dt)`, same for glide drag) and sub-steps when dt > 1/60 (fixed ~1/90 s steps, cap 4). Test: one 0.33 s step never moves the hero away from the clutch target.
- Core (TDD): `ClutchMapper.Step` clamps its internal target to the bounds it is given (`ArenaBounds`); puppeteer and CURSOR pass theirs. Test: drag past the wall, reverse 1 cm, the position moves inward at once.
- Core (TDD): `Reset()` on `HandClutchSampler`, `PalmPushRecognizer`, `PalmsTogetherRecognizer`; `XRHandsInputSource.OnEnable` resets all of them. Test: hold, gap, reset, step → zero delta and no push.
- `HandGestureTracker`: on the untracked → tracked frame, snap pose/strengths/aim to raw values; smooth from the next frame.
- Revive/respawn: `HandPuppeteerController` resets its clutch on `HeroHealth.Respawned` (and `PointingBeamController` cancels charge/pending shot, already partly there).
- Palm-push speed uses the same (clamped) dt as the hand smoothing.
- `ProjectSettings/TimeManager.asset`: Maximum Allowed Timestep 0.1, Fixed Timestep 0.02 (editor script or the Time settings API; list the change).
- DONE: tests; compile; device items (hitch recovery, wall drag, regrab after tracking loss, revive).

### P8. Run death and state fixes (D8) [BR-4b/c, BC-1, BR-5, BR-8, BR-9, BR-10, BR-6, BC-6, BC-7, BC-8, BC-9, BC-5]
- `HeroHealth.SetAutoRespawn(bool)`: off at run start, on at run end; during a run only `Revive`/`ResetHealth` bring the player back. A player death is resolved in every non-Idle run phase (revive or Defeat); drop the `IsPaused` gate in `RunStateMachine.ReportPlayerDeath` (TDD).
- Kills during a paused frame are counted (TDD where it lives in Core).
- `RunDirector` subscribes to the match phase change and ends the run synchronously (keep the Update check as fallback).
- `RunChoiceMenu`: lambdas `Choose(() => run.RerollShop())`, `Choose(() => run.LeaveShop())`.
- No bot friendly fire: a team id on the hero (Player/Bot); `RaycastIgnoringSelf` skips same-team heroes; `HeroHealth.OnBeamHit` ignores same-team shooters as a backstop. Quick Match unchanged.
- Core guards (TDD): zero portals → a fallback portal (Arena + Random); `MaxAlive <= 0` treated as 1; `TriggerGesture` with `gripSettleTime <= 0`; `TutorialSequencer` telegraph time 0 → no NaN; `WristMenuGesture` press requires `fistStrength <= MaxFistStrength`.
- DONE: tests; compile; scenes rebuilt.

### P9. Tracking-lost cue and system gesture (D6, D7) [CR-1, CR-7]
- While the aim hand is untracked, the reticle (ASSIST) or cursor marker (CURSOR) turns grey and the lock-on ring hides; while the clutch hand is untracked, the player's ground marker turns grey. Colors are `[SerializeField]` + `[Tooltip]`. No text.
- Read the Meta aim flags (`MetaAimHand` / `MetaAimFlags.SystemGesture` from com.unity.xr.hands, verify the API in `Library/PackageCache` before use). While the system gesture is active on a hand, its pinch reads as not pressed. Log start/end to `PerfSpikeLogger`. If the API does not compile on this package version, log a blocker and skip.
- Wire the remaining logger edge events (hand/head tracking, focus, pause, phases).
- DONE: compile; scenes rebuilt; device items.

### P10. ASSIST charge misfire (D13) [Hyun device report 2026-10-08]
- Symptom (Hyun): in ASSIST, a normal pinch shot is often taken as a charge. Mechanism found in code: the charge starts on the very first pinch frame (`ChargeInputRule` → `ChargeShotModel.Step(held)`: indicator on, hero slowed to 60 %), and `MinChargeTime` is only 0.3 s. Releasing needs the *smoothed* `PinchStrength` (lerped in `HandGestureTracker`) to fall below `pinchResetThreshold` 0.5, i.e. thumb–index > ~3.75 cm, so a quick tap often reads as a ≥ 0.3 s hold and a charge shot fires on release as well.
- Core (TDD): add `ChargeParams.HoldDelay` (default 0.25 s). The charge only starts after the pinch has been held that long; before that there is no slowdown and no indicator, and a release fires nothing extra. `MinChargeTime` counts from the start of the charge. Tests: a 0.4 s tap fires one normal shot and no charge shot; a 0.25 + 0.3 s hold releases a charge; cancel rules unchanged.
- Release detection: the pinch *release* edge reads the raw (unsmoothed) pinch strength, or a release threshold of 0.6, whichever is simpler and testable (`PinchTrigger` keeps its hysteresis; log the choice).
- Expose each finished pinch (hold duration, became a charge or not) as an event/counter in Core; P13 writes it to the run telemetry so Hyun can tune `HoldDelay` from data.
- Approved tuning-default changes: new `HoldDelay` 0.25 s; the release change above. Existing `MinChargeTime` 0.3 stays.
- DONE: tests; compile; device item "ten quick shots in ASSIST → zero charge shots; a deliberate 1 s hold still charges".

### P11. Procedural sound effects (D14)
- The game has no audio at all (no clips assigned anywhere). Add sound without new assets or packages: Core (TDD on parameters only) `SfxSynth` recipes (oscillator types, pitch envelope, noise, ADSR, duration) rendered once at startup into `AudioClip.Create` PCM clips (never mid-fight). A `SfxPlayer` with a small pool of `AudioSource`s (3D for world events at the hero/bot position, 2D for UI), master volume and per-event volume as `[SerializeField]` + `[Tooltip]`.
- Events: player beam fire, charge start (after `HoldDelay`) + charge-ready ping + charged release, bot telegraph warning (important: helps dodging), hit dealt, hit taken, shockwave, bot down, island start countdown ticks + FIGHT, island cleared, chest open, item pick, shop buy, reroll, not-enough-crystals, portal pick, VICTORY, DEFEAT, menu point/press, pause/resume.
- Style: short, clean sci-fi synth (blips, zaps, soft thumps); nothing louder than the beam; no harsh high frequencies. Keep recipes in one file so they are easy to swap for real recordings later (each event = one serialized `AudioClip` slot that overrides the synth when assigned).
- Quick Match, tutorial and RUN all use it. Budget: < 2 MB of generated PCM total, generation < 100 ms at startup (log it in the perf log header).
- DONE: recipe tests (durations, no clipping: peak ≤ 0.9); compile; scenes rebuilt; device item.

### P12. Hit and kill feel (D15)
- Damage taken: a short red edge flash in the player's view, rendered by a world-space quad parented to the camera (no XR Origin movement), alpha ≤ 0.35, ≤ 0.25 s, with a comfort toggle. Kill: a small pooled burst (scaled sphere shards or a ring, reuse the P5 pool) at the bot. Crit: the existing yellow beam plus a higher-pitched hit sound.
- DONE: compile; scenes rebuilt; device item (comfort: no flicker, readable).

### P13. Run telemetry and summary script (D16)
- Core (TDD): a `RunRecord` built from run events: seed, aim mode, view mode, start time, result (Victory/Defeat/Quit), total time, per-island time and island type, damage taken per island, death island, revives used, items picked (id, level, island), shop buys/rerolls, crystals earned/spent, shots fired, hit rate, charge shots, pinch hold durations (from P10).
- HandProto: append one JSON line per run to `Application.persistentDataPath/run_log.jsonl` at run end or quit (no writes during fights).
- `AUTO/tools/run_summary.py` (Python 3, standard library only): reads one or more `run_log.jsonl`, prints run count, win rate, median/max run time vs the 10-minute limit, deaths per island, slowest islands, most-picked items, charge misfire rate (holds that turned into charges under 0.5 s), and suggests which `RunParams` knob to turn first (rule-based, from the round-2 report: `EnemyHealthPerIsland`, `BossHealthMult`, Arena bot count). Include a sample `run_log.jsonl` fixture and run the script on it in the session.
- DONE: tests; compile; script runs on the fixture; `PERF_REPORT.md` explains how to pull `run_log.jsonl` (adb and editor path).

### P14. Meta progression design doc only (D17)
- Write `docs/superpowers/specs/2026-10-09-meta-progression-design.md` (starts with `## 한국어 요약`). No code.
- Content: goal ("one more run" for judges who play 1–3 runs); 2–3 approaches (e.g. A: unlockable starting relic choice after the first win + best-time board; B: a "key" currency spent on permanent unlocks of new items into the pool; C: daily/seeded challenge run) with trade-offs for a ≤ 10-minute judged session; recommendation; data model on PlayerPrefs; UI touch points in the existing menu; risks; open questions for Hyun as D1… each with a recommended answer. Ground it in `docs/crab-champions-systems-analysis.md` and the current catalog.
- DONE: the doc is committed; `REPORT_FOR_HYUN.md` lists its open questions.

### P15. Builder strictness, scenes, tests, APKs [RF-7]
- `HandHeroSceneBuilder`: a missing serialized field during wiring throws (fails `BuildAll`) instead of only logging.
- Rebuild scenes, run all tests, build `BuildQuestApkDev` and `BuildQuestApkRelease`; record both paths and sizes in `PROGRESS.md`.
- DONE: 0 wiring errors, all tests green, two APKs in `MetaAwards\Build\`.

### P16. Final review and reports
- One code-review subagent over `19a5942..HEAD` (exclude `.unity`): fix every confirmed Critical/Important finding, re-test, record Minor ones.
- Write `AUTO/PERF_REPORT.md` and `AUTO/REPORT_FOR_HYUN.md` (section 6), then `STATUS: ALL_DONE`.

## 5b. Hard rules

1. **The XR Origin never moves or rotates.** No follow camera.
2. Don't delete the 20 legacy scripts in the `Scripts\` root or `Networking~`. Don't touch the scenes in `Assets/Scenes/`.
3. Every gesture threshold uses hysteresis. Every tunable is `[SerializeField]` + `[Tooltip]`. **Don't change existing gameplay tuning defaults** (approved exceptions: the Time settings in P7 and the charge hold delay/release in P10).
4. **Quick Match, the tutorial, both aim modes and RUN must keep working.** All existing tests stay green; existing tests may be updated only where a decision above deliberately changes behaviour (say which in the commit).
5. No account creation, login, payments or sign-ups.
6. **No `git push`.** No force commands. Never `git reset --hard` committed work.
7. Don't modify files outside the project folder and `MetaAwards\Build\`.
8. No new packages. `ProjectSettings/` changes only as named in P1, P2 and P7; list them in `PROGRESS.md`.
9. Never commit a broken compile. If it is unavoidable, mark it `[broken]` and fix it in the next commit.
10. In-game text in English. Reports for Hyun in Korean.
11. No XR/render setting changes (FFR, MSAA, latency mode, depth submission, refresh rate) — D11.

## 6. Reports (Korean)

### `AUTO/PERF_REPORT.md`
1. What was fixed, by audit ID, and what was deliberately not done (with reasons).
2. Remaining suspects, ranked, and the A/B levers from D11 (FFR, MSAA 4→2, latency mode PrioritizeInputPolling → default, depth submission, Physics simulation mode Script) with how to try each.
3. **How Hyun collects evidence**, copy-paste ready for PowerShell (one command per line, no `&&`; adb is `& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"`):
   - Standalone APK: install command, `logcat -c`, `logcat -s VrApi:* Unity:* PerfSpikeLogger:*` to a file while playing, and pulling `perf_log.txt` from `/sdcard/Android/data/<package>/files/` (read the package id from ProjectSettings).
   - Quest Link (editor): where the editor writes `perf_log.txt` (`%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\`), and a short Unity Profiler recipe (Window > Analysis > Profiler, record during play, after a freeze click the tallest frame, check PlayerLoop vs EditorLoop, GC.Alloc and the top marker).
   - What to write down at each freeze: clock time, what was on screen, whether bots/HUD kept moving.
4. How to read `perf_log.txt` (one example line explained).

### `AUTO/REPORT_FOR_HYUN.md`
1. Task table (done / blocked / not started).
2. Headset checklist by priority: freeze capture first (standalone APK and Link), then the charge fix (ten quick ASSIST shots → no charge shot), sound and hit feel, then each fix to feel (hitch recovery, wall drag, regrab after tracking loss, revive, no friendly fire, tracking-lost grey cue, wrist pause only with an open hand), then regressions (Quick Match, tutorial, both aim modes, a full RUN).
3. Decisions taken for Hyun (summary of `QUESTIONS_FOR_HYUN.md`), each reversible.
4. Changed settings and tuning (Time settings, Frame Timing Stats, stack traces) and how to undo each.
5. How to pull `run_log.jsonl` and run `python AUTO	oolsun_summary.py <file>`; the meta progression doc's open questions.
6. Three next steps.
