# CLAUDE_AUTONOMOUS_PLAN — Round 2: RUN mode (Crab Champions systems)

> Operating instructions for Claude Code while Hyun is away (~6 hours, from 2026-10-08 ~14:00).
> Read this file top to bottom at the start of every session. Sessions can end at any time
> (usage limits, crashes); `run_autonomous.ps1` starts a new one.
> **Memory lives only in files and git.**
>
> Background (read once per session, skim later):
> - `docs/crab-champions-systems-analysis.md` — the design reference Hyun wants applied.
>   Take the structure; the numbers are only starting points.
> - `HANDOFF_VR_HERO.md` — concept, ADRs, hard rules.
> - `docs/superpowers/specs/2026-10-08-aim-*.md` — the current aim systems.
>
> Logs, reports and decision notes are in Korean. Code, comments and commit messages are in English.
> The previous run's plan is archived in `AUTO/archive/2026-10-07/`.

---

## 0. Facts

- Unity **6000.6.4f1**. Branch **`auto/2026-10-08-run`** (from `hyun/aim-modes` @ `33f3204`). Remote `origin` exists.
- Competition: hands only, seated, quick start/pause, **a complete fun session within 10 minutes**.
  Judges play alone, so the bot game is the main game.
- Tests: 170 EditMode tests pass at the start. They must all stay green.

## 1. Mode

**Unattended.** Never ask questions; nobody answers.
- When a choice is needed, take the most reasonable default and log it in `AUTO/QUESTIONS_FOR_HYUN.md`
  (task / question / choice / why / how to undo).
- When stuck, log it in `AUTO/BLOCKERS.md` and move on to the next task. Don't spend more than 30 minutes on one problem.

## 2. Session protocol (every session, in this order)

1. Read this file, then `AUTO/PROGRESS.md`, `AUTO/DECISIONS.md`, `AUTO/BLOCKERS.md`, and `git log --oneline -15`.
2. If a task is `IN_PROGRESS`, the previous session was cut off.
   - Run `git status`, then a compile check.
   - If it is salvageable, finish it.
   - Otherwise run `git stash push -m "abandoned-<task>"` and restart the task.
3. Otherwise mark the first `TODO` task `IN_PROGRESS` and start it.
4. When a task finishes:
   - compile check → EditMode tests → commit `[auto] R<n>: <summary>`
   - mark it `DONE` in `PROGRESS.md` with 2–4 lines of results and caveats, then commit.
5. **At most 2 tasks per session.** Then tidy `PROGRESS.md` and end the session.
6. When every task is `DONE` or `BLOCKED`:
   - write the return report (section 6)
   - put `STATUS: ALL_DONE` on the first line of `PROGRESS.md`
   - commit and end.

## 3. Verification (no headset)

- Commands (Unity must not be running; exit 4 = Unity open → log a blocker, keep coding, mark commits `[unverified]`):
  - Compile: `powershell -NoProfile -ExecutionPolicy Bypass -File "AUTO\tools\compile_check.ps1"`
  - Tests: the same command + `-Tests`
  - Scenes: the same command + `-ExecuteMethod HandHero.EditorTools.HandHeroSceneBuilder.BuildAll`
    - Then grep `AUTO\logs\execute.log` for `has no field` / `has no array field` (must be empty).
  - APK: the same command + `-BuildTarget Android -TimeoutMinutes 120 -ExecuteMethod HandHero.EditorTools.BuildScript.BuildQuestApk`
    - The APK build regenerates the scenes; commit them.
    - Always `git checkout -- ProjectSettings/UnityConnectSettings.asset` before committing.
- **Pure logic goes in `HandHero.Core` with EditMode tests first (TDD)**: write the test, watch it fail, implement, watch it pass.
  - MonoBehaviour wiring and scenes = compile + scene build + the device checklist.
- **Never hand-edit `.unity` YAML.** Scenes come only from `HandHeroSceneBuilder.BuildAll`, which must stay idempotent.
- Use `git add <paths>`. Never `git add -A`, because `Library/` noise or stray files could get in.

## 4. Task queue (in order)

Decisions behind these tasks: `AUTO/DECISIONS.md` (Hyun approved all on 2026-10-08).

### R1. Tabletop assist cone (Q7)
- `PointingBeamController`: add `tabletopAssistAngle` 6 and `tabletopAssistReleaseAngle` 8, both with tooltips.
- Use them while the view is the tabletop. Detect it via `HandGestureTracker.Instance.WorldScale > 1.5`, or via an optional `ArenaViewMode` reference; pick one.
- VR arena keeps 4 / 6. The bot is unaffected (its assist is 0).
- DONE: compile + tests; the decision is logged.

### R2. Item foundation (Core, TDD) — doc §1, §6.1, §6.2
- `ItemDefinition` (plain C# class or struct), with:
  - `Id`, `Name`, `Description` (English, one line)
  - `Category` {WeaponMod, AbilityMod, Perk, Relic}
  - `Rarity` {Common, Epic, Legendary, Greed}
  - `Theme` {Damage, Ability, Health, Speed, Economy, Critical, Defense}
  - `SpawnWeight`, `Tags`, `RequiredTags`, `Unique`, `MaxLevel`
  - one or more `StatEffect` (stat, base, ScalingType), plus optional debuff effects
- `ScalingType` {Linear, LinearMultiplier, Hyperbolic, Exponential} and `Scaling.Evaluate(type, base, level)`, exactly as doc §6.2. Damage-taken has a global floor: you always take at least 25%.
- `Inventory`: `Add(id)` levels up a duplicate; `Level(id)`, `OwnedTags`, `TotalLevels`. Unique items cannot be added twice.
- `ItemCatalog` (static, Core): the starter list in §5.
- Tests: every scaling type, the hyperbolic limit < 1, the damage-reduction floor, inventory leveling, unique rejection, owned tags.

### R3. Hero stats from the inventory (Core, TDD)
- `HeroStats` struct and `HeroStats.From(Inventory, ItemCatalog)`.
- Fields:
  - combat: `DamageMult`, `FireCooldownMult`, `CritChance`, `CritDamageMult`, `ChargeDamageMult`, `ChargeTimeMult`, `ShockwaveRadiusMult`, `StunDurationAdd`
  - survival: `MaxHealthAdd`, `MaxHealthMult`, `HealOnClear`, `SpeedMult`, `DamageTakenMult` (floored at 0.25)
  - economy and run: `CrystalGainMult`, `CrystalPerClear`, `ExtraChoices`, `InterestRate`, `Revives`
- Empty inventory = neutral values (all mults 1, adds 0).
- Tests per item family, including Greed debuffs and the floor.

### R4. Chest and portal rolling (Core, TDD) — doc §1.3–1.4, §2.2–2.3, §6.3
- Seeded RNG (`System.Random`, injectable) so tests are deterministic.
- `ChestRoller.Roll(chestType, inventory, rng)` returns item choices.
  - Default 3 choices + `ExtraChoices`. Upgrade = 2 choices, owned items only. Relic and Greed = 1.
  - Weighted sampling without replacement.
  - Pool filters: required tags must be owned, unique items not already owned, chest type ↔ theme / rarity rules.
  - Owned items get ×1.75 weight.
- Chest types: Damage, Ability, Health, Economy, Random, Epic, Upgrade, Relic, Greed, Spiked.
  - Spiked = Epic pool; the cost is applied by the run, see R6.
- `PortalRoller`: after a clear, offer 2 portals (3 at 50% chance from island 3 on). Each portal = (next island type, chest type).
  - The first island of the run always rewards a Damage chest.
  - Never offer two identical portals.
- Tests: filters, owned bonus (statistical over N seeded rolls), unique exclusion, the upgrade-only rule, no duplicate choices.

### R5. Run state machine (Core, TDD) — doc §2, §3.3
- `RunStateMachine` phases: `Intro → Island(n) → IslandCleared → ChoosePortal → OpenChest → (Shop) → … → Boss → Victory | Defeat`.
- **9 islands**:
  - 1–8 are Arena / Horde / Elite, picked through the portals.
  - The shop is forced before island 5.
  - Island 9 = Boss.
- Island rules:
  - Arena: defeat all bots. Count 1 (islands 1–2), 2 (3–5), 3 (6–8).
  - Horde: survive 45 s; bots respawn, at most 2 alive.
  - Elite: 1 bot with health ×3, damage ×1.5.
  - Boss: 1 bot with health ×6 and a faster fire interval.
- Enemy scaling: bot max health × (1 + 0.15 × islandIndex).
- **Player health carries over between islands.** Heal 25 + `HealOnClear` on each clear.
- **Death = run over** (Defeat), unless `Revives` > 0.
- Time budget: aim for about 8–9 minutes total. Log the estimate in `PROGRESS.md`.
- Pause works like the match: the timers stop.
- Tests: the full happy path, shop insertion, boss at 9, defeat on death, revive, horde timer, enemy scaling numbers.

### R6. Economy and shop (Core, TDD) — doc §4, §6.4–6.5
- Crystals:
  - per kill: 10 × `CrystalGainMult`
  - per clear: 25 + `CrystalPerClear`
  - plus interest = `InterestRate` × balance
- Prices:
  - `COMMON_BASE_PRICE` 100
  - rarity mult Common 1 / Epic 3 / Legendary 8 / Greed 2
  - inflation 1 + 0.05 × islandIndex
- Shop: 4 pedestals rolled from the Random pool. Reroll price = 25 × inflation × 1.5^rerolls.
- Every other economy number references the base price.
- Spiked chest: costs 33% of max HP and can never kill (HP floor 1).
- Tests: prices, reroll growth, purchase adds a level, cannot buy without enough crystals, the spiked HP floor.

### R7. Runtime: stats into combat
- `RunHeroStats` (MonoBehaviour on the player hero) holds the `Inventory` and exposes the `HeroStats`.
- Apply to the player only. `PointingBeamController`, `HeroHealth`, `FlyingCharacter` and `ShockwaveController` read optional multipliers through small hooks:
  - damage × DamageMult, plus crit
  - cooldown × FireCooldownMult
  - charge damage and time
  - max health and damage taken
  - speed
  - shockwave radius and stun
- **Quick Match must behave exactly as today** (no stats component = neutral).
- The bot never gets items; the bot uses the enemy scaling from R5.

### R8. Runtime: islands and bots
- `RunDirector` (new MonoBehaviour) drives `RunStateMachine`.
  - It spawns bots from a bot prefab that the scene builder generates. Each spawned bot gets its own `BotInputSource`, `HeroHealth` and controllers, with the enemy set to the player.
  - It scales bot health and damage.
  - It counts kills → crystals, and handles the objectives (all dead / timer).
  - It applies the player-health carry-over and the heal on clear.
- Run mode and Quick Match must not run at the same time.
  - Reuse `MatchDirector` for pause, focus loss and menu return. Add a `MatchPhase` or a mode flag only if needed, and keep the `MatchStateMachine` tests green.
- Island 1 starts 3 s after RUN is pressed. Show a countdown like the match.

### R9. Runtime: choice UI (hands only)
- World-space panels at the menu position (`SeatUI`), selected with the existing `HandMenuPointer` (point + pinch):
  - **Portal panel**: 2–3 big buttons — "ARENA → DAMAGE CHEST", etc.
  - **Chest panel**: 3 item cards — name, level after pick (`Lv 2`), one-line description, rarity color.
  - **Shop panel**: 4 items with prices, REROLL (price), LEAVE.
- Hide the hero controls while a panel is up (like the menu).
- Reuse `HandMenuButton`; extend it with a "custom action" callback instead of only `MenuAction`.
- Labels must auto-size like the menu buttons (`textWrappingMode` NoWrap + auto size; see `MenuButton()` in the builder).
- **Never create a label without `PlaceLocal()`** (anchoredPosition bug, see the builder comment).

### R10. Runtime: HUD and menu
- Main menu grid 2×3: QUICK MATCH, RUN, TUTORIAL / MR TABLE (Arena_Main only), AIM.
  - START is renamed to QUICK MATCH.
  - The tutorial-first rule stays on QUICK MATCH and RUN.
- RUN HUD (world-fixed, same place as the match HUD):
  - island `3/9` + type
  - objective (bots left / seconds left)
  - crystals
  - player health
- End screens: VICTORY / DEFEAT with islands cleared and items collected; MENU button.

### R11. Scenes, debug keys, APK
- `HandHeroSceneBuilder` wires everything in both scenes.
  - Sandbox keeps the debug keyboard/mouse. Add keys: `R` = start run, number keys `1`–`3` = pick a portal / chest card, `B` = buy the first affordable shop item, `N` = leave the shop.
- Rebuild the scenes, run all tests, build the APK.
- Update the debug key table in the report.

### R12. Final review and report
- Dispatch one code-review subagent (most capable model) over `33f3204..HEAD` (skip `.unity` files).
- Fix Critical and Important findings with test-first. Log the minors.
- Write the return report (section 6). Mark `ALL_DONE`.

## 5. Starter catalog (R2) — names are placeholders, keep them short and English

| Id | Category | Theme | Rarity / weight | Tags (required) | Effect (scaling) |
|---|---|---|---|---|---|
| power_cell | WeaponMod | Damage | Common 1.0 | Damage | DamageMult +15%/lv (LinearMultiplier) |
| rapid_coil | WeaponMod | Damage | Common 1.0 | FireRate | cooldown −12% (Hyperbolic) |
| focus_lens | WeaponMod | Critical | Common 1.0 | Critical | CritChance 10% (Hyperbolic), crit = ×2 |
| sharp_focus | WeaponMod | Critical | Epic 1.5 | Critical (Critical) | CritDamageMult +25%/lv |
| overcharge | AbilityMod | Ability | Common 1.0 | Charge | ChargeDamageMult +20%/lv |
| quick_charge | AbilityMod | Ability | Common 1.5 | Charge (Charge) | charge time −15% (Hyperbolic) |
| shock_amp | AbilityMod | Ability | Common 1.0 | Shockwave | ShockwaveRadiusMult +20%/lv |
| stun_lock | AbilityMod | Ability | Epic 1.5 | Shockwave (Shockwave) | StunDurationAdd +0.3 s/lv (Linear) |
| hull_plating | Perk | Health | Common 1.0 | Health | MaxHealthAdd +20/lv (Linear) |
| nano_repair | Perk | Health | Common 1.0 | Healing | HealOnClear +10/lv (Linear) |
| afterburner | Perk | Speed | Common 1.0 | Speed | SpeedMult +10%/lv |
| deflector | Perk | Defense | Epic 1.0 | Defense | damage taken −10% (Hyperbolic, floor 25%) |
| crystal_magnet | Perk | Economy | Common 1.0 | Economy | CrystalGainMult +25%/lv |
| paycheck | Perk | Economy | Common 1.0 | Economy | CrystalPerClear +20/lv (= 0.2 × base price) |
| big_chests | Relic | — | Legendary 0.2, unique | Relic | ExtraChoices +1 |
| dividends | Relic | Economy | Legendary 0.2, unique | Relic, Economy | InterestRate 10% |
| second_wind | Relic | Health | Legendary 0.2, unique | Relic | Revives +1 (revive at 50% HP) |
| glass_cannon | Perk | Damage | Greed 1.0 (Greed chest only) | Greed | DamageMult +60%, debuff MaxHealthMult −30% |
| blood_price | Perk | Economy | Greed 1.0 (Greed chest only) | Greed | CrystalGainMult +100%, debuff DamageTakenMult +25% |

Greed items cannot be dropped. Relics have max level 1.

## 5b. Hard rules

1. **The XR Origin never moves or rotates.** No follow camera.
2. Don't delete the 20 legacy scripts in the `Scripts\` root or `Networking~`. Don't touch the scenes in `Assets/Scenes/`.
3. Every gesture threshold uses hysteresis. Every tunable is `[SerializeField]` + `[Tooltip]`. **Don't change existing tuning defaults.**
4. **Quick Match, the tutorial and both aim modes must keep working unchanged.** All existing tests stay green.
5. No account creation, login, payments or sign-ups (Meta, Photon, Devpost, …).
6. **No `git push`** (the settings deny it). No force commands. Never `git reset --hard` committed work.
7. Don't modify files outside the project folder and `MetaAwards\Build\`.
8. No new packages. `ProjectSettings/` changes only if a build needs them; list them in `PROGRESS.md`.
9. Never commit a broken compile. If it is unavoidable, mark it `[broken]` and fix it in the next commit.
10. Copy, menus and HUD text in English (the judges are English-speaking). Reports for Hyun in Korean.

## 6. Return report (`AUTO/REPORT_FOR_HYUN.md`, Korean)

So Hyun can understand the state in 5 minutes:
1. A table of done / blocked / not started tasks.
2. **A headset checklist**, by priority, with what to feel for each item: the run flow, portal and chest choices, item effects felt in combat, shop, difficulty curve, run length in minutes.
3. How to try it in the editor: scene, debug keys (`R`, `1`–`3`, `B`, `N`, …).
4. The decisions I made for Hyun (summary of `QUESTIONS_FOR_HYUN`), so Hyun can reverse any of them.
5. Three good next steps (e.g. meta progression, Anvil enhancements, balance pass).
6. Any changed tuning default (there should be none).
