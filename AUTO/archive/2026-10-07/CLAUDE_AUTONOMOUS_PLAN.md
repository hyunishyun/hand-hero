# CLAUDE_AUTONOMOUS_PLAN — VR 핸즈퍼스트 아레나 히어로 무인 작업 계획

> 이 파일은 Claude Code가 **Hyun 부재 중(약 12시간) 무인으로** 작업하기 위한 운영 지침이다.
> 매 세션 시작 시 이 파일을 처음부터 끝까지 읽는다. 세션은 리밋/크래시로 언제든 끊길 수 있고,
> `run_autonomous.ps1`이 새 세션을 띄워 이어서 작업시킨다. **기억은 파일과 git에만 남는다.**
>
> 배경 문서: 프로젝트 루트의 `HANDOFF_VR_HERO.md` (반드시 1회 정독), `HANDPROTO_GUIDE.md`, `SETUP_GUIDE.md`
> 오너: Hyun. 설명/로그는 한국어, 코드 주석은 영어.

---

## 0. 대회 사실 (2026-10-07 공식 페이지에서 확인됨)

- 마감 **2026-11-18 12:00 PST**. 제출물 = APK(개발자 대시보드 "Competition" 릴리스 채널 + 초대 URL) + 3분 미만 영상 + 폼.
- 하드 요건: **컨트롤러를 한 번도 페어링하지 않고 처음부터 끝까지 완주 가능**해야 함.
- 가이드: 착석 플레이(2피트 반경), 빠른 콜드 스타트, 깔끔한 일시정지/재개, 10분 안에 완결된 재미.
- 심사 기준: 혁신성 / 경험 설계(착석·핸즈퍼스트) / 기술 구현(핸드트래킹, 시선, 패스스루, 공간 앵커, FoV 인지) / 완성도.
- 특별상: Best Social & Multiplayer, Best First Five Minutes, Boldest Original Concept 등.
- **결론: 심사위원은 혼자 테스트한다 → 싱글 봇전이 본편, 멀티는 보너스.**

---

## 1. 두 가지 모드

### Phase 0 — 대화형 (Hyun이 자리에 있을 때 딱 1회)
Hyun이 `claude`를 대화형으로 실행하고 "CLAUDE_AUTONOMOUS_PLAN.md 읽고 Phase 0 진행해"라고 하면:

1. 환경 점검 (읽기 전용):
   - **경로 주의**: HANDOFF 문서의 `E:\A_4\A_4`는 옛 경로다. 실제 프로젝트는 이 파일에 적힌 `C:\Users\AISTUDIO\...\MetaAwards\A_4`.
     현재 폴더에 `Assets`, `Packages`, `ProjectSettings`가 있는지 확인. 없고 하위 `A_4\`에 있으면 그게 진짜 루트이므로
     Hyun에게 알리고, 이 파일과 `run_autonomous.ps1`의 경로를 고친 뒤 파일들을 그 루트로 옮기자고 제안.
   - 경로에 공백과 아포스트로피(`Hyun's Playground`)가 있다. 모든 명령에서 경로를 **큰따옴표**로 감쌀 것 (작은따옴표 금지).
   - `git status` / `git log -5` / 리모트 유무. git 저장소가 아니면 그 사실 보고.
   - `Assets/MyAssets/Scripts/` 하위 폴더 목록, `Packages/manifest.json` (XR Hands, XRI, OpenXR, Photon 존재 여부)
   - Unity 에디터 경로 탐색: `C:\Program Files\Unity\Hub\Editor\6000.1.14f1\Editor\Unity.exe` 등
   - Android 빌드 모듈 설치 여부 (`...\Editor\Data\PlaybackEngines\AndroidPlayer` 존재)
   - `claude --version`, `claude --help`로 이 버전에서 쓸 수 있는 권한 플래그 확인
2. **아래 질문 목록을 한 번에 한국어로 묻는다.** 각 질문에 [추천 기본값]을 붙이고,
   Hyun이 "다 기본값" 이라고 하면 전부 기본값으로 확정한다. (Hyun은 "추천대로 진행" 권한을 이미 줬음)
3. 답을 `AUTO/DECISIONS.md`에 기록하고 커밋한다.
4. `.claude/settings.json`이 없으면 Hyun이 준 템플릿(`claude_settings.json`)을 그 위치에 복사.
5. 베이스라인: 작업 브랜치 생성, 백업 태그, 첫 컴파일 체크 (T0 일부를 여기서 수행해 무인 모드 실패를 미리 잡는다).
6. Hyun에게 출발 전 체크리스트를 출력하고 종료:
   - Unity 에디터 **완전히 종료** (배치모드 컴파일이 프로젝트 락에 걸림)
   - PC 절전/화면잠금 해제는 스크립트가 처리하지만, **노트북이면 전원 연결**
   - PowerShell에서 `.\run_autonomous.ps1` 실행 후 출발

#### Phase 0 질문 목록
| # | 질문 | 추천 기본값 |
|---|---|---|
| Q1 | Unity.exe 경로가 맞는가? (탐색 결과 제시) | 탐색 결과 |
| Q2 | Fusion SDK 미임포트면 `Networking\` 폴더를 `Networking~`로 이름 변경해 Unity가 무시하게 해도 되는가? (파일 보존, 나중에 되돌림) | 예 |
| Q3 | git: 작업 브랜치 `auto/2026-10-07` 생성 + 시작 시점 태그 `pre-auto-2026-10-07`. 리모트 push 허용? | 브랜치·태그 예 / **push 아니오** |
| Q4 | git 저장소가 아니라면 `git init` + Unity용 .gitignore 생성해도 되는가? | 예 (Library/Temp/Build 등 제외) |
| Q5 | 패키지 추가 허용? (Unity OpenXR Meta + AR Foundation — 패스스루 모드용, T10에서만) | 예, 단 T10 도달 시에만 |
| Q6 | 패스스루 테이블탑 모드를 **옵션 토글**로 구현해도 되는가? (기본 모드는 기존 VR 아레나 유지) | 예 (토글, 기본 꺼짐) |
| Q7 | "손바닥 밀기=넉백"은 ADR 3번(물리 넉백 없음)과 충돌. 넉백 대신 **충격파 = 상대 감속/경직**으로 구현? | 예 |
| Q8 | APK 빌드 출력 경로 = `C:\Users\AISTUDIO\Desktop\Hyun's Playground\MetaAwards\Build\` 맞는가? 배치모드 Android 빌드 시도 허용? | 예 |
| Q9 | Networking 스크립트 작성 날짜 (디비전 판단용, 코드 작업과 무관) | 모르면 생략 |
| Q10 | 그 밖에 손대면 안 되는 폴더/씬/에셋? | 레거시 `Scripts\` 루트 20개 삭제 금지 (이미 규칙) |

### Phase A — 무인 모드 (`run_autonomous.ps1`이 `claude -p`로 반복 실행)
- **절대 질문하지 않는다.** 아무도 대답하지 않는다. 판단이 필요하면 추천 기본값을 택하고
  `AUTO/QUESTIONS_FOR_HYUN.md`에 "무엇을 왜 그렇게 결정했는지" 남기고 진행.
- 막히면 `AUTO/BLOCKERS.md`에 기록하고 **다음 태스크로 넘어간다.** 같은 문제에 30분 이상 쓰지 않는다.

---

## 2. 무인 세션 프로토콜 (매 세션 반드시 이 순서)

1. 이 파일 → `AUTO/PROGRESS.md` → `AUTO/DECISIONS.md` → `AUTO/BLOCKERS.md` → `git log --oneline -15` 순으로 읽는다.
2. `PROGRESS.md`에서 `IN_PROGRESS` 태스크가 있으면 이전 세션이 중간에 끊긴 것:
   - `git status`로 미커밋 변경 확인 → 컴파일 체크 → 통과하면 마저 완성, 엉망이면 `git stash push -m "abandoned-<task>"` 후 그 태스크를 처음부터.
3. 없으면 섹션 4의 순서에서 첫 번째 미완료 태스크를 `IN_PROGRESS`로 표시하고 시작.
4. 작업 단위는 작게. 태스크 하나 끝날 때마다:
   - 컴파일 체크 통과 (섹션 3) → EditMode 테스트 통과(있으면)
   - `git add -A && git commit -m "[auto] T<n>: <요약>"`
   - `PROGRESS.md`의 해당 태스크를 `DONE`으로, 핵심 결과/주의점 2~4줄 기록 후 커밋
5. **한 세션에서 태스크 최대 2개**를 끝내면 PROGRESS.md를 정리하고 세션을 종료한다
   (컨텍스트를 새로 시작하는 편이 품질이 높다. 스크립트가 바로 다음 세션을 띄운다).
6. 모든 태스크가 DONE/BLOCKED이면 `PROGRESS.md` 맨 위에 `STATUS: ALL_DONE` 한 줄을 쓰고,
   섹션 6의 귀환 리포트를 작성·커밋한 뒤 종료한다.

---

## 3. 검증 방법 (헤드셋 없음)

**컴파일 체크** (T0에서 `AUTO/tools/compile_check.ps1`로 만들어 둘 것):
```
& "<Unity.exe>" -batchmode -nographics -quit -projectPath "C:\Users\AISTUDIO\Desktop\Hyun's Playground\MetaAwards\A_4" -logFile "AUTO\logs\compile.log"
```
- 종료 코드 + 로그의 `error CS` 검색으로 판정. "another Unity instance" 에러면 BLOCKERS에 기록하고
  코드 작업은 계속하되 커밋 메시지에 `[unverified]` 표시.

**EditMode 테스트**: `-runTests -testPlatform EditMode -testResults AUTO\logs\tests.xml` (`-quit` 빼고).

**씬/프리팹은 손으로 YAML 편집하지 말 것.** 반드시 에디터 스크립트로 생성한다:
`Assets/MyAssets/Editor/HandHeroSceneBuilder.cs`에 `[MenuItem]` + 배치모드용 static 메서드를 만들고
`-executeMethod HandHero.EditorTools.HandHeroSceneBuilder.BuildAll`로 실행. 재실행해도 같은 결과(멱등).

**헤드셋 없이 플레이 로직 검증**: 입력 추상화(T2) 덕분에 `ScriptedHandInputSource`로
"주먹→끌기→놓기→조준→핀치" 시퀀스를 재생하는 PlayMode/EditMode 테스트를 만들 수 있다.

---

## 4. 태스크 큐 (위에서부터 순서대로. 우선순위 = 심사 가능 빌드에 가까워지는 순)

### T0. 작업 기반
- 브랜치/태그 (DECISIONS 따라), `AUTO/` 폴더 (`PROGRESS.md`, `BLOCKERS.md`, `QUESTIONS_FOR_HYUN.md`, `logs/`, `tools/`)
- `AUTO/logs/`는 .gitignore. Q2 승인 시 `Networking` → `Networking~` (+ `.meta` 처리 주의: `Networking.meta`도 함께 옮겨 보관)
- 베이스라인 컴파일 체크 통과 확인. **DONE 기준: 클린 컴파일.**

### T1. 순수 로직 어셈블리 + 테스트 기반
- `Assets/MyAssets/Scripts/Core/` + `HandHero.Core.asmdef` (UnityEngine만 참조, Assembly-CSharp 참조 금지).
  여기에 MonoBehaviour와 분리된 순수 로직을 둔다: `HysteresisGate`, `SpringFlightModel.Step()`,
  `ClutchMapper`(상대 매핑), 이후 `MatchStateMachine`.
- 기존 HandProto 스크립트는 이 로직을 호출하도록 리팩토링하되 **인스펙터 필드명/기본값 유지** (Hyun의 튜닝값 보존).
- `Assets/MyAssets/Tests/EditMode/` + asmdef, 위 로직 단위 테스트.
- DONE: 테스트 통과, HandProto 동작 의미 불변.

### T2. 입력 추상화 = 미래의 네트워크 입력
- `HandInputData` 구조체 (Core): clutchHeld, clutchDelta 또는 목표점, aimOrigin/aimDir, fireTriggered, 제스처 플래그 비트필드.
- `IHandInputSource`: `XRHandsInputSource`(HandGestureTracker 사용), `DebugKeyboardMouseInputSource`(에디터용: 우클릭=클러치, 마우스=이동, 좌클릭=발사 등),
  `ScriptedHandInputSource`(테스트용), `BotInputSource`(T3).
- 컨트롤러들은 소스를 모르고 `HandInputData`만 소비 (ADR 9번).
- DONE: 에디터 Play 모드에서 키보드/마우스로 히어로 비행·발사 가능 (헤드셋 없이 Hyun이 귀환 후 바로 확인 가능).

### T3. 봇 상대 (본편의 핵심)
- `BotBrain` → `BotInputSource`로 **플레이어와 같은 입력 경로**를 사용 (같은 FlyingCharacter, 같은 속도 캡 = 공정).
- 상태: 접근 / 거리 유지 스트레이프 / 예고 후 발사 / 피격 직후 회피. 난이도 파라미터(반응시간, 조준 오차, 발사 간격) ScriptableObject.
- DONE: 테스트 씬에서 봇이 날아다니며 타겟(플레이어 히어로)을 공격.

### T4. 전투 규칙 (싱글 버전, Fusion 이식 대비)
- `HeroHealth`: ADR 3번 그대로 — 체력 감소 + 2초 50% 감속 + 0이면 3초 후 리스폰. `NetworkedPlayerHealth`와 같은 의미론으로 작성하고 주석에 대응 관계 명시.
- **적 빔 예고선**(약 0.6초, 굵어지며 색 변화) → 플레이어가 왼손 급끌기로 회피하는 루프.
- 피격 표현은 화면/히어로 이펙트만. **XR Origin에 어떤 힘도 가하지 않는다.**
- DONE: 봇 vs 플레이어 서로 맞추고 리스폰.

### T5. 제스처 2개 추가 (모두 히스테리시스)
- 두 손 모으기 → 차지샷 (차지 시간에 비례한 데미지/굵기, 차지 중 이동 불가 = 리스크).
- 손바닥 밀기 → 충격파 = 근거리 상대 감속/경직 (Q7 결정 따름).
- 제스처 인식은 XRHandsInputSource 쪽, 결과는 HandInputData 플래그로만 전달.
- DONE: 디버그 입력으로도 발동 가능 (키 매핑), 단위 테스트로 임계값 플리커 없음 확인.

### T6. 매치 루프
- `MatchStateMachine`(Core): Boot → Menu → Tutorial? → Countdown → Fight → RoundEnd → MatchEnd. 3판 2선승, 라운드 시간 제한.
- 매치 1판 10분 이내 (대회 가이드). 점수/라운드 월드 스페이스 HUD (FoV 중앙 근처, 목 돌리지 않아도 보이게).
- DONE: 메뉴에서 시작 → 매치 끝 → 메뉴 복귀가 디버그 입력으로 완주 가능.

### T7. 손만으로 되는 UI + 일시정지 (하드 요건)
- 월드 스페이스 메뉴: XRI Poke 또는 핀치 선택. 버튼 크고 간격 넓게.
- 손목 메뉴 버튼(왼손 손바닥 위로 뒤집으면 표시) → 일시정지/재개/메뉴로.
- `OnApplicationFocus/Pause` + OpenXR 세션 상태 변화 시 자동 일시정지, 트래킹 로스 시 클러치 해제 유지.
- 콜드 스타트: 부팅 → 메뉴까지 최소 단계.
- DONE: 컨트롤러 관련 코드 경로 없이 전체 흐름 완주 가능 (코드 검색으로 확인해 PROGRESS에 기록).

### T8. 30초 튜토리얼 ("Best First Five Minutes" 겨냥)
- 단계: 주먹 쥐기 → 히어로를 링까지 끌기 → 놓고 활공 → 가리키기 → 핀치로 타겟 맞추기 → 예고선 피하기.
- 각 단계 성공 조건 자동 감지, 텍스트 최소화 + 손 고스트 애니메이션 자리(플레이스홀더).
- 첫 실행 시 자동, 메뉴에서 재실행 가능.

### T9. 씬 빌더 + Android 빌드 파이프라인
- `HandHeroSceneBuilder.BuildAll()`: `Arena_Main.unity` 생성 (고정 XR Origin, 아레나 35×20×35 ~20m 앞, 플레이어 히어로, 봇, 매니저, UI). 빌드 세팅에 등록.
- 레이어 규칙 자동 설정: FlyingHero 레이어 aimMask 제외, Reticle 콜라이더 없음 (핸드오프 "알려진 함정").
- `BuildScript.BuildQuestApk()`: Android, ARM64, IL2CPP, OpenXR + Meta Quest 기능 + **핸드트래킹 지원 선언**, 출력 `C:\Users\AISTUDIO\Desktop\Hyun's Playground\MetaAwards\Build\HandHero_<날짜>.apk`.
- 배치모드 빌드 시도. 실패 시 원인을 BLOCKERS에 상세 기록 (모듈 미설치, SDK 경로 등).
- DONE: APK 생성 또는 명확한 블로커 기록.

### T10. (승인 시) 패스스루 테이블탑 모드
- Q5·Q6 승인 시에만. `ArenaRoot` 스케일 모드 전환(VR 아레나 ↔ 테이블 위 ~1m), 모드별 positionScale/aim 파라미터 프로파일.
- 패스스루 켜기/끄기, 카메라 배경 투명 처리. 기본값은 VR 아레나.
- 패키지 추가 후 컴파일 깨지면 즉시 되돌리고 BLOCKERS에 기록.

### T11. Fusion 이식 설계 문서 (코드 X, SDK 없음)
- `AUTO/FUSION_PORT_PLAN.md`: HandInputData → `INetworkInput` 매핑, SpringFlightModel을 FixedUpdateNetwork로 옮기는 방법,
  봇을 Host가 시뮬레이션하는 구조(심사위원 혼자서도 "멀티 아레나" 체험 가능하게), 남은 Networking 스크립트별 처리 표.

### T12. 버퍼 태스크 (위가 다 끝났을 때만)
- 그레이박스 폴리시: 히어로/봇 실루엣 구분, 빔/히트 VFX, 오디오 훅(AudioSource 자리만), 성능 점검(Draw call, 오버드로).
- 테스트 커버리지 보강.

---

## 5. 하드 규칙 (어기면 안 됨)

1. **XR Origin은 절대 이동/회전하지 않는다.** 추적 카메라 금지. (멀미 원칙, ADR 4·5)
2. 레거시 `Scripts\` 루트 20개 **삭제 금지**. 수정이 꼭 필요하면 최소한으로, 사유를 커밋 메시지에.
3. Networking 코드 삭제 금지 (`Networking~` 보관만).
4. 모든 제스처 임계값은 히스테리시스. 튜닝 노브는 `[SerializeField]` + 툴팁 + 핸드오프 기본값.
5. 계정 생성, 로그인, 결제, 외부 서비스 가입 금지 (Meta, Photon, Devpost 등). 필요하면 QUESTIONS_FOR_HYUN에.
6. git push 금지(승인 없으면), force 계열 명령 금지, `git reset --hard`로 커밋된 작업 날리기 금지.
7. 프로젝트 폴더(`C:\Users\AISTUDIO\Desktop\Hyun's Playground\MetaAwards\A_4`)와 빌드 출력(`C:\Users\AISTUDIO\Desktop\Hyun's Playground\MetaAwards\Build`) 밖의 파일 수정 금지.
8. `ProjectSettings/` 변경은 T9·T10에서만, 바꾼 항목을 PROGRESS에 목록화.
9. 승인되지 않은 패키지 추가 금지.
10. 컴파일 깨진 상태로 커밋 금지 (불가피하면 `[broken]` 표시 + 즉시 다음 커밋에서 수정).

---

## 6. 귀환 리포트 (`AUTO/REPORT_FOR_HYUN.md`, 한국어)

Hyun이 돌아와서 5분 안에 상황을 파악할 수 있게:
1. 완료 / 블로커 / 미착수 태스크 표
2. **헤드셋에서 확인할 것 체크리스트** (우선순위순, 각 항목에 "무엇을 느껴봐야 하는지")
3. 에디터에서 바로 확인하는 법 (씬 이름, 디버그 키 매핑)
4. 내가 대신 내린 결정 목록 (QUESTIONS_FOR_HYUN 요약) — Hyun이 뒤집을 수 있게
5. 다음에 하면 좋을 작업 3개
6. 바뀐 튜닝 기본값이 있으면 전부 표기 (원칙적으로 바꾸지 않음)
