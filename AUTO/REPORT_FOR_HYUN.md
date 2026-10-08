# 귀환 리포트 — 무인 작업 (2026-10-07, 브랜치 `auto/2026-10-07`)

> 5분 요약: **T0–T12 전부 DONE, 블로커 0개.** Quest APK가 `MetaAwards\Build\HandHero_20261007.apk`(58 MB)에 있다.
> 단, **헤드셋에서 한 번도 실행해 보지 않았다.** 모든 확인은 배치모드 컴파일 + EditMode 테스트 117개 + 씬/매니페스트 파일 검사까지다.
> push 안 함. 시작 시점 태그 `pre-auto-2026-10-07`.

## 1. 태스크 표

| 태스크 | 상태 | 한 줄 |
|---|---|---|
| T0 작업 기반 | DONE | git·브랜치·태그, `Networking~` 보관, Unity 6000.6.4f1 업그레이드, `compile_check.ps1` |
| T1 순수 로직 + 테스트 | DONE | `HandHero.Core` 어셈블리, 인스펙터 필드명·기본값 유지 |
| T2 입력 추상화 | DONE | `HandInputData` + XR/디버그/스크립트/봇 입력 소스 |
| T3 봇 | DONE | 플레이어와 같은 입력 경로·비행 모델·속도 상한 |
| T4 전투 규칙 | DONE | ADR 3(체력·2초 50% 감속·3초 리스폰), 봇 예고선 0.6초 |
| T5 제스처 2개 | DONE | 두 손 모으기 = 차지샷, 손바닥 밀기 = 충격파(경직, 넉백 없음) |
| T6 매치 루프 | DONE | 3판 2선승, 라운드 90초, 최악 8.2분 |
| T7 손 전용 UI + 일시정지 | DONE | 가리키기+핀치 메뉴, 왼손 손바닥 손목 버튼, 자동 일시정지, 컨트롤러 코드 0건 |
| T8 30초 튜토리얼 | DONE | 첫 START 때 자동, 최소 약 8초 |
| T9 씬 빌더 + APK | DONE | `Arena_Main` + `BuildQuestApk` |
| T10 패스스루 테이블탑 | DONE | 메뉴 MR TABLE 버튼, 기본은 VR 아레나 |
| T11 Fusion 이식 설계 | DONE | `AUTO/FUSION_PORT_PLAN.md` (코드 없음) |
| T12 버퍼 | DONE | 실루엣 구분, 바닥 원판 깊이 단서, 명중 이펙트, 오디오 자리 |

자세한 내용과 주의점은 `AUTO/PROGRESS.md`.

## 2. 헤드셋에서 확인할 것 (우선순위순)

APK 설치(PowerShell):
```
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" install -r "C:\Users\AISTUDIO\Desktop\Hyun's Playground\MetaAwards\Build\HandHero_20261007.apk"
```

1. **앱이 켜지고 손이 보이는가** — 컨트롤러를 내려놓고 실행. 메뉴가 정면 약간 아래(2.5m)에 뜨고, 오른손으로 가리키면 레이가 보여야 한다. 안 뜨면 `adb logcat -s Unity`부터.
2. **START → 튜토리얼** — 왼손 주먹 → 히어로를 초록 링까지 → 놓고 활공 → 오른손 조준 → 핀치 → 예고선 피하기. 30초 안에 "아, 이렇게 하는구나"가 오는지. 막히는 단계가 어디인지.
3. **클러치 느낌** — 주먹 쥐고 끌 때 히어로가 손을 따라오는 비율(`positionScale` 60)·스프링(`stiffness` 10)이 원래 튜닝과 같은지. 손을 펴면 활공하는지.
4. **조준·발사** — 핀치 순간 레티클이 튀지 않는지, 핀치 한 번에 한 발만 나가는지.
5. **봇전** — 봇의 예고선(노랑→빨강)이 보이고 왼손 급끌기로 피할 수 있는지. 봇이 너무 세거나 약한지.
6. **충격파** — 손바닥을 앞으로 밀면 나가는지. **당기는 동작에 나가면** `XRHandsInputSource.palmNormalLocal`을 (0,1,0)으로.
7. **차지샷** — 두 손바닥을 붙이면 히어로 위에 구가 커지는지, 떼면 굵은 빔이 나가는지. 손이 겹칠 때 추적이 끊겨 차지가 깨지는지.
8. **손목 일시정지** — 왼손을 펴서 손바닥을 얼굴 쪽으로 → PAUSE 버튼 → 같은 손 핀치. Quest 시스템 메뉴와 겹치는지.
9. **HUD·메뉴 크기** — 고개를 돌리지 않고 점수·배너가 읽히는지, 버튼이 너무 크거나 작은지.
10. **MR TABLE** — 패스스루가 실제로 보이는지, 탁자 위 1m 아레나의 위치·입체감, 빔·예고선이 너무 가는지.
11. **바닥 원판(T12)** — 히어로 거리감에 도움이 되는지, 거슬리는지.

## 3. 에디터에서 바로 확인하기

- 씬: `Assets/MyAssets/Scenes/HandHero_Sandbox.unity` (헤드셋 없이 키보드/마우스), `Arena_Main.unity` (APK용, XR 손 입력만).
- 씬은 손으로 고치지 말고 메뉴 **HandHero > Build All Scenes**로 다시 만든다(봇 난이도 에셋은 덮어쓰지 않음).
- 디버그 키(샌드박스):

| 입력 | 동작 |
|---|---|
| 우클릭 드래그 / WASD·QE | 클러치(히어로 끌기) |
| 마우스 휠 | 깊이 방향 끌기 |
| 커서 | 조준 |
| 좌클릭 / Space (누르고 있기) | 누르면 발사, 계속 누르면 차지 → 떼면 차지샷 |
| C (누르고 있기) | 차지 (긴 핀치와 같음) |
| Ctrl + 우클릭 드래그 / WASDQE / 휠 | 조준 마커 끌기 (CURSOR 모드) |
| F | 충격파 |
| Enter | 시작 / 결과·튜토리얼 건너뛰기 |
| T | 튜토리얼 |
| M (메뉴에서) | 조준 모드 ASSIST ↔ CURSOR |
| P | 일시정지/재개 |
| Esc | 메뉴로 |
| 마우스 클릭 | 메뉴 버튼 |

- 컴파일·테스트·빌드: `AUTO/DECISIONS.md` "도구" 절. APK 빌드 명령은 `PROGRESS.md` T9.
- 빌드할 때마다 Unity가 `ProjectSettings/UnityConnectSettings.asset`을 켠다 → 커밋 전에 `git checkout -- ProjectSettings/UnityConnectSettings.asset`.

## 4. 내가 대신 내린 결정 (뒤집을 수 있음)

전체 목록과 되돌리는 방법은 `AUTO/QUESTIONS_FOR_HYUN.md`. 굵직한 것만:

- 빔 데미지 20(5발 사망), 차지샷 30→70, 충격파 반경 6m·1.2초 15% 속도·쿨다운 3초.
- 봇: 반응 0.35초, 조준 오차 4°, 발사 간격 2.2+0~1초, 선호 거리 12m. `Generated/Bot/BotDifficulty_Normal.asset`.
- 라운드 = 첫 KO에서 끝, 90초 시간 종료 시 체력 많은 쪽, 2선승, 최대 5라운드.
- 메뉴 선택 = XRI Poke가 아니라 **가리키기 + 핀치**(게임 조작과 같은 동작).
- 일시정지 = `Time.timeScale 0`, 재개는 항상 플레이어가 직접.
- 튜토리얼은 첫 START 때만 자동(PlayerPrefs `HandHero.TutorialSeen`), 문구는 영어.
- 트래킹 원점 = Device + 눈높이 1.2m 고정(착석 레이아웃 통일).
- 앱 이름 **Hand Hero**, 패키지 **com.hyun.handhero** (대회 대시보드에서 앱 만들 때 이 이름).
- Android XR Support OpenXR 기능 끔(Quest 전용 APK), **Unity OpenXR Meta 2.6.1 추가**(패스스루).
- 테이블탑 = XR Origin **스케일만** 바꿈(위치·회전은 안 건드림, 하드 규칙 1 유지).
- 히어로 구분을 색 + 모양으로, 바닥 원판 깊이 단서 추가(T12).
- Fusion 이식 시 싱글은 지금 경로 유지, 네트워크 입력은 절대 위치 기반(T11).

## 5. 다음에 하면 좋을 작업 3개

1. **헤드셋 1회차 테스트 → 튜닝** (위 2절 체크리스트). 결과에 따라 튜토리얼 문구·봇 난이도·손 임계값 조정. 이게 마감 전 가장 값진 작업.
2. **사운드·고스트 손** — 빔 `fireSound`, 피격·충격파 소리(AudioSource 자리 있음), 튜토리얼 고스트 손(지금은 점 하나)을 실제 손 모양 애니메이션으로. "Best First Five Minutes"에 직접 영향.
3. **멀티 여부 결정** — `FUSION_PORT_PLAN.md` 7절의 5가지 결정. 넣는다면 Photon 가입·App ID(Hyun) 후 N1(Core 준비, 지금도 가능)부터. 11/1까지 시작 못 하면 싱글로 출품 추천.
   (그 외: 3분 영상 촬영 계획, 대시보드 "Competition" 릴리스 채널 업로드 — 계정 작업이라 Hyun 몫.)

## 6. 바뀐 튜닝 기본값

**Hyun이 정한 값은 하나도 바꾸지 않았다.**
- 주먹·핀치 임계값 4개(grab 0.7 / release 0.45, pinchFire 0.8 / pinchReset 0.5)는 **같은 이름·같은 값으로** `HandPuppeteerController`/`PointingBeamController`에서 `XRHandsInputSource`로 **옮겼다**(T2). 그 컴포넌트를 쓰던 씬이 없어서 잃은 인스펙터 값은 없다.
- `HandGestureTracker` 스무딩은 값은 그대로 두고 unscaled 시간으로만 바꿨다(일시정지 중에도 손 추적, T7).
- 새로 생긴 값(데미지·봇·매치·제스처 임계값 등)은 전부 위 4절과 `QUESTIONS_FOR_HYUN.md`에 있다.
