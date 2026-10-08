# DECISIONS — Phase 0 (2026-10-07, Hyun: "다 기본값으로 진행해")

Phase 0 질문 Q1–Q11은 전부 추천 기본값으로 확정됐다. 무인 세션은 이 파일을 계획서보다 구체적인 규칙으로 따른다.

## 환경 사실 (Phase 0 점검, 2026-10-07)

- 프로젝트 루트: `C:\Users\AISTUDIO\Desktop\Hyun's Playground\MetaAwards\A_4` (현재 폴더가 맞다. `E:\A_4\A_4`는 옛 사본이므로 건드리지 않는다).
- 프로젝트 Unity 버전: **6000.2.10f1** (`ProjectSettings/ProjectVersion.txt`). HANDOFF에 적힌 6000.1.14f1은 옛 정보.
- Phase 0 시점에 설치된 에디터는 **6000.6.4f1뿐**이었다 → Hyun 승인으로 6000.6.4f1로 업그레이드했다 (아래 Q1).
- Hub의 보조 프로세스 `C:\Program Files\Unity Hub\resources\unity.exe serve`는 에디터가 아니다 (`compile_check.ps1`이 이를 무시한다).
- Fusion/Photon SDK: 미임포트. `Networking\` 10개 중 7개가 `using Fusion`을 쓴다.
- `XROriginSync.cs`(루트)는 Networking 타입을 주석에서만 언급하므로 컴파일과는 무관하다.
- `HandProto\`와 `Networking\`에는 `.meta`가 없었다 (Unity가 한 번도 임포트하지 않은 상태).
- Claude CLI 2.1.293: `-p --max-turns`가 동작하는 것을 확인했다.

## 결정

| # | 결정 |
|---|---|
| Q1 (수정, Hyun 승인) | **프로젝트를 Unity 6000.6.4f1로 업그레이드했다** (Phase 0에서 수행, 클린 컴파일 확인). Android Build Support도 6000.6.4f1에 설치돼 있다. 컴파일·테스트·빌드는 반드시 `AUTO/tools/compile_check.ps1`로 돌린다(ProjectVersion과 같은 버전만 실행). **다른 에디터 버전으로 프로젝트를 열지 않는다.** |
| Q1-업그레이드 내역 | 에디터가 패키지를 자동으로 올렸다: XRI 3.2.1→3.6.1, XR Hands 1.7.0→1.9.0, OpenXR 1.15.1→1.18.0, AR Foundation 6.2.0→6.6.2, Android XR OpenXR 1.0.2→1.4.0, XR Management 4.5.3→4.7.0, URP 17.2→17.6, Input System 1.14.2→1.20.0, Timeline 1.8.9→6.6.0 외. **UnityGLTF 2.18.3은 6.6에서 컴파일되지 않아(`GetInstanceID` obsolete, URP RenderGraph API) 2.22.1로 올렸다** — 에러 63개가 전부 이 패키지에서 나왔다. `Assets/XR/AndroidXR/`는 에디터가 지웠다(androidxr-openxr 1.4.0). `Assets/Samples/XR Interaction Toolkit/3.2.1/`은 옛 버전 폴더 그대로 남아 있고 컴파일은 된다. 샘플 재임포트는 필요할 때만 한다. |
| Q1-fallback | `compile_check.ps1`이 `EDITOR_MISSING`(exit 3)을 내면(학교 PC 리셋 등) BLOCKERS에 한 번만 기록한다. 코드 작업은 계속하고 커밋 메시지에 `[unverified]`를 붙인다. |
| Q2 | `Assets/MyAssets/Scripts/Networking` → `Networking~`로 옮겼다 (git mv, 커밋됨). `.meta`가 원래 없었으므로 보관할 meta도 없다. 되돌릴 때: `git mv "Assets/MyAssets/Scripts/Networking~" Assets/MyAssets/Scripts/Networking` (Fusion 임포트 후). |
| Q3 | 브랜치 `auto/2026-10-07`, 태그 `pre-auto-2026-10-07`(= main의 베이스라인 스냅샷 `0c3df1d`). **push 금지.** |
| Q4 | `git init`(main) + Unity `.gitignore` 완료. LFS는 쓰지 않는다. |
| Q5 | 패키지 추가는 **T10 도달 시에만** 허용한다 (Unity OpenXR Meta 등). AR Foundation 6.2.0은 이미 설치돼 있다. |
| Q6 | 패스스루 테이블탑 = 옵션 토글, **기본 꺼짐**. 기본 모드는 VR 아레나. |
| Q7 | 손바닥 밀기 = **충격파(근거리 상대 감속/경직)**. 넉백은 없다(ADR 3 유지). |
| Q8 | APK 출력 = `C:\Users\AISTUDIO\Desktop\Hyun's Playground\MetaAwards\Build\`. 배치모드 Android 빌드 시도를 허용한다. 모듈이 없으면 BLOCKERS에 기록한다. |
| Q9 | 생략 (Networking 파일 수정일은 2025-11). |
| Q10 | 레거시 `Scripts\` 루트 20개 삭제 금지 + **`Assets/Scenes/`의 기존 씬(BasicScene, SampleScene, A4) 수정 금지**. 새 씬은 `Assets/MyAssets/Scenes/` 아래에 씬 빌더로 만든다. |
| Q11 | `HANDOFF_VR_HERO.md`를 Downloads에서 프로젝트 루트로 복사했다. |

## 도구

- 컴파일: `powershell -NoProfile -ExecutionPolicy Bypass -File "AUTO\tools\compile_check.ps1"`
- EditMode 테스트: 위 명령 + `-Tests`
- 에디터 메서드: 위 명령 + `-ExecuteMethod HandHero.EditorTools.HandHeroSceneBuilder.BuildAll`
- 종료 코드: 0 OK / 1 컴파일 에러·테스트 실패 / 2 기타 Unity 실패 / 3 에디터 버전 없음 / 4 Unity 실행 중
- **첫 실행은 Library 재임포트 때문에 오래 걸린다(수십 분). 기본 타임아웃은 60분.**
