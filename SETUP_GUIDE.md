# VR 멀티플레이 컴뱃 레이서 — Phase 0~1 셋업 가이드

확정된 기준: Fusion 2 Host Mode / 2인 수직 슬라이스 / PvP 전용 / 피격 시 체력 감소 + 일시 감속, 0이면 리스폰 / 표현은 화면 이펙트(시점에 물리 회전 전달 금지)

환경: Unity 6000.1.14f1, Quest 3, XR Interaction Toolkit

---

## 0. 준비물

1. **Photon 계정 + Fusion AppId** 발급
   - dashboard.photonengine.com → 새 앱 생성 (타입: Fusion) → App Id 복사
2. **Fusion 2 SDK** 임포트 (Photon 공식 다운로드 페이지의 최신 Fusion 2 유니티 패키지)
   - 임포트 후 `Fusion > Fusion Hub` (또는 Realtime Settings)에 App Id 붙여넣기
3. **Fusion Physics Addon** 임포트 — NetworkRigidbody3D, RunnerSimulatePhysics3D가 여기 들어있음 (SDK 다운로드에 동봉)
4. 새 스크립트 11개를 `Assets/Scripts/Networking` 폴더에 추가

주의: Fusion SDK 마이너 버전에 따라 콜백 시그니처나 클래스 위치가 조금 다를 수 있음.
컴파일 에러가 나면 에러 그대로 가져오면 바로 맞춰줄게.

---

## 1. Runner 프리팹

빈 GameObject → 프리팹화 → 이름 `NetworkRunnerPrefab`
- `NetworkRunner` 컴포넌트
- `RunnerSimulatePhysics3D` 컴포넌트 (물리를 Fusion 틱에 맞춰 시뮬레이션)

## 2. 오토바이 프리팹 (플레이어)

기존 오토바이 프리팹을 복제해서 `PlayerBike_Networked`로 만들고 구성:

루트:
- `Rigidbody` (기존 그대로)
- `NetworkObject`
- `NetworkRigidbody3D` (Physics Addon) — Interpolation Target은 비워둬도 됨
- `NetworkedMotorcycle` ← 기존 MotorcycleController 제거하고 교체
- `NetworkedPlayerHealth`
- `NetworkedGunController`
- `GunPresentation`
- `LocalPlayerBinder`
- `HitboxRoot` (Fusion) — 지연 보상 핏 판정용

피격 판정용 자식:
- 차체를 덮는 박스 콜라이더 위치에 `Hitbox` 컴포넌트 추가 (HitboxRoot가 자동 인식)
- 아바타 머리/몸에도 Hitbox를 달면 부위별 판정 가능 (지금은 차체 1개로 충분)

아바타 자식 (`AvatarRoot`):
- 로컬 오프셋을 XROriginSync의 positionOffset과 동일하게 (기본 0, 1.5, -0.5)
- 자식으로 Head / LeftHand / RightHand (간단한 메시: 헬멧 + 장갑 정도)
- 루트에 `NetworkRig` 컴포넌트, 세 Transform 연결
- Head의 Renderer들을 NetworkRig의 hiddenForLocalPlayer에 등록 (내 시야 가림 방지)
- 총 메시는 RightHand(총 손) 자식으로, 그 총구 Transform을 GunPresentation의 gunBarrel에 연결

오디오:
- 기존 `MotorcycleAudioController`를 그대로 쓰되, 참조 필드 타입을
  `MotorcycleController` → `NetworkedMotorcycle`로 교체 (메서드 이름은 호환되게 맞춰놨음:
  maxSpeed / GetCurrentSpeed() / IsGrounded()).
  단, accelerateAction/brakeAction을 직접 읽는 부분은 원격 오토바이에선 입력이 없으므로,
  Phase 2에서 "속도 기반"으로 바꾸는 리팩토링 예정. 지금은 내 오토바이 소리만 정상이어도 OK.

## 3. 씬 구성 (MainLevel)

1. `NetworkLauncher` 빈 오브젝트
   - `NetworkGameLauncher` — runnerPrefab, playerBikePrefab 연결
   - `RespawnManager` — 스폰 포인트 Transform 2개 이상 등록 (서로 떨어진 위치)
2. `HardwareInputCollector` 빈 오브젝트 (또는 XR Origin에)
   - 운전 손: accelerate(트리거), brake(그립), steer(조이스틱), jump(버튼)
   - 총 손: fire(트리거)
   - XR 참조: xrOrigin / head(메인 카메라) / leftHand / rightHand / gunBarrel
   - 기존 InputAction 에셋 그대로 재사용 가능
3. XR Origin
   - 교체된 `XROriginSync` (motorcycle 필드는 비워둠 — 런타임에 바인딩됨)
4. 씬에 미리 놓여 있던 오토바이는 **삭제** (이제 네트워크 스폰)
5. 기존 씬의 Monster / WaveManager / 관련 UI는 비활성화 또는 제거 (PvP 슬라이스 범위 밖)
6. HUD: HUDController의 PlayerHealth 참조 부분은 `LocalPlayerBinder.LocalHealth`를
   읽도록 수정 필요 (Start에서 null이면 코루틴으로 대기 → 할당). 원하면 다음 턴에 수정본 제공.
7. DamageFlash: 프리팹의 LocalPlayerBinder → OnLocalDamaged UnityEvent에
   씬의 DamageFlash.TriggerFlash를 연결 (FindFirstObjectByType 의존 제거)

## 4. 레이어 / 충돌

- `PlayerBike` 레이어 생성, 오토바이 프리팹에 적용
- NetworkedGunController의 hitMask: PlayerBike + 지형/장애물 레이어 포함
  (지형을 빼면 벽 뒤의 적이 맞는 버그가 생김)

## 5. 테스트 절차

### 1단계 — 에디터 단독 (헤드셋 1대)
Play → Host로 시작 → 오토바이 1대 스폰 → 주행/점프/사격이 기존 데모와 같은 느낌인지 확인.
여기서 느낌이 다르면 prediction 셋업 문제이니 멈추고 디버그.

### 2단계 — 에디터 + 빌드 (같은 PC, 2피어)
Build & Run으로 데스크톱 빌드 하나 띄우고 에디터도 Play.
둘이 같은 세션에 붙고(sessionName 동일), 서로의 오토바이가 보이고 움직이면 Phase 0 통과.

### 3단계 — Quest 2대 (진짜 검증)
두 헤드셋에 빌드 설치, 같은 Wi-Fi 필요 없음 (Photon 클라우드 릴레이 경유).
체크리스트:
- [ ] 상대 오토바이가 부드럽게 움직이는가 (텔레포트/떨림 없이)
- [ ] 상대 아바타의 머리/손이 자연스럽게 따라오는가
- [ ] 서로 쏘면 체력이 깎이고 감속이 걸리는가
- [ ] 죽으면 3초 후 스폰 포인트에서 리스폰되는가
- [ ] 내가 맞아도 시점이 절대 흔들리지 않는가 (화면 플래시만)
- [ ] 둘 다 72Hz 이상 유지되는가

## 6. 알려진 다음 작업 (Phase 2 후보)

- 피격 시 비네팅 강화 + 감속 중 시각 피드백 (속도선 감소 등)
- 원격 오토바이 엔진음을 입력이 아닌 동기화 속도 기반으로
- HUDController / DamageFlash 정식 멀티 대응 리팩토링
- 트랙 + 체크포인트 + 랩 시스템 (레이스 구조)
- 무기 픽업 / 무기 다양화 (리볼트 감성)
- 로비 씬 → 게임 씬 Fusion 씬 전환

## 새 파일 목록

| 파일 | 역할 | 출처 |
|---|---|---|
| BikeInputData.cs | 네트워크 입력 구조체 | 신규 |
| HardwareInputCollector.cs | VR 입력 수집 (씬) | Motorcycle/GunController의 입력부 이관 |
| NetworkGameLauncher.cs | 세션 시작 + 플레이어 스폰 | 신규 |
| RespawnManager.cs | 스폰 포인트 관리 | 신규 |
| NetworkedMotorcycle.cs | 예측 가능한 오토바이 시뮬레이션 | MotorcycleController 변환 |
| NetworkedPlayerHealth.cs | 서버 권위 체력 + 감속 + 리스폰 | PlayerHealth 변환 + 신규 설계 |
| NetworkedGunController.cs | 지연 보상 사격 판정 | GunController 전투부 변환 |
| GunPresentation.cs | 사격 이펙트 (로컬) | GunController 이펙트부 이관 |
| NetworkRig.cs | 원격용 아바타 동기화 | 신규 (Fusion VR 샘플 패턴) |
| LocalPlayerBinder.cs | 내 오토바이 ↔ 씬 표현 연결 | 신규 |
| XROriginSync.cs | XR 리그 추종 (런타임 바인딩 지원) | 기존 코드 소폭 수정 |
