# PROGRESS

상태 값: TODO / IN_PROGRESS / DONE / BLOCKED(<이유>)

| 태스크 | 상태 | 메모 |
|---|---|---|
| T0 작업 기반 | DONE | Phase 0에서 완료: git·브랜치·태그·`.gitignore`·`AUTO/`·`Networking~`·`compile_check.ps1`. Unity 6000.6.4f1 업그레이드 후 `compile_check.ps1` → `RESULT: OK`. 첫 배치 실행 이후 컴파일 체크 1회는 약 1–2분. |
| T1 순수 로직 어셈블리 + 테스트 | DONE | `Scripts/Core/` + `HandHero.Core.asmdef`(참조 없음): `HysteresisGate`(임계값을 매 스텝 받아 인스펙터 실시간 튜닝 유지), `SpringFlightModel.Step` + `ArenaBounds`, `ClutchMapper`. FlyingCharacter·HandPuppeteerController·PointingBeamController가 Core를 호출하고 인스펙터 필드명·기본값은 그대로. `Tests/EditMode/` 19개 통과(원래 FlyingCharacter 수식과 1스텝 일치 테스트 포함). 에디터 재직렬화 변경은 별도 커밋 `a519806`. |
| T2 입력 추상화 | DONE | Core: `HandInputData`(+`HandGestures` 플래그, T5용 ChargeHeld/Shockwave 예약), `IHandInputSource`, `HandClutchSampler`(입력 쪽 히스테리시스+델타), `ClutchMapper`(시뮬레이션 쪽 상대 매핑으로 분리), `ScriptedHandInputSource`. 씬용: `HandProto/Input/`의 `XRHandsInputSource`·`DebugKeyboardMouseInputSource`(우클릭 드래그/WASDQE=클러치, 휠=깊이, 커서=조준, 좌클릭·Space=발사). **주먹·핀치 임계값 4개는 같은 이름·기본값으로 XRHandsInputSource로 옮겼다**(이 컴포넌트를 쓰는 씬이 없어 잃은 값 없음). `HandHeroSceneBuilder.BuildAll` → `Assets/MyAssets/Scenes/HandHero_Sandbox.unity` 생성(배치모드 OK). EditMode 24/24. **에디터 Play로 마우스 비행·발사를 실제로 눌러 본 확인은 헤드리스라 못 함 → Hyun 확인 필요.** |
| T3 봇 상대 | IN_PROGRESS | |
| T4 전투 규칙 | TODO | |
| T5 제스처 2개 | TODO | |
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
