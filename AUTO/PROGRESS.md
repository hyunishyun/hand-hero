# PROGRESS

상태 값: TODO / IN_PROGRESS / DONE / BLOCKED(<이유>)

| 태스크 | 상태 | 메모 |
|---|---|---|
| T0 작업 기반 | DONE | Phase 0에서 완료: git·브랜치·태그·`.gitignore`·`AUTO/`·`Networking~`·`compile_check.ps1`. Unity 6000.6.4f1 업그레이드 후 `compile_check.ps1` → `RESULT: OK`. 첫 배치 실행 이후 컴파일 체크 1회는 약 1–2분. |
| T1 순수 로직 어셈블리 + 테스트 | DONE | `Scripts/Core/` + `HandHero.Core.asmdef`(참조 없음): `HysteresisGate`(임계값을 매 스텝 받아 인스펙터 실시간 튜닝 유지), `SpringFlightModel.Step` + `ArenaBounds`, `ClutchMapper`. FlyingCharacter·HandPuppeteerController·PointingBeamController가 Core를 호출하고 인스펙터 필드명·기본값은 그대로. `Tests/EditMode/` 19개 통과(원래 FlyingCharacter 수식과 1스텝 일치 테스트 포함). 에디터 재직렬화 변경은 별도 커밋 `a519806`. |
| T2 입력 추상화 | DONE | Core: `HandInputData`(+`HandGestures` 플래그, T5용 ChargeHeld/Shockwave 예약), `IHandInputSource`, `HandClutchSampler`(입력 쪽 히스테리시스+델타), `ClutchMapper`(시뮬레이션 쪽 상대 매핑으로 분리), `ScriptedHandInputSource`. 씬용: `HandProto/Input/`의 `XRHandsInputSource`·`DebugKeyboardMouseInputSource`(우클릭 드래그/WASDQE=클러치, 휠=깊이, 커서=조준, 좌클릭·Space=발사). **주먹·핀치 임계값 4개는 같은 이름·기본값으로 XRHandsInputSource로 옮겼다**(이 컴포넌트를 쓰는 씬이 없어 잃은 값 없음). `HandHeroSceneBuilder.BuildAll` → `Assets/MyAssets/Scenes/HandHero_Sandbox.unity` 생성(배치모드 OK). EditMode 24/24. **에디터 Play로 마우스 비행·발사를 실제로 눌러 본 확인은 헤드리스라 못 함 → Hyun 확인 필요.** |
| T3 봇 상대 | DONE | Core `BotBrain`(+`BotParams`, 상태 Idle/Approach/Strafe/Evade): 클러치를 계속 쥔 채 ClutchMapper 목표를 따라 하며 손 델타를 내보내므로 **플레이어와 같은 퍼펫티어·비행 모델·속도 상한**을 쓴다(손 속도 상한 1.5m/s). 반응 지연, 조준 오차 원뿔, 발사 간격+지터, **0.6초 조준 고정 예고 후 발사**(`IsTelegraphing`/`TelegraphProgress`/`LockedAimPoint` 공개 → T4 예고선용), 피격 시 회피·예고 취소. 씬용 `BotInputSource`, `BotDifficulty`(SO), `BeamHitReceiver`(빔 피격 이벤트 → T4 체력이 구독). `PointingBeamController`는 자기 히어로만 건너뛰는 레이캐스트로 바뀜(레이어 트릭 제거). 샌드박스 씬에 빨간 BotHero 추가. EditMode 37/37. **에디터 Play로 봇이 실제로 날며 쏘는지는 헤드리스라 못 봄 → Hyun 확인 필요.** |
| T4 전투 규칙 | DONE | Core `HeroHealthModel`(ADR 3: 피격=체력↓+2초 50% 감속, 0이면 사망→3초 뒤 리스폰, 사망 중 피격 무시; `NetworkedPlayerHealth`와 같은 의미론, 대응 관계 주석). 씬용 `HeroHealth`(인스펙터 필드명·기본값은 NetworkedPlayerHealth와 동일, 흰색 피격 플래시, 체력 바, 사망 시 숨김·콜라이더 끔), `FlyingCharacter`에 `SetSpeedMultiplier`/`Kill`/`Respawn`/`IsAlive` 추가(maxSpeed만 곱함, 튜닝값 그대로). 사망 중엔 퍼펫티어·봇이 클러치를 놓아(`ClutchMapper.Reset`) 리스폰 때 쥔 주먹이 스폰 위치에서 다시 잡힘. `BotTelegraphLine`: 봇 조준 고정 0.6초 동안 노랑→빨강으로 굵어지는 예고선. 빔 데미지 20. **XR Origin/힘 관련 코드 없음(검색 확인).** EditMode 46/46. **에디터 Play로 서로 맞히고 리스폰되는 장면은 헤드리스라 못 봄 → Hyun 확인 필요.** |
| T5 제스처 2개 | DONE | Core `PalmsTogetherRecognizer`(손바닥 거리 0.10 붙음/0.18 떨어짐, 한 손 추적 손실 0.3초 유예), `PalmPushRecognizer`(손바닥 법선 방향 속도 1.2 발동/0.4 재장전, 주먹 0.35 이하만), `ChargeShotModel`(최소 0.3초·최대 1.5초, 놓을 때 발사), `HeroHealthModel.ApplyStun`(데미지 없는 감속, 피격 감속과 겹치면 더 센 쪽). 차지샷: 데미지 30→70, 굵기 1.5→4배, **차지 중 히어로 제자리 고정**, 차지 중 일반 발사·충격파 막힘. 충격파: `ShockwaveController` 반경 6m 안 다른 히어로 1.2초 15% 속도 경직(보라 틴트), 쿨다운 3초, 확장 링. 디버그 키 **C(누르고 있기)=차지, F=충격파**. EditMode 70/70(임계값 사이 진동 시 플리커 없음 테스트 포함). **손바닥 법선(`palmNormalLocal` = -Y)과 실제 손 제스처 감도는 헤드셋에서 확인 필요.** 봇은 아직 새 제스처를 쓰지 않음. |
| T6 매치 루프 | TODO | |
| T7 손 전용 UI + 일시정지 | TODO | |
| T8 30초 튜토리얼 | TODO | |
| T9 씬 빌더 + Android 빌드 | TODO | |
| T10 패스스루 테이블탑 | TODO | Q5·Q6 승인됨 |
| T11 Fusion 이식 설계 문서 | TODO | |
| T12 버퍼 | TODO | |

## 세션 로그

- 2026-10-07 Phase 0 (대화형): 환경 점검과 결정 기록을 마쳤다. 자세한 내용은 DECISIONS.md.
- 2026-10-07 Phase 0: Unity 6000.6.4f1로 업그레이드(UnityGLTF 2.22.1로 올림)하고 클린 컴파일을 확인했다. T0 DONE. 다음은 T1.
- 2026-10-07 무인 세션 1: T1 DONE (EditMode 19/19). 6.6 업그레이드로 생긴 에디터 재직렬화(URP·OpenXR·XRI·CompositionLayers 설정)를 별도 커밋으로 정리했다. 다음은 T2.
- 2026-10-07 무인 세션 1: T2 DONE (EditMode 24/24, 샌드박스 씬 생성). 세션당 2개 한도 도달로 종료. 다음은 T3(봇).
- 2026-10-07 무인 세션 2: T3 DONE (봇, EditMode 37/37), T4 DONE (전투 규칙·예고선, EditMode 46/46). 샌드박스 씬에 빨간 BotHero·체력 바 추가. 세션당 2개 한도 도달로 종료. 다음은 T5(제스처 2개).
