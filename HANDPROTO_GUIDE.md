# 핸드트래킹 그레이박스 프로토타입 — 셋업 가이드

확정 컨셉: **아레나 고정 시점 + 왼손 퍼펫티어(주먹 클러치) + 오른손 포인팅 조준 & 핀치 발사**
Fusion 불필요 — XR Hands 패키지만으로 지금 바로 컴파일/실행됩니다.

## 0. 패키지/프로젝트 설정 (한 번만)

1. Package Manager에서 **XR Hands (com.unity.xr.hands)** 설치 확인 (샘플이 이미 있으니 설치돼 있을 확률 높음)
2. Project Settings → XR Plug-in Management → OpenXR (Android 탭):
   - **Hand Tracking Subsystem** 체크
   - Meta Quest 기능 그룹 사용 시 **Meta Hand Tracking Aim**은 켜도 되고 안 켜도 됨 (우리는 자체 에임 레이 사용)
3. Quest 기기 설정에서 핸드트래킹 허용 (Settings → Movement tracking → Hand tracking)
4. 에디터 테스트: 링크 케이블/에어링크로 Quest 연결 시 핸드트래킹이 에디터 Play에서도 동작

## 1. 새 씬 구성 (기존 씬 건드리지 말 것 — HandProtoScene 권장)

```
HandProtoScene
├── XR Origin (VR)            ← 기존 프리팹 재사용, 위치 고정 (이동 없음!)
│   └── Main Camera
├── HandTracking              ← 빈 오브젝트
│   └── HandGestureTracker    (xrOrigin, headCamera 연결)
├── Arena                     ← 빈 오브젝트, 플레이어 앞 ~20m
│   ├── FlyingHero            ← 캡슐 하나 (콜라이더 유지)
│   │   ├── FlyingCharacter   (arenaCenter=Arena, visual=자식 메시)
│   │   └── Visual            ← 캡슐 메시 (기울기 연출용 자식)
│   ├── Reticle               ← 작은 스피어 (콜라이더 제거!)
│   ├── BeamRenderer          ← LineRenderer만 있는 빈 오브젝트
│   ├── Target x 3~5          ← 큐브, PrototypeTarget 부착
│   └── 바닥/벽 등 그레이박스 지형 (콜라이더 필수 — 에임 레이가 맞을 표면)
└── Controllers               ← 빈 오브젝트
    ├── HandPuppeteerController  (character=FlyingHero, useLeftHand=true)
    └── PointingBeamController   (character, reticle, beam 연결)
```

주의사항:
- **Reticle에 콜라이더가 있으면 에임 레이가 자기 자신을 맞춰서 떨림** — 반드시 제거
- FlyingHero의 레이어를 따로 만들어 PointingBeamController의 aimMask에서 빼면
  자기 캐릭터를 조준점으로 잡는 문제 방지
- XR Origin은 절대 움직이지 않음 — 관람석 고정 시점이 멀미 방어의 핵심

## 2. 조작법

| 입력 | 동작 |
|---|---|
| 왼손 주먹 쥐기 | 클러치 ON — 손을 움직이면 캐릭터가 따라 비행 |
| 왼손 펴기 | 클러치 OFF — 캐릭터는 관성으로 활공, 손 재배치 |
| 오른손으로 가리키기 | 아레나에 조준점(레티클) 표시 |
| 오른손 핀치 (엄지+검지) | 캐릭터가 조준점으로 빔 발사 |

클러치는 마우스 들기와 같은 개념: 쥐고 끌고, 펴서 손 위치 리셋, 다시 쥐고 끌기.

## 3. 튜닝 노브 (헤드셋 쓰고 정할 값들)

느낌을 결정하는 순서대로:
1. **HandPuppeteerController.positionScale** (기본 60) — 손 1cm당 캐릭터 60cm.
   너무 크면 조종이 거칠고, 작으면 클러치를 자주 반복해야 함. 40~100 사이 탐색
2. **FlyingCharacter.stiffness/damping** (10/5) — 높이면 기민, 낮추면 둥실.
   히어로 느낌은 보통 기민 쪽이 잘 맞음
3. **FlyingCharacter.glideDrag** (0.8) — 클러치 놓았을 때 활공 길이
4. **클러치 임계값** (0.7/0.45) — 오발동하면 grab을 0.8로, 안 잡히면 0.6으로
5. **HandGestureTracker의 fist/pinch 거리값** — 손 크기에 따라 조정 가능

## 4. 이 프로토타입으로 검증할 것

- [ ] 주먹-끌기-펴기 루프가 30초 안에 몸에 익는가
- [ ] 클러치 반복이 귀찮지 않은가 (positionScale 튜닝으로 해결되는가)
- [ ] 조준점이 손 떨림 없이 안정적인가 (어깨 앵커 레이 검증)
- [ ] 핀치 발사가 의도대로만 나가는가 (오발/불발 빈도)
- [ ] 고정 시점에서 캐릭터 추적이 목 아프지 않은가 (아레나 크기/거리 조정)
- [ ] 5분 플레이 후 팔 피로 정도 (팔꿈치를 무릎/팔걸이에 받치는 자세가 되는가)

## 5. 다음 단계 (프로토 통과 시)

1. 제스처 공격 어휘 확장: 손바닥 밀기=넉백 웨이브, 두 손 모으기=차지샷
2. 회피 요소: 상대 빔 예고선 → 왼손으로 급격히 끌어서 회피하는 재미 검증
3. Fusion 이식: BikeInputData → HandInputData(클러치 상태 + 목표 위치 + 에임 레이 + 제스처 플래그),
   FlyingCharacter의 스프링 모델을 FixedUpdateNetwork로 이동 (서버가 속도 캡 강제)
4. 아레나 멀티: 각 플레이어는 자기 관람석에서 같은 아레나를 봄 — 스폰 포인트가 관람석이 됨

## 파일 목록

| 파일 | 역할 |
|---|---|
| HandGestureTracker.cs | XR Hands 래퍼 — 주먹/핀치 강도, 어깨 앵커 에임 레이 |
| FlyingCharacter.cs | 스프링 추적 비행 모델 + 활공 + 아레나 경계 + 뱅킹 연출 |
| HandPuppeteerController.cs | 왼손 클러치 + 상대 매핑 퍼펫티어 |
| PointingBeamController.cs | 오른손 조준 레티클 + 핀치 발사 빔 |
| PrototypeTarget.cs | 그레이박스 사격 타겟 (피격 플래시/리스폰) |
