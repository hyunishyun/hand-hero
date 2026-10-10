# QUESTIONS_FOR_HYUN — Round 5

대신 내린 결정. (형식: 태스크 / 질문 / 택한 답 / 이유 / 뒤집으려면)

## T0 ASSIST 핀치 놓기

1. **T0 / 누르기 판정: 강도 0.8인가, Meta 플래그가 켜지는 순간인가 / 강도 0.8 유지.**
   - 이유: Meta 신호가 이 펌웨어에서 오는지 아직 모른다(D1 위험). 누르기를 어느 기기에서나 같게 둔다. Meta 플래그는 놓기에만 쓴다.
   - 뒤집으려면: `PinchTrigger.Step(in PinchSample, …)`의 누르기 조건에 `sample.MetaPinching` 상승 에지를 넣는다.
2. **T0 / 누르기·놓기에 어떤 값을 쓰나 / 부드럽게 하기 전 값(`RawPinchStrength`).**
   - 이유: "최고값에서 0.2 하락"을 3프레임 안에 읽으려면 원시값이 필요하다. 부드럽게 한 값은 약 5프레임 늦다. 누르기도 같은 값을 써서 기준을 하나로 맞췄다. 그래서 누르기가 1–2프레임(15–30 ms) 빨라진다. 메뉴 포인터와 손목 메뉴는 그대로 부드럽게 한 값을 쓴다.
   - 뒤집으려면: `XRHandsInputSource.Sample()`에서 `Strength = aimHand.PinchStrength`로 바꾼다.
3. **T0 / Meta 플래그가 꺼지면 언제 놓나 / 이번 누름에서 플래그가 한 번 켜진 뒤 2프레임 연속 꺼지면 놓는다. Meta의 "Valid" 플래그가 있는 프레임만 Meta 값으로 본다.**
   - 이유: Meta 플래그는 완전히 쥐었을 때만 켜진다. 천천히 쥐면 강도 0.8을 먼저 넘는다. 그 사이 꺼져 있는 것을 놓기로 읽으면 차지가 아예 안 된다.
   - 뒤집으려면: Meta를 끄려면 인스펙터에서 `metaPinchReleaseFrames = 0`으로 둔다. 조건을 바꾸려면 `PinchTrigger.ReleaseRule`의 `_metaSeen` 조건을 고친다.
4. **T0 / 쉬는 엄지(0.71)에서 다시 누를 때 / 놓인 뒤 가장 낮았던 값보다 같은 0.2만큼 올라와야 다시 누른다.**
   - 이유: 놓는 지점에서 손이 떨려도 두 번 쏘지 않게 하는 히스테리시스다. 실제 핀치는 1.0까지 올라가므로 쉽게 넘는다.
   - 뒤집으려면: 인스펙터의 `pinchRelativeRelease` 하나로 놓기와 다시 누르기가 함께 바뀐다(0 = 예전 절대 기준만).
5. **T0 / 시스템 제스처(손바닥을 헤드셋 쪽으로) 뒤 처리 / 시스템 제스처를 `PinchTrigger` 안에서 추적 끊김처럼 다룬다. 핀치를 떨어뜨리고, 다시 열어야 쏠 수 있다.**
   - 이유: 예전 `SystemGestureGate`는 강도가 0.6 아래로 내려가야 다시 열린 것으로 봤다. 쉬는 엄지는 0.71이라, 시스템 제스처 뒤 ASSIST 사격이 막힐 수 있었다. 쥔 차지는 예전처럼 취소된다(쏘지 않음). `HandMenuPointer`는 그대로 게이트를 쓴다.
   - 뒤집으려면: git에서 `XRHandsInputSource`의 이전 게이트 체인을 되살린다.
6. **T0 / 런 기록 필드 이름 / `min_strength`(쥔 동안 가장 낮은 강도), `release_strength`, `release_by`에 `peak_strength`와 `meta_seen`을 더했다.**
   - 이유: D2의 "최소값"은 "쥔 동안 최소 강도"로도, 한국어 요약의 "최소 거리"(= 최고 강도)로도 읽힌다. 그래서 둘 다 기록했다. `meta_seen`은 Meta 신호가 이 기기에서 실제로 오는지 보여 준다.
   - 뒤집으려면: `RunRecordJson.ToJson`에서 필드를 지운다.
7. **T0 / `release_by` 우선순위 / meta → absolute → relative 순으로 이름을 붙인다.**
   - 이유: 같은 프레임에 여러 규칙이 맞으면, absolute를 먼저 보아야 "예전 규칙도 놓았을 것"(absolute)과 "새 규칙만 놓은 것"(relative)이 갈린다.
   - 뒤집으려면: `PinchTrigger.ReleaseRule`의 검사 순서를 바꾼다.

## T1 남은 Minor 13건

8. **T1 / S7-1-1 시야 규칙이 실제로 배치를 버리는지 어떻게 확인할까? 첫 스폰(인덱스 0)까지 반드시 보이게 할까? / 테스트만 더했다. 규칙은 "스폰 하나라도 보이면 됨" 그대로 둔다.**
   - 테스트: 섬 높이만 한 넓은 벽 1개와 바닥 가까운 스폰 1개로, 시야 규칙만 배치를 버릴 수 있게 만든다. 시드 100개 중 첫 시도가 막힌 시드가 10개를 넘는지, 시도 200번이면 모든 시드가 대체 배치 없이 열린 시야로 끝나는지 본다.
   - 이유: 인덱스 0 규칙(선택 사항)을 넣으면 지금 시드의 배치가 모두 바뀐다. T3가 같은 파일을 크게 고친다. 같은 난수로 흉내 내 보니 첫 시도가 막힌 시드는 100개 중 24개, 200번 시도로 실패한 시드는 0개였다.
   - 뒤집으려면: `ArenaLayout.HasOpeningSightLine`에서 `spawns[0]`만 검사하게 바꾸고 테스트를 고친다.
9. **T1 / S7-1-2 규칙을 만족하는 배치가 없고 대체 배열도 없으면 무엇을 돌려줄까? / 대체 배열이 있으면 그 복사본을, 없으면 `null`을 돌려준다. 시도하다 만 배열은 절대 돌려주지 않는다. `UsedFallback`은 `true`다.**
   - 이유: 반쯤 놓인 배열은 놓이지 않은 조각을 (0,0,0)에 남긴다. `null`이면 호출부가 자기 배치를 그대로 쓴다. 지금 호출부(`ArenaLayoutApplier`)는 항상 기본 배치를 넘기므로 게임 동작은 같다.
   - 뒤집으려면: `ArenaLayout.Generate` 마지막 `return`에서 `: null`을 `: pieces` / `: spawns`로 되돌린다.
10. **T1 / S8-1-2 권한 대화상자를 답 없이 닫으면? / 이 실행 동안은 거부로 본다(다시 묻지 않는다). `perf_log`에 새 이벤트 `PERMISSION_DISMISSED`를 남긴다.**
    - 이유: 계획대로다. 거부와 구별해서 기록하면 기기에서 무엇이 일어났는지 알 수 있다. 새 enum 값은 맨 끝(코드 10)이라 기존 코드 값은 그대로다.
    - 뒤집으려면: `RoomScanProbe.RequestPermission`의 `PermissionRequestDismissed` 구독 한 줄을 지운다.
11. **T1 / S8-1-3 매니저를 켜다가 예외가 나면 무엇을 기록할까? / `UNAVAILABLE`(사유 `failed: …`)을 한 번만 남기고, 그 뒤로는 STARTED, STOPPED, SUMMARY를 남기지 않는다. 서브시스템이 모두 죽은 경우도 같다.**
    - 이유: 계획대로다. 예전에는 실패 뒤에도 STARTED(서브시스템 0)와 STOPPED가 이어져서 로그가 헷갈렸다. 판단은 Core `RoomScanFlow.OnFailed()`가 한다.
    - 뒤집으려면: `RoomScanFlow.Step`의 `if (_failed) return c;`를 지우고, 프로브 `Update`의 `if (!SetManagers(...)) return;`을 예전처럼 반환값 없이 부른다.
12. **T1 / S8-2-1 · F2-3 · F4-2 권한 확인(JNI)을 얼마나 자주 부를까? / 답을 아직 모를 때만 부른다. 처음엔 바로, 그 뒤로는 0.5초에 한 번(`permissionPollInterval`). 허용, 거부, 닫음, 서브시스템 없음, 실패 뒤에는 부르지 않는다.**
    - 이유: 계획대로다. 답을 안 뒤에는 `Step`이 그 값을 쓰지 않는다.
    - 뒤집으려면: `RoomScanProbe`의 `permissionPollInterval`을 0으로 두면, 답을 모를 때 매 프레임 부른다.
13. **T1 / F2-2 MR TABLE 메뉴를 떠나는 프레임의 일을 어떻게 줄일까? / (1) 콘솔 로그(`Report`, 서브시스템 확인)를 `HHLog.Info`로 바꿨다. 릴리스 빌드에서는 호출 자체가 없어진다. `perf_log` 기록은 그대로다. (2) 요약과 매니저 끄기를 2프레임 뒤로 미뤘다(`stopDelayFrames` = 2). 그동안 매니저는 매치 카운트다운 안에서 2프레임 더 돈다.**
    - 이유: 미루는 일은 Core `RoomScanFlow.StopDelayFrames`가 맡아서 테스트할 수 있다. 경고(`LogWarning`)는 드물어서 그대로 둔다. 그래서 릴리스 logcat에는 `[RoomScanProbe]` 정보 줄이 더는 나오지 않는다(`perf_log`에는 그대로 있다).
    - 뒤집으려면: `RoomScanProbe`의 `stopDelayFrames`를 0으로 둔다. 콘솔 로그는 `HHLog.Info`를 `Debug.Log`로 되돌린다.
14. **T1 / F1-2 STARTING RELIC 화면에 머문 시간을 런 시간에서 뺄까? / 뺀다. `RunTime`은 섬 1 인트로부터 센다. 최단 승리 기록(`MetaProgress`)도 `RunTime`을 쓰므로 함께 빠진다. 상자, 상점, 포털 선택 시간은 그대로 센다.**
    - 이유: D3 결정이다. 4차 S5-2와 반대라서 테스트 `RunTime_CountsTheRelicChoice_LikeEveryOtherChoice`를 일부러 `RunTime_LeavesOutTheStartRelicChoice`로 바꿨다. 이 화면에서 `RunTime`이 아예 멈추므로, 일시정지 테스트(`StartRelic_WaitsForTheChoice_AndPausesLikeAChest`)는 계속 도는 `PhaseTime`을 보도록 고쳤다.
    - 뒤집으려면: `RunStateMachine.Tick`에서 `if (Phase != RunPhase.StartRelic)`를 지우고 테스트를 되돌린다.
15. **T1 / F3-3 런 중에 RESET PROGRESS를 누르면 그 런은 어떻게 할까? / 그 런은 끝날 때 메타에 아무것도 쓰지 않는다(런 수, 최고 기록, 해금, Save 모두 없음). 포기(Quit)도 같다. 런 기록(`run_log.jsonl`) 한 줄은 그대로 남는다. 다음 런부터는 평소대로 쓴다.**
    - 이유: D3 결정이다. `MetaProgress.OnRunStarted()`와 `RunVoided`를 더했다. `run_log`에 "초기화됨" 필드는 넣지 않았다. T0·T3도 `RunRecord`를 고쳐서 충돌을 줄이려고 했다. 개발 빌드 콘솔에는 한 줄이 남는다.
    - 뒤집으려면: `RunDirector.BeginRun`의 `_meta.OnRunStarted();`를 지운다.
16. **T1 / F3-4 Gunner 색을 무엇으로 바꿀까? / 몸통은 연두(chartreuse) (0.6, 1, 0.1), 색상각 87°. 빔은 연두 (0.6, 1, 0.2), 90°. 보스의 Gunner 패턴 빔도 같이 바뀐다.**
    - 이유: 과녁 주황(25°)과 CURSOR 주황(32°)에서 55–65° 떨어진다. 적 계열(빨강, 보라, 분홍)과 플레이어 파랑에서도 멀다. 예전 빔은 CURSOR 주황과 똑같았다. 초안의 빔 (0.75, 1, 0.3)은 81°라서 ASSIST 조준점 노랑(60°)과 21°밖에 차이가 나지 않았다(초안 메모의 27°는 몸통 값이었다). 그래서 빔을 90°로 옮겨 30° 떨어뜨렸다. 몸통은 조준점과 27°, 체력바 초록(129°)과 42°, 빔은 체력바와 39°, 크리티컬 빔·코 노랑(49°)과 41° 차이다. 테스트가 과녁·CURSOR 주황과 40° 이상, 조준점 노랑과 25° 이상을 지킨다.
    - 뒤집으려면: `BotArchetypes.Get`의 Gunner `BodyColor`와 `GunnerAttack.BeamColor`. 씬의 `RunDirector` > Archetypes에서도 바로 바꿀 수 있다.
17. **T1 / F4-1 지느러미 색 / 빨간 계열 그대로 둔다(빌더가 `Fin_L`·`Fin_R`을 `baseColorRenderers`에서 뺀다). 맞았을 때 번쩍이는 효과는 그대로 받는다.**
    - 이유: 4차 S6-3 결정과 코드를 맞췄다.
    - 뒤집으려면: `HandHeroSceneBuilder.ArchetypeShapes`의 `new[] { "Body" }`에 `"Fin_L", "Fin_R"`을 다시 넣고 씬을 다시 만든다.
18. **T1 / F4-3 어떤 필드에 `[Tooltip]`을 달까? / 지적된 필드(`idleText`, `confirmText`, `doneText`, `RenderWarmup.vignette`, `BotArchetype`의 `Id`·`Shapes`·`Attack`)에 더해, 같은 파일의 `ResetProgressButton.run`과 `RunParams`의 `EliteHealthMult`·`EliteDamageMult`·`BossHealthMult`에도 달았다.**
    - 이유: 규칙 5b.3. 값은 바뀌지 않는다.
    - 뒤집으려면: (해당 없음)
19. **T1 / `SECOND_PC_SETUP.md` 0절을 고칠까? / 고치지 않았다.**
    - 이유: Phase 0 커밋(`ae71d0f`)에서 이미 고쳐져 있다. 지금은 "브라우저 계정에 따라 CLI가 개인 계정으로 승인될 수 있다"는 함정 설명이다.
    - 뒤집으려면: (해당 없음)
