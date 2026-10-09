# PERF_REPORT — Round 3 (작성 중)

> P16에서 1–4절(고친 것, 남은 의심 원인, 증거 수집 방법, `perf_log.txt` 읽는 법)을 채운다. 지금은 P13의 런 기록 절만 있다.

## 런 기록(`run_log.jsonl`) 가져오기와 요약

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

**요약에 나오는 것:** 런 수·승률, 승리 런 시간(중앙값·최대, 10분 초과 수), 섬별 사망(전체/그 섬에서 진 런), 오래 걸린 섬, 많이 고른 아이템, 차지 오인률(0.5초 미만 핀치 중 차지샷이 나간 비율), 명중률, 먼저 돌릴 손잡이 제안(`EnemyHealthPerIsland` → `BossHealthMult` → Arena 봇 수, 차지 오인이 10% 넘으면 `HoldDelay`).

**한 줄의 주요 필드:** `seed`(이 세션 RNG 시드, 같은 앱 실행의 두 번째 런부터는 이어지는 난수라 재현용은 아님), `aim`(Assist/Cursor), `view`(Vr/Table), `result`(Victory/Defeat/Quit), `total_s`(일시정지 뺀 런 시간), `death_island`, `revives`, `islands[]`(`n`, `type`, `fight_s` = FIGHT부터 클리어까지, `damage` = 받은 피해, `deaths`), `items[]`(`id`, `level`, `island`, `from` chest/shop), `shots`/`hits`/`hit_rate`(봇·히어로에 맞은 빔만 명중), `charge_shots`, `hold_s[]`/`hold_charged[]`(핀치마다 쥔 시간·차지샷 여부).
