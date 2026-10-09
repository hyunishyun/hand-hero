# PROGRESS — Round 4 (first-run hitch, balance, leftover fixes, meta progression A)

상태 값: TODO / IN_PROGRESS / DONE / BLOCKED(<이유>)

| 태스크 | 상태 | 메모 |
|---|---|---|
| S1 첫 런 끊김 | DONE | `c119152`. 런 봇 풀을 씬 로드 때 미리 만들고, `RenderWarmup`이 로드 직후 3프레임 동안 봇·빔 3색·피격·처치 폭발·비네트·차지 구슬을 작게 그린다. 로거에 25 ms 작은 끊김 카운터(`HITCH`, `hitches=`, `worst_hitch=`)와 헤더의 warmup 시간. 테스트 469/469, 씬 재생성. **주의:** 3차 로그를 다시 보니 30초 Stale은 시스템 프로세스의 레이어와 겹친다(`QUESTIONS_FOR_HYUN.md` S1-2). |
| S2 밸런스 | DONE | `aaee943`. Horde 45→30초, `BossHealthMult` 6→5, 새 `EnemyDamageMult` 1.15(Elite는 1.5×1.15). 옛 값을 고정하던 `IslandSpec_CountsAndEnemyScaling`을 D2에 맞게 고쳤다. 새 테스트 3개, 472/472. 씬이 `RunParams`를 직렬화하므로 씬도 다시 만들었다. `run_summary.py`는 이미 10분 제한 대비 시간을 출력한다(변경 없음). 시간 추정은 아래. |
| S3 남은 Minor 4건 | TODO | |
| S4 메타 진행 Core | TODO | |
| S5 메타 진행 연결 | TODO | |
| S6 적 다양성 1단계(생김새·공격) | TODO | |
| S7 지형 다양성 1단계(위치 무작위) | TODO | |
| S8 MR 방 스캔 스파이크 | TODO | |
| S9 씬·테스트·APK 2종 | TODO | |
| S10 최종 리뷰·보고서 | TODO | |

## 세션 로그

- 2026-10-09 Phase 0 (대화형): 브랜치 `auto/2026-10-09-run`(`perf/freeze-hunt` `a3b1dd1`에서). 3차 기기 로그를 `AUTO/device_logs/2026-10-09/`에 커밋, 3차 AUTO 파일은 `AUTO/archive/2026-10-09/`로. 시작 시점 테스트 461/461.
- 2026-10-09 세션 1 (무인): S1, S2 완료. 테스트 461 → 472, 모두 통과. 씬 두 번 재생성. 헤드셋 확인 없음. 다음 세션은 S3부터.

## S2 시간 추정 (3차 기기 런 기준, 헤드셋 없이 계산)

- 3차 런: 587.9초 = 전투 445.5초 + 전투 밖 142.4초(카운트다운·CLEARED 45초 + 상자·포털·상점 약 97초).
- Horde 3개: 45 → 30초, **−45초**.
- 보스: 102.5초 × 5/6 ≈ 85.4초, **약 −17초**.
- 봇 데미지 ×1.15는 시간을 직접 바꾸지 않는다. 받은 피해는 166 → 약 190으로 예상한다(부활이 생기면 길어질 수 있다).
- 합계 약 **526초 ≈ 8분 46초**(목표 8–9분 안). S5의 시작 유물 상자(약 5초)와 S6의 적 종류가 더해지면 기기에서 다시 잰다: `python AUTO\tools\run_summary.py <run_log.jsonl>`.
