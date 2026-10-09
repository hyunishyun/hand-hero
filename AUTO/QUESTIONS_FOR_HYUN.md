# QUESTIONS_FOR_HYUN — Round 4

무인 세션이 대신 내린 결정. (형식: 태스크 / 질문 / 택한 답 / 이유 / 뒤집으려면)

## S1-1 GPU 워밍업 방식
- **질문:** Unity 6.6의 워밍업 API(`GraphicsStateCollection`, `ShaderWarmup`)를 쓸까, 직접 그려서 워밍업할까?
- **택한 답:** 직접 그린다. `RenderWarmup`이 씬 로드 직후 3프레임 동안 카메라 앞 12 m에 작게(×0.05) 런 봇 1기, 빔 3색, 피격 이펙트, 처치 폭발, 비네트(알파 1/255), 차지 구슬을 그리고 풀에 돌려준다.
- **이유:** 두 API 모두 에디터 어셈블리에 있다(`UnityEngine.Rendering.GraphicsStateCollection`, `UnityEngine.Experimental.Rendering.ShaderWarmup`). 그런데 `GraphicsStateCollection`은 기기에서 먼저 `BeginTrace`/`SaveToFile`로 기록한 컬렉션이 있어야 한다(무인으로 못 만든다). `ShaderWarmup.WarmupShader`는 셰이더의 모든 변형을 굽기 때문에 URP Lit에서는 오래 걸리고, Vulkan에서는 실제 렌더 패스 상태를 몰라서 실제로 쓸 파이프라인과 다를 수 있다. 실제 프레임에서 실제 물체를 그리는 쪽이 기기의 렌더 패스와 정확히 맞는다.
- **뒤집으려면:** 씬의 `Match` 오브젝트에서 `RenderWarmup` 컴포넌트를 끄면 된다(로거 헤더에 warmup이 안 나온다). 나중에 기기에서 `GraphicsStateCollection` 트레이스를 한 번 기록하면 그 파일로 바꿀 수 있다.

## S1-2 3차 VrApi 로그 다시 보기: 30초 끊김과 시스템 레이어
- **질문:** RUN 시작 직후 30초의 Stale 프레임이 정말 게임 때문인가?
- **택한 답:** D1(미리 만들기 + 워밍업 + 작은 끊김 카운터)은 계획대로 넣었다. 다만 로그를 다시 보니 다른 원인 후보가 있어서 기록해 둔다.
- **근거:** 세션 전체 Stale 167프레임 중 146프레임이 컴포지터 레이어 수(`LCnt`)가 바뀐 시점 ±1초 안에 있다. 12:49:39–12:50:10에는 다른 프로세스(pid 3551, 앱 시작 전부터 있던 시스템 프로세스)가 레이어를 올렸다 내렸다 했고, 그 프로세스가 사라진 뒤로 Stale이 0이 됐다. 같은 구간에 게임 쪽 프레임은 가장 긴 것이 16.5 ms였다(`perf_log` flush 줄). 즉 시스템 오버레이(알림, 안내 창 등)가 뜬 것이 주원인일 수 있다.
- **확인하려면:** 다음 기기 테스트에서 RUN 시작 직후 헤드셋 안에 시스템 알림이나 안내 창이 떴는지 본다. logcat에서 `LCnt=`가 1이 아닌 구간과 Stale을 같이 본다.

## S3-1 핀치 기록: 상한 vs 런 중에만
- **질문:** 핀치 기록 리스트를 256개 상한으로 자를까, 런 중에만 쌓을까?
- **택한 답:** 런 중에만 쌓는다. 런이 시작될 때 켜고, `run_log.jsonl` 기록이 끝나면 끈다. 퀵 매치·메뉴의 핀치는 기록하지 않는다.
- **이유:** 9분 런이면 핀치가 256개를 넘을 수 있다(발사 쿨다운 0.35초). 상한을 두면 런 기록의 앞부분이 사라져서 차지 오인률 계산이 틀어진다.
- **뒤집으려면:** `PointingBeamController.Awake`의 `_pinchHolds.Recording = false;` 한 줄을 지우면 예전처럼 항상 쌓는다.

## S3-2 CURSOR에서 시스템 제스처 뒤의 방아쇠
- **질문:** 시스템 제스처 동안 차지는 취소된다. 제스처가 끝났을 때 CURSOR의 검지 방아쇠가 아직 당겨져 있으면 어떻게 할까?
- **택한 답:** 지금 규칙 그대로 둔다. 방아쇠를 계속 당기고 있으면 차지가 처음부터(0.25초 대기 뒤) 다시 쌓이고, 놓으면 그 차지가 나간다. ASSIST 핀치는 이미 한 번 펴야 다시 쏜다(3차 P9).
- **이유:** 방아쇠 입력 배선은 3차에서 기기 확인을 마쳤고, 이번 Minor는 "제스처 때 차지가 나가는 것"만 고치는 범위다. 다시 쌓이는 차지는 일부러 계속 당긴 경우에만 나간다.
- **뒤집으려면:** `XRHandsInputSource.Sample`에서 `_systemGesture.Edge == 1`일 때 `_trigger.RequireReopen()`을 부르면 방아쇠도 한 번 놓아야 다시 쏜다.

## S4-1 메타 진행 저장 키를 설계 문서와 조금 다르게
- **질문:** 설계 문서 5절의 키 표를 그대로 쓸까?
- **택한 답:** 두 키를 더하고 두 키를 뺐다.
  - 더함: `hh.meta.firstWinAim`(첫 승리의 조준 모드. "다른 모드로 승리 → Dividends"를 판정하려면 필요), `hh.meta.aims`(기록이 있는 조준 모드 목록. PlayerPrefs는 키 목록을 못 읽어서, RESET PROGRESS가 모드별 키를 지우려면 필요).
  - 뺌: `hh.meta.newUnlocks`(끝 화면이 `MetaChanges`를 바로 받으므로 저장할 필요가 없다), `hh.meta.lastStart`(카드 미리 선택 기능이 계획에 없다).
- **그 밖의 규칙:** `runs`는 끝난 런 수(중도 포기 포함)다. 그 모드의 첫 기록도 `NEW BEST`로 센다(첫 런 끝 화면에 NEW BEST가 뜬다).
- **뒤집으려면:** `MetaProgress.cs`의 키 상수와 `OnRunEnded`. 첫 기록에 NEW BEST를 안 띄우려면 `BestIsland(aim) > 0`일 때만 `NewBestIsland`를 켠다.

## S5-1 메뉴의 최고 기록 줄 위치
- **질문:** 계획은 "메인 메뉴 그리드 위에 TextMeshPro 한 줄"이다. 어디에 둘까?
- **택한 답:** 새 텍스트 오브젝트를 만들지 않고, 메뉴 배너(`HAND HERO`) 아래 작은 줄을 쓴다. 기록이 생기면 안내 문구 `point and pinch to choose` 자리에 `BEST  ISLAND 7  -  WIN 9:12`가 나온다. 지금 조준 모드에 기록이 없으면 안내 문구가 그대로 나온다.
- **이유:** 배너(4 m, 눈높이 +0.15 m)의 아래 끝과 메인 패널 그리드(2.5 m)의 위 끝이 시야각으로 이미 거의 맞닿아 있다(둘 다 약 -2.4°). 그 사이에 줄을 하나 더 넣으면 배너와 겹친다. 안내 문구는 처음 하는 사람에게만 필요하다.
- **그 밖에:** 계획의 가운뎃점(·)은 기본 폰트 아틀라스에 없을 수 있어서 다른 HUD 문구처럼 ASCII `-`를 썼다. 승리 기록은 `ISLAND 9  -  WIN m:ss`로 보인다.
- **뒤집으려면:** `MetaText.MenuBanner`가 안내 문구와 기록 줄을 함께 내도록 바꾸거나, 씬 빌더에서 MainPanel 아래쪽(AIM 버튼 밑)에 TextMeshPro를 하나 더 만들고 `MetaText.BestLine`을 넣는다.

## S5-2 시작 유물 고르는 시간도 런 시간에 들어간다
- **질문:** STARTING RELIC 화면에 머문 시간을 런 시간(최단 승리 기록)에 넣을까?
- **택한 답:** 넣는다. 상자·포털·상점에서 고르는 시간도 이미 런 시간에 들어간다.
- **뒤집으려면:** `RunStateMachine.Tick`에서 `Phase == RunPhase.StartRelic`이면 `RunTime`을 늘리지 않는다(`StartRelicTests.RunTime_CountsTheRelicChoice_LikeEveryOtherChoice`도 같이 고친다).

## S5-3 RESET PROGRESS는 일시정지 패널에만
- **질문:** 메인 메뉴에도 초기화 버튼을 둘까?
- **택한 답:** 계획대로 일시정지 패널에만 둔다(RESUME·MENU 아래 둘째 줄, 어두운 빨강). 퀵 매치나 런 중에 일시정지하면 보인다. 첫 누름은 `CONFIRM RESET`으로 바뀌기만 하고, 3초 안에 다시 눌러야 지운다. 지운 뒤 1.5초 동안 `PROGRESS RESET`이 보인다. 패널이 닫히면 확인 대기도 취소된다.
- **이유:** 실수로 눌리기 어렵게(D5). 진행 중인 런은 이미 고른 시작 유물을 그대로 가진다.
- **뒤집으려면:** 씬 빌더의 `Button_RESET PROGRESS` 블록을 지우면 버튼이 사라진다. 확인 시간은 `ResetProgressButton.confirmWindow`.

## S5-4 시작 유물 기록
- **질문:** 고른 시작 유물을 `run_log.jsonl`에 남길까?
- **택한 답:** 남긴다. 새 필드 `"start_relic":"second_wind"`(고르지 않았거나 NONE이면 `""`). 섬 1 전에 고르므로 `items` 목록이 아니라 런 필드다. JSON 버전은 1 그대로(필드 추가만).
- **뒤집으려면:** `RunRecordJson.ToJson`의 `start_relic` 한 줄.
