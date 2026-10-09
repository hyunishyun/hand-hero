# CLAUDE_AUTONOMOUS_PLAN — Round 4: first-run hitch, balance, leftover fixes, meta progression A, enemy/terrain design, MR room-scan spike

## 한국어 요약

- **목표:** 3차 기기 테스트 결과를 반영해 게임 자체를 다듬는다.
  - 독립형에서는 멈춤이 없었다. 다만 RUN 시작 직후 약 30초 동안 프레임이 조금씩 빠졌는데, 이걸 없앤다.
  - 런이 쉽고 길었다(9분 48초, 받은 피해 166). 밸런스를 맞춘다.
  - 리뷰에서 남은 Minor 4건을 고친다.
  - 메타 진행 A안(시작 유물 + 개인 최고 기록)을 **심사용 기능 없이**, 좋은 게임의 기본기로 구현한다.
  - 다음 큰 줄기를 준비한다: 적 다양성과 지형 다양성은 **설계 문서**로, MR 테이블이 내 방을 스캔해 지형을 만드는 기능은 **가능성 확인용 프로브(스파이크)**로 만든다.
- **근거:** `AUTO/device_logs/2026-10-09/`
  - `perf_log.txt`: 게임플레이 중 50 ms 넘는 프레임 0개. 메뉴에서만 138·395·167 ms.
  - `freeze_session.txt`: VrApi 기준으로 앱이 빠뜨린 프레임이 앱 시작과 RUN 시작 직후 30초에 몰려 있다.
  - `run_log.jsonl`: 승리, 587.9초, Horde 45초 ×3, 보스 102초.
- **바뀌는 것(파일):**
  - 첫 런 끊김: 풀·이펙트 미리 만들기(로딩 때), GPU 워밍업, `PerfSpikeLogger` 작은 끊김 카운터
  - 밸런스: `RunParams`(Horde 시간, 보스 체력, 런 봇 데미지 배수)
  - Minor 4건: 시스템 제스처 때 차지 취소, `PerfSpikeLogger`, `PinchHoldStats`, 봇 `Random`
  - 설계 문서 2개(적, 지형), `RoomScanProbe`(MR TABLE 전용) + OpenXR 평면·바운딩 박스 기능 설정
  - 메타 진행: Core `MetaProgress` + `IKeyValueStore`(새 파일), `RunStateMachine`(StartRelic 단계), `RunDirector`, `RunChoiceMenu`, `MatchHud`·메뉴·끝 화면·일시정지 패널, `HandHeroSceneBuilder`
- **결정:**
  - **D1** 첫 런 끊김 제거
    - RunBot 풀과 이펙트 풀을 런 시작 때가 아니라 **씬 로드 때** 미리 만든다.
    - 로드 직후 봇·빔·피격·처치 폭발·비네트를 몇 프레임 실제로 그려서 셰이더와 파이프라인을 미리 준비한다(GPU 워밍업).
    - 로거는 50 ms 스파이크와 별도로 25 ms 넘는 "작은 끊김"도 센다.
  - **D2** 밸런스(퀵 매치는 그대로)
    - Horde 45 → 30초
    - 보스 체력 배수 6 → 5
    - 런 봇 데미지 ×1.15
  - **D3** 리뷰에서 남은 Minor 4건
    - 시스템 제스처가 시작되면 차지를 쏘지 않고 취소한다.
    - 로거의 머리 장치 조회를 0.25초 간격으로 줄인다.
    - 핀치 기록은 런 중에만 쌓는다.
    - 봇 `Random`을 스폰마다 새로 만들지 않고 재사용한다.
  - **D4** 메타 진행 A
    - 시작 유물 해금 조건: 섬 5 도달 → Second Wind, 첫 승리 → Big Chests, 다른 조준 모드로 승리 → Dividends
    - 섬 1 전에 시작 유물 상자(해금된 것 + NONE)
    - 조준 모드별 개인 최고 기록(최고 섬, 최단 승리 시간): 메뉴에 한 줄, 끝 화면에 NEW BEST·UNLOCKED
    - Greed는 넣지 않는다.
  - **D5** 심사 현장용 기능은 넣지 않는다(쇼케이스 해금 토글, 심사위원마다 초기화하는 흐름 없음). 진행 초기화는 일반 게임처럼 일시정지 메뉴의 RESET PROGRESS(확인 한 번 더)로만 한다.
  - **D7** 적 다양성 설계 문서(코드 없음): 손만 쓰고 앉아서 하는 플레이어에게 서로 다른 대응을 요구하는 적 4–6종 후보, 보스 패턴 2–3개, 먼저 만들 조각(적 2종 + 보스 패턴 1개), Hyun이 정할 질문(추천 답 포함).
  - **D8** 지형 다양성 설계 문서(코드 없음): 섬마다 엄폐물·통로·높이·위험 요소가 바뀌는 방식. 접근은 직접 만든 모듈 교체 / 규칙 기반 절차 생성 / 혼합 중에서 추천한다. 테이블 크기에서도 동작해야 한다.
  - **D9** MR 방 스캔 스파이크
    - MR TABLE 모드에서만 Meta OpenXR의 평면·바운딩 박스(·메시)를 켜서, 무엇이 얼마나 빨리 잡히는지 `perf_log`에 기록하고 디버그 와이어프레임으로 보여 준다.
    - 이 작업에 한해 해당 OpenXR 기능과 scene 권한 설정 변경을 허용한다. 새 패키지는 넣지 않는다.
    - 권한을 거부하면 지금과 똑같이 동작한다.
    - 결과는 지형 설계 문서의 "방 기반 지형" 절에 넣는다.
  - **D6** 브랜치 `auto/2026-10-09-run`(`perf/freeze-hunt` `a3b1dd1`에서), 커밋 `[auto] S<n>: …`, push 금지.
- **테스트·검증:**
  - 순수 로직은 TDD: `MetaProgress`, StartRelic 단계, 차지 취소 규칙, 작은 끊김 카운터, 밸런스 값.
  - 461개 테스트를 유지한다. 옛 밸런스 값을 고정해 둔 기존 테스트는 D2에 맞게 고치고 커밋에 적는다.
  - 컴파일, 씬 재생성, APK 2종, 최종 리뷰.
  - **헤드셋 확인은 없다.**
- **기기에서 확인할 것:**
  - RUN 시작 직후 30초가 부드러운지(VrApi Stale, `perf_log`의 작은 끊김 수)
  - 런 시간 8–9분, 난이도 체감
  - 시작 유물 상자, 최고 기록 줄, NEW BEST·UNLOCKED, RESET PROGRESS
  - 3차에서 못 해 본 것: 부활, DEFEAT 화면
  - MR TABLE에서 권한 허용 → 방의 평면·가구가 잡히는지(와이어프레임, `perf_log`). Quest의 공간 설정(Space Setup)을 먼저 해 둬야 한다.
- **위험:**
  - GPU 워밍업이 로딩을 조금 늘린다(목표 1초 이내).
  - 밸런스 값은 런 한 판에서 나온 추정이다. 다음에 `run_summary.py`로 다시 잰다.
  - Second Wind를 처음부터 가지고 시작하면 런이 쉬워질 수 있다. 기기에서 확인한다.
  - MR 프로브는 기기 없이 확인할 수 없다. 권한·기능 설정이 MR 모드 시작을 깨뜨릴 위험이 있어서, 권한을 거부했을 때와 기능이 실패했을 때 모두 지금처럼 동작하게 만든다.

---

> Operating instructions for Claude Code while Hyun is away. Read this file top to bottom at the start of every session.
> Sessions can end at any time (usage limits, crashes); `run_autonomous.ps1` starts a new one. **Memory lives only in files and git.**
>
> Background (read once per session, skim later):
> - `AUTO/device_logs/2026-10-09/` — Hyun's standalone device logs from round 3 (`perf_log.txt`, `run_log.jsonl`, `freeze_session.txt` = UTF-16 logcat with VrApi lines).
> - `docs/superpowers/specs/2026-10-09-meta-progression-design.md` — implement approach **A** with the answers in section 4 below. Where the doc differs (showcase toggle, per-judge reset), section 4 wins.
> - Round 3 plan and reports: `AUTO/archive/2026-10-09/` (`PERF_REPORT.md` section 1 lists the four leftover minors).
>
> Logs, reports and decision notes for Hyun are in Korean. Code, comments and commit messages are in English.

## 0. Facts

- Unity **6000.6.4f1**. Branch **`auto/2026-10-09-run`** (from `perf/freeze-hunt` @ `a3b1dd1`). Remote `origin` exists; never push.
- Round 3 device results (Hyun, 2026-10-09, standalone release APK, Quest 3, 72 Hz):
  - No gameplay frame over 50 ms; frame ~14 ms, GPU 1–2 ms. Spikes only in Menu: 138 and 395 ms at app start, 167 ms right before RUN started (gc +2, heap 3.9 → 4.9 MB: the RunBot pool prewarm).
  - VrApi (app pid 22378): stale-frame bursts at app start (Stale=41) and during the first ~30 s after RUN started (12:49:36–12:50:09, 7–37 stale per second); near zero afterwards. Those frames were under 50 ms, so the logger missed them.
  - Run (CURSOR, tabletop): Victory, 587.9 s, damage taken 166 (easy), Boss 102 s, island 7 Arena 85 s, three Horde islands 45 s each.
  - Confirmed on device: charge misfire fixed (ASSIST and CURSOR), sounds, hit feel, hitch recovery, wall drag, tracking-lost grey cue, no bot friendly fire, system-gesture pinch, colors, Quick Match, tutorial, label autosize, rapid trigger. Not tried yet: revive, DEFEAT screen.
- The Quest Link editor freeze is treated as editor/Link-side and is out of scope.
- Tests: 461 EditMode tests pass at the start. They must all stay green.

## 1. Mode

**Unattended.** Never ask questions; nobody answers.
- When a choice is needed, take the most reasonable default that fits section 4 and log it in `AUTO/QUESTIONS_FOR_HYUN.md` (task / question / choice / why / how to undo).
- When stuck, log it in `AUTO/BLOCKERS.md` and move on. Don't spend more than 30 minutes on one problem.

## 2. Session protocol (every session, in this order)

1. Read this file, then `AUTO/PROGRESS.md`, `AUTO/DECISIONS.md`, `AUTO/BLOCKERS.md`, and `git log --oneline -15`.
2. If a task is `IN_PROGRESS`, the previous session was cut off. Run `git status`, then a compile check. If it is salvageable, finish it; otherwise `git stash push -m "abandoned-<task>"` and restart the task.
3. Otherwise mark the first `TODO` task `IN_PROGRESS` and start it.
4. When a task finishes: compile check → EditMode tests → commit `[auto] S<n>: <summary>` → mark it `DONE` in `PROGRESS.md` with 2–4 lines (results, caveats) → commit.
5. **At most 2 tasks per session.** Then tidy `PROGRESS.md` and end the session.
6. When every task is `DONE` or `BLOCKED`: write the report (section 6), put `STATUS: ALL_DONE` on the first line of `PROGRESS.md`, commit and end.

## 3. Verification (no headset)

- Commands (Unity must not be running; exit 4 = Unity open → log a blocker, keep coding, mark commits `[unverified]`):
  - Compile: `powershell -NoProfile -ExecutionPolicy Bypass -File "AUTO\tools\compile_check.ps1"`
  - Tests: the same command + `-Tests`
  - Scenes: the same command + `-ExecuteMethod HandHero.EditorTools.HandHeroSceneBuilder.BuildAll` (the builder throws on a missing serialized field).
  - APK: the same command + `-BuildTarget Android -TimeoutMinutes 120 -ExecuteMethod HandHero.EditorTools.BuildScript.BuildQuestApkDev` (and `BuildQuestApkRelease`). The APK build regenerates the scenes; commit them. Always `git checkout -- ProjectSettings/UnityConnectSettings.asset` before committing.
- **Pure logic goes in `HandHero.Core` with EditMode tests first (TDD)**: write the test, watch it fail, implement, watch it pass. The test assembly references only Core, so put every rule you want tested there; keep MonoBehaviours thin. MonoBehaviour wiring and scenes = compile + scene build + a device-checklist item.
- **Never hand-edit `.unity` YAML.** Scenes come only from `HandHeroSceneBuilder.BuildAll`, which must stay idempotent.
- Use `git add <paths>`. Never `git add -A`.

## 4. Decisions (approval status and details: `AUTO/DECISIONS.md`)

D1 first-run hitch: prewarm pools at scene load + GPU warmup + a 25 ms hitch counter · D2 balance: Horde 30 s, BossHealthMult 5, run-bot damage ×1.15 (Quick Match unchanged) · D3 the four leftover minors · D4 meta progression A · D5 no judge-only features · D6 branch/commit rules · D7 enemy variety design doc · D8 terrain variety design doc · D9 MR room-scan spike (OpenXR plane/bounding-box features + scene permission approved for S8 only).

## 5. Task queue (in order)

### S1. First-run hitch (D1)
- Move the RunBot pool prewarm (`RunDirector`, `MaxAlive + 1`) and every effect pool prewarm to scene load (the owning component's `Start`). Starting a run must not instantiate anything.
- GPU warmup right after scene load, while the menu opens: for 2–3 frames, draw one prewarmed RunBot (not registered as a target, controls off), a beam with the player, bot and crit colors, one BeamImpact, one KillBurst, the damage vignette at minimal non-zero alpha and the charge orb, placed inside the camera view at the far side of the arena and scaled small but non-zero so they still render; then return everything to its pool. Before writing this by hand, check whether Unity 6.6 has a usable PSO/shader warmup API (e.g. `GraphicsStateCollection`, `ShaderWarmup`): confirm it exists in the editor's managed assemblies or package cache and is meant for Vulkan; use it only if it compiles; log the choice in `QUESTIONS_FOR_HYUN.md`.
- `PerfSpikeLogger`: keep the 50 ms spike records; add, per flush, the count of frames over `hitchThresholdMs` (25) and the worst one, and record the first such frame after each phase change (TDD the counter in Core). Put the warmup time in the session header next to `sfx … ms`.
- DONE: Core tests; compile; scenes rebuilt; device item (first 30 s of a RUN smooth; app start may take up to ~1 s longer).

### S2. Balance (D2)
- `RunParams`: `HordeTime` 45 → 30, `BossHealthMult` 6 → 5, new `EnemyDamageMult` 1.15 applied to every run bot's shot damage, on top of the Elite/Boss multipliers. Quick Match and the tutorial bot keep their damage.
- Recompute the run-time estimate (method: "R5 시간 추정" in `AUTO/archive/2026-10-08/PROGRESS.md`) using the device numbers above and write it in `PROGRESS.md`. Make `run_summary.py` print the run time against the 10-minute limit if it doesn't already.
- DONE: tests (update tests that pin the old values; say which in the commit); compile.

### S3. Leftover minors (D3)
- Core (TDD): when the system gesture starts on the aim hand while a charge is held, the charge is cancelled (no shot). A pinch still closed after the gesture ends must reopen (already true; keep the test).
- `PerfSpikeLogger`: query the head XR device at most every 0.25 s (reuse the cached list).
- `PinchHoldStats`: record only while a run is active, or cap at 256 and drop the oldest; no list growth outside runs.
- Bots: one `System.Random` per bot, reseeded per spawn, instead of a new one per spawn.
- DONE: tests; compile.

### S4. Meta progression core (D4) — TDD
- Core `IKeyValueStore` (minimal: get/set int, float, string; has; delete; save) and `MetaProgress` per the design doc section 5 (keys under `hh.meta.`, version 1, version mismatch → reset).
- `OnRunEnded(result, islandReached, runSeconds, aimMode) → MetaChanges` (new unlock bits, new best island, new best time). Unlocks: island 5 reached (any result, including quitting after reaching it) → Second Wind; first Victory → Big Chests; a Victory with the other aim mode than the first Victory → Dividends. Bests never get worse; quit runs count for island reached, never for time.
- `StartingRelicChoices()` → unlocked relic ids in a fixed order. `Reset()` clears only `hh.meta.*` (aim mode and tutorial-seen live in other keys and must survive).
- A dictionary-backed store for tests; a `PlayerPrefs` adapter in HandProto.
- DONE: tests (thresholds, per-mode bests, never worse, quit rules, version reset, reset keeps foreign keys); compile.

### S5. Meta progression wiring (D4, D5)
- `RunStateMachine`: new phase `StartRelic` before island 1's intro when at least one relic is unlocked. Choosing a relic adds it to the inventory (so `ChestRoller` never offers it again); NONE skips. Pause works as in OpenChest. TDD the transitions.
- `RunChoiceMenu`: the starting relic chest uses the existing card UI (unlocked relics + a NONE card), title `STARTING RELIC`; the 0.4 s arm delay applies.
- Main menu: one TextMeshPro line above the grid, `BEST  ISLAND 7  ·  WIN 9:12`, for the current aim mode (updates when AIM toggles); hidden before the first run.
- End panel: under the run summary, `NEW BEST` and/or `UNLOCKED: SECOND WIND START` lines at the existing font size.
- Pause panel: `RESET PROGRESS`, which needs a second press (`CONFIRM RESET`) within a few seconds. No showcase toggle (D5).
- Write meta progress at the same moment as `run_log.jsonl` (end screen / quit) with one `PlayerPrefs.Save()`; never during a fight.
- DONE: tests; compile; scenes rebuilt (0 wiring errors).

### S6. Enemy variety design doc (D7) — no gameplay code
- Write `docs/superpowers/specs/2026-10-09-enemy-variety-design.md` (starts with `## 한국어 요약`; the rest in English).
- Ground it in the code (`BotBrain`, `BotInputSource`, `RunBot`, `RunRules.Island`, `IslandType`, `HeroHealth`, `PointingBeamController`, `ShockwaveController`) and in `docs/crab-champions-systems-analysis.md`.
- Content: what the player currently reads and answers (telegraph line → dodge; one archetype); 4–6 candidate archetypes that each ask the hands-only, seated player for a **different answer** (e.g. a shielded bot that must be flanked or shockwaved, a fast swarmer that rewards the shockwave, a sniper with a long telegraph that forces cover, a support that buffs/heals others, a splitter); 2–3 boss pattern ideas with phases; how each archetype reuses the shared hero/beam/health path (same flight model and rules as the player — ADR fairness) vs needs new code; readability in VR (silhouette, color, sound cue from the P11 synth); how islands mix them (`IslandType`, spawn tables per island depth); a first slice to build (2 archetypes + 1 boss pattern) with a rough task list; risks.
- End with open questions for Hyun as D1… each with a recommended answer.
- DONE: the doc is committed.

### S7. Terrain variety design doc (D8) — no gameplay code
- Write `docs/superpowers/specs/2026-10-09-terrain-variety-design.md` (same format).
- Read how the arena is built today (`HandHeroSceneBuilder`, `ArenaBounds`, `ViewLayout`, `ArenaViewMode`, cover/occlusion in `PointingBeamController` raycasts, `RunDirector` spawn points) and the comfort rules (XR Origin never moves; seated; tabletop scale).
- Content: goals (each island reads differently and changes tactics: cover, lanes, height, hazards); approaches — A: a set of hand-authored layout modules swapped per island by the builder/run (seeded pick), B: procedural placement from rules (grid of cover pillars/walls/platforms with constraints: line-of-sight lanes, spawn safety, flight clearance), C: hybrid; how a layout plugs into the run (portal shows the terrain type?), spawn points, bot navigation (bots fly with the same spring model — do they need avoidance?), beam cover, performance on Quest (static batching, colliders, draw calls), and how it must also work at tabletop scale. Recommend one; first slice; risks; open questions with recommended answers.
- DONE: the doc is committed.

### S8. MR room-scan spike (D9) — capability probe, throwaway-safe
- Question to answer: in this project (Meta OpenXR 2.6.1, AR Foundation 6.6.2, already installed), can MR TABLE mode get the player's room layout — planes (floor, walls, table, couch…), bounding boxes (furniture with classifications) and/or the room mesh — and how fast, so a later round can generate island terrain from it?
- Probe, kept small and isolated: a `RoomScanProbe` component that only runs in MR TABLE mode, enables `ARPlaneManager` / `ARBoundingBoxManager` (and `ARMeshManager` if cheap) when the matching Meta OpenXR features are on, and writes to `perf_log.txt` (via `PerfSpikeLogger.Mark`) and the Unity log: whether each subsystem started, time to first result, counts and classifications, sizes, and the largest horizontal surface near the table position. A debug wireframe of what it found, toggled by a serialized flag (default on in dev builds, off in release).
- Settings: enabling the Meta OpenXR Planes / Bounding Boxes (/ Meshing) features for Android and the scene permission the feature requires (`com.oculus.permission.USE_SCENE`, asked at runtime only when MR TABLE is chosen) is **approved for this task only**; change them through an editor script, list every changed key in `PROGRESS.md`. Verify each API name in `Library/PackageCache/com.unity.xr.meta-openxr@*/` and `com.unity.xr.arfoundation@*/` before use. If something cannot be done without a new package or a manifest hand edit, log a blocker and keep only the research.
- Must not change VR arena mode or Quick Match/RUN behaviour; if the permission is denied, MR TABLE works exactly as today.
- Add a section to the S7 doc: "Room-based terrain" — what the probe can tell us, how scanned surfaces could become island terrain at tabletop scale (e.g. real table edge = arena edge, furniture boxes → cover blocks), privacy note (scene data stays on device, never logged beyond counts/sizes), and the device steps Hyun needs (Space Setup on Quest).
- DONE: compile; scenes rebuilt; device item "MR TABLE → allow permission → perf_log shows planes/boxes".

### S9. Scenes, tests, APKs
- Rebuild scenes, run all tests, build `BuildQuestApkDev` and `BuildQuestApkRelease` (if the dev APK keeps stale content again, do a clean build); record paths and sizes in `PROGRESS.md`.

### S10. Final review and report
- One code-review subagent over `a3b1dd1..HEAD` (exclude `.unity`): fix every confirmed Critical/Important finding, re-test, record Minor ones.
- Write `AUTO/REPORT_FOR_HYUN.md` (section 6), then `STATUS: ALL_DONE`.

## 5b. Hard rules

1. **The XR Origin never moves or rotates.** No follow camera.
2. Don't delete the 20 legacy scripts in the `Scripts\` root or `Networking~`. Don't touch the scenes in `Assets/Scenes/`.
3. Every gesture threshold uses hysteresis. Every tunable is `[SerializeField]` + `[Tooltip]`. **Don't change existing gameplay tuning defaults** except the S2 balance values.
4. **Quick Match, the tutorial, both aim modes and RUN must keep working.** All existing tests stay green; existing tests may be updated only where a decision above deliberately changes behaviour (say which in the commit).
5. No account creation, login, payments or sign-ups.
6. **No `git push`.** No force commands. Never `git reset --hard` committed work.
7. Don't modify files outside the project folder and `MetaAwards\Build\`.
8. No new packages. `ProjectSettings/` / XR settings changes only as named in S8; list any in `PROGRESS.md`.
9. Never commit a broken compile. If it is unavoidable, mark it `[broken]` and fix it in the next commit.
10. In-game text in English. Reports for Hyun in Korean.
11. No XR/render setting changes (FFR, MSAA, latency mode, depth submission, refresh rate).
12. Build for players, not for a judging booth: no showcase-only toggles or judge-specific flows (Hyun, 2026-10-09).

## 6. Report (`AUTO/REPORT_FOR_HYUN.md`, Korean)

1. Task table (done / blocked / not started).
2. Headset checklist by priority:
   - first 30 s of a RUN smooth (logcat command in `AUTO/archive/2026-10-09/PERF_REPORT.md` section 3, plus the hitch counts in `perf_log.txt`)
   - run length and difficulty (`python AUTO\tools\run_summary.py <run_log.jsonl>`)
   - starting relic chest after reaching island 5, the best line on the menu, NEW BEST / UNLOCKED on the end panel, RESET PROGRESS
   - revive and the DEFEAT screen (not tried in round 3)
   - MR TABLE room-scan probe (Space Setup first, allow the permission, what perf_log and the wireframe show; MR TABLE still works if denied)
   - regressions: Quick Match, tutorial, both aim modes
3. Decisions taken for Hyun (summary of `QUESTIONS_FOR_HYUN.md`), each reversible.
4. Changed values and how to undo each.
5. The open questions of the enemy and terrain design docs (with recommended answers), so Hyun can answer them in one message.
6. Three next steps, game-first.
