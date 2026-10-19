# 개발 기준

**적용 범위**: Unity 2022.3.62f3 / URP 14.0.12 / C# (Unity 기본 스크립팅 백엔드) / 주 대상 Windows Standalone x64. 주요 라이브러리: VContainer 1.19.0, UniTask 2.5.11, MessagePipe 1.8.2, R3 1.3.1, ZLogger 2.5.10, ZString 2.6.0, DOTween(`Assets/Plugins`), Input System 1.19.0, Addressables 1.29.0, Test Framework 1.4.6, 공통 코드 `com.huliacdev.template`.

공동 작업 절차와 저장소 경계는 [AGENTS.md](../AGENTS.md)에 있고, 현재 상태와 인계 기록은 [HANDOFF](agents/HANDOFF.md)에 있다. 이 문서는 기술 기준만 다룬다.

## 설계와 코드

- 실패 경로까지 설명할 수 있는 가장 단순한 구현을 고른다.
- 이미 설치된 라이브러리(UniTask, MessagePipe, R3, ZString, DOTween, 공통 코드 매니저)를 우선 활용한다. 다만 설치된 실제 버전의 API를 `Library/PackageCache`나 공식 문서에서 확인한 뒤 쓴다.
- 불필요한 전역 싱글톤, 새 프레임워크, 추상화 계층을 만들지 않는다. 의존성은 VContainer 주입이나 명시적인 인자로 전달하고, `FindObjectOfType`·`GameObject.Find` 같은 암묵적 검색에 기대지 않는다.
- 상태와 화면 전환의 책임자를 명시한다. 버튼 연타, 중복 이벤트, 늦게 도착한 응답을 막는 가드를 둔다. 복귀·취소·해제는 반복 호출해도 안전해야 한다.
- 핫패스(`Update`, 매 프레임 콜백)에서 검색, `GetComponent`, 리플렉션을 피하고 참조를 캐시한다.
- 직렬화 필드 이름을 바꿀 때는 `[FormerlySerializedAs]`로 기존 씬·프리팹 값을 보존한다.
- 예외를 조용히 삼키지 않는다. 무조건적인 null 무시(`?.` 남발로 실패 숨기기)나 무한 재시도를 하지 않는다. 실패하면 영문 로그를 남기고 사용자 탈출 경로를 둔다.

## GC·관리 메모리

- 목표는 워밍업이 끝난 뒤 반복 핫패스의 지속 할당을 0에 가깝게 유지하는 것이다. 초기화와 화면 전환 시점의 할당은 허용한다. 실측하지 않고 "GC 0"을 보장한다고 말하지 않는다.
- 매 프레임 코드에서 컬렉션 생성, 문자열 연결(`ZString` 사용), 박싱, 캡처 람다, LINQ를 검토하고 버퍼를 재사용한다.
- 추측으로 최적화하지 않는다. Profiler, Memory Profiler(1.1.12), Profile Analyzer로 확인한 뒤 판단한다.
- 캐시·이벤트·큐에는 크기 제한, 만료, 초기화 책임자를 둔다. 장시간 운영 앱이므로 시간이 지나며 커지는 컬렉션을 특히 주의한다.
- 풀링은 측정으로 확인된 대상에만 적용한다. 이중 반환을 막고, 반환할 때 상태를 초기화한다.
- `GC.Collect()`를 기본 해결책으로 쓰지 않는다.
- 로그는 빈도를 제한한다(매 프레임 로그 금지). 로그 파일 보존 정책은 공통 코드의 로그 보관 기능을 따른다.

## 네이티브 자원

- 런타임에 만든 `Texture2D`·`RenderTexture`·`Material`·스트림·소켓·`UnityWebRequest`·시리얼 포트마다 소유자와 해제 경로(`Destroy`, `Release`, `Dispose`)를 정한다. 생성된 이미지를 반복해서 다룰 때는 이전 텍스처 해제를 빠뜨리지 않는다.
- 공유 에셋(`sharedMaterial`, 프로젝트 에셋)을 런타임에 변경하지 않는다.
- 영상, 음향, DOTween 트윈, 코루틴, 구독은 화면이 끝날 때 정리한다. `DOTween.KillAll()`처럼 전역에 영향을 주는 정리는 쓰지 않고, 대상이나 ID를 지정해 정리한다.
- 정적 참조가 씬 객체나 큰 에셋을 붙잡고 있지 않게 한다.

## 비동기·이벤트·스레드

- 앱·세션·화면의 수명을 구분하고, 수명마다 별도의 취소 범위(`CancellationTokenSource`, `destroyCancellationToken`)를 둔다. 만든 CTS는 `Cancel`·`Dispose`한다.
- 취소 토큰을 하위 요청까지 전달한다. 취소할 수 없는 작업은 세대 ID로 늦게 도착한 결과를 무효화한다.
- `async void`는 이벤트 진입점에서만 쓴다. 그 외에는 `UniTask`/`UniTaskVoid`를 쓴다. fire-and-forget은 예외와 취소를 처리할 책임자가 있을 때만 쓴다.
- 메인 스레드에서 `.Wait()`·`.Result`·`GetAwaiter().GetResult()`로 블로킹하지 않는다.
- MessagePipe·R3 구독과 해제를 대칭으로 둔다. 예: `OnEnable`에서 구독했으면 `OnDisable`에서 해제한다.
- 외부 장치(시리얼·아두이노 등)의 콜백은 크기가 제한된 큐를 거쳐 메인 스레드로 넘긴다. 워커 스레드에서 Unity API를 호출하지 않는다.

## API·설정·파일

- 요청마다 타임아웃, 취소, 재시도 상한을 둔다. 공통 코드의 `ApiRetryUtil` 등 기존 유틸이 있으면 우선 쓴다.
- 결제·생성·전송처럼 부작용이 있는 요청은 멱등성 계약이 없으면 자동으로 재시도하지 않는다. 특히 과금되는 AI 생성 요청이 이에 해당한다.
- 응답을 검증하고, 가장 최근에 성공한 결과만 적용한다. 실패하면 관람객이 첫 화면으로 돌아갈 수 있는 탈출 경로를 둔다.
- 설정의 준비 상태를 명시하고, 값이 허용 범위 안에 있는지 검사한다. 범위를 벗어난 값은 경고 로그를 남기고 기본값을 쓴다.
- 파일은 임시 파일에 쓴 뒤 교체하는 방식으로 저장하고, 쓰기 실패를 처리한다.
- API 키는 저장소 밖의 승인된 경로(환경 변수, 설치 PC 전용 설정 등)로 주입한다. 배포물과 `StreamingAssets`에 키를 넣지 않는다.

## 에셋·패키지·변경 관리

- `.meta` 파일과 GUID를 보존한다. 에셋을 이동·이름 변경할 때는 Unity 에디터(또는 Unity MCP)로 해서 `.meta`가 함께 따라가게 한다.
- 씬과 프리팹은 가능하면 에디터 도구로 수정한다. YAML을 직접 고친 경우에는 에디터에서 열어 확인한다.
- `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, `Build(s)/`, `*.csproj`, `*.sln`, IDE 설정 같은 캐시·임시·빌드 결과·자동 생성 파일을 소스 변경에 섞지 않는다.
- URP 에셋처럼 에디터가 자동으로 업그레이드한 직렬화 변경은 의도한 작업과 분리해 기록한다.
- 큰 이미지·영상·폰트는 `.gitattributes` 규칙에 따라 Git LFS로 관리한다. DLL과 서드파티 폴더는 일반 파일로 둔다.
- DLL, 플랫폼 설정, IL2CPP/AOT, Editor 전용 설정(예: `Assets/Plugins/Roslyn`)을 바꾸면 Windows 대상 빌드에서 확인한다.

## 검증 기준과 성능 기록

- 위험에 맞는 검증을 고른다. 구현을 그대로 복제하는 테스트나 사소한 확인을 위한 테스트는 만들지 않는다. 필요한 경우 Unity Test Framework(EditMode/PlayMode)를 쓴다.
- 확인할 경로: 첫 화면 복귀, 버튼 연타, 반복 진입, 취소 직후 도착한 응답, 설정 파일 누락·손상, API 실패·타임아웃, 장비 단절, 앱 재실행.
- PC 종료, 작업 스케줄러, 실제 API 전송 같은 위험 동작은 모의 실행이나 로그 확인으로 검증한다. 실제로 발동시키지 않는다.
- 컴파일 확인은 에디터 또는 배치 모드(`Unity.exe -batchmode -quit -projectPath <경로> -logFile <파일>`)로 한다. 이 확인은 에디터가 닫혀 있을 때만 한다. 로그의 `error CS` 개수와 종료 코드를 근거로 남긴다.
- 성능은 설치 PC나 동등한 장비에서 실제 빌드로 측정하고, 에디터 측정 결과와 구분해 기록한다. 측정하지 않았으면 측정했다고 쓰지 않는다.
- 검증 기록은 HANDOFF에 남긴다: 환경, 시나리오, 결과, 증거 위치, 수행하지 못한 항목과 그 이유.

## 공식 참고

- Unity 2022.3 매뉴얼: https://docs.unity3d.com/2022.3/Documentation/Manual/index.html
- 관리 메모리와 GC: https://docs.unity3d.com/2022.3/Documentation/Manual/performance-managed-memory.html
- 에셋 메타데이터(.meta): https://docs.unity3d.com/2022.3/Documentation/Manual/AssetMetadata.html
- 에디터 명령줄 인자(배치 모드): https://docs.unity3d.com/2022.3/Documentation/Manual/EditorCommandLineArguments.html
- URP 14: https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/index.html
- Unity Test Framework 1.4: https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/index.html
- VContainer: https://vcontainer.hadashikick.jp/
- UniTask: https://github.com/Cysharp/UniTask
- MessagePipe: https://github.com/Cysharp/MessagePipe
