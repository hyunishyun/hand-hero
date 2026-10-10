# T3 초안 메모 — 지형 2단계 (새 조각·테마)

브랜치 `draft/r5b-T3` (`ae71d0f` 위). 유니티 없이 만든 초안이다. 아래 결정은 통합할 때 `AUTO/QUESTIONS_FOR_HYUN.md`로 옮긴다.
(형식: 태스크 / 질문 / 택한 답 / 이유 / 뒤집으려면)

## T3-1 떠 있는 발판이 다른 조각 위에 걸쳐도 되나
- **질문:** 바닥 평면 간격 규칙(3 m)을 모든 조각 쌍에 그대로 쓸까, 발판은 위아래로 떨어져 있으면 겹쳐 보여도 될까?
- **택한 답:** 둘 중 하나가 발판이면, 바닥 평면 간격이 3 m 이상이거나 위아래 빈 높이가 `FlightLaneClearance`(2.5 m) 이상이면 된다. 바닥 조각끼리는 예전처럼 바닥 평면 간격만 본다.
- **이유:** 4차 규칙을 그대로 옮긴 Python 시뮬레이션(시드 400개)에서 벽 2·발판 2·가는 기둥 2일 때 바닥 간격만 쓰면 38%가 대체 배치로 떨어졌다. 위아래 간격을 허용하니 0%였고, 필요한 시도 수도 중앙값 4번이었다. 발판 밑으로 2.5 m 넘게 비어 있으니 영웅과 빔이 지나갈 수 있다.
- **뒤집으려면:** `Core/ArenaLayout.cs`의 `PlacePieces`에서 `|| ((floats || IsFloating(floating, j)) && VerticalGap(...) >= p.FlightLaneClearance)` 조건을 지운다. 이때는 조각 표의 최대값도 1/1/1 정도로 낮춰야 대체 배치가 잦지 않다.

## T3-2 발판 높이와 비행 통로
- **질문:** 계획의 "y 2–8"을 어떤 좌표로 볼까, 통로 여유는 얼마로 할까?
- **택한 답:** 아레나 좌표(플레이어 시작점 y=0, 바닥 y=-10)에서 발판 중심 2–8 m(`PlatformMinY`/`PlatformMaxY`). 발판 위아래로 바닥·천장·아래 조각까지 2.5 m(`FlightLaneClearance`)를 비운다. 그래서 실제 상한은 7.2 m다. 시작 높이 ±1.5 m(`StartLaneHalfHeight`) 띠에는 발판이 들어오지 않는다(기본값에서는 하한 2 m라서 작동하지 않는 안전장치다).
- **이유:** 적 스폰 높이(2–8 m)와 같은 높이라 공중 엄폐물이 된다. 바닥 조각은 아래쪽 절반만 가린다. MR TABLE에서 눈높이는 아레나 y≈+7이라 발판 윗면이 눈높이 근처까지 올 수 있다(기기 확인 항목).
- **뒤집으려면:** 씬 `Match` > `ArenaLayoutApplier` > `Rules`의 `Platform Min Y`/`Platform Max Y`/`Flight Lane Clearance`/`Start Lane Half Height`. TABLE에서 발판이 메뉴나 배너를 가리면 `Platform Max Y`를 6으로 낮춘다.

## T3-3 섬 깊이별 조각 수 (기본 표)
- **질문:** 섬마다 새 조각을 몇 개씩 둘까?
- **택한 답:** (낮은 벽 / 발판 / 가는 기둥, 최소–최대, 섬 시드로 고름)
  - 섬 1–2: 0–1 / 0–1 / 0–1
  - 섬 3–4: 1 / 0–1 / 0–1
  - 섬 5–6: 1–2 / 1 / 1–2
  - 섬 7–8: 1–2 / 1–2 / 1–2
  - 섬 9(보스): 1 / 1 / 1
  - 오늘의 기둥 2개는 항상 있다. 씬의 풀은 종류마다 2개(`ArenaPieceTable.DefaultPoolSize`)다.
- **이유:** 깊은 섬일수록 엄폐물이 늘어난다(평균 1.5 → 4 → 4.5개). 보스전은 길어서 7–8보다 덜 복잡하게 했다. 섬 1에도 새 조각이 나올 수 있어서 기기 테스트에서 바로 보인다. 모든 행의 최대 개수에서 시드 200개 모두 대체 배치 없이 놓인다(테스트).
- **뒤집으려면:** `ArenaLayoutApplier` > `Piece Table`. 모든 `Max`를 0으로 하면 4차 아레나(기둥 2개)와 같다. 3개 이상으로 올리려면 빌더의 `DefaultPoolSize`도 올리고 씬을 다시 만든다.

## T3-4 낮은 벽 방향
- **질문:** 낮은 벽(6 × 3 × 1)을 돌려서 놓을까?
- **택한 답:** 돌리지 않는다. 긴 변이 x축(좌석을 마주보는 방향)이다.
- **이유:** 배치 규칙(시야선, 과녁 피하기)이 축 정렬 상자로 계산한다. 좌석을 마주보는 방향이 엄폐물로 가장 쓸모 있다.
- **뒤집으려면:** 다음 라운드에 `ArenaLayoutResult`에 조각별 90° 회전 플래그를 넣고, 회전한 조각은 x/z 크기를 바꿔 계산한다.

## T3-5 테마 3종과 색
- **질문:** 어떤 테마와 색을 쓸까?
- **택한 답:** `Dusk`(남색빛 회색 + 따뜻한 빛 1.05), `Frost`(밝은 청회색 + 차가운 빛 1.15), `Ember`(짙은 현무암색 + 주황빛 1.1). 바닥·벽·조각 색은 채도 0.3 이하, 빛 색은 0.35 이하다(테스트). 지금 모습은 빛 1.2, 흰색이다.
- **이유:** 영웅(파랑), 적(빨강·보라·자홍), 과녁과 CURSOR 표시(주황)가 채도 0.75 이상이라, 아레나를 무채색에 가깝게 두면 적이 계속 눈에 띈다. 빛도 거의 흰색이라 영웅 색이 크게 변하지 않는다. 실제 아트는 아트 패스 때 한다.
- **뒤집으려면:** `Match` > `ArenaThemeApplier` > `Themes`에서 색을 바꾼다. 배열을 비우면 모든 섬이 지금 모습이다.

## T3-6 테마 순서
- **질문:** 섬마다 테마를 어떻게 고를까?
- **택한 답:** 런 시드로 테마 3개의 순서를 한 번 섞고, 섬 1, 2, 3, 4…에 그 순서를 반복한다. 연속한 두 섬은 테마가 절대 같지 않고, 3섬마다 세 테마가 한 번씩 나온다. 대체 배치가 나와도 테마는 적용한다.
- **이유:** 완전 무작위면 같은 테마가 연달아 나와서 "섬마다 다르게 보인다"는 목표가 약해진다.
- **뒤집으려면:** `Core/ArenaTheme.cs`의 `ArenaThemes.ForIsland`.

## T3-7 색은 MaterialPropertyBlock으로
- **질문:** 테마 색을 머티리얼 교체로 할까, MaterialPropertyBlock으로 할까?
- **택한 답:** 계획대로 MaterialPropertyBlock(기존 `RendererTint`). 런이 끝나면 블록을 지워 원래 색으로 돌린다.
- **이유:** 머티리얼을 복제하지 않는다. 대신 그 렌더러 약 10개는 SRP Batcher에서 빠진다. 큐브 10개라 비용은 작을 것으로 본다.
- **확인하려면:** 기기 `perf_log.txt`에서 테마가 있는 섬과 없는 퀵 매치의 프레임 시간을 비교한다. 차이가 보이면 테마별 머티리얼 3벌로 바꾼다.

## T3-8 런 기록 필드
- **질문:** 런 기록에 테마와 조각 수를 어떻게 남길까?
- **택한 답:** 섬마다 `layout_seed` 뒤에 `"theme"`, `"low_walls"`, `"platforms"`, `"thin_pillars"`를 쓴다. 대체 배치로 떨어진 섬은 `theme`은 있고 `layout_seed`가 없다. 버전 `v`는 1 그대로다(필드를 더하기만 함). `run_summary.py`가 테마별 섬 수·싸움 시간·받은 피해, 조각 평균, 대체 배치 수를 출력한다.
- **뒤집으려면:** `RunRecordJson.ToJson`의 `HasTerrain` 블록.

## 통합 메모 (통합하는 에이전트용)
- **검증(유니티 없이):**
  - 메인 체크아웃의 유니티 생성 csproj 4개를 스크래치 폴더로 복사해서 소스를 이 워크트리로 바꾸고 `dotnet build`로 컴파일했다. Core, EditMode 테스트, Assembly-CSharp, Assembly-CSharp-Editor(빌더 포함) 모두 오류 0이다.
  - 같은 DLL로 리플렉션 러너를 만들어 EditMode 테스트를 유니티 밖에서 돌렸다. 631개 중 629개가 통과했다. 실패 2개는 환경 문제다(`ArenaLayoutTests.SpawnPoints_InsideTheSpawnArea...`는 네이티브 `Bounds.Contains`, `PerfLogTests.Formatter_DoesNotAllocateAfterWarmUp`는 `UnityEngine.TestRunner`). 둘 다 이 초안 전부터 있던 테스트다. 새 테스트 30개는 모두 통과했다.
  - 4차 `ArenaLayout`(ae71d0f)과 새 코드를 기둥 2개 + 과녁 4개 입력으로 비교했다. 섬 시드 4500개에서 배치가 완전히 같았다. 새 조각이 0개인 섬은 4차와 같은 위치가 나온다.
  - 유니티에서 해야 할 것: `compile_check.ps1`, `-Tests`(예상 631/631), `BuildAll`(새 필드: `ArenaLayoutApplier.lowWalls/platforms/thinPillars/pieceTable/themes`, 새 컴포넌트 `ArenaThemeApplier`).
- **충돌 가능 지점:**
  - `Core/ArenaLayout.cs`: T1(S7-1-1, S7-1-2)도 고친다. T3는 `ArenaLayoutParams` 끝에 필드 4개, `Generate`의 마지막 인자 `bool[] floating = null`, `PlacePieces`, 끝부분 헬퍼 3개를 바꿨다. 대체 배열 처리(`Generate` 끝의 fallback return)는 건드리지 않았다. `ArenaLayoutApplier`가 대체 배치를 직접 처리하므로 S7-1-2를 어떻게 고쳐도 영향이 없다(대체 배열에 null을 넘긴다).
  - `Core/RunRecord.cs`·`AUTO/tools/run_summary.py`: T0도 고친다. T3는 `IslandRecord` 필드, `RunRecorder.IslandTerrain`, islands JSON 루프, `terrain_lines` 함수와 그 호출 한 줄만 바꿨다. `run_summary.py` 머리 설명문에 "island themes and terrain pieces (round 5)"를 더하면 좋다(충돌을 피하려고 이 초안에서는 안 고쳤다).
  - `HandHeroSceneBuilder.cs`: `// Round 5 T3:` 블록 두 개(레이아웃 블록 바로 뒤, `Cube` 헬퍼 바로 앞의 `PiecePool`).
  - `RunDirector.cs`: `ApplyIslandLayout`과 `layout` 툴팁만. `ArenaLayoutApplier.Apply(int)`는 `ApplyIsland(int runSeed, int island)`로 바뀌었다(다른 호출부 없음).
- **PROGRESS 제안(T3 DONE):** 섬 깊이별로 낮은 벽·떠 있는 발판·가는 기둥(풀 2개씩)을 시드로 더 놓고, 테마 3종(Dusk/Frost/Ember)을 섬마다 바꾼다. 발판은 2–7.2 m, 위아래 2.5 m 통로. 런 기록에 섬별 `theme`·조각 수. 새 테스트 30개. 결정 T3-1~T3-8.
- **기기 확인 항목:** 섬마다 모양과 색이 달라 보이는지, 어떤 조각도 시작점과 스폰을 막지 않는지, 발판 밑과 위로 날아서 지나갈 수 있는지, MR TABLE에서 발판·가는 기둥이 메뉴·배너를 가리지 않는지, 퀵 매치·튜토리얼·메뉴로 돌아오면 원래 색과 기둥 2개만 남는지, `run_summary.py`의 "Terrain per island" 줄.
