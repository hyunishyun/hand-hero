# PROGRESS — Round 5 (ASSIST pinch release, minors, enemy movement, terrain stage 2, demo mode)

상태 값: TODO / IN_PROGRESS / DONE / BLOCKED(<이유>)

| 태스크 | 상태 | 메모 |
|---|---|---|
| T0 ASSIST 핀치 놓기 판정 | DONE | `38ca4b6`. 테스트 628/628(새 27개). 놓기 = Meta 플래그 2프레임 꺼짐 / 절대 0.6 / 최고값 대비 0.2 하락 중 먼저 오는 것. 런 기록에 핀치별 진단값, `run_summary.py`가 모드별로 요약. |
| T1 남은 Minor 13건 | TODO | |
| T2 적 움직임 성격 | TODO | |
| T3 지형 2단계(새 조각·테마) | TODO | |
| T4 데모 모드 | TODO | |
| T5 씬·테스트·APK·리뷰·보고서 | TODO | |

## 세션 로그

- 2026-10-09 Phase 0 (대화형): 브랜치 `auto/2026-10-09-r5`(`a8cd3c2` 위). 4차 기기 로그 `AUTO/device_logs/2026-10-09-r4/` 분석: ASSIST에서 핀치 놓기를 못 읽음(쥔 시간 중앙값 최대 7.9초, 최대 22초; CURSOR는 0.125초). 4차 AUTO 파일은 `AUTO/archive/2026-10-09-r4/`로. 시작 테스트 601/601.
- 2026-10-10 T0 DONE (`38ca4b6`):
  - 결과: 쉬는 엄지(2.8 cm = 0.71)에서도 3프레임 안에 놓음을 읽는다. 빠른 탭 10번 → 발사 10번, 차지 시작 0번. 1초 쥐기 → 0.25초 뒤 차지가 시작되고, 놓으면 차지샷이 나간다. 예전 규칙은 같은 입력에 1발만 쏘고 계속 쥔 채로 남는다(테스트로 재현). 누르기·놓기는 부드럽게 하기 전 값을 쓴다. 시스템 제스처는 `PinchTrigger` 안에서 처리한다.
  - 런 기록: 핀치마다 `min_strength`, `release_strength`, `release_by`(meta/absolute/relative/lost/none), `peak_strength`, `meta_seen`. 샘플은 `AUTO/tools/fixtures/run_log_round5_sample.jsonl`.
  - 주의: Meta 플래그는 이번 누름에서 한 번 켜진 뒤에만 놓기에 쓴다. 이 펌웨어에서 오는지는 기기에서 `meta_seen`으로 확인한다. 선택 7건은 `QUESTIONS_FOR_HYUN.md`에 있다. 씬 2개를 다시 만들었다(새 직렬화 필드).
  - 기기 확인: ASSIST로 30초 연사 → 발사 수가 핀치 수와 비슷하고, 의도하지 않은 차지가 없는지(`run_summary.py`의 "Pinch holds by aim mode": Assist의 1초 넘는 쥐기 비율, release_by 분포).
