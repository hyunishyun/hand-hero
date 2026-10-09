# 두 번째 PC에서 무인 작업 돌리기

학교 교실의 다른 PC에서 Hand Hero 무인 작업을 **동시에** 돌리기 위한 준비 목록이다. 위에서부터 순서대로 하면 된다.
명령은 모두 **Windows PowerShell**에서 한 줄씩 입력한다(`&&`는 쓰지 않는다).

## 0. 먼저 알아 둘 것

- **Claude 계정 = 사용 한도.** 한도는 계정 단위다. 두 PC가 같은 계정으로 로그인하면 한도도 같이 나눠 쓴다. 그러면 두 대를 돌려도 한도에 두 배 빨리 닿을 뿐이다.
  - 이 PC(NB-R123-08)의 CLI는 `NB-R123-08@scad.edu`(조직 "Scad", subscription pro)로 로그인되어 있다. 이 계정이 2026-10-09 15:05에 월 지출 한도에 걸렸다.
  - 두 번째 PC는 **그 PC 자신의 계정이나 다른 계정**으로 로그인해야 한도가 따로 계산된다.
  - 확인 명령: `claude auth status` (이메일, 조직, 구독 종류가 나온다)
- **같은 브랜치를 두 PC가 동시에 고치면 안 된다.** PC마다 브랜치와 계획(`CLAUDE_AUTONOMOUS_PLAN.md`)을 따로 두고, 끝난 뒤 PR로 합친다.
  - 씬 파일(`.unity`)은 두 브랜치 모두에서 다시 만들어지므로 합칠 때 반드시 충돌한다. 해결법: 합친 뒤 씬 빌더(`HandHeroSceneBuilder.BuildAll`)로 씬을 다시 만들어 커밋한다.
  - `AUTO/` 진행 파일도 충돌한다. 한쪽 것을 고르고, 다른 쪽은 `AUTO/archive/`로 옮긴다.

## 1. 설치 확인 (두 번째 PC)

| 무엇 | 확인 명령 | 없으면 |
|---|---|---|
| Git | `git --version` | 학교 PC 이미지에 보통 있다. 없으면 IT 또는 git-scm.com |
| GitHub CLI(선택) | `gh --version` | 없어도 된다. git이 로그인 창을 띄운다 |
| Unity **6000.6.4f1** + Android Build Support(OpenJDK, SDK & NDK 포함) | `Test-Path "C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe"` | Unity Hub에서 **정확히 이 버전**을 설치한다. 다른 버전으로 열면 프로젝트가 업그레이드되니 절대 안 된다 |
| Claude Code CLI | `claude -v` (이 PC는 2.1.293) | 공식 설치 안내(code.claude.com/docs)를 따른다. 학교 PC라 IT 정책을 먼저 확인한다 |
| Python(선택, `run_summary.py`용) | `python --version` | 없어도 무인 작업은 돈다 |

## 2. 로그인

- Claude: `claude` 를 한 번 실행하면 브라우저 로그인이 뜬다(또는 대화 창 안에서 `/login`). 끝나면 `claude auth status`로 계정을 확인한다.
- GitHub: 처음 `git clone` 할 때 로그인 창이 뜬다. 또는 `gh auth login`.
- **PC를 떠날 때:** Windows "자격 증명 관리자"에서 `git:https://github.com`을 지운다. 공용 PC이기 때문이다.

## 3. 프로젝트 받기

경로는 짧게 둔다(Unity가 긴 경로를 싫어한다). 예시:

```
mkdir C:\HH
cd C:\HH
git clone https://github.com/hyunishyun/hand-hero.git
cd hand-hero
git fetch origin
git checkout -b <이 PC 작업 브랜치 이름> origin/<시작할 브랜치>
```

- `<시작할 브랜치>`는 그때 가장 최신 브랜치다. 예: 4차가 push된 뒤라면 `auto/2026-10-09-run`
- **Unity Hub로 한 번 열어서** 가져오기(Library 생성, 오래 걸림)가 끝나면 Unity를 **닫는다**. 무인 작업은 Unity가 꺼져 있어야 컴파일·빌드를 할 수 있다.
- `run_autonomous.ps1`과 `AUTO\tools\compile_check.ps1`은 자기가 있는 폴더를 프로젝트로 쓴다. 그래서 경로가 달라도 그대로 동작한다(2026-10-09 수정).

## 4. Claude 스킬과 설정 (선택이지만 권장)

무인 작업은 계획 파일만 보고 일하므로 스킬이 없어도 돈다. 다만 대화형으로 계획을 세우거나 리뷰할 때는 같은 규칙을 쓰는 게 좋다.

```
git clone https://github.com/hyunishyun/claude-skills.git "$HOME\.claude\skills"
Set-Content -Path "$HOME\.claude\CLAUDE.md" -Value "@~/.claude/skills/user-instructions.md"
```

- 플러그인(superpowers 등)은 대화형 `claude` 안에서 `/plugin` 메뉴로 설치한다. 이 PC에 깔린 것 중 Unity 작업에 쓰는 건 주로 **superpowers**다.
- 프로젝트 안의 `.claude\settings.json`(허용 목록, push 금지)은 저장소에 들어 있어서 따로 할 일이 없다.

## 5. 무인 작업 시작

1. 그 PC용 계획을 준비한다: `CLAUDE_AUTONOMOUS_PLAN.md`, `AUTO\PROGRESS.md`, `AUTO\DECISIONS.md`(승인됨). 대화형 Claude와 같이 써서 커밋한다. **다른 PC가 하는 작업과 파일이 겹치지 않게** 나눈다.
2. Unity를 끈다.
3. 아래 두 줄을 실행한다:

```
Set-ExecutionPolicy -Scope Process Bypass
.\run_autonomous.ps1 -Hours 12
```

- `-Ultracode`는 한도를 아주 빨리 쓴다(2026-10-09에 약 55분 만에 세션 한도 도달). 꼭 필요할 때만 붙인다.
- 끝나면 `git push -u origin <브랜치>`로 올리고 GitHub에서 PR을 만든다(무인 실행은 push하지 않는다).

## 6. 두 PC 작업 나누기 (예)

| PC | 브랜치 | 작업 |
|---|---|---|
| A (NB-R123-08) | `auto/2026-10-09-run` | 4차 S7–S10 (지형 무작위, MR 스캔 프로브, APK, 보고서) |
| B | 새 브랜치 | 겹치지 않는 작업. 예: 멀티플레이 Core 준비(FUSION_PORT_PLAN N1, Core 파일만), 튜토리얼에 새 적 소개 추가, 테스트·도구 정리 |

B의 작업은 A의 4차가 끝난 브랜치에서 시작하면 합칠 때 충돌이 가장 적다.
