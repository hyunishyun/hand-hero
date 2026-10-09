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

## S6-1 Gunner 연발 간격 0.36초(계획 예시 0.18초 대신)
- **질문:** 3연발 간격을 0.18초로 할까?
- **택한 답:** 0.36초. 봇도 플레이어와 같은 빔 컨트롤러를 쓰고, 그 연사 제한(쿨다운)이 0.35초다. 0.18초로 두면 두 번째 발이 쿨다운에 걸려 어차피 0.35초에 나간다. 그래서 실제로 나가는 간격을 그대로 적었다(봇은 플레이어 규칙을 따른다는 ADR).
- **뒤집으려면:** `RunDirector`의 Archetypes 표에서 Gunner `BurstGap`을 줄이고, 봇 프리팹의 `PointingBeamController.fireCooldown`도 같이 줄여야 실제로 빨라진다(플레이어 쿨다운은 그대로 둘 것).

## S6-2 연발의 2·3번째 발은 다시 조준한다
- **질문:** 연발 세 발이 예고선 한 곳으로 다 갈까, 매 발 다시 조준할까?
- **택한 답:** 첫 발은 예고선(고정 조준), 2·3번째 발은 그 순간 보이는 위치(반응 지연 0.35초 + 조준 오차)로 다시 조준한다. 예고선은 첫 발에만 있다. 맞아서 회피하면 남은 발은 취소된다.
- **이유:** 한 곳으로만 쏘면 첫 발만 피하면 끝이라 Striker와 다를 게 없다. 다시 조준하면 "Gunner가 쏘면 계속 움직여라"가 된다. 한 발 피해는 ×0.45라서 다 맞아도 Striker 한 발의 1.35배다.
- **뒤집으려면:** `BotBrain.UpdateShooting`의 연발 분기에서 `_lockedAim` 재계산 한 줄을 지운다(`BotAttackTests.BurstShots_ReAimAtTheTarget`도 같이).

## S6-3 생김새·색·소리
- **택한 답:** 모양이 1차 구분, 색이 2차 구분(색약 대비, T12와 같은 원칙). 모든 적은 지금처럼 빨간 계열 지느러미와 노란 코를 유지한다.
  - Striker: 지금 그대로(빨강, 추가 모양 없음)
  - Sniper: 보라(차가운 색, 플레이어의 파랑과 겹치지 않게), 위로 솟은 바늘 + 앞쪽 가는 총열. 빔은 가늘고 4배 오래 남는다. 발사음 높이 0.7
  - Gunner: 주황, 몸을 가로지르는 넓은 블록. 발사음 1.35(짧고 높음)
  - Lancer: 분홍, 앞으로 긴 창 + 가로대. 빔 굵기 ×4(차지샷 최대 굵기), 예고선 ×2.5 굵기. 발사음 0.55(낮고 김)
  - Boss: 짙은 진홍, 블록 + 창. Gunner 연발 2번 → Lancer 2번 → 반복
- **예고선:** 굵기는 원형마다 다르고, 시작 색은 그 원형의 빔 색이다. 끝은 모두 같은 빨강(빨강 = 곧 쏜다)이다.
- **뒤집으려면:** 숫자·색은 씬의 `Match` > `RunDirector` > Archetypes 표(직렬화)에서 바꾼다. 모양은 씬 빌더 `ArchetypeShapes`.

## S6-4 Lancer의 "넓은" 샷은 보이는 것만 넓다
- **질문:** Lancer 샷의 피격 판정도 넓힐까?
- **택한 답:** 넓히지 않는다. 플레이어 차지샷도 보이는 굵기만 넓고 판정은 같은 가는 레이다. 봇에게만 넓은 판정을 주면 규칙이 달라진다.
- **뒤집으려면:** `PointingBeamController.Fire`의 `RaycastIgnoringSelf`를 굵기에 맞춘 SphereCast로 바꾼다(플레이어 차지샷도 같이 바뀐다).

## S6-5 섬별 출현표
- **택한 답:** 계획의 첫 추정 그대로. 섬 1–2 Striker만, 3부터 Gunner, 5부터 Sniper, 7부터 Lancer. 열린 종류는 모두 같은 확률. Elite는 그 섬 표에서 한 종류 + Elite 배수. 보스 섬은 Boss. 섬 1–2는 난수를 쓰지 않아 예전 스폰 시드 흐름을 바꾸지 않는다.
- **뒤집으려면:** `RunDirector` > Rules의 `GunnerFromIsland` / `SniperFromIsland` / `LancerFromIsland`(0이면 기본값).

## S6-6 원형별 기록
- `run_log.jsonl`에 `kills_by`(원형별 처치 수)와 `damage_by`(원형별로 플레이어가 받은 피해)를 더했다. `run_summary.py`가 "By bot archetype (kills / damage taken)" 줄과 시작 유물 집계를 출력한다.

## S7-1 지형 배치 규칙 기본값
- **질문:** 기둥·봇 스폰 지점 무작위 배치의 공정성 숫자를 얼마로 둘까?
- **택한 답:** (아레나 로컬, m) 벽에서 1.5 안쪽, 기둥끼리 가장자리 간격 3, 플레이어 시작점 반경 5와 스폰 지점 반경 3 안에는 기둥 없음(기둥은 바닥 대각선의 반을 반지름으로 보는 원), 스폰 지점끼리 5 이상, 스폰 영역은 먼 쪽 상자(중심 (0, 5, 13), 크기 20×6×6: 지금 스폰 (6,3,12)·(-7,5,12)·(1,7,15)를 모두 담는다). 시작점에서 스폰 지점 하나 이상은 기둥에 가리지 않는다(3D 선분 대 기둥 상자). 시도 200번 안에 못 찾으면 지금 배치 그대로(경고 로그).
- **이유:** 시드 0–199 전부 규칙을 만족하고(테스트), 지금 배치와 비슷한 밀도다.
- **뒤집으려면:** 씬의 `Match` > `ArenaLayoutApplier` > Rules (직렬화). 무작위 배치를 끄려면 `RunDirector`의 `Layout` 칸을 비운다.

## S7-2 기둥은 플레이어 영웅보다 앞쪽(+z 2 이상)에만, 과녁과 겹치지 않게
- **질문:** 계획에 없는 제약을 더해도 될까?
- **택한 답:** 두 가지를 더했다. (1) 기둥 중심은 시작점보다 +z로 2 m 이상 앞 → 좌석과 플레이어 영웅 사이에 기둥이 서서 내 영웅을 가리는 일이 없다. (2) 회색 과녁 4개(`Target_1..4`, 2 m 상자)는 그대로 두고 기둥이 0.5 m 이상 떨어진다(겹치면 과녁이 기둥 속에 묻힌다). 둘 다 테스트가 있다(`PiecesStayOnTheFarSideOfThePlayerStart`, `PiecesKeepOffTheKeepClearBoxes`). 미리 써 둔 테스트는 고치지 않았다.
- **뒤집으려면:** Rules의 `MinPieceForward`를 -100으로, `ArenaLayoutApplier`의 `Keep Clear` 배열을 비운다.

## S7-3 배치 시드는 런마다 따로
- **질문:** 섬 배치 시드의 "런 시드"로 무엇을 쓸까?
- **택한 답:** 런 기록의 `seed`(`_rngSeed`)는 씬 로드 때 한 번만 정해져서 같은 세션의 런이 모두 같은 값이다. 그대로 쓰면 런마다 섬 배치가 똑같다. 그래서 런 시작 때 별도 값(`seed`가 0이면 TickCount, 아니면 `seed*53+29`)을 뽑고, 섬마다 `ArenaLayout.IslandSeed(그 값, 섬 번호)`를 쓴다. 상자·포털·상점·봇 스폰 난수열은 하나도 더 쓰지 않는다(예전과 같은 흐름). 재현은 `run_log.jsonl`의 섬별 `layout_seed`로 한다(배치가 적용된 섬에만 기록, 대체 배치면 기록 없음).
- **뒤집으려면:** `RunDirector.BeginRun`의 `_layoutRunSeed` 한 줄.

## S8-1 방 스캔은 평면 + 바운딩 박스만, 메시는 끔
- **질문:** Meta OpenXR의 Planes, Bounding Boxes, Meshing 중 무엇을 켤까?
- **택한 답:** Android에서 `Meta Quest: Planes`와 `Meta Quest: Bounding Boxes`만 켰다(에디터 스크립트 `RoomScanXRSettings.Apply`, `OpenXRPackageSettings.asset`의 `m_enabled` 두 줄). Meshing은 끈 채로 둔다.
- **이유:** 계획은 메시를 "싸면" 넣으라고 했다. `ARMeshManager`는 XR Origin 자식 + 메시 프리팹 + 렌더링이 필요하고, 방 모양 그대로의 데이터라 개인정보 면에서도 무겁다. 테이블 위 지형 1단계에는 테이블 평면과 가구 박스로 충분하다.
- **뒤집으려면:** 두 기능을 끄려면 Project Settings > XR Plug-in Management > OpenXR(Android 탭)에서 체크를 해제하거나 그 두 줄을 0으로. 씬의 `Match` > `RoomScanProbe`의 Probe Enabled를 끄면 권한도 묻지 않는다.

## S8-2 프로브는 MR TABLE + 메인 메뉴에서만
- **질문:** MR TABLE에서 매치·런을 하는 동안에도 평면·박스 매니저를 켜 둘까?
- **택한 답:** 메인 메뉴에 있을 때만 켠다. 매치(퀵 매치·튜토리얼·RUN)가 시작되면 멈추고 요약을 한 번 남긴다. 메뉴로 돌아오면 다시 켠다(권한은 다시 묻지 않음). 첫 결과 시간은 처음 켠 때만 잰다.
- **이유:** Meta는 실시간 스캔이 아니라 공간 설정 데이터를 돌려주므로 메뉴에서 몇 초면 다 나온다. 게임 중에는 프레임에 영향이 0이어야 한다(계획: VR·퀵 매치·RUN을 절대 깨뜨리지 않기).
- **뒤집으려면:** `RoomScanProbe.InMenu()`가 항상 true를 돌려주게 한다.

## S8-3 권한은 세션당 한 번, 거부하면 다시 묻지 않음
- **질문:** `com.oculus.permission.USE_SCENE`을 언제, 몇 번 물을까?
- **택한 답:** MR TABLE을 처음 켰을 때 한 번만. 거부하면 그 실행 동안 다시 묻지 않고 매니저를 켜지 않는다(MR TABLE은 지금과 같음). 앱을 다시 켜면 MR TABLE에서 다시 한 번 묻는다(Android가 "다시 묻지 않음"을 기억하면 대화상자 없이 바로 거부로 온다).
- **뒤집으려면:** Core `RoomScanFlow`의 `PermissionState.Denied` 처리.

## S8-4 perf_log에 짧은 메모를 붙이는 방식
- **질문:** `PerfSpikeLogger.Mark`는 숫자 하나(detail)만 받는다. 분류·크기 요약은 어떻게 남길까?
- **택한 답:** `PerfRecordKind.RoomScan`을 새로 만들고 `Mark(kind, detail, note)` 오버로드를 더했다. `PerfSample`에 `Note` 문자열 칸이 생겼고, 줄 끝에 ` | <메모>`로 쓴다. 프레임 기록은 메모가 null이라 할당이 없다(기존 무할당 테스트 그대로 통과).
- **뒤집으려면:** `PerfSample.Note`와 오버로드를 지우고 `RoomScanProbe.Report`가 Unity 로그에만 쓰게 한다.

## S8-5 "테이블 근처" 기준
- **질문:** "테이블 근처의 가장 큰 수평면"의 테이블은 어디인가?
- **택한 답:** 지금 MR TABLE에서 가상 아레나 바닥 중심이 보이는 자리. 그 점에서 수평 1.0 m, 높이 0.5 m 안에 중심이 있는, 위를 보는 평면(또는 박스 윗면) 중 넓이가 가장 큰 것. 로그의 `dy`·`d`는 실제 테이블이 지금 아레나 바닥에서 얼마나 떨어져 있는지 알려 준다(다음 라운드의 "테이블에 맞추기" 기준).
- **뒤집으려면:** 씬의 `RoomScanProbe` > Near Table Radius / Near Table Height Gap.

## S9-1 개발 APK는 클린 빌드로 다시 만들었다(새 빌드 메서드 2개)
- **질문:** 개발 APK가 또 커졌을 때(증분 빌드 207.6 MB, zip 항목 합은 126.4 MB) 어떻게 클린 빌드를 할까?
- **택한 답:** `BuildScript`에 `BuildQuestApkDevClean`·`BuildQuestApkReleaseClean`(메뉴 HandHero > Build Quest APK (dev, clean) / (release, clean))을 더했다. `BuildOptions.CleanBuildCache`만 더 켜고 나머지는 같다. 개발 APK를 클린으로 다시 만들었더니 126.5 MB(파일 크기 = 항목 합)가 됐다. 릴리스는 증분 빌드로도 55.4 MB(항목 합과 같음)라 클린 없이 만들었다.
- **이유:** `Library/` 안의 Gradle 출력을 손으로 지우는 것보다 Unity가 정한 방법이 안전하고, 다음에도 같은 명령으로 반복할 수 있다. 클린 빌드는 약 1분 더 걸린다(6.4분 vs 5.6분).
- **뒤집으려면:** 두 메서드와 `clean` 매개변수를 지운다(`BuildScript.cs`). 커진 개발 APK `HandHero_20261009_1917_dev.apk`(207.6 MB)는 지우지 않고 `MetaAwards\Build\`에 남겨 두었다. 설치에는 문제없지만 `_1923_dev`를 쓰면 된다.
