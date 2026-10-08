# Aim Lock Feedback + Smaller Assist + CURSOR Index Trigger Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

## 한국어 요약

**목표**
- 보정 범위를 절반으로 줄인다.
- 적에게 붙으면 빨간 락온 링(팝 애니메이션)을 띄운다.
- CURSOR 모드는 세 손가락 쥐기로 마커를 끌고 검지 방아쇠로 쏜다.

**바뀌는 것 (파일)**
- Core: 새 `TriggerGesture` + 테스트, `HandInputData`
- 입력: `HandGestureTracker`, 손·디버그 입력 소스
- 조준·표시: `PointingBeamController`, 새 `LockOnRing`
- 기타: `TutorialDirector`, 씬 빌더

**결정**
- spec `docs/superpowers/specs/2026-10-08-aim-feedback-trigger-design.md`의 D1–D9를 그대로 따른다.
- 계획에서 새로 정한 결정은 없다.

**테스트·검증**
- Core TDD 8개 + 기존 149개 = 157개
- 배치 컴파일 → `BuildAll` → APK
- **실행 중에는 Unity를 닫아 둔다.**

**기기에서 확인할 것**
- spec 요약의 체크리스트와 같다.

**위험**
- 손을 크게 움직일 때 방아쇠가 가려짐
- 0.15초 억제로 첫 발이 한 박자 늦음

---

**Goal:** Smaller assist, a visible lock-on ring, and a 3-finger grip + index trigger in CURSOR mode.

**Architecture:** The trigger rule is pure logic in `HandHero.Core` (`TriggerGesture`, TDD). The tracker exposes grip and index curl. The XR source fills new `TriggerFired` / `TriggerHeld` fields and drags the CURSOR marker with the grip. `PointingBeamController` picks the fire input per mode and drives a presentation-only `LockOnRing`.

**Tech Stack:** Unity 6000.6.4f1, C#, XR Hands 1.9, NUnit EditMode.

**Spec:** `docs/superpowers/specs/2026-10-08-aim-feedback-trigger-design.md`

## Global Constraints

**Values**
- Trigger: pull 0.7 / release 0.45 on `IndexCurl`.
- Grip: on 0.7 / off 0.45 on `GripStrength`. The CURSOR clutch reuses `grabThreshold` 0.7 / `releaseThreshold` 0.45.
- `gripSettleTime` 0.15 s.
- Assist defaults: `assistAngle` 4, `assistReleaseAngle` 6, `cursorAssistRadius` 1.2, `cursorAssistReleaseRadius` 1.8. The bot's `assistAngle` stays 0.
- Lock-on ring:
  - `minRadius` 1.2 m, `angularSize` 2.5°, width 0.06 × the distance factor
  - color (1, 0.15, 0.15), 48 segments
  - pop 1.6 → 1.0 over 0.15 s, unscaled time
- Tutorial strings in CURSOR:
  - Aim: `Grip with your RIGHT middle, ring and little fingers and drag the orange marker onto the target`
  - Shoot: `Pull your RIGHT index finger to shoot`

**Hard rules**
- Hysteresis on every gesture threshold. Tunables are `[SerializeField]` + `[Tooltip]`.
- Never hand-edit `.unity` files; scenes come from `HandHeroSceneBuilder.BuildAll`.
- The XR Origin is never moved.
- English comments. Commits `Area: what changed` + `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Commands as in `docs/superpowers/plans/2026-10-08-aim-modes.md` (compile_check `-Tests` / `-ExecuteMethod` / APK). Unity must be closed.
- Building the APK regenerates the scenes (new file IDs); commit them with the build.
- Revert `ProjectSettings/UnityConnectSettings.asset` before committing.

## Review Focus

1. **Full fist from an open hand** (grip and index close in the same few frames). Expected: no shot, and no shot until the index opens and pulls again. Owner: Task 1, `FullFist_GripAndIndexTogether_DoesNotFire`.
2. **Relaxing the grip while charging with the trigger.** Expected: the charge is not released early. Owner: Task 1, `GripRelease_DuringHold_SettleBlocksNewEdgeOnly`.
3. **Switching mode in the menu while a target was locked.** Expected: the ring hides (the lock is cleared in `ApplyModeVisuals`). Owner: Task 3, Step 2.
4. **The locked target dies or the hero dies.** Expected: the ring hides that frame. Owner: Task 3, Step 2.
5. **ASSIST reticle when the snap releases.** Expected: no visible jump, because the reticle always shows the raw point. Owner: Task 3, Step 2.

---

### Task 1: `TriggerGesture` (Core) + input fields

**Files:**
- Create: `Assets/MyAssets/Scripts/Core/TriggerGesture.cs`
- Modify: `Assets/MyAssets/Scripts/Core/HandInputData.cs` (after `PinchHeld`)
- Test: `Assets/MyAssets/Tests/EditMode/TriggerGestureTests.cs`

**Interfaces:**
- Produces:
  - `public struct TriggerState { public bool Fired; public bool Held; }`
  - `public class TriggerGesture { TriggerState Step(bool tracked, float indexCurl, float gripStrength, float pullThreshold, float releaseThreshold, float gripOnThreshold, float gripOffThreshold, float gripSettleTime, float dt); void RequireReopen(); }`
  - `HandInputData.TriggerFired`, `HandInputData.TriggerHeld`

- [ ] **Step 1: Write the failing tests.**
  Constants: Pull 0.7, Rel 0.45, GripOn 0.7, GripOff 0.45, Settle 0.15, Dt 0.02. Helper `Step(index, grip, tracked = true)`.
  - `Pull_FiresOnce_HeldWhilePulled`: grip 0 throughout; index 0, then 1, 1, 1 → fires only on the first pull; `Held` true on all three.
  - `ReleaseBelow_RearmsNextPull`: 1, 0.3, 1 → fires twice.
  - `GunGripThenPull_Fires`: grip 1 with index 0 for 10 steps (0.2 s), then index 1 → fires.
  - `FullFist_GripAndIndexTogether_DoesNotFire`: grip 0, index 0 for one step; then grip 1, index 1 for 20 steps → never fires. Then index 0.3, then index 1 → fires.
  - `PullWithoutGrip_Fires`: grip 0, index 0 then 1 → fires.
  - `GripRelease_DuringHold_SettleBlocksNewEdgeOnly`: settled gun grip, pull (fires). Then grip drops to 0 for 5 steps with index 1 → `Held` stays true, no new fire.
  - `TrackingLoss_RequiresReopen`: pull (fires); untracked; back at index 1 → not fired, not held; 0.3 then 1 → fires.
  - `RequireReopen_HeldTriggerDoesNotFire`: `RequireReopen()`, index 1 → nothing; 0.3, 1 → fires.

- [ ] **Step 2: Run `-Tests` — expect `COMPILE_ERRORS` naming `TriggerGesture`.** Then add an empty stub so it compiles, re-run, and expect the 8 new tests to fail.

- [ ] **Step 3: Implement.** State:
  - `HysteresisGate _grip`, `HysteresisGate _index`, `float _settle`, `bool _mustReopen`.
  - Untracked → `RequireReopen()`, `_grip.Reset()`, `_settle = 0`, return default.
  - A grip edge (≠ None) → `_settle = gripSettleTime`; else `_settle -= dt` while it is > 0.
  - While `_mustReopen`: clear it when `indexCurl <= releaseThreshold`, otherwise return default.
  - `edge = _index.Step(indexCurl, pull, release)`.
  - If `edge == Rising && _settle > 0`: `_index.Reset()`, `_mustReopen = true`, return default.
  - Return `{ Fired = edge == Rising, Held = _index.IsOn }`.
  - `RequireReopen()` = `_index.Reset(); _mustReopen = true;`.
  - Add the two `HandInputData` fields, with a comment: "CURSOR aim fire: index-finger trigger (edge) and its hold (charge)."

- [ ] **Step 4: Commit, then run `task-done` with `-Tests`** — expect `total=157 passed=157`.
  Message: `Core: TriggerGesture index trigger with grip-settle full-fist rejection`

### Task 2: Tracker values + input sources

**Files:**
- Modify: `Assets/MyAssets/Scripts/HandProto/HandGestureTracker.cs`
  - `HandState` gets `GripStrength` and `IndexCurl`
  - computed in the fist block (lines ~171-186) from the same tip loop
- Modify: `Assets/MyAssets/Scripts/HandProto/Input/XRHandsInputSource.cs`
- Modify: `Assets/MyAssets/Scripts/HandProto/Input/DebugKeyboardMouseInputSource.cs`

**Interfaces:**
- Consumes: `TriggerGesture` (Task 1).
- Produces:
  - `HandGestureTracker.HandState.GripStrength`, `.IndexCurl` (0..1, smoothed with `valueSmoothing`)
  - sources fill `TriggerFired` / `TriggerHeld`
  - the XR source's `AimClutchHeld` comes from `GripStrength`

TDD exception (input wiring). Verification = compile + tests.

- [ ] **Step 1: Tracker.** In the tip loop, keep each tip's distance.
  - Index raw curl = `InverseLerp(open, closed, indexDist)`.
  - Grip = the same mapping on the average of middle, ring and little.
  - Smooth both with `valT`, like `FistStrength`.
  - Update the header comment list.

- [ ] **Step 2: XR source.**
  - `_aimClutch.Step(aimHand.IsTracked, aimHand.GripStrength, …)` instead of `FistStrength`.
  - Add `TriggerGesture _trigger`.
  - Fields with tooltips: `triggerPullThreshold` 0.7, `triggerReleaseThreshold` 0.45, `gripSettleTime` 0.15.
  - Each frame (before the tracking early-return): step it with `grabThreshold` / `releaseThreshold` as the grip thresholds, and set `data.TriggerFired` / `TriggerHeld`.
  - `OnEnable` also calls `_trigger.RequireReopen()`.

- [ ] **Step 3: Debug source.** `TriggerFired = FireTriggered`, `TriggerHeld = PinchHeld`, set after the existing pinch block.

- [ ] **Step 4: Commit + `task-done` `-Tests`** (157 OK).
  Message: `Input: grip strength and index curl, CURSOR drag on a 3-finger grip, index trigger`

### Task 3: Controller — per-mode fire, defaults, raw reticle, lock-on ring

**Files:**
- Create: `Assets/MyAssets/Scripts/HandProto/LockOnRing.cs`
- Modify: `Assets/MyAssets/Scripts/HandProto/PointingBeamController.cs`

**Interfaces:**
- Produces: `public class LockOnRing : MonoBehaviour { public void Show(Transform target); public void Hide(); }`
  - fields `minRadius` 1.2, `angularSize` 2.5, `width` 0.06, `color` (1, 0.15, 0.15), `popScale` 1.6, `popTime` 0.15, `LineRenderer line`
- `PointingBeamController`: new `[SerializeField] LockOnRing lockOnRing`; `lockedReticleColor` removed.

TDD exception (presentation + MonoBehaviour wiring over tested Core). Verification = compile + tests + device.

- [ ] **Step 1: `LockOnRing`.**
  - `LateUpdate`: if a target is set, place the ring at the target position facing `Camera.main`.
    - `r = max(minRadius, dist * tan(angularSize°))`
    - pop factor = `Lerp(popScale, 1, t / popTime)` on unscaled time
    - set 48 local points of radius `r × pop`, and line width `width × max(1, dist / 10)`
  - `Show` restarts the pop only when the target changes.
  - `Hide` disables the line and clears the target.
  - `OnDisable` → `Hide()`.

- [ ] **Step 2: Controller.**
  - Default changes: 4 / 6 / 1.2 / 1.8 (field initializers + tooltips unchanged).
  - In Cursor: build `fireInput = input` with `FireTriggered = input.TriggerFired` and `PinchHeld = input.TriggerHeld`. Use `fireInput` for `ChargeInputRule.Decide` and the normal-fire check. Assist uses `input` unchanged.
  - `UpdateAim`:
    - store `_rawAimPoint` (raycast / far point, smoothed with the existing `reticleSmoothing`)
    - place the reticle at `_rawAimPoint` (scale by its distance)
    - `_aimPoint` = snapped target or `_rawAimPoint`
    - reticle tint stays the idle color: delete the `Tint(reticleRenderer, …)` call and `lockedReticleColor`
  - `UpdateCursor`: marker tint is always `cursorColor`.
  - After each selection (both modes): `if (lockOnRing) { if (_assistTarget) lockOnRing.Show(_assistTarget.transform); else lockOnRing.Hide(); }`.
  - Also hide in `ApplyModeVisuals` on a mode change, in `ResetAim`, and in `OnDisable`.

- [ ] **Step 3: Commit + `task-done` `-Tests`** (157 OK).
  Message: `Aim: lock-on ring, raw ASSIST reticle, halved assist, CURSOR fires with the index trigger`

### Task 4: Tutorial, scene builder, scenes, APK

**Files:**
- Modify: `Assets/MyAssets/Scripts/HandProto/Match/TutorialDirector.cs` (Aim/Shoot prompts)
- Modify: `Assets/MyAssets/Editor/HandHeroSceneBuilder.cs` (player ring)
- Regenerated: both scenes

- [ ] **Step 1: Tutorial.** For CURSOR mode, use the two exact strings from Global Constraints. ASSIST is unchanged.

- [ ] **Step 2: Builder.** After the player controller wiring:
  - create `LockOnRing` GameObject `LockOnRing` with a `LineRenderer` (`beamMat`, `useWorldSpace = false`, `loop = true`, disabled)
  - `SetRefs(ring, ("line", lr))`
  - `SetRefs(pointing, ("lockOnRing", ring))`

- [ ] **Step 3: Verify.**
  - `-Tests` → OK 157.
  - `-ExecuteMethod …BuildAll` → OK, and `execute.log` has no `[HandHeroSceneBuilder] … has no field`.
  - `grep "assistAngle: 4" Arena_Main.unity` → 1 match (player); `assistAngle: 0` → 1 match (bot).

- [ ] **Step 4: APK build** → `RESULT: OK`. Revert UnityConnect. Commit the builder, tutorial and scenes.
  Message: `Scenes: lock-on ring for the player, CURSOR trigger tutorial prompts`

- [ ] **Step 5: Final whole-branch review** of the range `b281882..HEAD` (fresh reviewer, most capable model), then the Korean device checklist in chat.
