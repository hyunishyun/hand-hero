# QUESTIONS_FOR_HYUN — Round 5

대신 내린 결정. (형식: 태스크 / 질문 / 택한 답 / 이유 / 뒤집으려면)

## T0 ASSIST 핀치 놓기

1. **T0 / 누르기 판정: 강도 0.8인가, Meta 플래그가 켜지는 순간인가 / 강도 0.8 유지.**
   - 이유: Meta 신호가 이 펌웨어에서 오는지 아직 모른다(D1 위험). 누르기를 어느 기기에서나 같게 둔다. Meta 플래그는 놓기에만 쓴다.
   - 뒤집으려면: `PinchTrigger.Step(in PinchSample, …)`의 누르기 조건에 `sample.MetaPinching` 상승 에지를 넣는다.
2. **T0 / 누르기·놓기에 어떤 값을 쓰나 / 부드럽게 하기 전 값(`RawPinchStrength`).**
   - 이유: "최고값에서 0.2 하락"을 3프레임 안에 읽으려면 원시값이 필요하다. 부드럽게 한 값은 약 5프레임 늦다. 누르기도 같은 값을 써서 기준을 하나로 맞췄다. 그래서 누르기가 1–2프레임(15–30 ms) 빨라진다. 메뉴 포인터와 손목 메뉴는 그대로 부드럽게 한 값을 쓴다.
   - 뒤집으려면: `XRHandsInputSource.Sample()`에서 `Strength = aimHand.PinchStrength`로 바꾼다.
3. **T0 / Meta 플래그가 꺼지면 언제 놓나 / 이번 누름에서 플래그가 한 번 켜진 뒤 2프레임 연속 꺼지면 놓는다. Meta의 "Valid" 플래그가 있는 프레임만 Meta 값으로 본다.**
   - 이유: Meta 플래그는 완전히 쥐었을 때만 켜진다. 천천히 쥐면 강도 0.8을 먼저 넘는다. 그 사이 꺼져 있는 것을 놓기로 읽으면 차지가 아예 안 된다.
   - 뒤집으려면: Meta를 끄려면 인스펙터에서 `metaPinchReleaseFrames = 0`으로 둔다. 조건을 바꾸려면 `PinchTrigger.ReleaseRule`의 `_metaSeen` 조건을 고친다.
4. **T0 / 쉬는 엄지(0.71)에서 다시 누를 때 / 놓인 뒤 가장 낮았던 값보다 같은 0.2만큼 올라와야 다시 누른다.**
   - 이유: 놓는 지점에서 손이 떨려도 두 번 쏘지 않게 하는 히스테리시스다. 실제 핀치는 1.0까지 올라가므로 쉽게 넘는다.
   - 뒤집으려면: 인스펙터의 `pinchRelativeRelease` 하나로 놓기와 다시 누르기가 함께 바뀐다(0 = 예전 절대 기준만).
5. **T0 / 시스템 제스처(손바닥을 헤드셋 쪽으로) 뒤 처리 / 시스템 제스처를 `PinchTrigger` 안에서 추적 끊김처럼 다룬다. 핀치를 떨어뜨리고, 다시 열어야 쏠 수 있다.**
   - 이유: 예전 `SystemGestureGate`는 강도가 0.6 아래로 내려가야 다시 열린 것으로 봤다. 쉬는 엄지는 0.71이라, 시스템 제스처 뒤 ASSIST 사격이 막힐 수 있었다. 쥔 차지는 예전처럼 취소된다(쏘지 않음). `HandMenuPointer`는 그대로 게이트를 쓴다.
   - 뒤집으려면: git에서 `XRHandsInputSource`의 이전 게이트 체인을 되살린다.
6. **T0 / 런 기록 필드 이름 / `min_strength`(쥔 동안 가장 낮은 강도), `release_strength`, `release_by`에 `peak_strength`와 `meta_seen`을 더했다.**
   - 이유: D2의 "최소값"은 "쥔 동안 최소 강도"로도, 한국어 요약의 "최소 거리"(= 최고 강도)로도 읽힌다. 그래서 둘 다 기록했다. `meta_seen`은 Meta 신호가 이 기기에서 실제로 오는지 보여 준다.
   - 뒤집으려면: `RunRecordJson.ToJson`에서 필드를 지운다.
7. **T0 / `release_by` 우선순위 / meta → absolute → relative 순으로 이름을 붙인다.**
   - 이유: 같은 프레임에 여러 규칙이 맞으면, absolute를 먼저 보아야 "예전 규칙도 놓았을 것"(absolute)과 "새 규칙만 놓은 것"(relative)이 갈린다.
   - 뒤집으려면: `PinchTrigger.ReleaseRule`의 검사 순서를 바꾼다.
