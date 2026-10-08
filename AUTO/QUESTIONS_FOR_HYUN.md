# QUESTIONS_FOR_HYUN

무인 세션이 대신 내린 결정. (형식: 태스크 / 질문 / 택한 답 / 이유 / 뒤집으려면)

- T1 / 에디터가 6.6 업그레이드 후 배치 실행 때 다시 쓴 설정 에셋(`Assets/Settings/*URP*`, `Assets/XR/*`, `Assets/XRI/Settings/Resources/`, `Assets/CompositionLayers/`, `ProjectSettings/Packages/com.unity.learn.iet-framework/Settings.json`)을 커밋할까? / 커밋함(`a519806`, 손으로 고친 것 없음) / 매 실행마다 다시 생겨 작업 트리가 더러워지고, 하드 규칙 8의 ProjectSettings 변경은 사람이 의도한 변경을 막으려는 취지라 판단 / 되돌리려면 `git revert a519806`(다음 에디터 실행 때 다시 생긴다).
- T2 / 주먹·핀치 임계값(grab 0.7/release 0.45, pinchFire 0.8/pinchReset 0.5)을 어디에 둘까? / `XRHandsInputSource`로 옮김(이름·기본값 동일) / 계획서 T5가 "제스처 인식은 XRHandsInputSource 쪽"이라 했고, 봇·디버그 입력은 아날로그 강도가 없음 / 컨트롤러 쪽으로 되돌리려면 HandInputData에 FistStrength·PinchStrength를 추가해야 함.
- T2 / 샌드박스 씬에서 플레이어 히어로 레이어 / 내장 "Ignore Raycast"(2) 레이어 사용, aimMask에서 제외 / 하드 규칙 8(ProjectSettings는 T9에서만) 때문에 새 레이어를 만들지 않음 / T9에서 전용 FlyingHero 레이어로 교체 예정. **→ T3에서 바뀜(아래).**
- T3 / 봇 빔이 플레이어 히어로를 맞혀야 하는데 플레이어가 Ignore Raycast 레이어라 맞힐 수 없음 / 레이어 대신 `PointingBeamController`가 **자기 히어로의 콜라이더만 건너뛰는** 레이캐스트(`RaycastIgnoringSelf`)를 쓰고, 두 히어로 모두 Default 레이어 / 새 레이어 없이(규칙 8) 플레이어·봇이 서로 맞힐 수 있고 aimMask 의미는 그대로 / T9에서 FlyingHero 레이어를 만들면 aimMask로 바꿔도 됨(서로 맞혀야 하므로 "자기 제외"는 남겨야 함).
- T3 / 봇 난이도 기본값 / 반응 0.35초, 조준 오차 4°, 발사 간격 2.2+0~1초, 예고 0.6초, 선호 거리 12±3m, 손 속도 상한 1.5m/s(사람 손 수준) / 계획서 T4의 0.6초 예고와 맞추고, 12m에서 4° 오차 ≈ 0.84m라 가만히 있으면 대부분 맞고 피하면 빗나감 / `Assets/MyAssets/Generated/Bot/BotDifficulty_Normal.asset`에서 조절(Play 중 실시간 반영, 씬 빌더가 덮어쓰지 않음).
- T4 / 빔 1발 데미지 / 플레이어·봇 모두 20(5발에 사망, 체력 100) / ADR 3에 데미지 값이 없어서 정함. 플레이어 쿨다운 0.35초, 봇은 약 2.8–3.8초에 1발이라 플레이어가 유리하지만 봇은 움직이고 피격 시 회피함 / `PointingBeamController.damage`(각 컨트롤러 인스펙터, 씬 빌더 기본값). 밸런스는 T6 매치 루프에서 다시 볼 것.
- T4 / 피격 표현 / 히어로 쪽만: 흰색 0.12초 플래시 + 머리 위 체력 바, 사망 시 히어로 숨김 → 3초 뒤 시작 위치에서 리스폰. 화면 비네트는 안 넣음 / 계획서 "XR Origin에 어떤 힘도 가하지 않는다", 레거시 `DamageFlash`는 `PlayerHealth`에 묶여 있음 / 화면 효과가 필요하면 `HeroHealth.Damaged` 이벤트에 붙이면 됨(T12 폴리시 후보).
- T5 / 차지샷·충격파 수치 / 차지 최소 0.3초·최대 1.5초, 데미지 30→70(일반 20), 빔 굵기 1.5→4배. 충격파 반경 6m, 경직 1.2초 동안 최대 속도 15%, 쿨다운 3초 / 계획서에 값이 없어서 정함. 풀차지 70 + 일반 1발 = 처치, 대신 차지 중 제자리 고정이 리스크 / `PointingBeamController`의 Charge Shot 항목, `ShockwaveController` 인스펙터.
- T5 / 충격파는 어느 손으로? / 양손 다(`pushWithLeftHand`/`pushWithRightHand` 둘 다 켬). 손이 펴져 있어야(주먹 0.35 이하) 하고, 손바닥이 향한 방향으로 1.2m/s 이상 밀어야 발동 / 클러치(주먹) 끌기·급끌기 회피가 충격파로 오인되지 않게 / 한 손만 쓰려면 토글 끄기.
- T5 / 손바닥 법선 방향 / `palmNormalLocal = (0,-1,0)` (XR Hands 손바닥 관절은 손등이 +Y라고 판단) / 헤드셋 없이 확인 불가. **당기는 동작에 충격파가 나가면** XRHandsInputSource 인스펙터에서 (0,1,0)으로 바꾸면 됨.
- T5 / 두 손을 붙이면 추적이 자주 끊기는 문제 / 한 손이 끊겨도 0.3초까지는 차지 유지, 더 길면 차지 해제(충전됐으면 그때 발사) / 손이 겹치는 순간 차지가 깨지지 않게 / `chargeLostGraceTime`.
- T6 / 라운드 규칙 / 라운드 = 먼저 쓰러뜨린 쪽 승(첫 KO에서 라운드 끝), 90초 시간 종료 시 체력 많은 쪽 승·같으면 무승부, 2선승, 무승부가 이어져도 최대 5라운드 / 대회 가이드 "10분 안에 완결" — 최악 8.2분 / `MatchDirector`의 Rules 항목(라운드 시간 등).
- T6 / 라운드 사이 조작 / 카운트다운·결과 화면 동안 플레이어·봇 입력 소스를 끔(히어로는 스폰 위치에서 대기) / 공정한 라운드 시작 / `MatchDirector.fightOnly` 배열에서 빼면 항상 조작 가능.
- T6 / HUD 위치 / 좌석 앞 4m에 월드 고정(머리 따라오지 않음), 점수줄은 눈높이보다 0.9m 위(약 13°), 배너는 정면 약간 위. 배너는 Fight 시작 0.8초 뒤 숨김 / 멀미·FoV 중앙 원칙 / 씬 빌더의 `WorldText` 위치값.
- T6 / TMP 기본 폰트 에셋(`LiberationSans SDF.asset`)을 에디터가 처음 쓰면서 새 형식으로 다시 저장함 / 커밋함(손으로 고친 것 없음) / a519806과 같은 이유 / `git revert 5e0b8af`는 T6 코드까지 되돌리므로 그 파일만 `git checkout <이전 커밋> -- <경로>`.
- T7 / 메뉴 버튼 선택 방식 / XRI Poke 대신 **오른손으로 가리키기 + 핀치**(조준과 같은 레이). 버튼은 좌석 앞 2.5m, 0.8×0.32m / 착석 2피트 반경에서 손이 닿는 거리에 버튼을 두면 아레나를 가리고, 게임 조작(가리키기+핀치)과 같은 동작이라 배울 것이 하나 줄어듦. XRI 의존도 없음 / Poke를 원하면 패널을 0.5m 앞으로 옮기고 XRI Poke Interactable을 붙여야 함.
- T7 / 일시정지 진입 / **왼손 손바닥을 얼굴 쪽으로 0.25초 → 손목 PAUSE 버튼, 왼손 핀치로 누름**. 주먹 상태(클러치)에서는 안 뜸 / 싸우는 오른손(핀치=발사)과 겹치지 않고, Quest 시스템의 "왼손 손바닥 + 핀치 = 메뉴" 관례와 같음(시스템이 이 제스처를 가로채면 포커스 잃음 → 자동 일시정지로도 걸림) / `WristMenu` 인스펙터(`gesture` 값, `useLeftHand`).
- T7 / 일시정지 구현 / `Time.timeScale = 0` + 플레이어·봇 입력 끔. 손 추적 스무딩(`HandGestureTracker`)은 unscaled 시간으로 바꿈(튜닝 값은 그대로, 평소 timeScale 1이라 동작 동일) / 히어로·빔·체력 타이머를 한 번에 멈추는 가장 단순한 방법 / 다른 방식을 원하면 `MatchDirector.OnPausedChanged`.
- T7 / 자동 일시정지 / 포커스 잃음·앱 일시정지·헤드셋 벗음(userPresence 하강 엣지)에서 일시정지, **재개는 항상 플레이어가 직접** / 돌아와서 바로 맞는 일 방지 / `MatchDirector`의 `pauseOnFocusLoss`·`pauseOnHeadsetRemoved`. 에디터에서 Game 뷰 밖을 클릭해도 일시정지됨(P 키로 재개).
- T8 / 튜토리얼 문구 언어 / 영어 한 줄(예: "Make a fist with your LEFT hand") / 심사위원이 영어권 + 기존 HUD도 영어 / `TutorialDirector.PromptText`.
- T8 / 첫 실행 판정 / START를 누르면 튜토리얼을 한 번도 끝내거나 건너뛰지 않았을 때만 튜토리얼부터(PlayerPrefs `HandHero.TutorialSeen`). 앱을 켜자마자 자동 시작하지는 않음 / 콜드 스타트에서 메뉴 1단계는 유지하고 "Best First Five Minutes"는 START 한 번으로 바로 배우게 / 다시 보려면 TUTORIAL 버튼, 기록 지우려면 PlayerPrefs 삭제.
- T8 / 연습 예고선 판정 / 실제 빔 레이캐스트 대신 "발사 순간 히어로가 고정점에서 1.5m 이상 벗어났나"로 판정, 데미지 없음 / 튜토리얼에서 죽거나 체력이 깎이지 않게, 테스트 가능하게 / `TutorialDirector`의 Rules(`DodgeHitRadius` 등).
