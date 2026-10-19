# AGENTS.md — 공동 작업 기준

이 문서는 저장소 전체에 적용되는 모든 AI 에이전트(Claude Code, Codex 등)의 공동 기준이며, **공통 규칙의 유일한 원본**이다. 기술 기준은 [docs/DEVELOPMENT_GUIDELINES.md](docs/DEVELOPMENT_GUIDELINES.md)에 둔다. 에이전트별 진입 파일(예: [CLAUDE.md](CLAUDE.md))에는 규칙 사본을 두지 않는다.

- 사용자의 명시적 요청과 각 도구의 상위 정책이 이 문서보다 우선한다.
- 별도 개인 메모리, 계정 연결, 외부 공통 스킬 없이 이 저장소 문서만으로 작업할 수 있어야 한다.
- 문서와 실제 파일이 다르면 실제 파일을 확인하고, 차이를 [HANDOFF](docs/agents/HANDOFF.md)에 기록한다.

## 0. 프로젝트 개요

이 저장소는 개인 Unity 시작 템플릿(0_Startup 복사본)에서 파생된 전시·키오스크용 Unity 프로젝트다. 아래 항목은 아직 확정되지 않았다. 확인되면 이곳에 기록하고, 확인되기 전에는 추측해서 채우지 않는다.

- 전시명, 설치 장소, 씬 흐름: 미기록. 현재 씬은 `Assets/Scenes/0_Idle.unity` 하나다.
- 하드웨어·외부 연동(센서, RFID, 시리얼, API 엔드포인트): 미기록. `Settings.json`의 `apiUrl`은 비어 있다.
- 확인된 버전·식별자·로컬 상태는 [HANDOFF의 현재 프로젝트 확인값](docs/agents/HANDOFF.md#현재-프로젝트-확인값)을 기준으로 한다.

## 1. 작업 전 필수 절차

새 세션, 재개, 인계, 컨텍스트 축약 후에는 파일을 수정하기 전에 다음을 수행한다.

1. 이 문서와 [개발 기준](docs/DEVELOPMENT_GUIDELINES.md)을 읽는다. 작업 경로에 추가 지침이 있으면 함께 읽는다.
2. [HANDOFF](docs/agents/HANDOFF.md)의 현재 상태, 진행 중 작업, 최신 기록을 확인한다. 연결된 미해결 [TroubleShooting](docs/TroubleShooting.md) 항목과 [TODO](TODO.md)도 확인한다.
3. 현재 경로, `git remote -v`, `git status --short`, 현재 브랜치, 최근 커밋, 관련 diff를 직접 확인한다. 기록만 보고 상태를 추정하지 않는다.
4. 관련 코드·설정·에셋·패키지 버전을 실제로 읽고 변경 범위와 검증 방법을 정한다. 수정을 시작하기 전에 HANDOFF에 기준 커밋, 작업자, 작업 ID, 수정 예정 파일을 기록한다.
5. 다른 작업자가 소유한 파일이나 의도를 알 수 없는 새 변경과 겹치면 편집을 멈추고 사용자 또는 담당자와 조율한다. 임의로 정리하거나 덮어쓰지 않는다.

- 최신 기록을 읽지 못했거나 충돌이 해소되지 않았으면 겹치는 구현을 시작하지 않는다.
- 작업 시작 직전과 완료 직전에 HANDOFF와 Git 상태를 다시 읽는다.

## 2. 저장소 경계

- **개발 저장소(push 대상)**: `origin` = `https://github.com/twotop6357/Thai_03_AI-Generated_Pictures.git` 하나뿐이다.
- **수정·push·PR 금지 저장소(다운로드만 허용)**: 아래 저장소는 패키지 공급처나 원본으로만 사용한다.
  - `https://github.com/wonjeong97/Template.git` (`com.huliacdev.template`, 공통 코드)
  - `https://github.com/wonjeong97/0_Startup` (템플릿 원본)
  - `https://github.com/CoplayDev/unity-mcp.git` (MCP for Unity)
  - `https://github.com/GlitchEnzo/NuGetForUnity.git`
  - OpenUPM 패키지(`com.cysharp.*`, `jp.hadashikick.*`)
  - 이 프로젝트를 만든 개인 템플릿 저장소. 이 프로젝트 작업 중에는 템플릿 저장소를 수정하지 않는다.
- 외부 패키지 캐시(`Library/PackageCache`)와 NuGet 패키지 폴더(`Assets/Packages`)는 직접 수정하지 않는다. 공통 코드 동작을 바꿀 때는 프로젝트의 파생 클래스(`GameManager : GameManagerBase`, `APIManager : ApiManagerBase`, `GameLifetimeScope : RootLifetimeScope`)나 확장 지점에서 구현한다.
- 사용자나 다른 에이전트의 변경을 삭제·되돌리기·덮어쓰기·일괄 포맷·임의 커밋하지 않는다. `git reset --hard`, `git checkout -- <파일>`, `git clean`, 강제 push를 정리 수단으로 쓰지 않는다.
- 요청과 관련된 최소 범위만 수정한다. 불필요한 패키지 업데이트나 구조 개편을 섞지 않는다.
- **브랜치**: 기본적으로 `main`에서 직접 작업한다. 사용자가 요청한 경우에만 브랜치를 만들고, 이름은 사용자가 지정한 이름을 우선한다.
- **커밋·push는 사용자가 요청할 때만** 한다.

## 3. 프로젝트 운영상 필수 조건

- **로그 언어**: 런타임과 에디터 도구를 포함한 프로그램 로그 메시지는 모두 영문으로 쓰고 `[ClassName] message` 형식을 따른다. 코드 주석과 문서는 한글을 써도 된다. 예외를 다시 던지거나 기록할 때 원본 스택과 진단 정보를 숨기지 않는다.
- **기준 버전**: Unity `2022.3.62f3`, URP `14.0.12`. 에디터 버전을 바꾸지 않는다. 버전 변경이 필요하면 사용자에게 먼저 확인한다.
- **주 대상 플랫폼**: Windows Standalone x64. 빌드 씬 목록은 `0_Idle` 하나다. `productName`, `companyName`, `applicationIdentifier`는 아직 템플릿 기본값이며, 사용자가 정한 값으로 바꾸기 전까지 임의로 정하지 않는다. 이 값들은 로그 폴더와 PlayerPrefs 위치를 결정한다.
- **첫 화면 복귀**: `GameManager.ReturnToFirstScreen()`이 무입력 타임아웃(`InactivityTimeoutEvent`)과 수동 복귀가 함께 쓰는 진입점이다. 타임아웃 경로의 `move_idle_timeout` 로그는 `ApiManagerBase`가 직접 보낸다. 수동 복귀 경로에서만 `MoveIdleEvent`를 발행해 로그가 중복되지 않게 한다.
- **설정 로드**: `Assets/StreamingAssets/Settings.json`과 `ShutdownSettings.json`은 공통 코드(`GameManagerBase` 등)가 비동기로 읽는다. 설정을 읽지 못하면 오류 로그를 남기고, 비활동 복귀를 90초로 켠 대체 설정으로 실행한다. 설정 키를 추가하거나 바꿀 때는 기본값과 실패 시 동작을 함께 정하고 CHANGELOG에 기록한다.
- **DI**: VContainer를 쓴다. 루트 스코프는 `Assets/Prefabs/GameLifetimeScope.prefab`(VContainerSettings의 Root Lifetime Scope)이다. override할 때 `base.Configure`, `base.OnEnable`, `base.OnDisable` 호출을 보존한다. 프로젝트 의존성은 베이스 `Construct` 시그니처를 바꾸지 않고, 별도의 `[Inject]` 메서드로 추가한다.
- **사용자 요청 없이 실행하면 안 되는 위험 동작**:
  - **PC 종료·재시작**: ShutdownScheduler 실제 발동, `shutdown` 명령 실행, 자동 종료 요일 활성화 상태로 실행
  - **Windows 작업 스케줄러 등록·삭제**: ShutdownScheduleEditor 등을 통한 등록·삭제
  - **실제 API 서버로 전송**: 운영 `apiUrl`로의 로그·데이터 전송, 과금되는 외부 AI 생성 API 호출
- 비밀 키, 개인정보, 토큰을 소스·설정 파일(`StreamingAssets` 포함)·로그·인계 문서에 남기지 않는다.
- **로컬 전용 Asset Store 에셋**: `origin`은 공개 저장소다. 라이선스상 공개 재배포가 불가능한 에셋은 `.gitignore`로 제외하고, 각 PC에서 Asset Store로 직접 가져온다. 이런 에셋을 커밋하지 않는다. 현재 대상은 Particle Image(`Assets/AssetKits/`), Text Animator(`Assets/Plugins/Febucci/`), vFolders(`Assets/vFolders/`), vHierarchy(`Assets/vHierarchy/`)다. 새 에셋을 추가하면 라이선스를 확인하고 이 목록과 `.gitignore`를 함께 갱신한다. 프로젝트 코드가 이 에셋에 의존하게 되면, 새로 clone한 PC는 에셋을 가져오기 전까지 컴파일되지 않는다. 이 사실을 HANDOFF에 기록한다.
- **패키지**: `Packages/manifest.json`과 `Packages/packages-lock.json`을 함께 관리한다. Git 패키지의 고정 커밋은 lock 파일에 있다. 패키지를 업데이트하기 전에 복구 가능한 커밋을 확보한다. 커밋할 수 없으면 업데이트를 보류하고 이유를 HANDOFF에 기록한다. `Tools → Update Stack Packages`는 여러 패키지를 한꺼번에 올리므로, 요청 범위에 포함된 경우에만 실행한다.

## 4. 공동 작업과 인수인계

- 에이전트 사이에 고정된 구현자·검증자 우선순위는 없다. 작업마다 담당자와 검증자를 명시한다. 다른 에이전트가 실제로 확인하지 않은 내용을 교차 검증했다고 기록하지 않는다.
- **기본은 순차 인계다.** 같은 작업 트리에서 동시에 편집하지 않는다. 병행 작업은 사용자가 명시적으로 허용하고, 파일 범위와 별도 worktree·브랜치가 분리된 경우에만 한다.
- 같은 Unity 프로젝트를 여러 에디터 프로세스로 동시에 열지 않는다. Unity MCP나 배치 모드로 에디터를 다루는 담당자는 한 시점에 한 명뿐이다. 에디터가 열려 있는 동안 배치 모드를 실행하지 않는다.
- HANDOFF의 소유권 표는 소통 수단이지 원자적 잠금이 아니다. 충돌이 보이면 조율한다. 만료 시간만 보고 다른 작업자의 작업을 가져가지 않는다.
- 상태는 `진행 중 → 인계 대기 / 완료 / 차단`으로 전환한다. 차단이면 사유와 다음 행동을 적는다. 강제로 인수할 때는 사용자 승인을 받고, 남아 있는 diff와 기존 담당자를 확인한 뒤 인수한다.
- 작업을 종료·중단·인계하기 전에 HANDOFF의 현재 상태를 갱신하고, 최신 기록 맨 위에 다음을 추가한다: 수정 파일, 실제 검증, 미검증 항목, 남은 위험, 다음 행동. 커밋하지 않았으면 미커밋이라고 적는다.
- HANDOFF·TODO·TroubleShooting을 갱신하기 직전에 파일을 다시 읽고 필요한 부분만 편집한다. 다른 작업자의 기록은 지우거나 고쳐 쓰지 않는다.
- 외부 스킬이 없거나 과거 에이전트를 쓸 수 없다는 이유로 검증 기록을 만들어 내지 않는다.

## 5. 완료 조건

- 변경한 동작의 정상·실패·취소·반복 경로를 변경 범위에 맞게 검증한다. 문서만 바꿨으면 링크, 내용, 실제 파일과의 일치를 확인한다.
- 치명적 장애는 즉시 [TroubleShooting](docs/TroubleShooting.md)에 기록하고 HANDOFF에서 연결한다. 원인이 확정되지 않았으면 `추정`으로 표시하고 확인 방법을 적는다.
- 테스트가 실패했거나 환경이 없어 수행하지 못했으면 실패·미수행과 그 이유를 명시한다. 실제로 실행하지 않은 컴파일·빌드·테스트를 통과로 보고하지 않는다.
- 최종 보고에는 변경 내용, 검증 결과, 남은 항목을 짧게 적는다. 운영자나 관람객이 겪는 동작이 바뀌면 [CHANGELOG](CHANGELOG.md)를 갱신한다.
