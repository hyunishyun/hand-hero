# PROGRESS — Round 5 (ASSIST pinch release, minors, enemy movement, terrain stage 2, demo mode)

상태 값: TODO / IN_PROGRESS / DONE / BLOCKED(<이유>)

| 태스크 | 상태 | 메모 |
|---|---|---|
| T0 ASSIST 핀치 놓기 판정 | DONE | `38ca4b6`. 테스트 628/628(새 27개). 놓기 = Meta 플래그 2프레임 꺼짐 / 절대 0.6 / 최고값 대비 0.2 하락 중 먼저 오는 것. 런 기록에 핀치별 진단값, `run_summary.py`가 모드별로 요약. |
| T1 남은 Minor 13건 | DONE | `9acd761`(`draft/r5b-T1` 병합). 테스트 650/650(새 22개, 일부러 바꾼 테스트 1개). 13건 모두 수정. Gunner 빔은 조준점 노랑과 30° 떨어지게 (0.6, 1, 0.2)로 옮김. 씬 2개와 RunBot 프리팹 재생성. |
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
- 2026-10-10 T1 DONE (`9acd761`, `draft/r5b-T1` 병합, 충돌 없음):
  - 결과: 배치(S7-1-1 시야 규칙 테스트, S7-1-2 대체 배열이 없으면 `null`), 방 스캔 프로브(S8-1-2 대화상자 닫음 = 거부 + `PERMISSION_DISMISSED`, S8-1-3 실패는 `UNAVAILABLE` 한 줄만, S8-2-1 권한 확인은 0.5초 간격이고 답을 알면 멈춤, F2-2 콘솔 로그는 개발 빌드만, 요약과 종료는 2프레임 뒤). F1-2 STARTING RELIC 시간은 런 시간과 최단 기록에서 뺌. F3-3 런 중 RESET이면 그 런은 메타에 쓰지 않음. F3-4 Gunner 연두. F4-1 지느러미 빨강 유지. F4-3 툴팁.
  - 검증: 새 테스트 22개는 각 규칙을 일부러 망가뜨렸을 때 모두 실패하는 것을 확인했다. 사전 리뷰 3건을 반영했다. T1-P1: 일시정지 테스트가 `PhaseTime`을 본다. T1-P3: Gunner 빔 81°→90°, 테스트에 조준점 노랑 25° 조건 추가. 선택 12건은 `QUESTIONS_FOR_HYUN.md` 8–19번에 있다.
  - T3 통합 때 주의(T1-P2): `ArenaLayout.cs`의 `ArenaLayoutResult` 주석이 T3와 충돌한다. T1의 Pieces·SpawnPoints·UsedFallback 주석("대체 배열이 없으면 null, 반쯤 놓인 시도는 절대 돌려주지 않음")을 지키고, Pieces 줄에 T3의 "(on the floor, or in the air for floating pieces)"만 더한다. T3가 `Generate`를 다시 쓰면 마지막 `return`의 `: null` 규칙을 지켜야 S7-1-2 테스트 3개가 통과한다.
  - 기기 확인: (1) MR TABLE에서 권한 대화상자를 바깥을 눌러 닫기 → `perf_log`에 `ROOM_SCAN scan=PERMISSION_DISMISSED`, 다시 묻지 않음, 게임은 평소대로. (2) MR TABLE 메뉴에서 10–20초 뒤 매치 시작 → `SUMMARY`·`STOPPED`가 2프레임 뒤에 남고 매치 첫 프레임에 프로브 때문인 `HITCH`가 없음. (3) 섬 3 이후 Gunner가 연두색이고 과녁·CURSOR 주황, ASSIST 조준점과 헷갈리지 않음. 모든 원형의 지느러미가 빨강. (4) 런 중 일시정지 > RESET PROGRESS 두 번 → 런을 끝내도 NEW BEST·UNLOCKED가 없고, 다음 런은 평소대로 기록됨. (5) 유물이 해금된 상태에서 STARTING RELIC 화면에 30초 머문 뒤 런 → 끝 화면과 `run_log`의 시간에 그 30초가 들어가지 않음.
