# PERF_REPORT — Round 3 (멈춤 추적·성능·버그)

브랜치 `perf/freeze-hunt`(`19a5942`부터). 감사 ID는 `AUTO/PERF_AUDIT.md` 기준이다. **헤드셋에서는 아무것도 확인하지 않았다.** 아래 내용은 코드, EditMode 테스트 461개, 씬 재생성, APK 빌드까지만 검증했다.

## 1. 고친 것과 일부러 안 한 것

### 고친 것 (감사 ID별)

| 묶음 | 감사 ID | 무엇을 했나 | 태스크 |
|---|---|---|---|
| 진단 | CR-2, CR-3, RS-1, RS-15 | `PerfSpikeLogger`를 넣었다. 실제 시간으로 50 ms 넘는 프레임과 손·머리 추적, 착용, 포커스, 앱·게임 일시정지, 매치·런 단계, 시스템 제스처 엣지를 기록한다. Frame Timing Stats 켬. | P1, P9 |
| 빌드·로그 | RS-2, RS-3, CR-4 | `_dev`·`_release` APK를 따로 빌드한다. 릴리스는 빌드하는 동안만 Log·Warning 스택 트레이스를 끈다. 수다스러운 로그는 `HHLog`(개발·에디터 전용)로 옮겼다. | P2 |
| 매 프레임 낭비 | GC-1, GC-2, GC-6~9, GC-M2, GM-1~5, GM-7, GM-9, GM-11, GM-12, RF-3, RF-6, RS-12 | HUD는 값이 바뀔 때만 문자열을 다시 만든다. 조준 후보는 인덱스 루프로 돌고, 카메라·서브시스템·착용 체크는 캐시하거나 주기를 늦췄다. 링·바닥 표시는 바뀔 때만 쓰고, 메뉴 레이는 UI 레이어만 본다. | P3 |
| 머티리얼 | GC-4, GC-5, GC-10, GM-13, GM-M2, SP-2, SP-5, RS-5, BR-11 | `MaterialPropertyBlock`을 쓴다. 봇 하나당 머티리얼 4개 누수가 없어졌고, 렌더러 배열은 캐시한다. | P4 |
| 생성/파괴 스파이크 | SP-1, SP-3, SP-4, SP-6, SP-9, GC-3, GC-M1, GM-6, RF-1, RF-2(이펙트 부분), CR-6 | Core `ObjectPool<T>`(테스트 있음)를 만들었다. 피격 이펙트와 처치 폭발은 풀에서 꺼내 쓴다. 런 봇은 런 시작 때 `MaxAlive+1`개를 미리 만들고 `Activate/Deactivate`로 재사용한다(전투 중 Instantiate/Destroy 없음). | P5, P6, P12 |
| 봇 동기화 | GM-10, GM-M1, SP-7, BR-7, BC-2, BC-M1 | 봇마다 다른 시드를 주고 첫 발사 시간을 ×U(0.5, 1.0)로 흩뜨린다. | P6 |
| 끊김 뒤 튐 | BC-3, BR-12, RS-7, CR-5(고정 스텝 부분), CR-8 | 스프링을 지수 감쇠 + 서브스텝으로 바꿨다. Maximum Allowed Timestep 0.1, Fixed Timestep 0.02. 손바닥 밀기는 0.1초 넘는 프레임을 속도로 읽지 않는다. | P7 |
| 손 입력 | BC-4, BR-13, BR-1, BR-2, BR-3 | 클러치 목표를 아레나 안으로 제한한다. 입력이 다시 켜지면 상태를 리셋하고, 추적이 다시 잡힌 첫 프레임은 스냅한다. 부활 때 클러치·차지를 리셋한다. | P7 |
| 런 상태 | BR-4, BR-5, BR-6, BR-8, BR-9, BR-10, BC-1, BC-5~9, RF-4(사망 부분) | 런 중 자동 부활을 끄고, 사망은 모든 단계에서 처리한다. 봇끼리 아군 사격은 없앴다(팀). 포털 0개·MaxAlive 0·settle 0·예고 시간 0에 가드를 넣었고, 손목 일시정지는 편 손에서만 받는다. | P8 |
| 추적 끊김 표시 | CR-1, CR-7 | 손 추적이 끊기면 레티클·커서·바닥 표시가 회색이 된다. Meta 시스템 제스처 중의 핀치는 무시한다. | P9 |
| 빌더 | RF-7(실패 처리 부분) | 직렬화 필드가 없으면 `BuildAll`이 예외로 실패한다. | P15 |

성능과 별개로 들어간 것: ASSIST 차지 오인 수정(P10), 합성 효과음(P11), 피격·처치 타격감(P12), 런 기록(P13), 메타 진행 설계 문서(P14).

### 일부러 안 한 것

| 감사 ID | 이유 |
|---|---|
| RS-4, RS-9, RS-11, RS-M1 (지연 모드, FFR, 예측 시간, 깊이 제출) | D11: 기기 비교 없이 체감이 바뀌는 XR 설정이다. 아래 2절에 A/B 방법을 적었다. |
| CR-5 Physics Simulation Mode Script | 효과는 있겠지만 빔 판정이 바뀔 수 있어서 A/B 후보로만 남겼다. Fixed Timestep 0.02로 따라잡기 스텝은 이미 최대 5개로 줄었다. |
| RS-6 셰이더 워밍업·컴파일 로그 | 근거가 없고(plausible), GraphicsSettings 변경은 이번 라운드 허용 목록에 없다. 2절 실험 후보. |
| RS-8 스카이박스, RS-10 볼륨 업데이트 | 렌더 설정이라 D11에 따라 보류. 영향은 작다(low). |
| RS-13 TMP 폰트·자동 크기 | 패널을 처음 켤 때 한 번 생기는 비용이고, 근거는 plausible이다. 멈춤이 상자·상점 첫 화면에 몰리면 다시 본다. |
| RS-14 `PlayerPrefs.Save` | 메뉴·튜토리얼 이벤트 때만 실행된다(전투 중 아님). |
| SP-8 히어로에 키네마틱 Rigidbody | 레이캐스트 타이밍이 바뀔 수 있어서, 측정한 뒤에 정한다. |
| GM-8 선택 메뉴 갱신 할당 | 패널이 바뀔 때만 생기는 할당이고, 감사에서도 선택 사항이었다. |
| RF-2 `PointingBeamController` 전체 분할, RF-5 제어 게이트 통합, RF-7 `BuildScene` 분할 | D10: 리팩토링은 필요한 만큼만 한다(회귀 위험, 멈춤과 무관). |

### 최종 리뷰에서 남은 Minor (P16)

리뷰 서브에이전트(`19a5942..32d8742`) 결과: Critical·Important 0건, Minor 5건.

- **고침:** 런 기록이 "차지샷이 나간 핀치"만 적어서, D13이 겨냥한 오인(빠른 핀치에 감속·구슬이 뜨는 것)이 데이터에 안 보였다. 핀치마다 `hold_started`를 적고, 요약 스크립트는 이 값으로 오인률을 센다(`c2eec3f`).
- **남김 1:** ASSIST에서 차지를 쥔 채 오른손바닥을 얼굴로 돌려 Quest 메뉴를 열면, 시스템 제스처가 핀치를 0으로 만들어서 차지샷이 한 발 나간다. 고치려면 제스처 시작 때 차지를 취소한다(입력 배선 변경이라 무인으로 하지 않음).
- **남김 2:** `PerfSpikeLogger`가 머리 XR 장치를 매 프레임 조회한다(`MatchDirector`는 P3에서 0.25초로 줄였다). 작은 네이티브 비용이다.
- **남김 3:** 핀치 기록 리스트가 런 밖(퀵 매치)에서도 계속 쌓여, 256개를 넘으면 리스트를 키울 때 작은 할당이 생긴다. 런 중에만 기록하거나 상한을 두면 된다.
- **남김 4:** 봇이 스폰될 때마다 `System.Random`을 하나 만든다(약 300 B). 지금 스폰 빈도에서는 무해하다.

## 2. 남은 의심 원인 (순위)과 A/B 레버

1. **Link로 에디터에서 할 때 에디터가 프레임을 제때 못 넘김.** 2026-10-08 로그에서 Link 전송은 정상이었고 OpenXR 세션도 계속 FOCUSED였다. 확인 방법: 같은 플레이를 **독립형 APK**로 해 보고 멈춤이 사라지는지 본다. Link에서는 Profiler에서 EditorLoop가 큰 프레임을 찾는다(3절).
2. **손 추적 끊김이 "멈춤"처럼 보임** (CR-1). 이제 회색 표시가 뜨고 `HAND_LOST` 기록이 남는다. 멈출 때 레티클이 회색이었는지만 보면 된다.
3. **기기 쪽 정지**(머리 추적 끊김, 시스템 오버레이, 발열, 컴포지터). `HEAD_LOST`·`FOCUS_LOST` 기록과 `VrApi` logcat으로 구분한다.
4. **GPU 한계**(풀 해상도 + MSAA 4x, FFR 꺼짐). `perf_log.txt`의 `gpu=`가 프레임 예산(72 Hz면 13.9 ms, 90 Hz면 11.1 ms)을 자주 넘으면 이쪽이다.
5. **남은 스파이크**(셰이더 첫 컴파일, TMP 패널 첫 표시). 스파이크가 특정 순간(첫 상자, 첫 상점)에 몰리면 이쪽이다.

A/B 레버(모두 무인으로는 바꾸지 않았다). 한 번에 하나만 바꾸고, 같은 길이의 RUN으로 `perf_log.txt`의 스파이크 수와 `gpu=` 값을 비교한다.

| 레버 | 어디서 | 기대 |
|---|---|---|
| FFR 켜기 | Project Settings > XR Plug-in Management > OpenXR(Android) > Foveated Rendering 기능 켜기 | GPU 시간 감소 |
| MSAA 4x → 2x | `Assets/Settings/Project Configuration/Performance URP Config.asset` > Quality > Anti Aliasing | GPU 시간 감소, 가장자리가 조금 거칠어짐 |
| 지연 모드 PrioritizeInputPolling → 기본값(PrioritizeRendering) | OpenXR(Android) 설정 > Latency Optimization | 프레임 안정, 손 반응이 조금 늦어질 수 있음 |
| 깊이 제출 None → Depth 16 bit | OpenXR(Android) 설정 > Depth Submission Mode | 프레임을 놓쳤을 때 재투영 품질이 좋아짐(가장자리 번짐 감소 기대) |
| Physics 시뮬레이션 모드 Script | Project Settings > Physics > Simulation Mode | 따라잡기 물리 스텝 제거. 빔 명중이 그대로인지 꼭 확인 |

## 3. 증거 모으는 법 (PowerShell, 한 줄씩)

### 독립형 APK

APK는 `MetaAwards\Build\`에 있다: `HandHero_20261009_0359_dev.apk`(개발, Profiler 연결 가능), `HandHero_20261009_0405_release.apk`(릴리스, 실제 성능). 멈춤 확인에는 **릴리스**를 먼저 쓴다.

```powershell
$adb = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"
& $adb install -r "C:\Users\AISTUDIO\Desktop\Hyun's Playground\MetaAwards\Build\HandHero_20261009_0405_release.apk"
& $adb logcat -c
& $adb logcat -s VrApi:* Unity:* > freeze_session.txt
```

마지막 줄은 플레이하는 동안 계속 돈다. 다 하면 Ctrl+C로 멈춘다. `[PerfSpikeLogger] ...` 줄은 `Unity` 태그로 나온다(별도 태그는 없다). 그다음 로그 파일을 가져온다.

```powershell
& $adb pull /sdcard/Android/data/com.hyun.handhero/files/perf_log.txt .\perf_log.txt
& $adb pull /sdcard/Android/data/com.hyun.handhero/files/perf_log_prev.txt .\perf_log_prev.txt
```

`perf_log_prev.txt`는 파일이 1 MB를 넘어 넘겨졌을 때만 있다. 파일은 메뉴로 돌아갈 때, 런이 끝날 때, 헤드셋을 벗거나 Quest 메뉴를 열 때(포커스 잃음) 써진다. **가져오기 전에 메뉴로 한 번 돌아간다.**

`VrApi` 줄에서는 `FPS=`가 갑자기 떨어지는 시각, `Stale=`(컴포지터가 같은 프레임을 다시 쓴 횟수), `CPU`/`GPU` 레벨, `Temp`(발열)를 본다.

### Quest Link (에디터)

- `perf_log.txt` 위치: `%USERPROFILE%\AppData\LocalLow\Hyun\Hand Hero\perf_log.txt`
- 열기: `notepad "$env:USERPROFILE\AppData\LocalLow\Hyun\Hand Hero\perf_log.txt"`
- Profiler:
  1. Window > Analysis > Profiler를 열고 Record를 켠 채 플레이한다.
  2. 멈추면 바로 Record를 끈다. CPU Usage 그래프에서 가장 높은 막대를 클릭한다.
  3. Hierarchy에서 `PlayerLoop`(게임)와 `EditorLoop`(에디터) 중 무엇이 큰지 본다. EditorLoop가 크면 에디터 탓이고, 독립형 APK로 확인한다.
  4. 같은 프레임의 `GC.Alloc` 열과 가장 큰 마커(Self ms)를 적어 둔다.

### 멈출 때마다 적을 것

- 시계 시각(초까지, 휴대폰 시계도 됨)
- 화면에 무엇이 있었나(섬 번호, 상자·상점, 메뉴, 봇 수)
- 봇과 HUD도 멈췄나, 내 히어로·레티클만 멈췄나(화면 멈춤인지 조작 멈춤인지)
- 레티클이 회색이었나(손 추적 끊김)

## 4. `perf_log.txt` 읽는 법

세션마다 맨 위에 머리줄이 하나 붙는다(값은 예시).

```
=== Hand Hero perf log | 2026-10-09 21:24:01 | app 0.1 | build release | device Oculus Quest 3 | refresh 72.0 Hz | unity 6000.6.4f1 | spike > 50 ms | frame timing on | sfx 41.2 ms 600 KB
```

빌드 종류, 기기, 주사율, 스파이크 기준, Frame Timing 사용 여부, 효과음 합성에 걸린 시간을 보여 준다.

기록 한 줄 예:

```
21:24:34.567 t=1234.568 SPIKE frame=87.3 cpu=12.1 rt=3.2 gpu=-1.0 gc=+1 heap=45.2MB match=Run run=Island isl=3 bots=2 ts=1.00 focus=1 L=1 R=0 head=1
```

| 필드 | 뜻 | 이 예에서 |
|---|---|---|
| `21:24:34.567` | 기기 시계 시각. 메모한 멈춤 시각과 맞춘다 | |
| `t=` | 앱 시작 뒤 초 | |
| `SPIKE` | 종류. 그 밖에 `HAND_LOST hand=R`, `HEAD_LOST`, `FOCUS_LOST`, `APP_PAUSE`, `GAME_PAUSE`, `RUN_PHASE`, `SYSGESTURE_START` 등 | 50 ms 넘는 프레임 |
| `frame=` | 이 프레임의 실제 간격(ms) | 87 ms = 72 Hz에서 프레임 6개를 놓침 |
| `cpu=` / `rt=` / `gpu=` | 메인 스레드 / 렌더 스레드 / GPU 시간(ms). -1은 측정값 없음 | CPU 12 ms로는 87 ms가 설명되지 않는다 → 게임 코드 밖(OS, 컴포지터, IO)을 의심 |
| `gc=+` | 이전 기록 뒤 GC 횟수 | 1번 있었다 |
| `heap=` | 관리 힙 크기 | |
| `match=` / `run=` / `isl=` / `bots=` | 그때 게임 상태 | 런 3번 섬 전투, 봇 2마리 |
| `ts=` | `Time.timeScale` (0이면 게임 일시정지) | |
| `focus=` | 앱 포커스 | |
| `L=` / `R=` / `head=` | 왼손 / 오른손 / 머리 추적 | **R=0: 오른손(조준 손) 추적이 끊겨 있었다** |

읽는 순서는 이렇다.

1. 메모한 멈춤 시각 근처의 줄을 찾는다.
2. `SPIKE`가 있으면 `cpu`·`gpu`가 큰지 본다. 둘 다 작으면 게임 밖 원인이다.
3. `SPIKE`가 없는데 멈췄다면, 앱은 제때 프레임을 냈다는 뜻이다. 근처의 `HAND_LOST`·`HEAD_LOST`·`FOCUS_LOST`, 또는 logcat `VrApi`의 `Stale`을 본다. Link라면 전송·에디터 쪽이다.

## 5. 런 기록(`run_log.jsonl`) 가져오기와 요약

런이 끝날 때마다(VICTORY·DEFEAT 화면, 또는 런 중에 MENU로 나감) 한 줄씩 붙는다. 전투 중에는 파일에 쓰지 않는다. `RunDirector`의 `writeRunLog`로 끌 수 있다.

**독립형 APK (Quest):** PowerShell에서 한 줄씩 실행한다.

```powershell
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" pull /sdcard/Android/data/com.hyun.handhero/files/run_log.jsonl .\run_log.jsonl
python AUTO\tools\run_summary.py .\run_log.jsonl
```

**에디터(Quest Link):** 파일은 `%USERPROFILE%\AppData\LocalLow\Hyun\Hand Hero\run_log.jsonl`에 있다.

```powershell
python AUTO\tools\run_summary.py "$env:USERPROFILE\AppData\LocalLow\Hyun\Hand Hero\run_log.jsonl"
```

여러 파일을 한 번에 넣어도 된다. 예시 데이터로 시험: `python AUTO\tools\run_summary.py AUTO\tools\fixtures\run_log_sample.jsonl`

**요약에 나오는 것:** 런 수·승률, 승리 런 시간(중앙값·최대, 10분 초과 수), 섬별 사망(전체/그 섬에서 진 런), 오래 걸린 섬, 많이 고른 아이템, 차지 오인률(0.5초 미만 핀치 중 차지가 시작된 비율), 명중률, 먼저 돌릴 손잡이 제안(`EnemyHealthPerIsland` → `BossHealthMult` → Arena 봇 수, 차지 오인이 10% 넘으면 `HoldDelay`).

**한 줄의 주요 필드:**

- `seed`: 이 세션의 RNG 시드. 같은 앱 실행에서 두 번째 런부터는 이어지는 난수라 재현용이 아니다.
- `aim`(Assist/Cursor), `view`(Vr/Table), `result`(Victory/Defeat/Quit)
- `total_s`: 일시정지를 뺀 런 시간
- `death_island`, `revives`
- `islands[]`: `n`, `type`, `fight_s`(FIGHT부터 클리어까지), `damage`(받은 피해), `deaths`
- `items[]`: `id`, `level`, `island`, `from`(chest/shop)
- `shots` / `hits` / `hit_rate`: 봇·히어로에 맞은 빔만 명중으로 센다
- `charge_shots`
- `hold_s[]`: 핀치마다 쥔 시간
- `hold_charged[]`: 그 핀치로 차지샷이 나갔는지
- `hold_started[]`: 그 핀치로 차지(감속·구슬)가 시작됐는지. P16에서 추가했고, 예전 기록에는 없다.
