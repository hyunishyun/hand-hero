# FUSION_PORT_PLAN — Photon Fusion 2 이식 설계 (T11, 코드 없음)

> 작성: 2026-10-07 무인 세션 6. Fusion SDK가 프로젝트에 없어서 **컴파일로 확인한 API는 하나도 없다.**
> 아래 Fusion API 이름은 `Networking~/`의 기존 스크립트(Fusion 2 기준으로 작성됨)에서 쓰던 것만 썼고,
> 처음 등장하는 것은 **[SDK 확인]** 표시를 붙였다. SDK 임포트 후 컴파일러가 맞는지 알려 준다.

## 0. 한 줄 결론

- **싱글(봇전)은 지금 코드 그대로 출품한다.** 심사위원은 혼자 테스트하므로 Fusion은 보너스(멀티)다.
- 이식은 **"Core는 그대로, HandProto의 MonoBehaviour 4개만 NetworkBehaviour 쌍둥이를 만든다"**.
  `SpringFlightModel`·`ClutchMapper`·`HeroHealthModel`·`ChargeShotModel`·`BotBrain`·`MatchStateMachine`은
  이미 엔진·네트워크와 분리된 순수 로직이라 `FixedUpdateNetwork` 안에서 그대로 호출한다.
- 핵심 변경은 **입력**이다: 프레임 단위 "델타·한 프레임 이벤트"를 틱 단위 "절대값·버튼·시퀀스 번호"로 바꾼다
  (Fusion은 빠진 입력을 이전 입력으로 반복하므로, 델타를 보내면 두 번 적용된다).
- Host가 봇을 시뮬레이션하므로 **혼자 Host를 열어도 "봇이 채운 멀티 아레나"**, 두 번째 사람이 들어오면 봇 자리를 넘겨받는다.

---

## 1. 현재 구조 요약 (이식 대상)

| 층 | 파일 | 네트워크에서의 역할 |
|---|---|---|
| Core (순수 로직, 참조 없음) | `HandInputData`, `SpringFlightModel`, `ClutchMapper`, `HandClutchSampler`, `HeroHealthModel`, `ChargeShotModel`, `BotBrain`, `MatchStateMachine`, `GestureRecognizers`, `TutorialSequencer`, `ViewLayout`, `WristMenuGesture` | 그대로 재사용. Fusion을 참조하지 않게 유지(테스트 유지). |
| 입력 소스 (로컬) | `XRHandsInputSource`, `DebugKeyboardMouseInputSource`, `BotInputSource` | 로컬 전용. `OnInput`에서 네트워크 입력으로 포장. 봇은 Host 전용. |
| 시뮬레이션 (MonoBehaviour, `Update`) | `FlyingCharacter`, `HandPuppeteerController`, `PointingBeamController`, `ShockwaveController`, `HeroHealth` | **Networked 쌍둥이**가 필요한 부분. `Update`+`Time.deltaTime`/`Time.time` → `FixedUpdateNetwork`+`Runner.DeltaTime`/`TickTimer`. |
| 매치·UI | `MatchDirector`, `MatchHud`, `HandMenu*`, `WristMenu`, `TutorialDirector`, `ArenaViewMode` | 매치 규칙만 Host로. 메뉴·튜토리얼·뷰 모드는 로컬. |

---

## 2. 입력: `HandInputData` → `HeroNetInput : INetworkInput`

### 2.1 무엇이 문제인가

| 현재 필드 | 문제 | 네트워크 필드 |
|---|---|---|
| `ClutchDelta` (프레임 델타) | 렌더 프레임(72–120Hz)과 틱(권장 60Hz)이 다르고, 입력이 빠지면 Fusion이 **직전 입력을 반복** → 델타가 두 번 적용돼 히어로가 튄다. | `ClutchHandPos` (좌석 로컬 손바닥 절대 위치, 미터). 델타는 시뮬레이션이 `[Networked] LastClutchHandPos`와의 차이로 계산. 반복 입력이면 차이 0 → 안전. |
| `ClutchHeld` | 문제없음 | `Buttons` 비트 `Clutch` |
| `FireTriggered` (한 프레임 엣지) | 틱 사이에 낀 엣지가 사라질 수 있음. | `Buttons` 비트 `Pinch` = 히스테리시스 거친 **핀치 유지 상태**. 엣지는 `input.Buttons.GetPressed(PreviousButtons)`로 시뮬레이션이 검출(기존 `NetworkedGunController`와 같은 방식). |
| `Gestures.Shockwave` (한 프레임 트리거) | 같은 문제 | `byte ShockwaveSeq` — 발동할 때마다 1 증가. 시뮬레이션은 `[Networked] LastShockwaveSeq`와 다르면 발동. 반복·누락에 강함. |
| `Gestures.ChargeHeld` | 유지 상태라 문제없음 | `Buttons` 비트 `Charge` |
| `HasAim` / `AimOrigin` / `AimDirection` | 서버는 플레이어 눈앞 레티클을 모름. 레티클 점은 로컬 레이캐스트+스무딩 결과. | `AimPoint` (월드, 스무딩된 레티클 위치) + `Buttons` 비트 `HasAim`. 빔은 원래도 **히어로 → 조준점**으로 쏘므로 서버에 필요한 건 조준점뿐. |
| (없음) | 원격 아바타 | `HeadPos/Rot`, `LeftPos/Rot`, `RightPos/Rot` (좌석 로컬) — 기존 `BikeInputData`의 리그 포즈와 같음. |

```csharp
// Sketch only — lives in a Fusion-referencing assembly, not in HandHero.Core.
public enum HeroButtons { Clutch = 0, Pinch = 1, Charge = 2, HasAim = 3 }

public struct HeroNetInput : INetworkInput
{
    public NetworkButtons Buttons;   // Clutch, Pinch (held), Charge (held), HasAim
    public Vector3 ClutchHandPos;    // seat-local palm position, tracking meters
    public Vector3 AimPoint;         // world-space smoothed reticle point
    public byte ShockwaveSeq;        // +1 per palm push

    // Remote avatar (seat-local), same as BikeInputData's rig poses.
    public Vector3 HeadPos;  public Quaternion HeadRot;
    public Vector3 LeftPos;  public Quaternion LeftRot;
    public Vector3 RightPos; public Quaternion RightRot;
}
```

크기 ≈ 4 + 12 + 12 + 1 + 3×28 = 약 113바이트/틱. 60Hz에서 약 6.8KB/s/플레이어 — 문제없는 수준.

### 2.2 Core 쪽에 먼저 추가할 것 (Fusion 없이 지금 TDD 가능)

1. **`ClutchDeltaTracker`** (Core): `Step(bool held, Vector3 handPos, ref Vector3 lastPos, bool lastHeld) → Vector3 delta`.
   잡은 첫 틱과 놓은 상태에서는 0, 손 추적 손실(= Clutch 비트 꺼짐)도 0. 현재 `HandClutchSampler`의 델타 규칙과
   같은 결과인지 비교 테스트를 붙인다. 이후 `ClutchMapper.Step(held, delta, ...)`에 그대로 넣는다.
2. **`HandInputData`에 `PinchHeld`·`ShockwaveSeq` 추가** (기존 `FireTriggered`·`Shockwave` 플래그는 그대로 둠 → 싱글 경로 불변).
   `XRHandsInputSource`/`DebugKeyboardMouseInputSource`/`BotBrain`이 같이 채우게 하면 `OnInput`은 단순 포장이 된다.
   - **2026-10-08 갱신 (aim modes, `hyun/aim-modes`)**: `HandInputData.PinchHeld`는 이미 들어갔다(차지 = 핀치 홀드).
     `AimClutchHeld`·`AimClutchDelta`(CURSOR 조준)도 추가됐다. 따라서 `HeroNetInput`에는 **`PinchHeld` 비트와
     오른손 손바닥 절대 위치**(`AimHandPos`)도 실어야 한다. 커서 델타는 왼손 클러치와 같은 규칙으로 시뮬레이션이
     직전 값과의 차이로 계산한다(빠진 입력이 반복돼도 두 번 적용되지 않게). 조준 모드(ASSIST/CURSOR)는 세션 시작 때
     정해지는 플레이어 설정이라 매 틱 보낼 필요가 없다.
3. **좌석 변환**: 좌석마다 아레나를 보는 방향이 다르면(2.3) `ClutchHandPos`와 리그 포즈를 좌석 로컬로 보내고,
   서버가 `seatRotation * delta`로 월드 델타를 만든다. 순수 함수라 Core에 둔다.

### 2.3 좌석 (XR Origin 하드 규칙과의 관계)

- 각 플레이어의 XR Origin은 **자기 기기에서만** 존재하고 네트워크로 보내지 않는다.
- PvP에서 두 사람이 같은 좌석에 겹쳐 앉으면 서로의 아바타가 겹치므로, 좌석을 아레나 반대편(북/남)에 둔다.
  **좌석 배치는 스폰 직후 XR Origin을 좌석 Transform으로 한 번 놓는 것**이고, 그 뒤로는 하드 규칙 1번대로 절대 움직이지 않는다.
  (첫 프레임 전에 정해지는 배치라 멀미 원칙과 충돌하지 않는다고 판단. Hyun이 싫으면 "둘 다 같은 쪽 좌석 + 아바타 좌우로 비킴"으로 대체.)
- 퍼펫티어 델타·조준은 각자 좌석 기준이라 "내 앞으로 손을 밀면 내 히어로가 나에게서 멀어진다"가 양쪽 모두 성립한다.

---

## 3. 시뮬레이션 이식 (`FixedUpdateNetwork`)

공통 규칙: `Time.deltaTime` → `Runner.DeltaTime`, `Time.time` 쿨다운 → `[Networked] TickTimer`,
예측 재시뮬레이션에서 살아남아야 하는 필드는 `[Networked]`, 연출은 `Render()` + `ChangeDetector`(기존 `NetworkedGunController` 패턴).

### 3.1 `NetworkedHero` (= `FlyingCharacter` + `HandPuppeteerController`)

| 현재 상태 | 네트워크 상태 |
|---|---|
| `transform.position`, `_velocity` | `NetworkTransform`[SDK 확인]이 위치 동기화, `[Networked] Vector3 Velocity` |
| `ClutchMapper._clutched/_target` | `[Networked] NetworkBool Clutched`, `[Networked] Vector3 ClutchTarget` (Core `ClutchMapper`를 상태 주입형으로 쓰려면 `Step`에 상태 struct를 넘기는 오버로드 추가) |
| `HandPuppeteerController._rooted/_rootPosition` | `[Networked] NetworkBool Rooted`, `[Networked] Vector3 RootPos` |
| `_speedMultiplier` | 매 틱 `NetworkedHeroHealth.SpeedMultiplier`에서 읽음 (저장 안 함) |
| `_alive` | `NetworkedHeroHealth.IsDead`에서 읽음 |

틱 순서(한 히어로): 입력 읽기 → 죽었으면 클러치 리셋 후 종료 → `Charge` 비트면 루트 고정 → `ClutchDeltaTracker` →
`ClutchMapper.Step` → `SpringFlightModel.Step(…, Runner.DeltaTime)` → 위치/속도 기록.
뱅킹·진행 방향 회전(`UpdateVisual`)은 **`Render()`로** (연출, 예측 대상 아님).

- Rigidbody/물리 애드온 **불필요**: 비행은 운동학적(스프링 수식)이라 기존 오토바이용 `NetworkRigidbody3D`·`RunnerSimulatePhysics3D`는 쓰지 않는다.
- ADR 10 그대로: 클라가 보내는 건 손 위치뿐, 속도 상한은 서버가 강제 → 손 위치를 조작해도 순간이동 불가.
  추가 방어: 서버에서 틱당 `ClutchHandPos` 변화량을 손 속도 상한(봇과 같은 1.5m/s의 2–3배)으로 클램프.

### 3.2 `NetworkedBeam` (= `PointingBeamController`)

- `Pinch` 엣지 + `[Networked] TickTimer FireCooldown` + 차지(`[Networked] float ChargeTime`, Core `ChargeShotModel`을 상태 주입형으로).
- 판정: `Runner.LagCompensation.Raycast(heroPos, dir, dist, player: Object.InputAuthority, …, HitOptions.IncludePhysX)`
  (기존 `NetworkedGunController`와 동일). 원점은 **히어로 위치**, 방향은 `AimPoint - heroPos` → 엄폐는 히어로 기준(현재와 같음).
- 자기 히어로 제외: 현재 `RaycastIgnoringSelf`는 콜라이더 계층으로 거른다. 네트워크에선 히트한 `Hitbox.Root`의 `InputAuthority`가 쏜 사람과 같으면 무시(기존 코드의 "no self-damage"와 같음). 다만 레이캐스트가 첫 히트에서 멈추므로 **히어로 Hitbox를 자기 레이에서 빼려면** 히어로 Hitbox를 별도 레이어로 두고 원점을 히어로 반경만큼 앞으로 민다(간단). [SDK 확인: `LagCompensation.RaycastAll` 있으면 그걸로 현재 로직 그대로]
- 히어로 프리팹: `HitboxRoot` + 구형 `Hitbox`[SDK 확인] (지금의 SphereCollider 반경 그대로).
- 데미지는 `HasStateAuthority`일 때만 `NetworkedHeroHealth.ServerApplyDamage`.
- 연출(빔 LineRenderer·굵기·사운드·히트 이펙트): `[Networked] int FireCount` + `LastOrigin/LastEnd/LastWidth` → `Render()`에서 `ChangeDetector`. `GunPresentation`을 거의 그대로 개조.
- 봇 예고선(`BotTelegraphLine`): `[Networked] NetworkBool Telegraphing`, `[Networked] Vector3 LockedAim`, `[Networked] float TelegraphProgress`를 `NetworkedBot`에 두고 모든 클라가 그림 → **사람도 예고선을 봐야 피할 수 있으므로 필수.**

### 3.3 `NetworkedShockwave` (= `ShockwaveController`)

- `ShockwaveSeq` 변화 + `[Networked] TickTimer Cooldown` → 서버만 판정.
- 범위 판정은 `Runner.LagCompensation.OverlapSphere`[SDK 확인] 또는 단순히 서버의 히어로 목록과 거리 비교(히어로 2–3명뿐이라 후자 추천, 지연 보상 없이도 반경 6m면 체감 차이 작음).
- 경직은 `NetworkedHeroHealth.ServerApplyStun(duration, multiplier)`. 링 연출은 `[Networked] int WaveCount` → `Render()`.

### 3.4 `NetworkedHeroHealth` (= `HeroHealth` + Core `HeroHealthModel`)

`Networking~/NetworkedPlayerHealth.cs`를 **이름만 바꾸고 거의 그대로** 쓴다(Core 모델이 처음부터 이 의미론으로 작성됨, 주석에 대응 관계 있음).

| `HeroHealthModel` | `NetworkedHeroHealth` |
|---|---|
| `CurrentHealth`, `IsDead`, `DamageCount` | `[Networked]` 동일 이름 |
| `_slowTimer`, `_respawnTimer` | `[Networked] TickTimer SlowTimer/RespawnTimer` (기존 그대로) |
| `_stunTimer`, `_stunMultiplier` (T5 추가) | `[Networked] TickTimer StunTimer`, `[Networked] float StunMultiplier` — **기존 파일에 없으니 추가** |
| `ApplyDamage` | `ServerApplyDamage` |
| `ApplyStun` | `ServerApplyStun` (신규) |
| `Tick` 리스폰 | `FixedUpdateNetwork`의 `RespawnTimer.Expired` (기존 그대로) |
| `SpeedMultiplier` (slow·stun 중 강한 쪽) | 같은 식으로 계산 (기존은 slow만 → stun 반영 수정) |

리스폰 이동은 `NetworkRigidbody3D.Teleport` 대신 `NetworkTransform.Teleport`[SDK 확인]로. 스폰 지점은 `RespawnManager` 재사용(플레이어 인덱스 → 히어로 스폰 지점).
흰색 피격 플래시·체력 바·사망 시 숨김은 `Render()`의 change detection으로(기존 `Damaged/Died/Respawned` 이벤트 그대로).

### 3.5 `NetworkedBot` (Host가 시뮬레이션)

- 봇 히어로 = 사람 히어로와 **같은 프리팹**, `InputAuthority` 없음, `StateAuthority` = Host.
- `NetworkedHero.FixedUpdateNetwork`에서 입력을 얻는 곳만 분기:
  `GetInput(out HeroNetInput input)` 실패 && `HasStateAuthority` && 봇 컴포넌트 있음 → `BotBrain.Step(...)` 결과를 같은 `HeroNetInput`으로 변환.
  → **봇과 사람이 같은 비행 모델·속도 상한·빔 규칙**을 쓰는 지금의 공정성이 네트워크에서도 유지된다.
- `BotBrain`은 `System.Random`·내부 타이머를 가진 **비예측 상태**라 Host에서만 돈다. 클라는 봇 히어로를 보간된 위치로만 본다(예측 불필요).
  Host는 재시뮬레이션하지 않으므로(서버는 각 틱을 한 번만 진행) 봇 상태를 `[Networked]`로 만들 필요 없음. 예고선 표시용 3개 값만 `[Networked]`.
- `BotBrain.NotifyHit()`는 `NetworkedHeroHealth`의 서버 측 피격 콜백에서 호출.

### 3.6 `NetworkedMatch` (= `MatchDirector`의 규칙 부분)

- Host에 `MatchStateMachine` 하나. `[Networked]` 미러: `Phase`, `Round`, `Wins[0..1]`(또는 플레이어별), `RoundWinner`, `MatchWinner`, `PhaseTimer`(TickTimer).
  Core 클래스는 내부 `_phaseTime`을 쓰므로, Host는 매 틱 `Tick(Runner.DeltaTime, …)` 후 상태를 `[Networked]`로 복사만 한다(클라는 읽기 전용).
- `MatchSide.Player/Opponent` → 네트워크에선 **좌석 인덱스 0/1**로 읽는다(Core enum은 그대로, 의미만 "좌석 0 = Player").
- `fightOnly`/`opponentOnly` 게이팅 → Host가 `Phase != Fight`면 모든 히어로의 입력을 무시(클러치 리셋)하는 방식으로. 로컬 입력 소스는 끄지 않는다(메뉴 포인터가 별도라 영향 없음).
- KO: `NetworkedHeroHealth.IsDead` 상승 엣지 → Host가 `ReportKO(좌석)`.
- HUD(`MatchHud`)는 각 클라가 `[Networked]` 미러를 읽어 표시 — 위치는 좌석 로컬(`SeatUI`)이라 그대로.

### 3.7 일시정지 (하드 요건, 멀티에서 의미가 바뀜)

- 멀티에서 `Time.timeScale = 0`은 **쓰지 않는다**(Fusion 틱이 멈추면 연결이 끊기는 게 아니라 다른 사람의 시뮬레이션과 어긋남).
- 추천: **개인 일시정지** = 손목 PAUSE → 로컬 메뉴만 뜨고, 내 히어로는 클러치가 풀려 활공(입력은 `Clutch=0`, `Pinch=0`으로 보냄).
  동시에 Host가 `[Networked] NetworkBool Paused[좌석]`을 보고 **그 히어로를 무적 + 라운드 타이머 정지**(상대가 일시정지 중인 사람을 때리는 일 방지).
  상대 화면에는 "OPPONENT PAUSED" 배너. 자동 일시정지(포커스 잃음·헤드셋 벗음)도 같은 경로.
- 한 명이 60초 이상 일시정지면 Host가 라운드를 무승부 처리(매치 10분 이내 유지). → Hyun 결정 필요(아래 7절).

### 3.8 로컬에 남는 것 (네트워크 무관)

`TutorialDirector`(튜토리얼은 세션 참가 **전** 싱글에서), `HandMenu*`/`HandMenuPointer`(로컬 UI, 버튼 동작만 RPC 또는 Host 호출로),
`WristMenu`, `ArenaViewMode`(VR 아레나/테이블탑은 사람마다 따로 골라도 됨 — 히어로 위치는 아레나 좌표라 공유 가능),
`HandGestureTracker`·`XRHandsInputSource`(입력 생성), `MatchHud`(표시).

---

## 4. 세션·스폰 흐름

```
Boot → Menu (로컬)
  ├─ START        → 지금의 싱글 봇전 (Fusion 없음, 현재 코드 경로)   ← 심사 본편
  ├─ TUTORIAL     → 로컬
  └─ ONLINE       → NetworkGameLauncher.StartSession(AutoHostOrClient)
                     Host: 좌석 0에 자기 히어로 + 좌석 1에 봇 스폰 → 매치 시작 가능
                     Client 입장: Host가 봇 디스폰, 좌석 1 히어로를 입장자 InputAuthority로 스폰, 매치 재시작
                     Client 퇴장: 좌석 1에 봇 다시 스폰 (매치 계속)
```

- **싱글을 `GameMode.Single`[SDK 확인]로 통일할지?** → 추천 **아니오**. 지금 싱글 경로는 검증됐고(EditMode 117개), 마감 전 회귀 위험만 늘어난다.
  대신 네트워크 경로의 시뮬레이션 컴포넌트가 Core를 공유하므로 규칙 차이는 생기지 않는다.
- 세션 이름: 사설 코드 방식(4자리 숫자 표시 → 상대가 손 메뉴 숫자 패드로 입력) 또는 공개 매치메이킹 1개 방. 대회 심사엔 공개 1개 방이면 충분(혼자면 봇이 채움).
- 콜드 스타트 요건: ONLINE은 메뉴 버튼 하나 추가일 뿐, 기본 흐름(START)은 그대로 최소 단계.
- **Photon 계정·App ID 필요 → 하드 규칙 5번(가입 금지) 때문에 무인 모드에서 불가. Hyun 작업.**

---

## 5. `Networking~/` 스크립트별 처리 표

| 파일 | 처리 | 이유 / 할 일 |
|---|---|---|
| `BikeInputData.cs` | **보존, 새 파일로 대체** | `HeroNetInput`(2.1)을 새로 만든다. 리그 포즈 필드 구성은 그대로 가져온다. 레이스 모드 부활용으로 원본 보존. |
| `HardwareInputCollector.cs` | **새 파일로 대체** (`HeroInputCollector`) | 컨트롤러 `InputAction`은 손 전용 원칙에 어긋남. `XRHandsInputSource.Current` + 좌석 로컬 변환 + 리그 포즈로 `HeroNetInput`을 만든다. 싱글톤·`Collect()` 구조는 그대로. |
| `NetworkGameLauncher.cs` | **수정해서 재사용** | 세션 시작·`OnPlayerJoined/Left`·`OnInput` 구조 그대로. 바꿀 것: 스폰 프리팹 → 히어로, 좌석/봇 교체 로직(4절), `OnInput`이 `HeroInputCollector` 사용, `autoStartOnPlay` 끔(ONLINE 버튼에서 시작), `sessionName` 변경. |
| `RespawnManager.cs` | **그대로 재사용** | 스폰 지점 배열 = 히어로 스폰 지점 2개(+좌석 Transform 2개는 별도 배열 추가). |
| `NetworkedPlayerHealth.cs` | **수정해서 재사용** (`NetworkedHeroHealth`) | 3.4. 경직(stun) 추가, Teleport 대상 교체, `NetworkRigidbody3D` 의존 제거. |
| `NetworkedGunController.cs` | **수정해서 재사용** (`NetworkedBeam`) | 3.2. 버튼 엣지·쿨다운·지연 보상·서버 데미지·`FireCount` 연출 패턴 그대로. 원점을 총구 → 히어로, 방향을 총구 회전 → `AimPoint`로, 차지샷 추가. |
| `GunPresentation.cs` | **수정해서 재사용** (`BeamPresentation`) | 순수 연출. LineRenderer 굵기(차지 배율)·색만 `PointingBeamController`의 값으로. |
| `NetworkedMotorcycle.cs` | **보존만** | 히어로 비행으로 대체(3.1). 오토바이 레이스 모드용으로 남김. 패턴(입력 → `FixedUpdateNetwork`, `[Networked]` 상태, `Render` 연출)은 `NetworkedHero`의 본보기. |
| `NetworkRig.cs` | **수정해서 재사용** | 아바타 부모를 "오토바이 위"에서 **"상대 좌석"**으로. 입력 타입만 `HeroNetInput`으로. 원격 플레이어의 손이 보이는 것 = 핸드트래킹 멀티의 소셜 포인트. |
| `LocalPlayerBinder.cs` | **수정해서 재사용** | `XROriginSync.SetMotorcycle` 호출 제거 → **좌석 배치 1회**(2.3)로 교체. 로컬 HUD·피격 플래시·튜토리얼 끄기 연결은 그대로. `LocalBike` → `LocalHero`. |
| `XROriginSync.cs` (루트) | **사용 안 함** | XR Origin이 히어로를 따라가는 것은 3인칭 고정 시점 원칙(ADR 4·5) 위반. 레거시 루트 스크립트라 삭제 금지, 씬에 붙이지 않는다. |

새로 만드는 것: `HeroNetInput`, `HeroInputCollector`, `NetworkedHero`, `NetworkedBot`, `NetworkedShockwave`, `NetworkedMatch`, 히어로 네트워크 프리팹(씬 빌더로 생성), 좌석 Transform 2개.

---

## 6. 작업 순서 (SDK 임포트 후, 추정)

| 단계 | 내용 | 검증 | 추정 |
|---|---|---|---|
| N0 | (Hyun) Photon 가입, Fusion 2 App ID, SDK 임포트. `Networking~` → `Networking` 복원(DECISIONS Q2의 git mv). | 컴파일 — 여기서 [SDK 확인] 항목이 다 드러남 | 0.5일 |
| N1 | Core 준비(2.2): `ClutchDeltaTracker`, `PinchHeld`/`ShockwaveSeq`, 좌석 변환, `ClutchMapper`·`ChargeShotModel` 상태 주입 오버로드 | EditMode TDD (Fusion 불필요 — **지금도 가능**) | 0.5일 |
| N2 | `HeroNetInput` + `HeroInputCollector` + `NetworkGameLauncher` 수정, `NetworkedHero`(비행만) | 에디터 2인스턴스(Unity 6 Multiplayer Play Mode 패키지 — 추가 승인 필요) 또는 PC 에디터 + Quest | 1일 |
| N3 | `NetworkedHeroHealth` + `NetworkedBeam` + `BeamPresentation` | 서로 맞히고 리스폰 | 1일 |
| N4 | `NetworkedBot` + 예고선 동기화, 좌석 교체(봇↔사람) | 혼자 Host → 봇전, 두 번째 입장 → 봇 교체 | 1일 |
| N5 | `NetworkedShockwave`, 차지샷, `NetworkedMatch` + HUD, 개인 일시정지 | 매치 완주 | 1일 |
| N6 | `NetworkRig` 원격 손, ONLINE 메뉴 버튼, 연결 끊김 처리 | Quest 2대 실기 | 1일 |

합계 약 6일. 마감(11/18) 역산으로 **11/1까지 N0이 안 되면 멀티는 포기하고 영상에서 "멀티 준비된 구조"로만 언급**하는 것을 추천.

---

## 7. Hyun이 정할 것

1. **멀티를 이번 출품에 넣을지** (추천: 싱글 완성도 먼저, 11/1 기준으로 판단).
2. 좌석 배치: 아레나 반대편 좌석(추천) vs 같은 쪽 나란히.
3. 멀티 일시정지: 개인 일시정지 + 무적 + 타이머 정지(추천), 60초 넘으면 라운드 무승부.
4. 매치메이킹: 공개 방 1개(추천, 심사용) vs 코드 입력 방.
5. 테스트 도구: Unity Multiplayer Play Mode 패키지 추가 허용 여부(Q5는 T10에서만 패키지를 허용했음).

## 8. 위험

- **[SDK 확인] 항목**: `NetworkTransform.Teleport`, `HitboxRoot/Hitbox`, `LagCompensation.OverlapSphere`/`RaycastAll`, `GameMode.Single` — 이름·시그니처가 다를 수 있음.
- 손 추적 입력은 노이즈가 커서 예측된 내 히어로와 서버 결과가 자주 어긋날 수 있음 → 스프링 모델이 부드러워 보정이 작게 보일 것으로 예상하지만 실기 확인 필요.
- 지연 보상 레이캐스트는 Hitbox 기준이라 정적 지형 엄폐는 `HitOptions.IncludePhysX`가 필요(기존 코드에 있음).
- 테이블탑 모드에서 XR Origin 스케일 35배 — 리그 포즈를 좌석 로컬(스케일 전 추적 공간)으로 보내야 상대 화면의 아바타 크기가 맞음.
- Fusion 무료 플랜 CCU·지역 설정은 계정 생성 후에 확인 가능.
