# QUESTIONS_FOR_HYUN — Round 3

무인 세션이 대신 내린 결정. (형식: 태스크 / 질문 / 택한 답 / 이유 / 뒤집으려면)

- **P1 / 튜토리얼도 "전투"로 볼까?** → 예. 튜토리얼 중에는 `perf_log.txt`에 쓰지 않는다. 이유: 튜토리얼에서도 날고 쏘기 때문에, 파일 쓰기로 생기는 끊김이 섞이면 안 된다. 기록은 버퍼에 남아 있다가 메뉴로 돌아갈 때 써진다. 뒤집으려면: `PerfFlushPolicy.InCombat`에서 `MatchPhase.Tutorial`을 빼고 테스트 `Policy_CombatIsFightTutorialAndRunIslands`를 고친다.
- **P1 / 앱이 백그라운드에서 돌아온 첫 프레임을 스파이크로 셀까?** → 아니오. `OnApplicationPause(false)`에서 감지기를 리셋한다. 이유: 멈춰 있던 시간은 프레임이 아니고, 바로 앞에 `APP_PAUSE` 기록이 있다. 뒤집으려면: `PerfSpikeLogger.OnApplicationPause`의 `_detector.Reset()`을 지운다.
- **P2 / 릴리스 빌드의 스택 트레이스를 어떻게 끌까?** → 릴리스 APK를 빌드하는 동안에만 Log·Warning을 None으로 바꾸고 빌드가 끝나면 원래 값(ScriptOnly)으로 되돌린다. 그래서 `ProjectSettings.asset`의 `m_StackTraceTypes`는 바뀌지 않는다. 이유: 이 설정은 프로젝트 전체에 하나뿐이고, 에디터 콘솔의 Stack Trace Logging 메뉴도 같은 값을 쓴다. 파일에 None을 박아 두면 에디터에서 Debug.Log의 스택 트레이스도 사라진다. 뒤집으려면: `BuildScript.BuildQuestApk(bool)`의 `try/finally` 복원 부분을 지운다.
- **P2 / 퍼프 로거의 flush 로그(`[PerfSpikeLogger] ...`)도 개발 빌드 전용으로 돌릴까?** → 아니오. 릴리스에서도 `Debug.Log`로 남긴다. 이유: 안전한 때에만, 드물게 찍히고, 릴리스 APK에서도 logcat으로 로그 파일 경로를 확인할 수 있어야 한다. 뒤집으려면: `PerfSpikeLogger.Flush`의 `Debug.Log`를 `HHLog.Info`로 바꾼다.
- **P3 / 메뉴 버튼 전용 레이어를 새로 만들까?** → 아니오. Unity 내장 `UI` 레이어(5)를 쓴다. 빌더가 메뉴 버튼 콜라이더를 UI 레이어에 두고, `HandMenuPointer.buttonLayers`를 UI로 설정한다. 이유: 새 레이어는 `TagManager.asset` 변경인데 이번 라운드에서 허용된 ProjectSettings 변경 목록에 없다. 카메라 컬링은 전부 Everything이라 보이는 것은 같다. 뒤집으려면: `HandHeroSceneBuilder`의 `go.layer = MenuButtonLayer` 줄과 `buttonLayers` 설정을 지우고 씬을 다시 만든다(필드 기본값은 모든 레이어).
- **P5 / 이펙트 풀이 가득 찼을 때(동시에 24개) 새 이펙트를 만들까?** → 아니오. 그 피격은 이펙트 없이 넘어간다. 이유: 전투 중 Instantiate를 없애는 것이 목적이고, 0.25초짜리 이펙트가 24개 넘게 겹칠 일은 사실상 없다. 뒤집으려면: 씬의 `BeamImpactPool.maxCount`를 올린다.
- **P6 / 첫 발사 지터(×U(0.5, 1.0))를 런 봇에만 줄까?** → 모든 봇(퀵 매치 봇 포함)에 준다. 지터는 `BotBrain.Reset`에 있어서 일시정지에서 돌아올 때도 적용된다. 이유: BC-M1은 "재개 후 모든 봇이 같은 프레임에 쏜다"도 문제로 짚었고, 퀵 매치 봇의 첫 발이 최대 절반 빨라지는 정도다. 뒤집으려면: `BotBrain.Reset`의 `_fireTimer`를 `_p.FireInterval`로 되돌리고 `BotSeedTests`의 지터 테스트 2개를 지운다.
- **P6 / 쓰러진 봇이 사라지기 전 0.3초(corpseTime)는?** → 예전처럼 둔다. 그동안 풀에 돌아가지 않고, 게임 시간 기준이라 일시정지 중에는 기다린다. 섬이 끝나면 즉시 모두 풀로 돌아간다. 뒤집으려면: `RunDirector.corpseTime`.
- **P7 / 손바닥 밀기(쇼크웨이브)는 몇 초 넘는 프레임부터 속도로 안 읽을까?** → 0.1초(`PalmPushRecognizer.MaxSpeedDt`). 그런 프레임은 위치만 다시 잡고 발동하지 않는다. 이유: 새 Maximum Allowed Timestep과 같은 값이고, 0.1초 넘게 멈춘 뒤의 손 위치는 스무딩 때문에 순간 이동처럼 보인다. 뒤집으려면: 상수를 바꾸고 `PalmPush_LongFrame_IsNotReadAsSpeed` 테스트를 고친다.
- **P8 / 섬 전투 밖(인트로·클리어·상자·상점·포털)이나 일시정지 중에 플레이어가 죽으면?** → 섬 안과 똑같이 처리한다(부활 아이템이 있으면 부활, 없으면 DEFEAT). 승리·패배 화면에서는 무시한다. 이유: 런 중 자동 부활을 껐기 때문에 무시하면 플레이어가 죽은 채로 남는다. 뒤집으려면: `RunStateMachine.ReportPlayerDeath`의 첫 줄 조건.
- **P8 / 봇끼리 아군 사격을 어떻게 막을까?** → `FlyingCharacter.team`(Player/Bot). 빔은 같은 팀 히어로를 통과하고(맞지도, 보이지도 않음), `HeroHealth`가 같은 팀 사격을 한 번 더 거른다. 퀵 매치 봇도 Bot 팀이라 플레이어와는 그대로 싸운다. 뒤집으려면: 빌더의 `SetEnum(flying, "team", ...)`을 지우고 씬을 다시 만든다.
- **P8 / 포털이 0개일 때 대체 포털은?** → Arena + Random 상자. 상자가 비면 예전처럼 상자 단계를 건너뛴다. 뒤집으려면: `PortalRoller.Fallback`.
- **P9 / Meta 시스템 제스처 중 핀치를 어느 손에서 무시할까?** → 조준 손(오른손)의 발사·차지 핀치와 메뉴 포인터 핀치만 무시한다. 왼손 손목 일시정지(`WristMenu`)는 그대로 둔다. 이유: 손목 일시정지는 일부러 Quest 시스템 동작(왼손바닥을 얼굴로 + 핀치)과 같게 만들었고, 시스템 제스처 플래그는 바로 그 자세에서 켜진다. 거기서도 무시하면 손목 일시정지가 아예 안 된다. 시스템 제스처가 끝날 때 핀치가 아직 닫혀 있으면 한 번 펴야 다시 쏜다. 뒤집으려면: `WristMenu.Update`에서 `hand.PinchStrength` 대신 `SystemGestureGate.Step(...)` 결과를 넘긴다.
- **P9 / 손 추적이 끊겼을 때 잠금 대상도 풀까?** → 아니오. 잠금 링만 숨기고 대상은 기억한다. 손이 돌아오면 다음 조준 계산에서 그대로 이어지거나 풀린다. 뒤집으려면: `PointingBeamController.ShowAimLost`에서 `SetAssistTarget(null)`을 부른다.
- **P10 / 핀치를 뗀 판정은 원시 값과 임계값 0.6 중 무엇으로?** → 임계값 0.6(`XRHandsInputSource.pinchResetThreshold` 0.5 → 0.6). 발사 임계값 0.8과의 히스테리시스는 그대로 남는다. 이유: 더 단순하고, `PinchTrigger`·`SystemGestureGate`가 같은 값을 쓰며 기존 테스트로 검증된다. 원시 값은 트래커·`PinchTrigger` API를 둘 다 바꿔야 한다. 이번 수정의 핵심은 `HoldDelay`다. 뒤집으려면: 그 필드를 0.5로 되돌리고 씬을 다시 만든다.
- **P10 / `HoldDelay`를 CURSOR(검지 방아쇠)에도 적용할까?** → 예. 차지 모델이 하나라 두 조준 모드에 같이 적용된다. 차지 아이템(Quick Charge 등)은 `HoldDelay`를 줄이지 않는다(입력 구분용이지 차지 속도가 아니므로). 뒤집으려면: `CombatMath.Charge`에서 `HoldDelay`도 곱하거나, CURSOR일 때 `charge.HoldDelay = 0`으로 넘긴다.
- **P11 / 봇 발사·차지 소리는?** → 봇 발사는 플레이어 발사와 다른 낮은 톱니파 `EnemyFire`(봇 위치 3D). 차지 시작·준비음은 플레이어만 낸다. 이유: 봇 차지까지 울리면 시끄럽고, 봇 공격은 예고 경고음(`BotTelegraph`)이 이미 알려 준다. 뒤집으려면: `PointingBeamController.UpdateChargeSounds`의 팀 검사를 지운다.
- **P11 / 명중 확인음과 피격음은 2D와 3D 중 무엇으로?** → 명중 확인(`HitDealt`)·피격(`HitTaken`)·메뉴·흐름 소리는 2D, 발사·쇼크웨이브·봇 처치·예고음은 그 위치의 3D(공간감 0.6, 8 m까지 최대, 200 m까지 선형 감쇠). 이유: 내 행동의 결과는 거리와 관계없이 분명해야 하고, 월드 사건은 방향을 알려 줘야 한다. 뒤집으려면: 호출부의 `SfxPlayer.Play`/`PlayUi`를 바꾸거나 씬의 `Sfx` 오브젝트에서 `spatialBlend`를 조정한다.
- **P11 / 상점에서 살 수 없는 물건을 누르면?** → 거절음(`Denied`). 크리스털 부족·매진·일시정지 중 모두 같은 소리. 뒤집으려면: `RunChoiceMenu.Choose`의 `else` 줄을 지운다.
- **P11 / 퀵 매치 라운드 결과음은?** → 라운드를 이기면 클리어 소리, 지면 소리 없음. 매치 승패는 VICTORY/DEFEAT. 뒤집으려면: `SfxCues.ForMatchPhase`.
- **P12 / 편안함 토글을 메뉴에 둘까?** → 아니오. 지금은 씬 `Main Camera`의 `DamageVignette.flashOnDamage` 인스펙터 토글과 코드용 `FlashOnDamage` 속성뿐이다. 이유: 메뉴 버튼 추가는 메뉴 배치 변경이라 무인으로 정하기에 디자인 결정이 크다. 뒤집으려면: 메뉴에 버튼을 만들고 `FlashOnDamage`를 바꾸게 한다(PlayerPrefs로 기억).
- **P12 / 처치 폭발을 플레이어 사망에도 낼까?** → 아니오. 봇(퀵 매치 봇·런 봇)만 낸다. 플레이어 사망은 DEFEAT·부활 연출이 따로 있다. 뒤집으려면: 빌더에서 플레이어 `HeroHealth.deathEffectPrefab`에도 `killBurstPrefab`을 넣는다.
- **P12 / 비네트를 화면 위에 덮는 방식은?** → 카메라 자식 쿼드(0.3 m 앞, 1.3 m 크기, 시야 약 ±65°), URP Unlit 투명(렌더 큐 3500, 깊이 쓰기 끔). URP Unlit에는 ZTest 속성이 없어서 0.3 m보다 가까운 손은 비네트 앞에 보일 수 있다. 뒤집으려면: 빌더의 `DamageVignette` 쿼드 위치·크기.
