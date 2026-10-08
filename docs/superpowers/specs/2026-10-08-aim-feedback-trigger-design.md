# Aim Lock Feedback, Smaller Assist, Index Trigger (CURSOR) — Design

## 한국어 요약

**목표 (2026-10-08 기기 테스트 피드백)**
1. 조준 보정 범위가 너무 커서 쉽다 → 절반으로 줄인다.
2. 적에게 붙었을 때 아무 표시가 없어 헷갈린다 → **락온 링**을 띄운다.
3. CURSOR 모드에서 이동하면서 쏘고 싶다 → 마커 끌기는 **세 손가락(중지·약지·새끼) 쥐기**, 발사는 **검지 방아쇠**로 바꾼다.

**바뀌는 것 (파일)**
- Core: 새 `TriggerGesture.cs`(검지 방아쇠 규칙)와 테스트. `HandInputData`에 `TriggerFired`·`TriggerHeld` 추가.
- `HandGestureTracker`: `GripStrength`(세 손가락)와 `IndexCurl`을 추가로 계산.
- `XRHandsInputSource`: CURSOR 마커 끌기를 세 손가락 쥐기로 바꾸고 방아쇠를 채운다.
- `DebugKeyboardMouseInputSource`: 방아쇠도 같은 키로 채운다.
- `PointingBeamController`: 모드별 발사 입력 선택, 보정 기본값 변경, 락온 링, ASSIST 레티클이 적 몸 안에 숨지 않게.
- 새 `LockOnRing.cs`(표시 전용).
- `TutorialDirector`: CURSOR 문구.
- 씬 빌더: 링 배선, 값 변경.

**결정**
- **D1 보정 크기**: ASSIST 4°/6°(전 8°/11°), CURSOR 1.2m/1.8m(전 2.5m/3.5m). 인스펙터로 조절한다.
- **D2 락온 표시**:
  - 붙은 적 주위에 빨간 링이 카메라를 향해 뜬다. 거리와 상관없이 크기가 보이게 한다.
  - ASSIST 레티클은 손이 가리키는 원래 위치에 남는다. 적 몸 안으로 들어가지 않는다.
  - 발사 조준점은 여전히 적이다.
- **D3 진입 표시**: 붙는 순간 링이 1.6배에서 1배로 0.15초 동안 줄어드는 "팝" 애니메이션.
- **D4 방아쇠 적용 범위**: CURSOR 모드에서만. ASSIST는 핀치 그대로. CURSOR에서는 핀치를 쓰지 않는다(이중 발사 방지).
- **D5 쥐기 정의**: CURSOR 마커 끌기 = 세 손가락 쥐기(GRIP, 0.7/0.45). 방아쇠 = 검지 굽힘(0.7/0.45).
  - 측정값: 총 쥔 자세 GRIP 1.00 / TRIGGER 0.00, 당김 GRIP 1.00 / TRIGGER 1.00.
- **D6 쥐지 않아도 방아쇠 발사**: 가능.
- **D7 주먹 전체 오발사 방지**: 세 손가락 쥐기 상태가 0.15초 안에 바뀌었으면 검지 굽힘을 발사로 치지 않는다. 이미 굽힌 검지는 한 번 펴야 다시 쏠 수 있다.
- **D8 방아쇠 = 핀치와 같은 규칙**: 당기는 순간 발사, 계속 당기고 있으면 차지, 놓으면 차지샷. 추적이 끊기거나 재개될 때는 다시 펴야 한다.
- **D9 브랜치**: `hyun/aim-modes`에 이어서 작업한다.

**테스트·검증**
- Core TDD로 `TriggerGesture`를 검증한다:
  - 한 번 당김에 한 발, 홀드
  - 쥐기 변화 직후 무시(주먹 전체), 펴야 재무장
  - 추적 손실
- 락온 링·레티클은 표시라서 컴파일과 기기 체크로 확인한다.
- 그 뒤 배치 테스트 → 씬 빌드 → APK.

**기기에서 확인할 것**
- 4°/1.2m 난이도
- 락온 링이 잘 보이는지, 팝 애니메이션이 이해되는지
- CURSOR에서 끌면서 검지로 연사·차지가 되는지
- **주먹 전체를 쥘 때 오발사가 없는지**
- 손을 움직일 때 방아쇠 오발사·누락이 없는지(측정에서 확인 못 한 부분)

**위험**
- 손을 크게 움직이거나 손목을 꺾을 때 검지가 가려지면 방아쇠가 오발사하거나 누락될 수 있다.
- 0.15초 억제 때문에, 쥐면서 바로 쏘는 빠른 동작이 한 박자 늦을 수 있다.

---

## Context

Device test of `hyun/aim-modes` (2026-10-08):
- ASSIST 8° and CURSOR 2.5 m make aiming too easy.
- When the assist snaps, nothing visible tells the player. In ASSIST the reticle moves to the
  target's center and hides inside the hero mesh. In CURSOR the locked tint (1, 0.55, 0.1) is
  nearly the cursor's own orange (1, 0.6, 0.15).
- In CURSOR, the right fist drags the marker and a pinch fires. Pinching means opening the fist,
  so the marker stops while shooting. Hyun proposed a gun grip: three fingers hold the marker,
  the index finger is the trigger.

Spike (throwaway probe, right hand, Quest 3 over Link). Curl uses the fist mapping, tip-to-palm
0.10 m = 0, 0.05 m = 1:

| Pose | Index | Middle / Ring / Little | Grip (M+R+L avg) | Trigger | Thumb–index |
|---|---|---|---|---|---|
| Open hand | 0.00 | 0.00 / 0.00 / 0.09 | 0.03 | 0.00 | — |
| Gun grip, index out | 10.9 cm → 0.00 | 2.3 / 2.3 / 3.4 cm → 1.00 | 1.00 | 0.00 | 10.3 cm |
| Gun grip, index curled | 3.6 cm → 1.00 | 2.4 / 2.2 / 3.1 cm → 1.00 | 1.00 | 1.00 | 5.5 cm |

Conclusions:
- The grip and the index are independent and saturate at 0/1, so the 0.7/0.45 hysteresis
  separates them with a large margin.
- A trigger pull leaves thumb–index at 5.5 cm, well clear of the pinch range.
- Not measured: stability while the hand moves fast. This is a device check.

## Goals

- Smaller assist defaults.
- A visible, unambiguous lock-on indicator in both modes.
- CURSOR: drag the marker with a 3-finger grip and fire / charge with the index trigger at the
  same time.
- A full fist must not fire.

## Non-goals

- Changing ASSIST input (the pinch stays).
- Bot behaviour (assist stays 0).
- Audio for lock-on (no clips yet).

## Design

### 1. Tracker values (`HandGestureTracker.HandState`)

Add two values, smoothed like `FistStrength` with the same `fingerOpenDistance` (0.10) /
`fingerClosedDistance` (0.05) mapping:

- `GripStrength` — average curl of the middle, ring and little fingertips.
- `IndexCurl` — curl of the index fingertip.

`FistStrength` is unchanged: the left-hand clutch and the pinch fist-block keep using it.

### 2. Trigger gesture (`TriggerGesture`, Core, new)

```
public struct TriggerState { public bool Fired; public bool Held; }
public class TriggerGesture
{
    public TriggerState Step(bool tracked, float indexCurl, float gripStrength,
        float pullThreshold, float releaseThreshold,       // 0.7 / 0.45 on indexCurl
        float gripOnThreshold, float gripOffThreshold,     // 0.7 / 0.45 on gripStrength
        float gripSettleTime, float dt);                   // 0.15 s
    public void RequireReopen();
}
```

- Tracks the grip state with its own `HysteresisGate` (0.7 / 0.45). Each grip edge (on or off)
  restarts a `gripSettleTime` timer.
- While the timer runs, a NEW index rising edge is ignored, and that curled index must re-open
  (index curl ≤ release) before it can fire. This is the full-fist rule: grip and index close
  together, so the index edge lands inside the settle window.
- An index already held when the grip changes stays held. Relaxing the grip mid-charge must not
  release the charge shot early.
- Otherwise the index `HysteresisGate` gives `Fired` on its rising edge and `Held = IsOn`.
- Tracking loss → `RequireReopen()`, the same rule as `PinchTrigger`.
- The grip state is NOT required to fire (D6).

### 3. Input data and sources

- `HandInputData` adds `bool TriggerFired` and `bool TriggerHeld`.
- `XRHandsInputSource`:
  - `AimClutchHeld` / `AimClutchDelta` now sample from `GripStrength` instead of
    `FistStrength`, with the same `grabThreshold` / `releaseThreshold`. The 3-finger grip
    drags the marker.
  - New `TriggerGesture`, stepped every frame from the aim hand. Fields: `triggerPullThreshold`
    0.7, `triggerReleaseThreshold` 0.45, `gripSettleTime` 0.15, each with a tooltip.
  - `OnEnable` also calls `_trigger.RequireReopen()`.
  - Pinch handling is unchanged. It still blocks on the aim-hand fist, so a gun grip (fist
    strength high) never pinches.
- `DebugKeyboardMouseInputSource`: `TriggerFired` / `TriggerHeld` mirror the pinch keys (left
  click / Space / C), so both modes fire with the same keys.

### 4. Fire input per mode (`PointingBeamController`)

- ASSIST: fire = `FireTriggered`, hold = `PinchHeld` (unchanged).
- CURSOR: fire = `TriggerFired`, hold = `TriggerHeld`. Pinch is ignored.
- `ChargeInputRule.Decide` keeps its rules. The controller passes it a copy of the input whose
  `PinchHeld` / `FireTriggered` are replaced by the trigger values in CURSOR mode, so cancel-on-
  aim-loss and pause behave the same for both.

### 5. Lock-on feedback (`LockOnRing`, new, presentation only)

- A `LineRenderer` circle (48 segments) that faces the main camera every `LateUpdate`.
- `Show(Transform target)` / `Hide()`.
- Radius = `max(minRadius 1.2 m, angularSize 2.5° × distance to camera)`, so it reads at any
  distance.
- Color red `(1, 0.15, 0.15)`, width 0.06 × the distance factor.
- On a new target it plays a pop: scale 1.6 → 1.0 over `popTime` 0.15 s (unscaled time).
- `PointingBeamController` gets `[SerializeField] LockOnRing lockOnRing`. It calls
  `Show(_assistTarget.transform)` when a target is picked (either mode) and `Hide()` otherwise.
- ASSIST reticle:
  - The reticle is placed at the raw aim point: the raycast hit or the far point, ignoring the
    snap. It no longer jumps into the hero.
  - The fire aim point (`AimPoint`) is still the snapped target.
  - The reticle keeps its idle color, because the ring now carries the lock.
- CURSOR marker: stays orange and stays where the hand put it. The ring carries the lock.
- `lockedReticleColor` is no longer used and is removed.

### 6. Defaults

| Field | Old | New |
|---|---|---|
| `assistAngle` | 8 | 4 |
| `assistReleaseAngle` | 11 | 6 |
| `cursorAssistRadius` | 2.5 | 1.2 |
| `cursorAssistReleaseRadius` | 3.5 | 1.8 |

Set in the field initializers. The scene builder doesn't override them; the bot keeps
`assistAngle` 0.

### 7. Tutorial (CURSOR prompts)

- Aim: `Grip with your RIGHT middle, ring and little fingers and drag the orange marker onto the target`
- Shoot: `Pull your RIGHT index finger to shoot`
- ASSIST prompts are unchanged.

### 8. Scene builder

- Create one `LockOnRing` GameObject (LineRenderer with `beamMat`) for the player and wire it to
  the player's controller. The bot gets none.

## Error handling

- Target dies or drops out of range → `Hide()` on the same frame the selection clears.
- A disabled controller hides its ring.
- Pause: the ring keeps its last state (time frozen), which is acceptable.

## Testing

EditMode (new `TriggerGestureTests`):
- `Pull_FiresOnce_HeldWhilePulled`
- `ReleaseBelow_RearmsNextPull`
- `GunGripThenPull_Fires` (grip settled > 0.15 s, then index 0 → 1)
- `FullFist_GripAndIndexTogether_DoesNotFire`, and stays silent until the index opens and pulls again
- `PullWithoutGrip_Fires` (D6)
- `GripRelease_DuringHold_SettleBlocksNewEdgeOnly` (releasing the grip while pulled does not fire again)
- `TrackingLoss_RequiresReopen`
- `RequireReopen_HeldTriggerDoesNotFire`

Existing tests must still pass (149).

Device checklist: in the Korean summary.
