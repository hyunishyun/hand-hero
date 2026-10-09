# QUESTIONS_FOR_HYUN — Round 4

무인 세션이 대신 내린 결정. (형식: 태스크 / 질문 / 택한 답 / 이유 / 뒤집으려면)

## S1-1 GPU 워밍업 방식
- **질문:** Unity 6.6의 워밍업 API(`GraphicsStateCollection`, `ShaderWarmup`)를 쓸까, 직접 그려서 워밍업할까?
- **택한 답:** 직접 그린다. `RenderWarmup`이 씬 로드 직후 3프레임 동안 카메라 앞 12 m에 작게(×0.05) 런 봇 1기, 빔 3색, 피격 이펙트, 처치 폭발, 비네트(알파 1/255), 차지 구슬을 그리고 풀에 돌려준다.
- **이유:** 두 API 모두 에디터 어셈블리에 있다(`UnityEngine.Rendering.GraphicsStateCollection`, `UnityEngine.Experimental.Rendering.ShaderWarmup`). 그런데 `GraphicsStateCollection`은 기기에서 먼저 `BeginTrace`/`SaveToFile`로 기록한 컬렉션이 있어야 한다(무인으로 못 만든다). `ShaderWarmup.WarmupShader`는 셰이더의 모든 변형을 굽기 때문에 URP Lit에서는 오래 걸리고, Vulkan에서는 실제 렌더 패스 상태를 몰라서 실제로 쓸 파이프라인과 다를 수 있다. 실제 프레임에서 실제 물체를 그리는 쪽이 기기의 렌더 패스와 정확히 맞는다.
- **뒤집으려면:** 씬의 `Match` 오브젝트에서 `RenderWarmup` 컴포넌트를 끄면 된다(로거 헤더에 warmup이 안 나온다). 나중에 기기에서 `GraphicsStateCollection` 트레이스를 한 번 기록하면 그 파일로 바꿀 수 있다.

## S1-2 3차 VrApi 로그 다시 보기: 30초 끊김과 시스템 레이어
- **질문:** RUN 시작 직후 30초의 Stale 프레임이 정말 게임 때문인가?
- **택한 답:** D1(미리 만들기 + 워밍업 + 작은 끊김 카운터)은 계획대로 넣었다. 다만 로그를 다시 보니 다른 원인 후보가 있어서 기록해 둔다.
- **근거:** 세션 전체 Stale 167프레임 중 146프레임이 컴포지터 레이어 수(`LCnt`)가 바뀐 시점 ±1초 안에 있다. 12:49:39–12:50:10에는 다른 프로세스(pid 3551, 앱 시작 전부터 있던 시스템 프로세스)가 레이어를 올렸다 내렸다 했고, 그 프로세스가 사라진 뒤로 Stale이 0이 됐다. 같은 구간에 게임 쪽 프레임은 가장 긴 것이 16.5 ms였다(`perf_log` flush 줄). 즉 시스템 오버레이(알림, 안내 창 등)가 뜬 것이 주원인일 수 있다.
- **확인하려면:** 다음 기기 테스트에서 RUN 시작 직후 헤드셋 안에 시스템 알림이나 안내 창이 떴는지 본다. logcat에서 `LCnt=`가 1이 아닌 구간과 Stale을 같이 본다.
