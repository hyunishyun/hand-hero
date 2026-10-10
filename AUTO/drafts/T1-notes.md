# T1 초안 메모 — 남은 Minor 13건 (브랜치 `draft/r5b-T1`, 컴파일 전)

통합하는 에이전트가 `AUTO/QUESTIONS_FOR_HYUN.md`와 `AUTO/PROGRESS.md`에 옮길 내용이다.
이 초안은 Unity 없이 썼다. 컴파일, 테스트, 씬 재생성은 통합할 때 한다.

## 대신 내린 결정 (형식: 태스크 / 질문 / 택한 답 / 이유 / 뒤집으려면)

| 태스크 | 질문 | 택한 답 | 이유 | 뒤집으려면 |
|---|---|---|---|---|
| T1 / S7-1-1 | 시야 규칙이 실제로 배치를 버리는지 어떻게 확인할까? 첫 스폰(인덱스 0)까지 반드시 보이게 할까? | 테스트만 더했다. 섬 높이만 한 넓은 벽 1개 + 바닥 가까운 스폰 1개로 시야 규칙만 배치를 버릴 수 있게 만들고, 같은 시드의 첫 시도(시도 1번)가 막힌 시드가 10개 넘게 나오는지, 시도 200번이면 모든 시드가 대체 배치 없이 열린 시야로 끝나는지 본다. 규칙은 "스폰 하나라도 보이면 됨" 그대로 둔다. | 인덱스 0 규칙(선택 사항)을 넣으면 지금 시드의 배치가 모두 바뀐다. T3가 같은 파일을 크게 고친다. Python으로 같은 난수(.NET `System.Random`)를 흉내 내 보니 첫 시도가 막힌 시드는 100개 중 24개, 200번 시도로 실패한 시드는 0개였다. | `ArenaLayout.HasOpeningSightLine`에서 `spawns[0]`만 검사하게 바꾸고 테스트를 고친다. |
| T1 / S7-1-2 | 규칙을 만족하는 배치가 없고 대체 배열도 없으면 무엇을 돌려줄까? | 대체 배열이 있으면 그 복사본을, 없으면 `null`을 돌려준다. 시도하다 만 배열은 절대 돌려주지 않는다. `UsedFallback`은 `true`다. | 반쯤 놓인 배열은 놓이지 않은 조각을 (0,0,0)에 남긴다. `null`이면 호출부가 자기 배치를 그대로 쓴다. 지금 호출부(`ArenaLayoutApplier`)는 항상 기본 배치를 넘기므로 게임 동작은 같다. | `ArenaLayout.Generate` 마지막 `return`에서 `: null`을 `: pieces` / `: spawns`로 되돌린다. |
| T1 / S8-1-2 | 권한 대화상자를 답 없이 닫으면? | 이 실행 동안은 거부로 본다(다시 묻지 않는다). `perf_log`에 새 이벤트 `PERMISSION_DISMISSED`를 남긴다. | 계획대로다. 거부와 구별해서 기록하면 기기에서 무엇이 일어났는지 알 수 있다. 새 enum 값은 맨 끝(코드 10)이라 기존 코드 값은 그대로다. | `RoomScanProbe.RequestPermission`의 `PermissionRequestDismissed` 구독 한 줄을 지운다. |
| T1 / S8-1-3 | 매니저를 켜다가 예외가 나면 무엇을 기록할까? | `UNAVAILABLE`(사유 `failed: …`)을 한 번만 남기고 그 뒤로는 STARTED, STOPPED, SUMMARY를 남기지 않는다. 서브시스템이 모두 죽은 경우도 같다. | 계획대로다. 예전에는 실패 뒤에도 STARTED(서브시스템 0), STOPPED가 이어져서 로그가 헷갈렸다. 판단은 Core `RoomScanFlow.OnFailed()`가 한다. | `RoomScanFlow.Step`의 `if (_failed) return c;`를 지우고, 프로브 `Update`의 `if (!SetManagers(...)) return;`을 예전처럼 반환값 없이 부른다. |
| T1 / S8-2-1 · F2-3 · F4-2 | 권한 확인(JNI)을 얼마나 자주 부를까? | 답을 아직 모를 때만, 처음엔 바로, 그 뒤로는 0.5초에 한 번(`permissionPollInterval`). 허용, 거부, 닫음, 서브시스템 없음, 실패 뒤에는 부르지 않는다. | 계획대로다. 답을 안 뒤에는 `Step`이 그 값을 쓰지 않는다. | `RoomScanProbe`의 `permissionPollInterval`을 0으로 두면 답을 모를 때 매 프레임 부른다. |
| T1 / F2-2 | MR TABLE 메뉴를 떠나는 프레임의 일을 어떻게 줄일까? | (1) 콘솔 로그(`Report`, 서브시스템 확인)는 `HHLog.Info`로 바꿨다. 릴리스 빌드에서는 호출 자체가 없어진다. `perf_log` 기록은 그대로다. (2) 요약과 매니저 끄기를 2프레임 뒤로 미뤘다(`stopDelayFrames` = 2). 그동안 매니저는 매치 안에서 2프레임 더 돈다. | 미루는 일은 Core `RoomScanFlow.StopDelayFrames`가 맡아서 테스트할 수 있다. 경고(`LogWarning`)는 드물어서 그대로 둔다. | `RoomScanProbe`의 `stopDelayFrames`를 0으로 둔다. 콘솔 로그는 `HHLog.Info`를 `Debug.Log`로 되돌린다. |
| T1 / F1-2 | STARTING RELIC 화면에 머문 시간을 런 시간에서 뺄까? | 뺀다. `RunTime`은 섬 1 인트로부터 센다. 최단 승리 기록(`MetaProgress`)도 `RunTime`을 쓰므로 함께 빠진다. 상자, 상점, 포털 선택 시간은 그대로 센다. | D3 결정이다. 4차 S5-2와 반대라서 테스트 `RunTime_CountsTheRelicChoice_LikeEveryOtherChoice`를 일부러 `RunTime_LeavesOutTheStartRelicChoice`로 바꿨다(커밋 메시지에 적었다). | `RunStateMachine.Tick`에서 `if (Phase != RunPhase.StartRelic)`를 지우고 테스트를 되돌린다. |
| T1 / F3-3 | 런 중에 RESET PROGRESS를 누르면 그 런은 어떻게 할까? | 그 런은 끝날 때 메타에 아무것도 쓰지 않는다(런 수, 최고 기록, 해금, Save 모두 없음). 포기(Quit)도 같다. 런 기록(`run_log.jsonl`) 한 줄은 그대로 남는다. 다음 런부터는 평소대로 쓴다. | D3 결정이다. `MetaProgress.OnRunStarted()`와 `RunVoided`를 더했다. `run_log`에 "초기화됨" 필드는 넣지 않았다. T0·T3가 `RunRecord`를 함께 고쳐서 충돌을 줄이려고 했다. | `RunDirector.BeginRun`의 `_meta.OnRunStarted();`를 지운다. |
| T1 / F3-4 | Gunner 색을 무엇으로 바꿀까? | 몸통은 연두(chartreuse) (0.6, 1, 0.1), 색상각 약 87°. 빔은 밝은 연두 (0.75, 1, 0.3), 약 81°. 보스의 Gunner 패턴 빔도 같이 바뀐다. | 과녁 주황(25°)과 CURSOR 주황(32°)에서 55–62° 떨어진다. 적 계열(빨강, 보라, 분홍)과 플레이어 파랑에서도 멀다. 체력바 초록(129°)과는 42°, 조준 노랑(60°)과는 27° 차이다. 예전 빔은 CURSOR 주황과 똑같았다. | `BotArchetypes.Get`의 Gunner `BodyColor`와 `GunnerAttack.BeamColor`. 씬의 `RunDirector` > Archetypes에서도 바로 바꿀 수 있다. |
| T1 / F4-1 | 지느러미 색 | 빨간 계열 그대로 둔다(빌더가 `Fin_L`·`Fin_R`을 `baseColorRenderers`에서 뺀다). 맞았을 때 번쩍이는 효과는 그대로 받는다. | 4차 S6-3 결정과 코드를 맞췄다. | `HandHeroSceneBuilder.ArchetypeShapes`의 `new[] { "Body" }`에 `"Fin_L", "Fin_R"`을 다시 넣는다. |
| T1 / F4-3 | 어떤 필드에 `[Tooltip]`을 달까? | 지적된 필드(`idleText`, `confirmText`, `doneText`, `RenderWarmup.vignette`, `BotArchetype`의 `Id`·`Shapes`·`Attack`)에 더해, 같은 파일의 `ResetProgressButton.run`과 `RunParams`의 `EliteHealthMult`·`EliteDamageMult`·`BossHealthMult`에도 달았다. | 규칙 5b.3. 값은 바뀌지 않는다. | (해당 없음) |
| T1 / SECOND_PC_SETUP | 0절을 고칠까? | 고치지 않았다. | Phase 0 커밋(`ae71d0f`)에서 이미 고쳐져 있다. 지금은 "브라우저 계정에 따라 CLI가 개인 계정으로 승인될 수 있다"는 함정 설명이다. | (해당 없음) |

## PROGRESS.md에 넣을 줄 (T1 DONE 때)

- Minor 13건 모두 수정: 배치(S7-1-1 테스트, S7-1-2 대체 배열 없으면 null), 방 스캔 프로브(S8-1-2 대화상자 닫음 = 거부 + `PERMISSION_DISMISSED`, S8-1-3 실패는 `UNAVAILABLE` 한 줄만, S8-2-1 권한 확인 0.5초 간격·답을 알면 중단, F2-2 콘솔 로그는 개발 빌드만·요약과 종료는 2프레임 뒤).
- F1-2 STARTING RELIC 시간은 런 시간·최단 기록에서 제외(테스트 1개를 일부러 바꿈), F3-3 런 중 RESET이면 그 런은 메타 미기록, F3-4 Gunner 연두, F4-1 지느러미 빨강 유지, F4-3 툴팁.
- 새 테스트 22개(+ 바뀐 테스트 1개). 씬과 RunBot 프리팹을 다시 만들어야 Gunner 색과 지느러미가 반영된다.

## 기기에서 확인할 것 (보고서 체크리스트용)

1. MR TABLE에서 권한 대화상자를 바깥을 눌러 닫는다 → `perf_log`에 `ROOM_SCAN scan=PERMISSION_DISMISSED`. 앱을 다시 켜기 전에는 다시 묻지 않는다. MR TABLE, 퀵 매치, RUN은 평소대로.
2. MR TABLE 메뉴에서 10–20초 기다린 뒤 매치를 시작한다 → `SUMMARY`·`STOPPED`가 매치 시작 2프레임 뒤에 남고, 매치 첫 프레임에 프로브 때문인 `HITCH`가 없다.
3. 섬 3 이후 Gunner가 연두색이고, 과녁(주황)과 CURSOR 표시(주황)와 헷갈리지 않는다. 모든 원형의 지느러미가 빨강이다.
4. 런 중 일시정지 > RESET PROGRESS 두 번 → 런을 끝내도 NEW BEST·UNLOCKED가 없고 메뉴의 최고 기록 줄이 비어 있다. 다음 런은 평소대로 기록된다.
5. 유물이 해금된 상태에서 STARTING RELIC 화면에 30초 머문 뒤 런을 한다 → 끝 화면과 `run_log`의 시간에 그 30초가 들어가지 않는다.

## 통합할 때 볼 것

- `HandHeroSceneBuilder.cs`: `ArchetypeShapes`의 한 블록만 바꿨다(`// Round 5 T1 (F4-1)`).
- `ArenaLayout.cs`(T3 담당 파일): `ArenaLayoutResult` 주석, `Generate` 위 주석, 마지막 `return` 블록만 바꿨다. T3가 `Generate`를 다시 쓰면 "대체 배열이 없으면 null" 규칙과 테스트 3개(`ImpossibleRules_WithoutFallbackArrays_…`, `ImpossibleRules_WithOnlyFallbackPieces_…`, `SightLineRule_RejectsBlockedTries_…`)를 지켜야 한다.
- `BotArchetype.cs`(T2 담당 파일): 툴팁 3줄과 Gunner 색 두 곳만 바꿨다.
- `RunDirector.cs`: `BeginRun`에 한 줄, `FinishRecord`에 한 줄, 주석.
- 씬: `RunDirector`의 직렬화된 `archetypes`(Gunner 색)와 `RoomScanProbe`의 새 필드 2개 때문에 씬을 다시 만들어야 한다.
