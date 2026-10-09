# PROGRESS — Round 3 (freeze hunt, performance, bug fixes)

상태 값: TODO / IN_PROGRESS / DONE / BLOCKED(<이유>)

| 태스크 | 상태 | 메모 |
|---|---|---|
| P1 PerfSpikeLogger | DONE | `51a5866`. Core에 `FrameSpikeDetector`(실제 시간 간격)·`TrackedEdge`·`PerfFlushPolicy`·`RingBuffer<T>`·`PerfSample`·할당 없는 `PerfLogFormatter` + 테스트 19개(340/340). `PerfSpikeLogger`를 두 씬의 `Match`에 붙임(배선 오류 0). 손·머리 추적, 착용 감지, 포커스, 앱 일시정지, 매치/런 단계, 게임 일시정지 엣지 기록. 튜토리얼도 전투로 보고 그동안은 파일에 안 씀. 시스템 제스처는 `PerfSpikeLogger.Mark`로 P9에서 연결. CR-2, CR-3(코드 부분), RS-1, RS-15 해결. |
| P2 빌드 2종·로그 정리 | DONE | `BuildQuestApkDev`/`BuildQuestApkRelease` → `HandHero_<yyyyMMdd_HHmm>_dev/_release.apk`, `BuildQuestApk`는 릴리스 별칭. 개발 APK 빌드로 경로 확인: `MetaAwards\Build\HandHero_20261008_2241_dev.apk`(126 MB, 6.2분). `HHLog.Info`(에디터·개발 전용)를 만들고 수다스러운 로그 1건(손 서브시스템 보고)을 옮김. 게임 코드의 나머지 `Debug.Log`는 퍼프 로거 flush뿐이라 릴리스에도 남김. 릴리스 스택 트레이스 Log·Warning → None은 빌드하는 동안만 적용하고 끝나면 되돌린다(`QUESTIONS_FOR_HYUN.md`). RS-2, RS-3, CR-4 해결. 테스트 340/340. |
| P3 매 프레임 낭비 제거 | DONE | `08ce4aa`. Core `HudKey`+`RunHudText.StatusKey/IntroKey/EndKey`로 `MatchHud`가 보이는 정수가 바뀔 때만 문자열을 다시 만든다(퀵 매치 점수·배너 포함). `HeroStatsCache`를 `RunStateMachine`·`RunHeroStats`가 같이 쓴다. 조준 후보 인덱스 루프(봇은 원뿔 0°라 스캔 생략), 카메라 캐시, 링·쇼크웨이브 링·바닥 표시는 바뀔 때만 쓰기, 손 서브시스템 탐색 0.5초 재시도, 착용 체크 0.25초, 메뉴 레이는 내장 UI 레이어만 검사. 테스트 353/353(새 13개), 씬 재생성 배선 오류 0. GC-1, GC-2, GC-6~9, GC-M2, GM-1~5, GM-7, GM-9, GM-11, GM-12 해결. 주의: 메뉴 버튼 콜라이더가 UI 레이어(5)로 바뀜(`QUESTIONS_FOR_HYUN.md`). |
| P4 머티리얼·렌더러 캐시 | DONE | `25d07ba`. `HeroHealth`의 피격 번쩍임·스턴 색을 `MaterialPropertyBlock` 하나로(속성 ID 캐시) 칠하고, 원래 색은 블록을 지워서 되돌린다. 봇 하나당 머티리얼 4개 복제·누수가 없어짐. `SetVisible`은 Awake에서 캐시한 렌더러·콜라이더 배열을 쓴다. `PrototypeTarget`·클러치 표시기도 프로퍼티 블록, `HandMenuButton`은 속성 ID. 테스트 353/353, 씬 재생성 배선 오류 0. GC-4, GC-5, GC-10, GM-13, SP-2, RS-5, BR-11 해결. 기기 확인: 피격 번쩍임(흰색)·스턴(보라) 색이 예전과 같은지. |
| P5 풀 코어 + BeamImpact 풀 | DONE | `9b89f55`. Core `ObjectPool<T>`(미리 생성, 상한, 상한에선 null, 두 번 반환·남의 물건 무시, `ReleaseAll`) + 테스트 9개(362/362). 씬 루트의 `BeamImpactPool`(미리 8개, 최대 24개)을 모든 히어로가 같이 쓰고, `ImpactFlash`는 끝나면 풀로 돌아간다. 프리팹이 다르거나 풀이 없는 씬은 예전 Instantiate 경로로 동작. 두 씬 재생성, 배선 오류 0. SP-3, GC-3, RF-2(이펙트 부분) 해결. 기기 확인: 피격 이펙트가 예전처럼 보이는지. |
| P6 RunBot 생명주기·풀 | DONE | `e4d5541`. `RunBot.Activate/Deactivate`(스폰 위치→체력 배율·리셋·부활→적·배율→새 시드→조종), `Died` 이벤트는 풀 생성 때 한 번만 구독. `RunDirector`는 런 시작 때 `MaxAlive+1`개를 미리 만들고(상한 2배), 처치 후 0.3초 뒤·섬 전환 때 즉시 비활성화해 풀로 돌린다(Destroy 없음). 퍼펫티어 `OnDisable` 클러치 리셋, 빔 `OnDisable`에서 빔·대기 발사 정리. Core `BotBrain.Reseed`·`SpawnSeed`·첫 발사 ×U(0.5,1.0) + 테스트 5개(367/367, 기존 봇 테스트 그대로 통과). 씬 재생성 배선 오류 0. SP-1, SP-4, SP-6, SP-9, RF-1, BR-4a, GM-10, GM-M1, BC-M1, BC-2 해결. 주의: 지터는 퀵 매치 봇에도 적용(`QUESTIONS_FOR_HYUN.md`). 기기 확인: 런에서 봇 2마리가 따로 움직이고 따로 쏘는지, 섬 전환 때 끊김이 줄었는지. |
| P7 비행·손 입력 견고화 | DONE | `4342b24`. 스프링을 지수 감쇠 + 1/60초 넘는 프레임은 약 1/90초 서브스텝(최대 4)으로 바꿔 끊김 뒤 뒤로 튀지 않음. 클러치·커서 목표를 아레나 안으로 제한(벽에서 되돌리면 바로 움직임). 입력 소스가 다시 켜질 때 클러치·손바닥 밀기·양손 모으기 상태 리셋, 손바닥 밀기는 0.1초 넘는 프레임을 속도로 안 읽고 손 스무딩과 같은 unscaled dt 사용. 추적이 다시 잡힌 첫 프레임은 스냅. 부활 시 클러치 리셋·차지·대기 발사 취소. 테스트 380/380(새 13개, 기존 `SingleStep` 테스트는 D5에 맞게 수정). BC-3, BR-12, RS-7, CR-5(고정 스텝 부분), BC-4, BR-13, BR-2, BR-3, BR-1, CR-8 해결. 기기 확인: 끊김 뒤 회복, 벽 드래그, 추적 끊긴 뒤 다시 잡기, 부활. |
| P8 런 사망·상태 버그 | DONE | `b9b0070`. 런 중 자동 부활 끔(시작 때 끄고 끝날 때 켬) → DEFEAT 뒤 히어로가 다시 나타나지 않음. 플레이어 사망은 런이 살아 있는 모든 단계·일시정지 중에도 처리(부활 또는 DEFEAT), 일시정지 프레임의 처치도 셈. 매치 단계가 바뀌면 런을 바로 끝냄. `RunChoiceMenu` 람다. 팀(Player/Bot)으로 봇끼리 아군 사격 없음(빔이 통과, 체력에서도 한 번 더 거름). 포털 0개 → Arena+Random, MaxAlive≤0 → 1, 트리거 settle 0, 튜토리얼 예고 시간 0, 손목 누르기는 편 손만. 테스트 394/394(새 14개, 기존 2개를 D8에 맞게 수정), 씬 재생성 배선 오류 0. BR-4b/c, BC-1, BR-5, BR-8, BR-9, BR-10, BR-6, BC-5~9 해결. 주의: 새 API 테스트는 구현과 같이 써서 따로 실패를 돌려 보지는 않음. 기기 확인: DEFEAT 화면 뒤 히어로 없음, 봇 2마리일 때 서로 안 맞음, 손목 일시정지는 편 손만. |
| P9 추적 끊김 표시·시스템 제스처 | DONE | `0492746`. 조준 손 추적이 끊기면 레티클(ASSIST)·커서 표시(CURSOR)가 회색, 잠금 링은 숨김(대상은 기억). 클러치 손이 끊기면 내 바닥 원판·낙하선이 회색(`HandInputData.AimHandLost/ClutchHandLost`, 프로퍼티 블록, 바뀔 때만). XR Hands 1.9.0의 `TryGetAimState` → `MetaAimHandState.aimFlags`의 `SystemGesture`를 손마다 읽고, 시작·끝을 퍼프 로그에 기록. Core `SystemGestureGate`(제스처 중 핀치 0, 끝난 뒤 닫힌 핀치는 한 번 펴야 함) + 테스트 7개(401/401)로 조준 손 발사·차지와 메뉴 포인터 핀치를 거름. 손목 일시정지는 일부러 제외(`QUESTIONS_FOR_HYUN.md`). 나머지 엣지(손·머리·포커스·일시정지·단계)는 P1에서 이미 연결됨. 씬 재생성 배선 오류 0. CR-1, CR-7 해결. 기기 확인: 손을 시야 밖으로 → 회색, 오른손바닥을 얼굴로 핀치 → 발사 없음, 왼손 손목 일시정지는 그대로 동작. |
| P10 ASSIST 차지 오인 | IN_PROGRESS | |
| P11 합성 효과음 | TODO | |
| P12 피격·처치 타격감 | TODO | |
| P13 런 기록·요약 스크립트 | TODO | |
| P14 메타 진행 설계 문서 | TODO | |
| P15 빌더 엄격화·씬·테스트·APK 2종 | TODO | |
| P16 최종 리뷰·보고서 | TODO | |

## 바꾼 ProjectSettings

(태스크가 바꿀 때마다 키·이전 값·새 값을 적는다.)

- P2: `m_StackTraceTypes`는 파일상 그대로(전부 ScriptOnly). 릴리스 빌드 중에만 Log·Warning → None.
- P7: `TimeManager.asset` Maximum Allowed Timestep 0.3333 → 0.1, Fixed Timestep 0.01 → 0.02 (`BuildScript.ApplyTimeSettings`, `ConfigurePlayer`에서도 호출).
- P1: `ProjectSettings.asset` `enableFrameTimingStats` 0 → 1 (`BuildScript.ApplyDiagnosticsSettings`, `ConfigurePlayer`에서도 호출).

## 세션 로그

- 2026-10-08 Phase 0 (대화형): 브랜치 `perf/freeze-hunt`(`19a5942`에서 분기). 감사 워크플로(에이전트 13개, 읽기 전용) 결과를 `AUTO/PERF_AUDIT.md`·`AUTO/perf_audit_raw.json`에 저장. 2차 무인 기록은 `AUTO/archive/2026-10-08/`로 옮김. `run_autonomous.ps1`이 "resets 3pm" 형식의 한도 메시지도 읽도록 고침. 시작 시점 테스트 321/321.
- 2026-10-08 무인 세션 1: P1, P2 완료. 테스트 340/340, 씬 재생성 배선 오류 0, 개발 APK 1개 빌드. 막힌 것 없음.
- 2026-10-08 무인 세션 2: P3, P4 완료. 테스트 353/353, 씬 재생성 배선 오류 0. 막힌 것 없음. ProjectSettings 변경 없음(메뉴 레이어는 내장 UI 레이어 사용).
- 2026-10-08 무인 세션 3: P5, P6 완료. 테스트 367/367, 씬 재생성 배선 오류 0. 막힌 것 없음. ProjectSettings 변경 없음.
- 2026-10-08 무인 세션 4: P7, P8 완료. 테스트 394/394, 씬 재생성 배선 오류 0. 막힌 것 없음. ProjectSettings 변경: `TimeManager.asset`(P7).
