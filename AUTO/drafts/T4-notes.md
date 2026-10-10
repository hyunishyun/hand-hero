# T4 데모 모드 — 대신 내린 결정 (초안, 통합 담당이 QUESTIONS_FOR_HYUN에 합친다)

(형식: 태스크 / 질문 / 택한 답 / 이유 / 뒤집으려면)

## T4-1 데모를 매치 단계로 둘까, 따로 돌릴까
- **질문:** 데모를 `MatchStateMachine`의 새 단계로 둘까, 별도 디렉터가 혼자 돌릴까?
- **택한 답:** 새 단계 `MatchPhase.Demo`를 enum 끝에 더했다. 메인 메뉴에서만 들어간다(`StartDemo`). 타이머가 없고 KO도 무시한다. `ReturnToMenu`로만 끝난다. 튜토리얼은 거치지 않는다. 실제 진행은 새 `DemoDirector`가 한다.
- **이유:** 일시정지, 포커스를 잃었을 때 멈춤, 헤드셋을 벗었을 때 멈춤, 손목 버튼 → MENU가 RUN처럼 그대로 동작한다. 퀵 매치, 런, 데모가 동시에 켜질 수 없다(모두 메뉴에서만 시작한다).
- **뒤집으려면:** 메뉴의 DEMO 버튼을 빌더에서 빼면 된다. 단계를 지우려면 `MatchPhase.Demo`, `StartDemo`, `MenuAction.StartDemo`, 그리고 데모 파일 세 개를 함께 지운다.

## T4-2 데모 적: 몇 마리, 얼마나 느리게
- **질문:** "느린 Striker 1–2마리"를 어떻게 만들까?
- **택한 답:**
  - Striker 2마리. 런 봇 프리팹으로 `DemoDirector`가 자기 인스턴스를 씬 로드 때 만든다. RunDirector의 풀은 쓰지 않는다.
  - 값(`DemoParams`, `DemoDirector.rules`에서 조절): 손 속도 ×0.6, 예고 시간 ×1.8, 발사 간격 ×1.6, 피해 ×0.4.
  - 쓰러지면 HeroHealth의 기본 규칙대로 3초 뒤 같은 자리에서 다시 나온다.
  - 스폰 위치는 런 스폰 지점 1, 2다.
- **이유:** 녹화에서 맞히기 쉽고, 예고선(빨간 선)을 피하는 장면도 찍을 수 있다. 런의 풀과 섞이지 않으니 데모의 느린 값이 런으로 새지 않는다. 손 속도는 1보다 크게 못 한다(공정성 상한).
- **뒤집으려면:** `DemoDirector.rules`에서 `BotCount`를 1이나 0으로, `BotSpeedScale`을 1로 바꾼다. `BotInputSource.SetHandSpeedScale`은 데모만 부른다.

## T4-3 무적의 범위
- **질문:** 무적이면 맞았을 때 아무 반응도 없어야 할까?
- **택한 답:** 체력만 줄지 않고 죽지 않는다. 번쩍임, 빨간 비네트, 맞는 소리, 2초 감속은 게임과 똑같이 나온다(`HeroHealthModel.Invulnerable`).
- **이유:** 계획은 "영웅, 빔, 이펙트, 소리는 게임 그대로"다. 맞은 걸 알아야 피하기 연습이 된다.
- **뒤집으려면:** `HeroHealthModel.ApplyDamage`의 `Invulnerable` 블록에서 `_slowTimer` 줄을 지우면 감속이 빠진다. `return HitOutcome.Ignored`로 바꾸면 모든 피격 반응이 빠진다.

## T4-4 연습 과녁
- **질문:** "다시 나오는 연습 과녁"은 무엇을 쓸까?
- **택한 답:** 아레나에 이미 있는 주황 과녁 4개(`Target_1–4`)를 쓴다. 이 과녁은 원래 3번 맞으면 사라졌다가 2초 뒤 다시 나온다. 데모에서만 ASSIST 조준 보조가 과녁에 붙는다. 빌더가 붙인 `AimAssistTarget`이 꺼져 있다가 데모에서만 켜진다.
- **이유:** 새 물체가 필요 없다. 퀵 매치와 런에서는 조준 보조가 과녁에 붙지 않으니 지금 게임 감각이 그대로다.
- **뒤집으려면:** 빌더의 데모 블록에서 `demoAssistTargets`를 빈 배열로 둔다.

## T4-5 캡션 규칙
- **질문:** 캡션을 언제 띄우고, 겹치면 무엇을 보일까?
- **택한 답:**
  - 시간: 0.8초 동안 다 보이고, 0.3초에 걸쳐 사라진다.
  - 띄우는 때:
    - GRAB: 주먹을 쥐는 순간
    - PINCH = FIRE: 일반 발사
    - HOLD = CHARGE: 차지가 시작될 때, 그리고 차지샷이 나갈 때 한 번 더
    - PUSH = SHOCKWAVE: 충격파가 나갈 때, 손바닥 방향으로 더 빨리 민 손 옆에
  - 같은 프레임에 겹치면: SHOCKWAVE > CHARGE > FIRE, SHOCKWAVE > GRAB.
  - CURSOR 모드에서는 FIRE가 `TRIGGER = FIRE`로 나온다.
  - 데모가 시작될 때 이미 쥐고 있던 주먹이나 차지에는 캡션을 띄우지 않는다.
- **이유:** 차지샷이 나갈 때 "PINCH = FIRE"가 뜨면 틀린 설명이 된다. CURSOR는 핀치가 아니라 검지 방아쇠로 쏜다. 입력에는 어느 손이 밀었는지가 없어서, 손바닥 속도로 고른다(밀기 인식기와 같은 기준).
- **뒤집으려면:** `GestureCaptions.timing`, Core의 `DemoCaptionModel.Step` 순서, `DemoCaptionText`.

## T4-6 캡션 위치와 크기
- **질문:** 녹화에서 읽히면서 영웅을 가리지 않으려면 어디에, 얼마나 크게 둘까?
- **택한 답:**
  - 위치: 손바닥에서 바깥쪽(왼손은 더 왼쪽, 오른손은 더 오른쪽)으로 7 cm, 위로 4 cm 떨어진 곳에서 글자가 시작해 바깥으로 이어진다.
  - 머리에서 본 영웅과 6° 안으로 겹치면 5 cm씩 아래로 내린다(최대 6번).
  - 글자: TMP 0.26(줄 높이 약 2.6 cm), 굵게, 흰색, 검은 외곽선 0.25. 머리를 향한다.
  - 좌석 공간(SeatUI) 아래에 있어서 MR TABLE에서도 실제 크기가 같다.
- **이유:** 손 바깥쪽은 화면 가운데(영웅이 주로 있는 곳)에서 멀다. 외곽선이 있어서 밝은 패스스루나 하늘 위에서도 읽힌다.
- **뒤집으려면:** `GestureCaptions.layout`(`SideOffset`, `UpOffset`, `HeroClearAngle`), 빌더의 글자 크기 `0.26f`.

## T4-7 반투명 손의 모양과 비용
- **질문:** 손을 어떻게 그리고, 비용은 얼마로 볼까?
- **택한 답:**
  - 모양: 손마다 관절 구 26개(지름 1.4 cm, 손목·손바닥 3 cm), 뼈 선 6개(손가락 5개 + 손가락 뿌리 관절을 잇는 선), 선 굵기 6 mm.
  - 재질: `GhostHands` 재질 하나(투명 Unlit, 깊이 테스트는 일반)를 쓰고, 손마다 MaterialPropertyBlock으로 색을 준다.
  - 색: 평소 연한 하늘색(알파 0.35), GRAB 중인 손은 초록(0.7), PINCH 중인 손은 노랑(0.75).
  - 비용: 데모에서만 렌더러 64개가 더 그려진다(SRP 배처 밖, 단일 패스 인스턴싱). 매 프레임 할당은 없다.
- **이유:** 녹화에서 손가락 모양까지 보여야 한다. 항상 맨 위에 그리지 않아서, MR TABLE에서도 실제 물체와 앞뒤가 자연스럽다.
- **뒤집으려면:** `GhostHands` 필드(크기, 색). 비용이 문제면 `jointSize`를 그대로 두고 관절 구를 끝마디만 남기는 식으로 줄인다(코드 변경).

## T4-8 메인 메뉴 배치
- **질문:** DEMO 버튼을 3×2 그리드 어디에 둘까?
- **택한 답:** 아랫줄 왼쪽에 둔다. Arena_Main은 DEMO / MR TABLE / AIM으로 아랫줄이 3칸이 된다. 샌드박스는 DEMO / AIM 두 칸을 가운데에 둔다.
- **이유:** 윗줄은 모드(QUICK MATCH / RUN / TUTORIAL)다. 5칸 그리드의 빈 칸을 채우면 크기와 간격이 지금과 같다.
- **뒤집으려면:** `HandHeroSceneBuilder`의 메인 메뉴 블록(“Round 5 T4” 주석)에서 x 좌표를 바꾼다.

## T4-9 그 밖의 작은 결정
- 디버그 키 `G` = 메뉴에서 데모 시작(에디터/데스크톱).
- 성능 로그: 데모는 전투로 친다. 녹화 중에는 `perf_log.txt`를 쓰지 않고, 일시정지나 메뉴로 돌아갈 때 쓴다.
- 데모 중에는 퀵 매치 봇(BotHero와 바닥 표시)을 숨긴다. 점수·타이머 줄과 배너는 비운다.
- 잡는 손은 왼손으로 가정한다(`XRHandsInputSource` 기본값과 같다). `GhostHands`와 `GestureCaptions`의 `clutchIsLeft`로 바꾼다.

## 통합 때 볼 곳 (공용 파일의 작은 변경)
- `HandGestureTracker`: `Subsystem`, `TrackingSpace` 읽기 전용 속성 2줄. T0가 같은 파일을 고치지만 위치가 다르다(`OnDestroy` 바로 아래).
- `PointingBeamController`: `IsCharging` 속성. Update의 `SetChargeSpeedMultiplier` 다음 줄, OnDisable 한 줄. T0의 `_pinchHolds.Add(step)` 근처라 충돌을 확인한다.
- `ShockwaveController`: `Fired` 이벤트.
- `BotInputSource`: `SetHandSpeedScale`과 `CurrentParams`의 한 줄. T2가 같은 파일을 고칠 수 있다.
- `MatchDirector`, `MatchHud`, `HandMenu`(주석), `HeroHealth`, Core의 `MatchStateMachine`, `HeroHealthModel`, `FrameSpikeDetector`(`PerfFlushPolicy`).
- 빌더: 메인 메뉴 그리드 블록과, `ViewModeSwitch` 바로 앞의 `// ---- Round 5 T4: demo mode (D6) ----` 블록.

## 검증 (Unity 없이 한 것)
- Unity가 쓰는 컴파일 응답 파일(`Library/Bee/.../*.rsp`)로 Roslyn을 직접 돌려 4개 어셈블리(Core, Tests, Assembly-CSharp, Editor)를 이 워크트리 소스로 컴파일했다. 오류 0, 이 워크트리 파일의 경고 0.
- Unity 밖의 .NET 8 리플렉션 러너로 EditMode 테스트를 돌렸다. 642개 중 640개가 통과했다. 새 테스트 41개는 모두 통과했다. 실패한 2개는 Unity 네이티브 호출이 필요한 테스트(`ArenaLayoutTests.SpawnPoints_…`)와 `UnityEngine.TestRunner`가 필요한 GC 테스트(`PerfLogTests.Formatter_…`)로, 이번 변경과 상관없다.
- **통합 담당이 할 일:** `compile_check.ps1`, `-Tests`(예상 601 + 41 = 642), `BuildAll`(씬과 `GhostHands` 재질 생성).

## 기기에서 확인할 것 (T4)
1. 메인 메뉴 아랫줄 DEMO / MR TABLE / AIM. DEMO를 누르면 점수·타이머 없이 바로 아레나가 나오고, 느린 Striker 2마리와 주황 과녁이 보인다.
2. 반투명 손이 실제 손 위치에 붙어 있다(VR ARENA와 MR TABLE 둘 다). 주먹을 쥐면 왼손이 초록, 핀치하면 오른손이 노랑으로 바뀐다.
3. GRAB / PINCH = FIRE / HOLD = CHARGE / PUSH = SHOCKWAVE가 손 옆에 약 1초 뜨고, Quest 녹화 영상에서 읽힌다. 영웅을 가리지 않는다.
4. 봇에게 맞아도 체력이 줄지 않는다(번쩍임과 소리는 난다). 쓰러진 봇은 3초 뒤 다시 나오고, 과녁은 2초 뒤 다시 나온다.
5. 왼손 손목 버튼 → 일시정지 → MENU로 메인 메뉴에 돌아간다. 그 뒤 퀵 매치와 RUN에서 체력이 정상으로 줄고, 퀵 매치 봇이 다시 보인다.
6. 데모 중 프레임: 끊김이 없는지 `perf_log.txt`의 데모 구간(일시정지·메뉴 때 기록)으로 확인한다.
