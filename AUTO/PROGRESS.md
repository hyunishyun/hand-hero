# PROGRESS — Round 3 (freeze hunt, performance, bug fixes)

상태 값: TODO / IN_PROGRESS / DONE / BLOCKED(<이유>)

| 태스크 | 상태 | 메모 |
|---|---|---|
| P1 PerfSpikeLogger | DONE | `51a5866`. Core에 `FrameSpikeDetector`(실제 시간 간격)·`TrackedEdge`·`PerfFlushPolicy`·`RingBuffer<T>`·`PerfSample`·할당 없는 `PerfLogFormatter` + 테스트 19개(340/340). `PerfSpikeLogger`를 두 씬의 `Match`에 붙임(배선 오류 0). 손·머리 추적, 착용 감지, 포커스, 앱 일시정지, 매치/런 단계, 게임 일시정지 엣지 기록. 튜토리얼도 전투로 보고 그동안은 파일에 안 씀. 시스템 제스처는 `PerfSpikeLogger.Mark`로 P9에서 연결. CR-2, CR-3(코드 부분), RS-1, RS-15 해결. |
| P2 빌드 2종·로그 정리 | DONE | `BuildQuestApkDev`/`BuildQuestApkRelease` → `HandHero_<yyyyMMdd_HHmm>_dev/_release.apk`, `BuildQuestApk`는 릴리스 별칭. 개발 APK 빌드로 경로 확인: `MetaAwards\Build\HandHero_20261008_2241_dev.apk`(126 MB, 6.2분). `HHLog.Info`(에디터·개발 전용)를 만들고 수다스러운 로그 1건(손 서브시스템 보고)을 옮김. 게임 코드의 나머지 `Debug.Log`는 퍼프 로거 flush뿐이라 릴리스에도 남김. 릴리스 스택 트레이스 Log·Warning → None은 빌드하는 동안만 적용하고 끝나면 되돌린다(`QUESTIONS_FOR_HYUN.md`). RS-2, RS-3, CR-4 해결. 테스트 340/340. |
| P3 매 프레임 낭비 제거 | DONE | `08ce4aa`. Core `HudKey`+`RunHudText.StatusKey/IntroKey/EndKey`로 `MatchHud`가 보이는 정수가 바뀔 때만 문자열을 다시 만든다(퀵 매치 점수·배너 포함). `HeroStatsCache`를 `RunStateMachine`·`RunHeroStats`가 같이 쓴다. 조준 후보 인덱스 루프(봇은 원뿔 0°라 스캔 생략), 카메라 캐시, 링·쇼크웨이브 링·바닥 표시는 바뀔 때만 쓰기, 손 서브시스템 탐색 0.5초 재시도, 착용 체크 0.25초, 메뉴 레이는 내장 UI 레이어만 검사. 테스트 353/353(새 13개), 씬 재생성 배선 오류 0. GC-1, GC-2, GC-6~9, GC-M2, GM-1~5, GM-7, GM-9, GM-11, GM-12 해결. 주의: 메뉴 버튼 콜라이더가 UI 레이어(5)로 바뀜(`QUESTIONS_FOR_HYUN.md`). |
| P4 머티리얼·렌더러 캐시 | DONE | `25d07ba`. `HeroHealth`의 피격 번쩍임·스턴 색을 `MaterialPropertyBlock` 하나로(속성 ID 캐시) 칠하고, 원래 색은 블록을 지워서 되돌린다. 봇 하나당 머티리얼 4개 복제·누수가 없어짐. `SetVisible`은 Awake에서 캐시한 렌더러·콜라이더 배열을 쓴다. `PrototypeTarget`·클러치 표시기도 프로퍼티 블록, `HandMenuButton`은 속성 ID. 테스트 353/353, 씬 재생성 배선 오류 0. GC-4, GC-5, GC-10, GM-13, SP-2, RS-5, BR-11 해결. 기기 확인: 피격 번쩍임(흰색)·스턴(보라) 색이 예전과 같은지. |
| P5 풀 코어 + BeamImpact 풀 | DONE | `9b89f55`. Core `ObjectPool<T>`(미리 생성, 상한, 상한에선 null, 두 번 반환·남의 물건 무시, `ReleaseAll`) + 테스트 9개(362/362). 씬 루트의 `BeamImpactPool`(미리 8개, 최대 24개)을 모든 히어로가 같이 쓰고, `ImpactFlash`는 끝나면 풀로 돌아간다. 프리팹이 다르거나 풀이 없는 씬은 예전 Instantiate 경로로 동작. 두 씬 재생성, 배선 오류 0. SP-3, GC-3, RF-2(이펙트 부분) 해결. 기기 확인: 피격 이펙트가 예전처럼 보이는지. |
| P6 RunBot 생명주기·풀 | IN_PROGRESS | 무인 세션 3 |
| P7 비행·손 입력 견고화 | TODO | |
| P8 런 사망·상태 버그 | TODO | |
| P9 추적 끊김 표시·시스템 제스처 | TODO | |
| P10 ASSIST 차지 오인 | TODO | |
| P11 합성 효과음 | TODO | |
| P12 피격·처치 타격감 | TODO | |
| P13 런 기록·요약 스크립트 | TODO | |
| P14 메타 진행 설계 문서 | TODO | |
| P15 빌더 엄격화·씬·테스트·APK 2종 | TODO | |
| P16 최종 리뷰·보고서 | TODO | |

## 바꾼 ProjectSettings

(태스크가 바꿀 때마다 키·이전 값·새 값을 적는다.)

- P2: `m_StackTraceTypes`는 파일상 그대로(전부 ScriptOnly). 릴리스 빌드 중에만 Log·Warning → None.
- P1: `ProjectSettings.asset` `enableFrameTimingStats` 0 → 1 (`BuildScript.ApplyDiagnosticsSettings`, `ConfigurePlayer`에서도 호출).

## 세션 로그

- 2026-10-08 Phase 0 (대화형): 브랜치 `perf/freeze-hunt`(`19a5942`에서 분기). 감사 워크플로(에이전트 13개, 읽기 전용) 결과를 `AUTO/PERF_AUDIT.md`·`AUTO/perf_audit_raw.json`에 저장. 2차 무인 기록은 `AUTO/archive/2026-10-08/`로 옮김. `run_autonomous.ps1`이 "resets 3pm" 형식의 한도 메시지도 읽도록 고침. 시작 시점 테스트 321/321.
- 2026-10-08 무인 세션 1: P1, P2 완료. 테스트 340/340, 씬 재생성 배선 오류 0, 개발 APK 1개 빌드. 막힌 것 없음.
- 2026-10-08 무인 세션 2: P3, P4 완료. 테스트 353/353, 씬 재생성 배선 오류 0. 막힌 것 없음. ProjectSettings 변경 없음(메뉴 레이어는 내장 UI 레이어 사용).
