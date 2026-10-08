# HANDOFF — VR 핸즈퍼스트 아레나 히어로 (Meta Awards 출품작)

> 이 문서는 다른 Claude 세션에 프로젝트를 인계하기 위한 핸드오프 문서다.
> 작성일: 2026-10-07 / 마지막 실작업: 2026-09-29
> 오너: Hyun (James Hyun Ha) — SCAD Immersive Reality, XR 엔지니어/디자이너

---

## 0. 한 줄 요약과 목표

**무중력 아레나에서 손만으로 히어로 캐릭터를 조종해 싸우는 3인칭 VR 게임 (Quest 3, 핸드트래킹 전용).**

최종 목표: **Meta VR Start Developer Competition (2회차) Gaming 트랙 수상.**
- 제출 페이지: https://start-developer-competition-26.devpost.com/
- 공식 발표: https://developers.meta.com/blog/meta-connect-2026-vr-start-developer-competition/
- **마감: 2026-11-18** / 상금 총 $1M ($20K~$100K × 20)
- 요건: **Meta Start 프로그램 멤버 필수** (미가입 시 즉시 신청 — 승인 리드타임 있음), Devpost 등록,
  신작 또는 기존작 대규모 업데이트, **처음부터 끝까지 손만으로 플레이 가능** (컨트롤러 지원은 추가 허용),
  seated play + 빠른 시작/일시정지 권장
카테고리 원문: "Gaming: Games designed for hands-first input or eyes and hands.
Puzzle, strategy, casual, social, narrative—genres that work well for seated play, without controllers."

모든 설계 결정은 이 카테고리 심사를 기준으로 내려졌다. 심사 포인트에 어긋나는 제안을 할 때는
반드시 그 트레이드오프를 Hyun에게 명시할 것.

## 1. 확정된 게임 컨셉 (바꾸려면 Hyun과 합의 필요)

- **아레나 고정 시점**: 플레이어(XR Origin)는 관람석처럼 고정. 내 캐릭터와 상대가
  눈앞의 아레나 공간(~35m)에서 날아다니며 싸움. **XR Origin은 절대 이동하지 않는다** — 멀미 방어의 핵심.
- **3인칭 퍼펫티어 조종 (왼손)**: 주먹 쥐기 = 클러치 ON, 손 이동량 × 배율만큼 캐릭터 목표점 이동
  (마우스식 **상대 매핑** — 절대 위치 매핑 아님). 손 펴면 캐릭터는 관성 활공, 손은 재배치.
- **포인팅 조준 + 제스처 공격 (오른손)**: 어깨 앵커 레이로 아레나를 직접 가리켜 조준(레티클),
  핀치로 캐릭터가 그 지점에 빔 발사. "지휘관 조준" — 조준은 내가, 발사는 캐릭터가.
- **무중력 히어로 비행**: 중력 없음, 스프링 추적 + 활공. 리볼트식 오토바이 레이서에서 피벗한 컨셉.
- **멀티플레이 PvP 지향**: Photon Fusion 2 Host Mode, 서버 권위 + 지연 보상 (아래 ADR 참고).
- 입력은 **핸드트래킹 전용** (컨트롤러 폴백은 논의된 바 없음).

### 컨셉 변천사 (왜 이렇게 됐는지)
1. 시작: 싱글 데모 — VR 오토바이 + 총 (한 손 조작 + 한 손 사격)
2. 1차 방향: 리볼트식 멀티 컴뱃 레이서 → Fusion 2 선정, 네트워킹 리팩토링 완료
3. 피벗 (9/29): Meta 카테고리가 핸즈퍼스트 — 오토바이는 "컨트롤러 이식"으로 보이는 반면,
   "손 기울여 날고 손짓으로 쏘는" 건 손 없이는 성립 불가 → 심사 스토리가 강함
4. 1인칭 vs 3인칭 논의 → 3인칭 아레나로 결정 (멀미 제거 + 트래킹 노이즈에 관대 + 히어로가 보임)

## 2. 수상 전략 (심사 관점 체크리스트)

카테고리 문구와의 매핑:
| 심사 키워드 | 우리의 대응 |
|---|---|
| hands-first input | 조종/조준/발사 전부 제스처 네이티브. 컨트롤러 바인딩 이식이 아님 |
| seated play | 고정 관람석 시점 + 팔꿈치를 무릎에 받치는 조종 자세로 설계됨 |
| casual / social | 아레나 PvP는 세션이 짧고 소셜. 멀티가 차별점 (핸드트래킹 멀티는 드묾) |
| without controllers | 100% 핸드트래킹. 트래킹 로스 시에도 사고 없는 열화(클러치 자동 해제→활공) |

차별화 포인트 (제출물/트레일러에서 강조할 것):
- **핸드트래킹 멀티플레이 PvP** — 이 조합 자체가 희소함
- **멀미 제로 설계** — 고정 시점 + 시점 비회전 원칙, 심사위원이 장시간 써도 안전
- **클러치 메커닉** — 마우스 들기의 VR 번역. 30초 안에 배워지는 조작이 목표
- Iron Man VR(컨트롤러 비행)과 Moss(3인칭 VR)의 문법을 결합한 새 장르 포지셔닝

다음 Claude가 반드시 확인할 것: **Hyun의 Meta Start 멤버십 상태**(아니면 가입부터),
Devpost 공식 룰의 세부 제출물 목록(빌드 전달 방식, 영상 요구사항), 심사 기준 배점.

## 3. 환경 & 프로젝트 위치

- Unity **6000.1.14f1**, Quest 3, XR Interaction Toolkit + **XR Hands** (com.unity.xr.hands)
- 유니티 프로젝트: **`E:\A_4\A_4`** (Hyun의 PC "nb-r203-09", Claude 데스크톱 브리지로 접근)
  - 주의: `E:\metaAwards\Build`, `E:\310fINAL\Build`는 빌드 출력물 폴더 — 스크립트 넣는 곳 아님
- 스크립트 위치: `Assets\MyAssets\Scripts\` (기존 싱글 데모 20개),
  `...\Scripts\Networking\` (Fusion용 10개), `...\Scripts\HandProto\` (핸드트래킹 프로토 5개)
- 가이드 문서: 프로젝트 루트의 `SETUP_GUIDE.md` (Fusion 셋업), `HANDPROTO_GUIDE.md` (프로토 씬 구성)
- Hyun과의 소통: 설명은 한국어, 코드 주석은 영어. 아키텍처 전체 그림 먼저, 그다음 단계별 구현.

## 4. 현재 상태 (2026-09-29 기준 — 인계받으면 먼저 최신화할 것)

완료:
- [x] 넷코드 선정 (Fusion 2) + 네트워킹 아키텍처 설계
- [x] Fusion용 스크립트 11개 작성·커밋 (오토바이 기준; `Networking\` + 교체된 `XROriginSync.cs`)
- [x] 핸즈퍼스트 피벗 결정 + 조종 방식 확정
- [x] 핸드트래킹 그레이박스 프로토 스크립트 5개 작성·커밋 (`HandProto\`) — **Fusion 불필요, 바로 컴파일됨**

미완 / 미확인 (인계 시 Hyun에게 확인):
- [ ] Fusion 2 SDK + Physics Addon 임포트 여부 — **임포트 전이면 `Networking\` 폴더가 컴파일 에러를 내는 게 정상.**
      급하면 해당 폴더를 임시로 빼두고 HandProto만 돌려도 됨 (서로 독립)
- [ ] HandProto 씬 구성 및 헤드셋 테스트 여부 — `HANDPROTO_GUIDE.md`의 하이어라키/체크리스트 참고
- [ ] 프로토 느낌 검증 결과 (positionScale, stiffness 등 튜닝값)
- [ ] 대회 마감일/제출 요구사항

## 5. 핵심 설계 결정 (ADR 요약 — 번복 시 사유 기록할 것)

1. **넷코드 = Photon Fusion 2, Host Mode (서버 권위)** — 빠른 VR 착수(공식 VR 샘플, 릴레이,
   물리 애드온)를 비용/통제권보다 우선. FishNet은 차점 탈락.
2. **서버 권위 + 클라 예측 + 지연 보상** — 경쟁 PvP의 공정성. 데미지 확정은 서버만.
3. **피격 페널티 = 체력 감소 + 일시 감속(2초, 50%) + 0이면 리스폰(3초)** — 물리 넉백 없음.
4. **피격 표현 = 화면 이펙트만. 플레이어 시점은 어떤 외력으로도 회전/이동하지 않는다** (멀미 원칙).
   1인칭 시절엔 XROriginSync의 Y축 전용 회전이 이 원칙의 구현체였고, 3인칭 아레나에선 시점 고정이 그 역할.
5. **3인칭 고정 아레나 시점** — 추적 카메라 금지 (vection 재발 방지).
6. **왼손 퍼펫티어 = 상대 매핑 + 주먹 클러치** — 절대 매핑 기각 사유: 캘리브레이션 불필요,
   트래킹 글리치가 순간이동이 아닌 소폭 밀림으로 끝남, 클러치 반복으로 무한 이동.
7. **오른손 조준 = 어깨 앵커 레이** (어깨 추정점→검지 관절 통과) — 핀치 순간 조준점이 안 튐.
8. **모든 제스처 임계값은 히스테리시스** (예: 쥠 0.7 / 놓음 0.45) — 경계 플리커 방지.
9. **입력은 구조체로 추상화** — 시뮬레이션은 입력의 출처(컨트롤러/손)를 모름. 피벗이 싸졌던 이유.
10. **스프링 추적 = 비행 모델이자 미래의 서버 시뮬레이션** — 입력은 "손이 원하는 목표점",
    서버가 속도 캡을 강제 → 순간이동 치트 원천 차단 + 트래킹 노이즈 필터.

## 6. 코드 지도

### HandProto (현재의 본류 — Fusion 불필요)
| 파일 | 역할 |
|---|---|
| HandGestureTracker.cs | XR Hands 래퍼. 주먹/핀치 강도(0..1), 월드 포즈, 어깨 앵커 에임 레이. 싱글톤 |
| FlyingCharacter.cs | 속도 캡 스프링 추적 + 활공(glideDrag) + 아레나 경계 클램프 + 뱅킹 연출 |
| HandPuppeteerController.cs | 왼손 클러치 + 상대 매핑 (positionScale 기본 60) |
| PointingBeamController.cs | 오른손 레티클 + 핀치 발사. 데미지 레이는 캐릭터 위치에서 발사 (엄폐는 캐릭터 기준) |
| PrototypeTarget.cs | 그레이박스 타겟 (피격 플래시, 3발에 사망, 리스폰) |

알려진 함정: Reticle에 콜라이더 금지 (에임 레이가 자길 맞춰 떨림), FlyingHero는 별도 레이어로
aimMask에서 제외, 트래킹 로스 시 클러치 자동 해제가 의도된 동작.

### Networking (오토바이 기준으로 작성됨 — 피벗 후 이식 대상)
BikeInputData(입력 구조체) / HardwareInputCollector / NetworkGameLauncher / RespawnManager /
NetworkedMotorcycle / NetworkedPlayerHealth / NetworkedGunController / GunPresentation /
NetworkRig / LocalPlayerBinder + 교체된 XROriginSync.

**그대로 살릴 것**: 서버 권위 패턴, NetworkedPlayerHealth(체력+감속+리스폰), 지연 보상 사격,
NetworkRig(아바타), LocalPlayerBinder(로컬/원격 분리), 세션 시작/스폰 흐름.
**교체할 것**: BikeInputData → HandInputData(클러치 상태 + 목표점 + 에임 레이 + 제스처 플래그),
NetworkedMotorcycle → FlyingCharacter의 스프링 모델을 FixedUpdateNetwork로 이동.
오토바이 관련 필드는 레이스 모드 부활 전까지 보존만.

### 기존 싱글 데모 (Scripts\ 루트)
몬스터/웨이브/UI 등 20개 — PvP 슬라이스 범위 밖. 삭제하지 말 것 (Hyun의 수업 제출물 이력).
멀티 맥락에서 재사용 시 FindFirstObjectByType 단일 플레이어 가정에 주의.

## 7. 로드맵 (마감 2026-11-18 역산 — 작성일 기준 6주)

- **주0 (즉시, 병행)**: Meta Start 멤버십 확인/신청 + Devpost 등록 + 공식 룰 정독.
- **P0 — 주1: 프로토 느낌 검증 (헤드셋 필수)**: HandProto 씬 구성 → 클러치-끌기 루프/조준 안정성/
  팔 피로 체크 (`HANDPROTO_GUIDE.md` 4절 체크리스트). 여기서 재미없으면 조종 방식부터 재논의.
- **P1 — 주2: 전투 어휘 확장**: 제스처 2~3개 추가 (손바닥 밀기=넉백, 두 손 모으기=차지샷),
  회피 재미 검증 (상대 빔 예고선 → 왼손으로 급격히 끌어 피하기), 타겟을 움직이는 봇으로.
- **P2 — 주3~4: Fusion 이식**: SDK 임포트 → 컴파일 에러 해결 → HandInputData 설계 →
  스프링 모델 서버 이동 → 헤드셋 2대 PvP 검증 (`SETUP_GUIDE.md` 테스트 절차 재활용).
- **P3 — 주4~5: 매치 구조**: 라운드/점수/리스폰 규칙, 관람석(스폰) 배치, 간단 로비,
  손만으로 되는 메뉴/일시정지 (제출 요건!).
- **P4 — 주5~6: 제출 폴리시**: 아트 패스, 사운드, 튜토리얼(조작 학습 30초 목표), 트레일러 영상,
  제출물 패키징. **마감 1주 전 제출 가능 상태가 목표.**
- **컷 라인 (일정 밀릴 때 버리는 순서)**: P2 멀티를 봇전으로 대체 → P1 제스처 수 축소.
  싱글 봇전이라도 "손맛 완성도"가 높으면 수상권. 멀티는 차별점이지 필수가 아님.

## 8. 튜닝 노브 (헤드셋 쓰고 정하는 값들)

| 노브 | 기본값 | 효과 |
|---|---|---|
| HandPuppeteerController.positionScale | 60 | 손 1cm당 캐릭터 이동. 크면 거칠고 작으면 클러치 노가다 (40~100 탐색) |
| FlyingCharacter.stiffness / damping | 10 / 5 | 기민 vs 둥실. 히어로 느낌은 기민 쪽 |
| FlyingCharacter.glideDrag | 0.8 | 클러치 해제 후 활공 길이 |
| 클러치 임계값 | 0.7 / 0.45 | 오발동↑ → 0.8, 안 잡힘 → 0.6 |
| HandGestureTracker 거리 파라미터 | fist 0.10/0.05, pinch 0.06/0.015 | 손 크기 보정 |
| 아레나 크기/거리 | 35×20×35m, ~20m 앞 | 목 피로와 가독성의 균형 |

## 9. 인계받은 Claude의 첫 액션

1. 디바이스 브리지로 `E:\A_4\A_4` 현재 상태 확인 (Networking/HandProto 폴더 존재, Photon 임포트 여부,
   새로 생긴 씬/스크립트) — 연결 안 되면 Hyun에게 데스크톱 앱 실행 요청
2. Hyun에게 확인: HandProto 헤드셋 테스트 해봤는지 + 느낌, 대회 마감일
3. 그 답에 따라 P0 완료 또는 P1/P2 진입
4. 컴파일 에러가 보고되면: Fusion 미임포트가 원인인지 먼저 구분할 것 (`Networking\`만 에러면 그것)
