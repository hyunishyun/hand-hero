# Crab Champions 시스템 분석 — 로그라이크 개발자를 위한 설계 레퍼런스

> 대상: 런(run) 기반 로그라이크 / 루터 슈터를 만드는 개발자
> 분석 대상: Crab Champions (Noisestorm 1인 개발, UE5, Steam Early Access)
> 기준: 공식 위키(crabchampions.wiki.gg) 및 개발자 패치노트(Anvil Update 등). 수치는 업데이트마다 바뀌므로 **"설계 의도와 구조"를 가져가고, 숫자는 출발점으로만** 쓰세요.

---

## 0. 문서 구성과 용어 정리

| 섹션 | 내용 |
|---|---|
| 1. 코어 파운데이션 | 모든 시스템이 올라가는 공통 데이터 구조 (아이템 카테고리·태그·가중치·스케일링 타입) |
| 2. 런 구조 | 섬(Island) → 포털 선택 → 보상 상자 → 바이옴 → 루프 |
| 3. 레벨 시스템 | 런 내부 성장(아이템 레벨·강화) + 적 스케일링 + 런 외부 진행(랭크·키·난이도) |
| 4. 상점·경제 시스템 | 크리스탈 수급, 상점 섬 구성, 가격·리롤·할인, 체력을 화폐로 쓰는 구조 |
| 5. 시스템 간 연결 | 경제 ↔ 성장 ↔ 위험을 잇는 아이템들 |
| 6. 구현 템플릿 | 데이터 스키마, 스케일링 함수, 가격 공식, 상자/상점 롤링 의사코드 |
| 7. 설계 교훈 체크리스트 | 그대로 가져다 쓸 수 있는 원칙 |

> **참고:** 게임 안에 "Core Foundation"이라는 이름의 시스템은 공개 자료에서 확인되지 않았습니다. 이 문서에서는 **"모든 시스템이 공유하는 핵심 기반 구조"**라는 의미로 해석해 1장에 정리했습니다.

---

## 1. 코어 파운데이션 — 모든 시스템의 공통 뼈대

Crab Champions의 콘텐츠 수백 개가 관리 가능한 이유는 **모든 업그레이드가 같은 메타데이터 형식을 공유**하기 때문입니다. 상자, 상점, 토템, 강화 모두 이 데이터를 읽어서 동작합니다.

### 1.1 업그레이드 5종 (인벤토리 카테고리)

| 카테고리 | 역할 | 레벨업 가능? | 비고 |
|---|---|---|---|
| Weapon Mods | 주무기 강화 (기본 데미지 가산, 발사 패턴, 확률 발동 투사체) | O | 확률형 모드는 **무기별로 기본 발동 확률이 다름** |
| Ability Mods | 스킬(수류탄·블랙홀·그래플 등) 강화 | O | |
| Melee Mods | 근접무기 강화 | O | 후기 업데이트에서 별도 카테고리로 분리 |
| Perks | 범용 패시브 (체력·경제·치명타·원소 등) | O | Greed 등급 존재 |
| Relics | 규칙을 바꾸는 강력한 효과 | **X** | **종류당 1개만 보유 가능**, 레벨업 불가 |

**설계 포인트**
- 레벨업 가능한 "스택형" 아이템(모드·퍽)과 **1회성 규칙 변경 아이템(렐릭)**을 분리해 밸런스 축을 나눴습니다. 렐릭은 스폰 가중치가 0.2 수준으로 낮게 설정돼 있습니다(일반 퍽은 대체로 1.0).
- 무기·스킬·근접무기는 **로비에서 런 시작 전에 고르고 런 중 교체 불가** → 빌드의 "축"을 먼저 고정하고, 런 중 루팅으로 그 축을 키우는 구조.

### 1.2 모든 아이템이 가진 메타데이터

위키에 정리된 각 아이템 항목은 거의 동일한 필드를 가집니다:

| 필드 | 예시 | 용도 |
|---|---|---|
| Category (테마) | Health / Luck / Speed / Elemental / Economy / Damage / Skill / Critical / Greed | 테마 상자(Health Chest 등)의 루트 풀 결정 |
| Rarity | Common / Epic / Legendary / Greed | 상자 등급, 가격, 분해 보상 |
| Spawn weight | 1.0 기본, 시너지 전용 1.5~2.0, 렐릭 0.2, 특수 0.05 | 롤링 확률 |
| Buff scaling | Linear / Linear as Multiplier / Hyperbolic / Exponential | 레벨당 효과 증가 방식 |
| Debuff scaling | Linear / Hyperbolic | 단점(트레이드오프)이 레벨당 커지는 방식 |
| Tag | Fire, Ice, Lightning, Poison, Arcane, Critical, Bounce, Turret, Healing | 시너지 판정 |
| Required tag | "같은 태그 아이템 보유 시에만 스폰" | **죽은 선택지 제거** |
| Key unlock | 키 토템으로 해금해야 풀에 들어감 | 메타 진행 |
| Appears in | 어떤 상자·토템에서 나오는지 목록 | 획득 경로 제어 |
| Cooldown | 0.1s ~ 10s | 발동형 효과의 과발동 방지 |

### 1.3 Required Tag — 가장 따라 하기 좋은 장치

- 예: "Fire 강도 +25%" 퍽은 **Fire 태그 아이템을 이미 가지고 있을 때만** 상자에 등장.
- 치명타 상자는 처음엔 치명타 확률 퍽만 나오고, 치명타 퍽을 하나 얻은 뒤에야 "치명타 시 화살 발사" 같은 파생 퍽이 풀에 들어옴.
- 효과: 초반 선택지에 "지금은 아무 의미 없는 아이템"이 섞이는 것을 막아 **선택의 체감 품질**을 높임.
- 개발자는 Anvil 업데이트에서 "Healing" 태그를 새로 만들어, 치유량 증가 퍽이 이미 치유 퍽을 가진 경우에만 나오게 바꿨습니다. 같은 원리를 계속 확장 중.

### 1.4 보유 아이템 가중치 보정

- 위키에 따르면 상자는 **플레이어가 이미 레벨 1 이상 보유한 모드·퍽이 더 잘 등장**하도록 가중됩니다.
- 이것이 "레벨 시스템"이 실제로 굴러가게 만드는 핵심 — 중복이 나와야 레벨이 오르니까요.

### 1.5 스케일링 타입 4종

| 타입 | 의미 | 쓰는 곳 | 이유 |
|---|---|---|---|
| Linear | 레벨 × 기본값을 가산 | 최대 체력 +250/레벨, 대시 +1/레벨 | 단순·직관적 |
| Linear as Multiplier | 레벨 × %를 **곱연산 계수**로 | 근거리 데미지 +30%/레벨 | 다른 데미지 버프와 곱해져 빌드 시너지 |
| Hyperbolic | 수확 체감, 100%에 점근 | 블록 확률, 상점 할인, 확률 발동 모드 | **100% 도달 = 게임 붕괴**인 수치 보호 |
| Exponential | 레벨당 급격히 증가 | 일부 Greed 퍽 | 위험한 고레벨 스택에 큰 보상 |

**교훈:** 확률·할인·피해감소처럼 상한이 있어야 하는 스탯은 무조건 Hyperbolic. 피해감소는 별도로 **전역 하한(최소 25%는 받음)**도 걸려 있습니다.

### 1.6 희귀도 체계

| 등급 | 획득처 | 특징 |
|---|---|---|
| Common | 대부분의 상자 | 효과는 약하지만 Grip Tape·Stamina처럼 **고유 기능**이 있는 것도 많음 |
| Epic | Epic·Spiked 상자, 엘리트 보상, 낮은 확률로 일반 상자 | |
| Legendary | 보스 섬 상자, Gold 토템, 극히 낮은 확률로 일반 상자 | 런을 바꾸는 효과 |
| Greed | Greed 상자·토템·상점에서만 | **항상 큰 단점 동반**, 인벤토리에서 버리기 불가, 퍽만 존재 |

- 개발 중 등급 명칭이 Rare→Common, Epic→Rare→Epic으로 몇 번 바뀌었습니다. "가장 흔한 아이템을 Rare라고 부르는 건 이상하다"는 이유. → **등급 이름은 플레이어 체감과 맞출 것.**

---

## 2. 런 구조 — 섬, 포털, 바이옴, 루프

```
[로비 섬] 무기/스킬/근접 선택, 키 토템, 난이도 설정
   ↓
[바이옴 1] 섬 1 → 섬 2 → ... → 엘리트 섬
   ↓        (섬 클리어마다 포털 2~4개 중 선택 = 다음 섬 종류 + 보상 상자 종류)
[바이옴 2] ...
   ↓
[바이옴 N: 화산] ... → 보스 섬 (30번째 섬)
   ↓
 승리(Crab Island) 또는 루프(무한 지속, 적 스케일 계속 상승)
```

### 2.1 섬 종류

| 섬 | 목표 | 설계 의도 |
|---|---|---|
| Arena | 모든 적 처치 | 기본형 |
| Horde | 정해진 시간(42~70초) 생존 | 처치 속도 아닌 생존력 시험 |
| Challenge | 원히트 등 페널티 조건 | 위험 ↑ 보상 ↑ |
| Parkour | 장애물 코스, **남은 시간에 따라 보상 등급 결정** | 이동 실력 보상 |
| Demolition | 적을 계속 생성하는 둥지 파괴 | 우선순위 판단 |
| Harvest | 표시 구역에 일정 시간 서 있기 | 위치 선정 |
| Elite | 바이옴 마지막 + 선택지로도 등장 | 1:1 강적 |
| Boss | 30섬마다 | 루프/종료 분기점 |
| Shop | 크리스탈 소비 | 4장 참조 |

- **Waves 섬은 삭제됨**: "Arena와 충분히 다르지 않고, '맞으면 끝' 기믹은 다른 모디파이어가 이미 커버한다"는 이유. → **섬 타입마다 명확히 다른 플레이 경험이 있어야 존재 가치가 있다.**

### 2.2 포털 = 위험과 보상의 메뉴

- 섬을 클리어하면 여러 포털이 생기고, 각 포털에 **다음 섬 타입 + 클리어 보상(상자 종류)**이 표시됩니다.
- 포털 위에 **2X(챌린지), Flawless(무피격 클리어 시 보너스)** 같은 수식이 붙기도 함.
- **XL 토템**: 포털 옆에 확률 등장. 다음 섬의 적 수를 늘리는 대신 추가 상자 1개. (초기엔 3배였다가 지루하다는 피드백으로 2배로 조정)
- 각 바이옴 첫 섬은 **데미지 상자 고정** → 초반 빌드 방향을 보장.

### 2.3 상자 종류 (보상 테이블)

| 상자 | 내용 | 조건/비용 |
|---|---|---|
| Damage / Health / Speed / Luck / Economy / Critical | 해당 테마 풀 | 포털 보상 |
| Random | Greed 제외 전체 풀 | |
| Epic | Epic 이상 | 엘리트, 완벽한 파쿠르 |
| Spiked | Epic 상자와 비슷 | **최대 체력의 33% 지불** |
| Legendary | Legendary | 보스 섬 |
| Greed | Greed 퍽 1개 (선택지 1개) | |
| Relic | 렐릭 (선택지 1개 적음) | |
| Upgrade | **이미 가진 아이템만** (선택지 2개) | 레벨업 전용 |
| Lesser | 랜덤 1개, 맵 곳곳 숨겨짐 | 탐험 보상 |

- 기본 규칙: **상자 1개 = 선택지 3개 중 1개 선택.** 선택지 수는 퍽·렐릭으로 ±조정.

---

## 3. 레벨 시스템

Crab Champions의 "레벨"은 하나의 경험치 바가 아니라 **세 층**으로 나뉩니다.

```
┌─ 런 내부(일회성) ───────────────────────────┐
│ A. 아이템 레벨: 같은 모드/퍽 중복 획득 = +1레벨 │
│ B. 강화(Enhancement): 특정 모드에 추가 속성    │
│ C. 적 스케일링: 섬 클리어마다 적 체력 증가      │
└────────────────────────────────────────────┘
┌─ 런 외부(영구) ─────────────────────────────┐
│ D. 키 → 키 토템 → 콘텐츠 해금                  │
│ E. 무기/스킬/근접별 랭크 → 계정 랭크           │
│ F. 난이도 + 모디파이어 → 챌린지 레벨           │
└────────────────────────────────────────────┘
```

### 3.1 A. 아이템 레벨 (런 내부 성장의 핵심)

- 경험치 레벨업이 **없습니다.** 캐릭터 스탯을 올리는 유일한 방법은 **아이템을 얻고, 같은 아이템을 또 얻어 레벨을 올리는 것.**
- 각 아이템의 효과는 1.5절의 스케일링 타입에 따라 레벨마다 증가 (예: Mango 최대 체력 +250/레벨, Linear).
- 레벨을 올리는 경로:
  1. 상자에서 같은 아이템 선택 (보유 아이템 가중치 덕분에 자주 등장)
  2. **Upgrade 상자** (보유 아이템만 등장)
  3. 상점에서 구매
  4. **Level Up 퍽**: 3섬마다 랜덤 모드/퍽 1개 자동 레벨업
- 업적 중 "한 런에서 모드나 퍽 하나를 20레벨까지" 같은 목표가 있어, **한 아이템에 몰빵하는 플레이**를 의도적으로 장려합니다.
- 아이템을 버리면 **분해(Salvage) → 크리스탈**로 전환 (4장).

**왜 경험치 레벨업 대신 아이템 레벨인가?**
- 모든 성장이 "선택"을 통해 일어남 → 매 섬이 의사결정 포인트.
- 같은 아이템을 계속 고르는 "집중"과 새 아이템을 고르는 "확장" 사이의 긴장이 생김.
- 체크리스트: Collector 퍽("보유한 퍽 총 레벨당 데미지 +4%"), Equalizer("장착 퍽 수가 짝수면 데미지 +30%")처럼 **아이템 레벨 자체를 참조하는 메타 아이템**이 이 시스템을 한 번 더 비틀어 줍니다.

### 3.2 B. 강화(Enhancement) — 아이템 레벨 위의 두 번째 축

- 대상: *Enhanceable* 태그가 붙은 무기/스킬 모드.
- 획득처:
  - **상점의 강화 토템** (크리스탈 지불, 결과 랜덤 — "주사위 굴리기")
  - **Anvil 포털 보상**: 섬 클리어 후 강화 선택지 3개 중 하나를 골라 **원하는 모드에 직접 적용** (정밀 타기팅)
- 한 모드당 강화 횟수 상한 있음(위키 기준 8회). 같은 강화를 중복 적용해 효과를 키울 수 있음.
- 강화도 Common(1.0) / Epic(0.5) / Legendary(0.25) 가중치와 Greed(위력 큰 대신 자신도 피해)가 존재 → **1장의 등급·가중치 체계를 그대로 재사용.**
- 관련 렐릭: Anvil 선택지 3→5개, 또는 2개 선택 가능.

**설계 교훈 (개발자 패치노트에서):** 랜덤 강화만 있을 때는 "원하는 모드를 키울 통제권이 부족"했고, 그래서 **타기팅 가능한 Anvil을 추가하되 랜덤 토템은 대안 경로로 유지**했습니다. → **같은 자원에 '확정 경로'와 '도박 경로'를 함께 두는 패턴.**

### 3.3 C. 적 스케일링 (난이도 곡선)

- 적 기본 체력은 **섬 클리어마다 일정 비율 증가**, 루프와 협동 인원수에도 비례 증가. 사실상 무한 스케일 (32비트 정수 한계까지 간 사례가 커뮤니티에 있음).
- 멀티플레이: 인원당 적 체력 보너스 + 스폰 예산 보너스를 **따로** 조정 (개발자가 둘의 비율을 계속 튜닝).
- Horde 섬 웨이브당 체력 증가율은 +25% → +5%로 하향: "총알 스펀지"가 되면 재미없다는 판단.
- 적 스폰은 **예산(budget) 방식**: 적마다 스폰 비용이 있고, 어려운 적은 비용이 높아 동시에 적게 나옴. 동시 최대 적 수도 난이도별로 상한.

### 3.4 D. 키 & 키 토템 (콘텐츠 해금)

- 보스 등에서 **키**를 얻고, 로비의 키 토템에 **3개를 넣으면 새 무기·스킬·퍽·모드 1개 해금.**
- **런당 1회만** 사용 가능 → 해금 속도가 플레이 횟수에 묶임.
- 모두 해금 후에는 키 토템이 "이번 런을 Epic 이상 랜덤 아이템 1개로 시작" 기능으로 전환 → **메타 진행이 끝나도 키의 가치가 유지됨.**
- 아이템 풀이 처음엔 작고 해금할수록 넓어짐 → 신규 유저가 쉬운 시너지를 먼저 경험.

### 3.5 E. 랭크 — 스탯이 아닌 "숙련 증명"

- 무기·스킬·근접무기 **각각**에 랭크가 있음: Bronze → Silver → Gold → Sapphire → Emerald → Ruby → Diamond.
- 해당 장비로 높은 난이도(챌린지 레벨)에서 승리해야 랭크 상승.
- **계정 랭크 = 장비 랭크들의 평균**, 단 Diamond는 **모든 장비가 Diamond**여야 함.
- 계정 랭크는 업적·스킨(코스메틱)과 연결되고 **전투력은 올려주지 않음.** 최상위 난이도(Ultra Chaos) 해금 조건으로도 쓰임.
- 커뮤니티 의견: "새 계정으로도 최고 난이도 클리어가 가능하다" → **메타 진행이 스탯 체크가 아닌 실력 게이트.** (Hades·RoR2 계열과 다른 선택)

### 3.6 F. 난이도와 챌린지 레벨

- 기본 난이도(Normal / Nightmare / Ultra Chaos) + **개별 토글 가능한 모디파이어**(정예 매복, 강화된 적, 원히트 등).
- 켜진 모디파이어 합으로 **챌린지 레벨** 산출 → 승리 시 랭크 판정에 사용.
- 런 종료 후 보여주는 Player Level = **클리어한 섬 수 × 챌린지 레벨** (개발자가 기존 복잡한 하이스코어 공식을 버리고 이렇게 단순화).
- 초기엔 "인벤토리 슬롯 잠금"을 기본 규칙으로 넣었다가 호불호가 갈리자 **모디파이어로 분리**하는 방향을 택함. → **논쟁적인 메커닉은 강제하지 말고 옵션화.**

---

## 4. 상점 · 경제 시스템

### 4.1 화폐: 크리스탈 (런 한정)

**수급처**
| 경로 | 비고 |
|---|---|
| 적 처치 드랍 | 기본 |
| 파괴 가능한 크리스탈 바위 | 맵 탐험 |
| 엘리트/보스 처치 보너스 | 패치에서 엘리트 +300%, 보스 +200%로 상향 |
| 아이템 분해(Salvage) | 강화된 아이템, 높은 등급 강화일수록 더 많이 |
| 크리스탈 토템 | 체력을 지불하고 크리스탈 획득 |
| 경제 퍽/모드 | 섬 클리어 보너스, 데미지→크리스탈 변환, 근접 처치 보너스 등 |

**소비처**: 상점 구매, 상점 리롤, 강화 토템, Gamble 토템.

### 4.2 체력도 화폐다 (두 번째 통화)

Crab Champions의 가장 독특한 경제 설계는 **체력을 지불 수단으로 쓰는 것**입니다.

| 지불처 | 비용 |
|---|---|
| Spiked 상자 | 최대 체력의 33% (→ 한 섬에서 최대 3개 열기 가능하도록 설계) |
| Loot 토템 | 체력 20% → 랜덤 아이템 |
| Chance 토템 | 체력 5% → 확률적 아이템 |
| Crystal 토템 | 체력 10% → 크리스탈 |
| Gold 토템 | **최대 체력** 일부 → 레전더리 |
| Health 토템 | 50% 확률로 최대 체력 2배 또는 절반 |

개발자 의도(Health 2.0 패치노트 요약): **"재미는 위험 구간에 있다"** — 체력을 화폐로 쓰는 것과 작은 실수에서 회복하는 것 사이의 균형을 위해, 첫 섬에 회복 상자를 보장하고 기본 체력 풀은 낮췄습니다. 또한 **최대 체력의 80% 이상을 한 번에 잃고 즉사하는 것은 방지**(원히트 모디파이어 제외).

### 4.3 상점 섬 구성

상점은 **특별한 섬**으로 루트 중간에 등장합니다. 구성 요소:

| 요소 | 기능 |
|---|---|
| 아이템 페데스탈 여러 개 | 모드·퍽 등 판매 |
| 무한 페데스탈 | 같은 슬롯을 반복 구매 가능, **구매할 때마다 원가의 15%씩 가격 상승** |
| Tony (상점 주인) | 상점의 얼굴/세계관 |
| 리롤 토템 | 판매 목록 재추첨, **누를수록 가격 상승** |
| 강화 토템 | 크리스탈 지불 → 랜덤 강화 (패치에서 "가장 빠른 OP 빌드 경로"라 비용 20% 인상) |
| DPS 해골 | 허수아비 — 빌드 테스트용 |
| 회복 정원 | 체력 회복 |
| 기타 랜덤 토템, 파괴 가능한 바위 | 변수 |

### 4.4 가격·할인 조절 장치

| 장치 | 효과 | 스케일링 |
|---|---|---|
| Tony's Black Card (퍽) | 상점 가격 할인 8%/레벨 | Hyperbolic (공짜 방지) |
| Ring of Value (렐릭) | 상점에 항상 75% 할인 페데스탈 1개 | 고정 |
| Special Delivery (퍽) | 판매 품목 +1/레벨 | Linear |
| Tony's Amulet (렐릭) | 판매 품목 +5 | 고정 |
| Valued Customer (퍽) | **구매할 때마다 최대 체력 증가** | Linear |
| Paycheck (퍽) | 섬 클리어마다 "커먼 아이템 가격의 20%"만큼 크리스탈 | 가격 기준점에 연동 |
| Gold Coating (퍽) | 치유량을 크리스탈로 전환, **상점에서는 작동 안 함** | 무한 루프 방지 예외 |

**설계 포인트**
- **가격 기준점(커먼 아이템 가격)**이 있고, 다른 경제 수치가 이를 참조 → 인플레이션이 와도 비율이 유지됨.
- 할인 버그 사례: 할인된 무한 페데스탈의 가격 상승이 할인가 기준으로 계산돼 과도하게 싸게 살 수 있었고, **원가 기준 상승**으로 수정. → 가격 상승 공식의 기준값을 명확히.
- "상점 안에서 무한 수급" 루프가 생길 수 있는 아이템(Gold Coating)은 **상점 내 비활성**으로 막음.

### 4.5 경제 퍽 = 빌드 아키타입

경제 아이템은 단순 편의가 아니라 **"돈 = 힘" 빌드**의 재료입니다:
- Money Is Power: 보유 크리스탈 250당 데미지 +1% (상한 있음)
- Money Shot: 타격 시 크리스탈 (저데미지·고연사 무기일수록 유리 — 개발자가 "의도된 트레이드오프"라고 명시)
- Ring of Dividends: 섬 클리어마다 보유 크리스탈의 10% 이자

→ 이 아이템들이 있기 때문에 **"상점에서 쓸까, 모아둘까"**가 실질적인 결정이 됩니다.

---

## 5. 시스템 간 연결 지도

```
          ┌──────── 체력 (2차 화폐) ────────┐
          │ Spiked 상자 / 토템 비용          │
          ▼                                  │
 [적 처치] ──→ 크리스탈 ──→ [상점] ──→ 아이템 획득 ──→ 아이템 레벨 ↑
     ▲              │          │  리롤/강화          │
     │              │          └─ Valued Customer ──→ 최대 체력 ↑
     │              ▼                                │
     │      Money Is Power / Dividends ──→ 데미지 ↑ ─┘
     │                                               │
     └──── 적 스케일링 (섬마다 체력 ↑) ◀── 섬 클리어 ◀─┘
                           │
                  Gemstone·Fortitude 등 "섬당 성장" 퍽이
                  적 스케일링과 같은 축으로 플레이어도 성장시킴
```

**핵심 관찰:** "섬 클리어마다 +X" 퍽(Gemstone 섬마다 데미지 +4%, Fortitude 섬마다 최대 체력 증가, Ring of Vigor 섬마다 최대 체력 배수 +3% 등)은 **적 스케일링과 같은 시계(섬 카운트)를 공유**합니다. 루프를 오래 돌수록 이 퍽들의 가치가 커져 무한 모드에서 빌드 선택을 바꿉니다.

---

## 6. 구현 템플릿

> 아래 공식·코드는 Crab Champions의 실제 내부 코드가 아니라, **관찰된 동작을 재현하기 위한 권장 구현**입니다.

### 6.1 아이템 데이터 스키마 (JSON 예시)

```json
{
  "id": "perk_firestarter",
  "category": "Perk",
  "theme": "Elemental",
  "rarity": "Common",
  "spawnWeight": 1.5,
  "tags": ["Fire"],
  "requiredTags": ["Fire"],
  "requiresUnlock": false,
  "maxLevel": null,
  "unique": false,
  "buff":   { "stat": "FireStrength", "base": 0.25, "scaling": "LinearMultiplier" },
  "debuff": null,
  "cooldown": 0,
  "appearsIn": ["Chest:Elemental", "Chest:Random", "Totem:Loot", "Totem:Chance", "Totem:Gamble", "Totem:Random", "Shop"],
  "enhanceable": false,
  "salvageValue": "auto"
}
```

렐릭이라면 `"unique": true, "maxLevel": 1, "spawnWeight": 0.2`.
Greed라면 `"rarity": "Greed", "droppable": false`와 `debuff` 필수.

### 6.2 스케일링 함수

```csharp
float Scale(ScalingType t, float baseValue, int level, float k = 1f)
{
    switch (t)
    {
        case ScalingType.Linear:
            return baseValue * level;                       // 가산
        case ScalingType.LinearMultiplier:
            return 1f + baseValue * level;                  // 곱연산 계수로 사용
        case ScalingType.Hyperbolic:
            // 레벨1에서 baseValue에 근접, 1.0에 점근
            return 1f - 1f / (1f + (baseValue / (1f - baseValue)) * level);
        case ScalingType.Exponential:
            return baseValue * (Mathf.Pow(1f + k, level) - 1f) / k;
    }
    return 0;
}
// 피해감소 등은 결과에 전역 하한 적용: incomingMultiplier = max(0.25, 1 - reduction)
```

### 6.3 상자 롤링 (가중치 + 태그 필터 + 보유 보정)

```python
def roll_chest(chest_type, player, n_choices=3):
    pool = [i for i in ALL_ITEMS
            if chest_type in i.appears_in
            and (not i.requires_unlock or i.id in player.unlocked)
            and all(t in player.owned_tags for t in i.required_tags)
            and not (i.unique and i.id in player.inventory)]

    def weight(i):
        w = i.spawn_weight
        if i.id in player.inventory:          # 보유 아이템 보정
            w *= OWNED_BONUS                  # 예: 1.5~2.0
        return w

    n = n_choices + player.extra_choices      # Big Chests, Coral Amulet 등
    picks = weighted_sample_without_replacement(pool, weight, n)
    return [maybe_upgrade_rarity(p, player.rarity_upgrade_chance) for p in picks]
```

### 6.4 상점 가격 모델

```python
COMMON_BASE_PRICE = 100                      # 경제 전체의 기준점

def base_price(item, island_index):
    rarity_mult = {"Common": 1, "Epic": 3, "Legendary": 8, "Greed": 2}[item.rarity]
    inflation   = 1 + 0.05 * island_index    # 섬 진행에 따른 인플레이션
    return COMMON_BASE_PRICE * rarity_mult * inflation

def final_price(item, island_index, player):
    discount = hyperbolic(0.08, player.level_of("TonysBlackCard"))
    return ceil(base_price(item, island_index) * (1 - discount))

def infinite_pedestal_price(original, times_bought):
    return ceil(original * (1 + 0.15 * times_bought))   # 원가 기준으로 상승

def reroll_price(n_rerolls, island_index):
    return ceil(COMMON_BASE_PRICE * 0.25 * (1 + island_index * 0.05) * (1.5 ** n_rerolls))

def paycheck(player, island_index):  # 다른 경제 수치는 기준 가격을 참조
    return 0.20 * COMMON_BASE_PRICE * (1 + 0.05 * island_index) * player.level_of("Paycheck")
```
> 숫자(희귀도 배수, 인플레이션 5%, 리롤 1.5배 등)는 예시입니다. 비율 구조만 가져가세요.

### 6.5 상점 섬 생성

```python
def generate_shop(player, island_index):
    slots = BASE_SHOP_SLOTS + player.level_of("SpecialDelivery") + (5 if player.has("TonysAmulet") else 0)
    items = roll_items(source="Shop", player=player, n=slots)
    shop = [Pedestal(i, final_price(i, island_index, player)) for i in items]
    if player.has("RingOfValue"):
        shop.append(Pedestal(roll_one(...), discount=0.75))
    shop.append(InfinitePedestal(...))
    shop += [RerollTotem(), EnhancementTotem(), DpsDummy(), HealingGarden()]
    shop += maybe_random_totems()
    return shop

def on_purchase(player, item):
    player.add(item)
    player.max_hp_base += 20 * player.level_of("ValuedCustomer")
```

### 6.6 런 종료 점수 / 랭크

```python
challenge_level = sum(m.weight for m in active_modifiers) + difficulty.base
player_level    = islands_cleared * challenge_level

if won:
    rank = rank_from_challenge_level(challenge_level)   # Bronze..Diamond 테이블
    gear_rank[weapon]  = max(gear_rank[weapon],  rank)
    gear_rank[ability] = max(gear_rank[ability], rank)
    gear_rank[melee]   = max(gear_rank[melee],   rank)

account_rank = average(gear_rank.values())
if any(r < DIAMOND for r in gear_rank.values()):
    account_rank = min(account_rank, RUBY)              # Diamond는 전부 Diamond일 때만
```

---

## 7. 설계 교훈 체크리스트

**코어 파운데이션**
- [ ] 모든 아이템이 같은 스키마(카테고리·희귀도·가중치·태그·스케일링)를 공유하는가?
- [ ] "지금은 쓸모없는 선택지"를 Required Tag로 걸러내는가?
- [ ] 확률·할인·피해감소는 Hyperbolic + 전역 하한으로 보호되는가?
- [ ] 스택형 아이템과 규칙변경형(유니크) 아이템이 분리돼 있는가?

**레벨 시스템**
- [ ] 성장이 "선택"을 통해서만 일어나는가? (자동 스탯업 최소화)
- [ ] 중복 획득을 장려하는 장치(보유 가중치, 업그레이드 상자)가 있는가?
- [ ] 아이템 레벨 위에 두 번째 축(강화)이 있고, **확정 경로 + 도박 경로**가 공존하는가?
- [ ] 메타 진행이 스탯 인플레가 아니라 **해금 + 숙련 증명**인가? (최고 난이도를 신규 계정으로도 깰 수 있는가)
- [ ] 해금이 끝난 뒤에도 메타 자원(키)이 쓸모를 유지하는가?

**상점 · 경제**
- [ ] 경제 수치가 하나의 기준 가격을 참조해 인플레이션에도 비율이 유지되는가?
- [ ] 반복 구매·리롤은 가격이 오르는가? 상승 기준값이 원가인가?
- [ ] 상점 안에서 무한 수급 루프가 생기는 아이템을 막았는가?
- [ ] 체력 같은 **두 번째 화폐**로 위험-보상 선택을 만들고 있는가?
- [ ] "모을까/쓸까"를 실제 결정으로 만드는 경제 빌드(이자, 보유량 비례 버프)가 있는가?
- [ ] 상점에 빌드 테스트용 허수아비가 있는가? (구매 결정 품질 향상)

**운영**
- [ ] 논쟁적 메커닉(슬롯 잠금 등)은 강제 대신 모디파이어로 분리했는가?
- [ ] 차별성 없는 콘텐츠(Waves 섬처럼)는 과감히 삭제하는가?

---

## 출처

- [Crab Champions Wiki — Perks](https://crabchampions.wiki.gg/wiki/Perks)
- [Crab Champions Wiki — Relics](https://crabchampions.wiki.gg/wiki/Relics)
- [Crab Champions Wiki — Weapon Mods](https://crabchampions.wiki.gg/wiki/Weapon_Mods)
- [Crab Champions Wiki — Shop](https://crabchampions.wiki.gg/wiki/Shop)
- [Crab Champions Wiki — Totems](https://crabchampions.wiki.gg/wiki/Totems)
- [Crab Champions Wiki — Chests](https://crabchampions.wiki.gg/wiki/Chests)
- [Crab Champions Wiki — Islands](https://crabchampions.wiki.gg/wiki/Islands)
- [Crab Champions Wiki — Rarities](https://crabchampions.wiki.gg/wiki/Rarities)
- [Crab Champions Wiki — Enhancements](https://crabchampions.wiki.gg/wiki/Enhancements)
- [Crab Champions Wiki — Game Mechanics](https://crabchampions.wiki.gg/wiki/Game_Mechanics)
- [Noisestorm — The Anvil Update Beta 패치노트 (Steam)](https://steamcommunity.com/app/774801/discussions/0/596261560284901413)
- [Early Access Update 4 패치노트 (Wiki)](https://crabchampions.wiki.gg/wiki/Early_Access_Update_4)
- [Steam 커뮤니티 — 계정 랭크 토론](https://steamcommunity.com/app/774801/discussions/0/596267508946729761)
- [Steam 커뮤니티 — Ultra Chaos 해금 조건 토론](https://steamcommunity.com/app/774801/discussions/0/4134934813429189855)
- [Crab Champions 업적 목록 (Vandal)](https://vandal.elespanol.com/logros/pc/crab-champions/83674)

위키 콘텐츠는 CC BY-SA 4.0 라이선스입니다. 이 문서는 요약·재구성이며, 구현 템플릿의 공식과 수치는 원작의 내부 코드가 아닌 예시입니다.
