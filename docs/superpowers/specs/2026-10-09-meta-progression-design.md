# Meta Progression — Design (doc only, no code)

## 한국어 요약

**목표**
- 심사위원은 보통 런을 1–3번 한다. 그래서 "한 판 더"를 부르는 장치가 필요하다.
- 첫 런이 끝나는 화면에서 바로 "다음 런은 이렇게 달라진다"가 보여야 한다. 그리고 두 번째 런은 첫 1분 안에 달라져야 한다.
- 이번 라운드(D17)에서는 설계만 한다. 코드는 Hyun이 아래 결정을 고른 뒤에 짠다.

**접근 3가지**
- **A. 시작 유물 선택 + 개인 최고 기록 (추천):** 런에서 어디까지 갔는지에 따라 "시작할 때 유물 1개 고르기"가 열린다. 메뉴와 끝 화면에 최고 기록(최고 섬, 최단 승리 시간)을 보여 준다. 지금 있는 상자 화면과 유물 3개를 그대로 쓴다.
- **B. 열쇠 → 아이템 해금:** 보스나 엘리트를 잡으면 열쇠를 얻는다. 열쇠 3개를 모으면 새 아이템이 풀에 들어온다. Crab Champions 방식이다. 하지만 1–3판으로는 해금까지 거의 못 가고, 새 아이템도 만들어야 한다.
- **C. 오늘의 시드 챌린지:** 같은 시드와 모디파이어로 하는 런에 최고 기록판을 붙인다. 다만 봇 AI와 손 입력이 결정적이지 않아서 "같은 런"이 되지 않는다. 기록판도 기기 하나 안에서만 의미가 있다.

**바뀌는 것(구현 시, 예상)**
- 새 Core 파일: `MetaProgress`(순수 규칙, 저장소 인터페이스 뒤의 PlayerPrefs)
- `RunStateMachine`: 섬 1 전에 시작 유물 상자를 여는 선택 단계
- `RunDirector`: 런이 끝날 때 기록과 같은 시점에 갱신
- `MatchHud`/메뉴: 최고 기록 한 줄, 끝 화면의 "NEW" 한 줄, 메뉴의 "START RELIC" 토글
- 씬 빌더: 버튼과 텍스트 배선

**결정(Hyun이 고를 것, 추천 답 포함)**
- **D1** 접근법 → **A**. 1–3판 안에 효과가 보이고, 새 에셋 없이 지금 있는 유물·상자 화면을 쓴다.
- **D2** 시작 유물이 열리는 조건 → **승리가 아니라 섬 5에 도달하면**(상점을 본 뒤). 진 심사위원도 두 번째 런에서 달라진 점을 받는다.
- **D3** 심사 현장에서 진행을 공유할지 → **심사위원마다 초기화**. 메뉴에 손목 길게 누르기 같은 숨은 "RESET PROGRESS"를 두거나, 쇼케이스용으로 "모두 해금" 토글을 둔다. 한 헤드셋을 돌려 쓰면 앞사람이 연 것을 다음 사람이 받기 때문이다.
- **D4** 최고 기록 기준 → **조준 모드별 최고 섬 + 최단 승리 시간**. ASSIST와 CURSOR는 난이도가 달라서 따로 센다.
- **D5** 시작 유물 고르기 방식 → **런 시작 때 유물 상자 화면(카드 3장, 이미 연 유물만) + "NONE" 카드**. 메뉴 토글보다 눈에 잘 띄고 지금 있는 UI를 그대로 쓴다.
- **D6** Greed 퍽(Glass Cannon, Blood Price)도 시작 후보로 넣을지 → **넣지 않는다**. 첫 섬부터 너무 극단적이라 Greed 상자에만 둔다.

**테스트·검증**
- `MetaProgress` 규칙(해금 조건, 최고 기록 갱신, 저장 버전, 초기화)은 Core에서 TDD로 한다. 저장소는 가짜 저장소로 테스트한다.
- 메뉴·끝 화면 배선은 컴파일 + 씬 재생성 + 기기 체크리스트로 확인한다.

**기기에서 확인할 것**
- 첫 런 끝 화면에 "NEW" 줄이 읽히는지
- 두 번째 런 시작 때 유물 카드가 나오는지
- RESET이 실수로 눌리지 않는지
- 10분 한도가 유지되는지(유물로 쉬워지면 짧아져야 한다)

**위험**
- 시작 유물이 첫 섬부터 너무 강하면 런이 싱거워진다. Glass Cannon·Blood Price 같은 Greed는 시작 후보에서 뺀다.
- PlayerPrefs는 앱을 지우면 같이 사라진다. 심사용으로는 오히려 장점이다.
- 진행을 공유하면 심사위원끼리 결과가 달라진다(→ D3).

---

## 1. Goal and constraints

- **Who plays:** judges at a showcase, 1–3 runs each, ≤ 10 minutes per run (round-2 estimate ≈ 9.3 min for a full run). Most judges will finish *one* full run, or lose one and start another.
- **So:** progression has to pay off **on the first end screen** (show what the next run gains) and **in the first minute of the next run** (the player sees the difference before island 1's fight). Slow unlock tracks (Crab Champions keys, `crab-champions-systems-analysis.md` §3.4) never reach their payoff in this window.
- **Skill, not stat inflation** (analysis §3.5, checklist §7 "meta progression = unlocks + proof of mastery"): the best run must stay possible on a fresh install; meta adds *choices*, not raw power.
- **No accounts, logins or servers** (hard rule 5). Storage is local: `PlayerPrefs`, like `AimModeSetting` and `MatchDirector.TutorialSeen` already do.
- **Comfort / UI:** no new floating text during fights; new UI lives on the menu and end panels that already exist (`MainPanel` 2×2 grid + AIM/VIEW, `MatchEndPanel`, the run choice panels).

## 2. What exists today (catalog, run, menus)

- 19 items in `ItemCatalog`: 14 stackable mods/perks, 3 **Relics** (Legendary, unique: Big Chests, Dividends, Second Wind), 2 **Greed** perks (Glass Cannon, Blood Price).
- `ChestType` already has `Relic`; `ChestRoller.Roll` handles pool filtering, unique and max level. The chest panel (`RunChoiceMenu`, 3 cards) is the run's main choice UI.
- `RunStateMachine`: Idle → Intro → Island → IslandCleared → OpenChest → (Shop before 5) → ChoosePortal …, boss on 9, Victory/Defeat.
- Run telemetry (P13) already writes one JSON line per run at the same moments a meta update would happen (end screen or quit).

## 3. Approaches

### A. Starting relic + personal best (recommended)

- **Unlock:** reaching an island milestone unlocks one relic as a **starting choice**: island 5 (after the first shop) → first relic; first Victory → second; Victory in the other aim mode → third. Order: Second Wind (safety, helps new players), Big Chests (more choices), Dividends (economy).
- **Use:** from the second run on, a "STARTING RELIC" chest opens before island 1's intro with the unlocked relics + a NONE card. Same chest UI, same input; the relic is then owned, so `ChestRoller` never offers it again in that run.
- **Personal best:** best island reached and fastest Victory time, per aim mode; shown as one line on the main menu and on the end panel with "NEW BEST" when beaten.
- **Cost:** small. One Core class, one optional run phase, two text lines, one end-screen line. No new items or art.
- **Trade-off:** shallow (3 unlocks); fine for a judged demo, thin for long-term play — B can stack on top later.

### B. Keys unlock new items into the pool

- **Unlock:** a key drops from Elite and Boss islands; 3 keys unlock one new item into the chest/shop pool (analysis §3.4). After everything is unlocked, a key buys "start with a random Epic".
- **Pros:** the pool starts small and grows, so first runs show simple synergies first; strongest long-term loop.
- **Cons:** with 1–3 runs a judge collects 1–4 keys: at most one unlock, usually after their last run. It needs new items (design, balance, sound, icons) to unlock, or it has to lock away current ones and shrink the first run. High cost for little payoff in a judged session.

### C. Daily / seeded challenge run

- **Idea:** a fixed seed + one modifier (e.g. "Elite ambush", "Glass Cannon start") with a local best board.
- **Cons:** the run is not deterministic: bot AI (`BotBrain` seeds per spawn, D9), hand input and frame timing all differ, so "the same run" is only the same portal/chest rolls. The board is per headset. Modifiers need balance work and UI. A good *later* layer once A exists.

## 4. Recommendation

Approach **A**, with the unlock tied to **progress, not victory** (D2), so a judge who loses still sees a change in run 2. Keep B and C as later layers; A's data model leaves room for both (a `keys` counter and a `challengeBest` map can be added under a new save version).

## 5. Data model (PlayerPrefs)

All keys under the prefix `hh.meta.`; one version key so a later change can migrate or reset cleanly.

| Key | Type | Meaning |
|---|---|---|
| `hh.meta.v` | int | save version (1) |
| `hh.meta.runs` | int | runs started (telemetry already has details; this is for the menu) |
| `hh.meta.wins` | int | Victories |
| `hh.meta.unlocks` | int | bit mask of unlocked starting relics (bit 0 Second Wind, 1 Big Chests, 2 Dividends) |
| `hh.meta.newUnlocks` | int | bits unlocked by the last run, cleared once the end panel showed them |
| `hh.meta.best.<Aim>.island` | int | best island reached (1–9, 10 = Victory) per aim mode |
| `hh.meta.best.<Aim>.time` | float | fastest Victory time in seconds, 0 = none |
| `hh.meta.lastStart` | string | last chosen starting relic id (pre-selects the card) |

- **Core:** `MetaProgress` (pure): `Load(IKeyValueStore)`, `OnRunEnded(result, islandReached, runSeconds, aimMode) → MetaChanges` (new unlocks, new bests), `StartingRelicChoices()`, `Reset()`. `IKeyValueStore` is a three-method interface; HandProto passes a `PlayerPrefs` adapter, tests pass a dictionary. TDD: unlock thresholds, per-mode bests, a best never gets worse, quit runs count for "island reached" but never for time, version mismatch → reset.
- **Write moment:** together with `RunLogFile.Append` in `RunDirector.FinishRecord` (end screen / quit), never during a fight; `PlayerPrefs.Save()` once there.

## 6. UI touch points

- **Main menu (`MainPanel`):** one `TextMeshPro` line above the 2×2 grid: `BEST  ISLAND 7  ·  WIN 9:12` (ASSIST/CURSOR follows the AIM button). Hidden before the first run.
- **End panel (`MatchEndPanel`):** under the run summary: `NEW BEST` and/or `UNLOCKED: SECOND WIND START`. One line each, existing font size.
- **Run start:** a new `RunPhase.StartRelic` before island 1's intro, only when at least one relic is unlocked. It reuses `RunChoiceMenu`'s chest cards (3 cards max + NONE in the 4th pedestal slot or as a card). Pausing works as in OpenChest.
- **Reset (D3):** a `RESET PROGRESS` button on the pause panel's second row, or a 3-second hold on the RUN button. It needs a confirm press, and it does not reset the aim mode or the tutorial-seen flag.
- **Showcase toggle (D3 alternative):** `[SerializeField] bool unlockAllForShowcase` on the meta component, so a build for judges can start with every relic available.

## 7. Risks

- **Balance:** Second Wind from island 1 removes the first defeat. That is fine for judges (longer, more complete runs), but run time grows only if they survive more islands; the 10-minute check still applies (`run_summary.py`).
- **Shared headset:** progress carries from one judge to the next (→ D3).
- **Discoverability:** if the end panel line is missed, the judge never knows. Keep it on the end panel, not as a toast.
- **Scope creep:** B and C are tempting; the D1 answer should state that they are post-demo.

## 8. Open questions for Hyun (each with a recommended answer)

- **D1 Which approach?** → **A** (starting relic + personal best). B and C later.
- **D2 What unlocks the first starting relic?** → **Reaching island 5**, not a Victory, so a losing judge still gets it.
- **D3 Progress at the showcase: shared or per judge?** → **Per judge**: a confirm-guarded RESET PROGRESS on the pause panel, plus an `unlockAllForShowcase` build toggle for demos.
- **D4 Personal best: per aim mode or one for all?** → **Per aim mode** (best island + fastest Victory).
- **D5 How is the starting relic picked?** → **A chest screen before island 1** (unlocked relics + NONE), reusing the run choice cards.
- **D6 Greed perks (Glass Cannon, Blood Price) as starting picks?** → **No**: too swingy for a first island; keep them in the Greed chest.
