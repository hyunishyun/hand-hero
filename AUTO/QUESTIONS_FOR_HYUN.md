# QUESTIONS_FOR_HYUN — Round 3

무인 세션이 대신 내린 결정. (형식: 태스크 / 질문 / 택한 답 / 이유 / 뒤집으려면)

- **P1 / 튜토리얼도 "전투"로 볼까?** → 예. 튜토리얼 중에는 `perf_log.txt`에 쓰지 않는다. 이유: 튜토리얼에서도 날고 쏘기 때문에, 파일 쓰기로 생기는 끊김이 섞이면 안 된다. 기록은 버퍼에 남아 있다가 메뉴로 돌아갈 때 써진다. 뒤집으려면: `PerfFlushPolicy.InCombat`에서 `MatchPhase.Tutorial`을 빼고 테스트 `Policy_CombatIsFightTutorialAndRunIslands`를 고친다.
- **P1 / 앱이 백그라운드에서 돌아온 첫 프레임을 스파이크로 셀까?** → 아니오. `OnApplicationPause(false)`에서 감지기를 리셋한다. 이유: 멈춰 있던 시간은 프레임이 아니고, 바로 앞에 `APP_PAUSE` 기록이 있다. 뒤집으려면: `PerfSpikeLogger.OnApplicationPause`의 `_detector.Reset()`을 지운다.
- **P2 / 릴리스 빌드의 스택 트레이스를 어떻게 끌까?** → 릴리스 APK를 빌드하는 동안에만 Log·Warning을 None으로 바꾸고 빌드가 끝나면 원래 값(ScriptOnly)으로 되돌린다. 그래서 `ProjectSettings.asset`의 `m_StackTraceTypes`는 바뀌지 않는다. 이유: 이 설정은 프로젝트 전체에 하나뿐이고, 에디터 콘솔의 Stack Trace Logging 메뉴도 같은 값을 쓴다. 파일에 None을 박아 두면 에디터에서 Debug.Log의 스택 트레이스도 사라진다. 뒤집으려면: `BuildScript.BuildQuestApk(bool)`의 `try/finally` 복원 부분을 지운다.
- **P2 / 퍼프 로거의 flush 로그(`[PerfSpikeLogger] ...`)도 개발 빌드 전용으로 돌릴까?** → 아니오. 릴리스에서도 `Debug.Log`로 남긴다. 이유: 안전한 때에만, 드물게 찍히고, 릴리스 APK에서도 logcat으로 로그 파일 경로를 확인할 수 있어야 한다. 뒤집으려면: `PerfSpikeLogger.Flush`의 `Debug.Log`를 `HHLog.Info`로 바꾼다.
- **P3 / 메뉴 버튼 전용 레이어를 새로 만들까?** → 아니오. Unity 내장 `UI` 레이어(5)를 쓴다. 빌더가 메뉴 버튼 콜라이더를 UI 레이어에 두고, `HandMenuPointer.buttonLayers`를 UI로 설정한다. 이유: 새 레이어는 `TagManager.asset` 변경인데 이번 라운드에서 허용된 ProjectSettings 변경 목록에 없다. 카메라 컬링은 전부 Everything이라 보이는 것은 같다. 뒤집으려면: `HandHeroSceneBuilder`의 `go.layer = MenuButtonLayer` 줄과 `buttonLayers` 설정을 지우고 씬을 다시 만든다(필드 기본값은 모든 레이어).
