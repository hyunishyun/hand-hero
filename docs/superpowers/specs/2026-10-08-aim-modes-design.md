# Aim Modes (ASSIST / CURSOR) + Pinch-Hold Charge — Design

## 한국어 요약

**목표**
- 오른손 조준이 어렵고 앞뒤(깊이) 조절이 안 되는 문제를 해결한다.
- 조준 방식 두 가지를 메뉴에서 고를 수 있게 한다.
  - **ASSIST**(기본): 레이 주변 원뿔 안의 적에게 레티클이 붙는다.
  - **CURSOR**: 오른손 주먹으로 3D 조준 마커를 상하좌우앞뒤로 끈다.
- 차지샷을 오른손 핀치 길게로 옮겨, 왼손으로 움직이면서 차지할 수 있게 한다.

**바뀌는 것 (파일)**
- Core(새 파일): `AimAssist.cs`, `AimCursorModel.cs`, `PinchTrigger.cs`
- Core(수정): `HandInputData.cs` — `PinchHeld`, `AimClutchHeld`, `AimClutchDelta` 추가
- 입력: `XRHandsInputSource.cs`, `DebugKeyboardMouseInputSource.cs`
- 조준·발사: `PointingBeamController.cs`, 새 `AimAssistTarget.cs`, 새 `AimModeSetting.cs`
- 이동: `HandPuppeteerController.cs`(차지 중 제자리 고정 제거), `FlyingCharacter.cs`(차지 감속 채널 추가)
- 표시: `HeroGroundMarker.cs`(임의의 Transform도 따라가게)
- 메뉴·흐름: `MatchDirector.cs`(`ToggleAimMode`), `HandMenuButton`/씬 빌더(AIM 버튼, 2×2 배치)
- 튜토리얼: `TutorialDirector.cs`(모드별 문구, 조준 판정을 "조준점이 타겟에 있나"로 변경)
- 봇: `BotDifficulty`/`BotInputSource`(봇 보정 원뿔 값)
- 테스트: 새 EditMode 테스트 3개 파일

**결정**
- **D1 핀치**: 누르는 즉시 일반 발사. 계속 쥐면 0.3초 뒤부터 차지가 쌓이고, 놓으면 차지샷.
  - 이유: 일반 발사가 늦어지지 않는다. 대안: 놓을 때 발사(반응이 느림).
- **D2 차지 중 이동**: 가능. 대신 최대 속도 60%. 피격 감속과는 곱으로 합쳐진다.
  - 대안: 완전 자유 / 지금처럼 정지.
- **D3 커서 조작**: 오른손 주먹 쥐고 끌기(왼손과 같은 상대 매핑), 놓으면 그 자리에 고정.
- **D4 커서 감도**: 60(왼손과 같음). `cursorPositionScale`로 따로 조절할 수 있다.
- **D5 커서 보정**: 커서 반경 2.5m 안의 적에게 붙고, 3.5m 밖으로 나가야 떨어진다(히스테리시스). 0이면 끈다.
- **D6 브랜치**: `hyun/aim-modes`.
- **D7 메뉴 텍스트 겹침 버그**: 이 브랜치에서 고친다. 원인은 스크린샷을 받은 뒤 별도 디버깅으로 확정한다. 이 문서의 메뉴 변경(2×2 배치)과는 독립이다.
- **D8 ASSIST 원뿔**: 8°에 붙고, 11°를 넘으면 떨어진다. 인스펙터에서 조절한다.
- **D9 두 손 모으기**: 코드는 남기고 기본값만 끈다(`palmsTogetherCharges = false`). 나중에 재활용한다.
- **D10 봇 보정 (Q4에서 바뀜)**: 봇도 같은 코드 경로를 쓴다. 다만 **봇 보정 원뿔의 기본값은 0°(끔)**이다.
  - 이유: 봇은 발사 0.6초 전에 조준점을 고정하고 예고선을 그린다. 발사 순간 보정이 적의 "현재 위치"로 다시 붙으면, 예고선을 보고 피해도 맞게 된다. 그러면 Hyun이 좋다고 한 "예고선 → 왼손으로 피하기" 루프가 깨진다.
  - 봇의 정확도는 기존 조준 오차 4°로 이미 조절되고 있다. 값을 올리고 싶으면 `BotDifficulty.assistAngle`로 바꾼다.
- **D11 모드 저장**: PlayerPrefs `HandHero.AimMode`. 처음 실행하면 ASSIST. 메인 메뉴에서만 바꿀 수 있다.
- **D12 커서 시작 위치**: 매 라운드 시작과 리스폰 때 아레나 중앙으로 돌아간다.

**테스트·검증**
- Core TDD:
  - 원뿔 붙기·떨어지기·가장 가까운 각도 선택
  - 반경 보정의 히스테리시스
  - 커서 상대 매핑·재잡기 시 순간이동 없음·경계 제한
  - 탭 vs 홀드(차지)
  - 주먹 중 핀치 무시
- 기존 117개 테스트 유지 + 새 테스트.
- 배치 컴파일 → EditMode 테스트 → 씬 빌더 → APK 빌드.

**기기에서 확인할 것**
- ASSIST가 너무 쉬운지 어려운지(원뿔 크기)
- 레티클이 붙을 때 표시가 잘 보이는지
- CURSOR에서 오른손 주먹 끌기의 감도와 깊이감(원판)
- **주먹을 쥘 때 오발사가 없는지**
- 차지 중 왼손 이동 느낌(60%)
- 튜토리얼 조준 단계가 두 모드 모두 통과되는지
- 4버튼 메뉴 배치

**위험**
- 오른손 주먹 안에서 엄지·검지가 가까워져 핀치로 읽힐 수 있다. → 주먹이 잡힌 동안과 놓은 뒤 0.15초는 핀치를 무시하는 게이트로 막는다. 기기에서 확인한다.
- 8° 원뿔이 너무 쉬울 수 있다. → 인스펙터로 조절.
- `HandInputData` 필드가 늘어나서 Fusion 이식 문서와 어긋난다. → `AUTO/FUSION_PORT_PLAN.md`에 한 줄 메모로 반영한다.

---

## Context

Headset playtest on 2026-10-08 (Quest Link, `Arena_Main`): clutch flight, tutorial, bot, shockwave,
wrist pause and floor discs all work. Two problems with the right hand:

1. **Aiming is hard and has no depth.** The aim point is the first collider hit by the
   shoulder-through-knuckle ray. Unless the ray crosses the enemy hero's collider exactly, the
   reticle lands on the back wall, so the player cannot choose a depth. An enemy at ~20 m spans
   ~3°, while hand-ray jitter is 1–2°. Also the beam travels from the player's hero to the
   reticle, so a reticle on the wall "behind" the enemy misses from the hero's position (parallax).
2. **Charging immobilizes the player.** The charge shot needs both palms together, so the left
   hand cannot fly the hero while charging (and the puppeteer roots the hero by design).

## Goals

- ASSIST aim mode (default): soft target magnetism on the existing ray.
- CURSOR aim mode: a 3D aim marker the right hand drags with a fist clutch, using the same
  relative mapping as the left-hand puppeteer, with a floor disc + drop line for depth.
- Selectable from the main menu, remembered across runs.
- Charge shot on a held right-hand pinch; the hero can keep flying (slowed) while charging.
- Everything stays hands-only, hysteresis-gated, and testable without a headset.

## Non-goals

- New uses for the palms-together gesture (kept, disabled by default).
- Bot charge shots or bot cursor mode.
- Fusion networking code (only a note in the port plan).
- Eye/head-gaze aiming.

## Design

### 1. Input data (`HandInputData`, Core)

Add three fields:

| Field | Meaning |
|---|---|
| `bool PinchHeld` | Aim-hand pinch gate is closed (level, hysteresis-gated). `FireTriggered` stays the rising edge. |
| `bool AimClutchHeld` | Aim-hand fist clutch is closed. |
| `Vector3 AimClutchDelta` | Aim-hand palm motion since last frame, tracking-space meters, zero when not held. |

Sources fill them; consumers decide by aim mode. The bot source leaves all three false/zero.

### 2. Pinch trigger (`PinchTrigger`, Core, new)

Owns the pinch `HysteresisGate` that today lives in `XRHandsInputSource`, plus the fist
suppression rule:

- `Step(tracked, pinchStrength, fireThreshold, resetThreshold, aimFistHeld, dt) → (FireTriggered, PinchHeld)`
- While `aimFistHeld` is true, and for `fistSuppressTime` (0.15 s) after it opens, the gate is held
  open: no fire edge, `PinchHeld = false`. A pinch already closed when suppression ends must open
  once before it can fire again (same "re-arm" rule as the menu pointer).
- Tracking loss resets the gate.

`XRHandsInputSource` uses `PinchTrigger` and a second `HandClutchSampler` for the aim hand
(same `grabThreshold` / `releaseThreshold` as the left hand). The existing pinch thresholds keep
their names and defaults (0.8 / 0.5).

### 3. Charge on pinch hold

- `XRHandsInputSource` gets `palmsTogetherCharges` (default **false**). When false, the
  palms-together recognizer still steps (state continuity) but never sets `ChargeHeld`.
  The shockwave rules are unchanged.
- `PointingBeamController`: `chargeHeld = input.PinchHeld || input.Has(HandGestures.ChargeHeld)`.
  The pinch's rising edge fires a normal shot (subject to `fireCooldown`); `ChargeShotModel` runs
  on `chargeHeld` with the existing `ChargeParams` (min 0.3 s, max 1.5 s). Releasing before
  0.3 s fires nothing extra (a tap = one normal shot). Releasing after 0.3 s fires the charge shot
  (existing damage 30→70 and width 1.5→4×).
- While charging, the normal-shot block stays (no extra normal shots during a hold).
- **Movement while charging (D2):** `HandPuppeteerController` no longer roots the hero on
  `ChargeHeld`. Instead `FlyingCharacter` gets a second multiplier channel,
  `SetChargeSpeedMultiplier(float)`, combined as `maxSpeed * hitMultiplier * chargeMultiplier`
  (the hit slow from `HeroHealth` keeps its own channel and is still set every frame).
  `PointingBeamController` sets it to `chargeMoveSpeedMultiplier` (0.6) while charging and back
  to 1 on release, cancel, death, or disable.

### 4. ASSIST mode (`AimAssist`, Core, new)

Pure selection logic, no Unity objects beyond math types:

- `SelectByAngle(Ray ray, IReadOnlyList<Vector3> candidates, int current, float acquireDeg, float releaseDeg) → int`
  - Keep `current` while its angle from the ray is ≤ `releaseDeg` and it is in front of the origin.
  - Otherwise pick the candidate with the smallest angle ≤ `acquireDeg`, or −1.
- `SelectByRadius(Vector3 point, IReadOnlyList<Vector3> candidates, int current, float acquireRadius, float releaseRadius) → int`
  - Same rule by 3D distance (used by CURSOR mode, D5). `acquireRadius = 0` disables it.

Scene side:

- `AimAssistTarget` (new MonoBehaviour) on every hero and on tutorial practice targets. A static
  registry lists enabled, alive targets. The `FlyingCharacter` link is optional, so a practice
  target counts as always alive. `PointingBeamController` excludes its own hero.
- `PointingBeamController` in ASSIST:
  - raw aim = existing raycast point;
  - if a target is selected (`assistAngle` 8°, `assistReleaseAngle` 11°), the aim point is the
    target's position (smoothed as today) and the reticle switches to `lockedReticleColor`;
  - the fire ray still goes from the hero to the aim point, so a locked shot at an unobstructed
    target hits.
- Defaults live on the controller; the bot's controller gets `assistAngle` from
  `BotDifficulty.assistAngle` (default **0** = off, D10) through `BotInputSource`.

### 5. CURSOR mode (`AimCursorModel`, Core, new)

- Wraps `ClutchMapper` with a persistent cursor position:
  - `Reset(Vector3 position)`
  - `Step(bool held, Vector3 delta, float positionScale, ArenaBounds bounds) → Vector3 position`
  - While held: `position = bounds.Clamp(mapper target)`. On regrab, the mapper starts from the
    current cursor position (no snap). Released: position stays.
- `PointingBeamController` in CURSOR:
  - drives the model from `AimClutchHeld` / `AimClutchDelta` with `cursorPositionScale` (60);
  - places the `cursorMarker` transform there;
  - aim point = cursor, or the `SelectByRadius` target position when one is within 2.5 m
    (release 3.5 m);
  - the reticle and the shoulder ray visual are hidden, and the marker is tinted
    `lockedReticleColor` while magnetized;
  - the cursor resets to the arena center on round start (`MatchDirector` → controller
    `ResetAim()`) and on the hero's respawn.
- `HeroGroundMarker` gains an optional `follow` Transform. When set, the marker sits under that
  transform and uses `character` only for bounds and visibility. The scene builder adds an orange
  disc + drop line for the cursor (visible only in CURSOR mode, and only while the cursor is
  active).
- Firing, cooldown, charge and pinch suppression are identical in both modes.

### 6. Mode setting and menu

- `AimModeSetting` (new MonoBehaviour, modeled on `ArenaViewMode`):
  - `enum AimMode { Assist, Cursor }`
  - `Mode`, `Toggle()`, `Changed` event
  - PlayerPrefs `HandHero.AimMode`, default Assist
- The player's `PointingBeamController` reads it; the bot's has none and is always Assist.
- `MatchDirector.MenuAction.ToggleAimMode` (main menu only, like `ToggleViewMode`).
- `AimModeSetting` holds the AIM button's label and writes `AIM: ASSIST` / `AIM: CURSOR`, the way
  `ArenaViewMode` writes `MR TABLE` / `VR ARENA` on its button.
- Main panel layout becomes a 2×2 grid of the existing 0.8×0.32 m buttons (0.15 m gaps), centered
  where the row was:
  - `Arena_Main`: START, TUTORIAL / MR TABLE, AIM
  - `HandHero_Sandbox`: START, TUTORIAL / AIM, (empty)

### 7. Tutorial

- `TutorialDirector` computes `AimOnTarget` as "the player controller's current aim point is
  within `targetAimRadius` of the practice target", read from a new
  `PointingBeamController.AimPoint` getter. This is mode-agnostic: ASSIST snaps the point onto
  the target, and CURSOR needs the marker dragged there.
- Prompt text per mode for the Aim step:
  - ASSIST: "Point at the target with your RIGHT hand"
  - CURSOR: "Make a RIGHT fist and drag the orange marker onto the target"
  - Shoot step unchanged.
- `TutorialSequencer` (Core) is unchanged.

### 8. Debug keyboard/mouse

- Left mouse / Space held → `PinchHeld` (press edge still = `FireTriggered`).
- `C` held → `PinchHeld` as well. The old palms-together stand-in is gone, because palms
  charging is off by default.
- **Left Ctrl + right-drag / WASDQE / wheel** → `AimClutchHeld` + `AimClutchDelta` instead of the
  hero clutch.
- Main-menu AIM button is clickable with the mouse like the others. Debug key **M** toggles the
  aim mode in the menu.

### 9. Docs

- `AUTO/FUSION_PORT_PLAN.md`: one note that `HeroNetInput` must carry `PinchHeld`,
  `AimClutchHeld` and the absolute aim-hand palm position (same absolute-position rule as the
  left clutch).
- Debug key table in `AUTO/REPORT_FOR_HYUN.md`.

## Error handling / degradation

- Aim hand lost: no fire, charge cancels if the pinch was the charge source (gate reset), aim
  clutch opens (cursor stays), reticle freezes (ASSIST) as today.
- Selected assist target dies or is disabled: selection drops on the next step.
- Hero dies while charging: charge cancels and the speed multiplier returns to 1.

## Testing

EditMode (new):

- `AimAssistTests`
  - acquires within 8°, not at 9°; keeps until 11°, drops at 12°
  - picks the smallest angle among several candidates
  - ignores candidates behind the origin
  - radius variant with hysteresis; radius 0 disables
- `AimCursorModelTests`
  - relative drag × scale
  - release keeps the position
  - regrab after the hand moved does not jump
  - clamps to bounds
  - `Reset` moves the cursor
- `PinchTriggerTests`
  - one fire edge per pinch
  - `PinchHeld` level follows the gate
  - fist held suppresses fire and hold
  - 0.15 s post-fist suppression, and a pinch closed during it must re-open before firing
  - tracking loss resets

Existing 117 tests must still pass. `ChargeShotModel` tests are unchanged, because the model
itself is unchanged.

Off-device verification: batch compile → EditMode tests → `HandHeroSceneBuilder.BuildAll` →
`BuildQuestApk`. Wiring (modes, menu, tutorial, debug keys) is checked on device / in the
editor, per Hyun's TDD exceptions.
