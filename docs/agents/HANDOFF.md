# HANDOFF

모든 에이전트는 작업 전에 이 문서의 현재 상태, 진행 중 작업·파일 소유권 표, 최신 작업 기록을 읽는다. 외부 handoff 스킬이나 개인 메모리를 전제하지 않는다. 기록 양식은 아래 [기록 방법](#기록-방법)을 따른다. 공동 작업 규칙은 [AGENTS.md](../../AGENTS.md)에 있고, 기술 기준은 [개발 기준](../DEVELOPMENT_GUIDELINES.md)에 있다.

## 현재 상태

- 마지막 갱신: 2026-10-19 14:50 +09:00 · Claude Code · SETUP-001
- 현재 작업: 없음 (SETUP-001 완료. 개발 준비 커밋에 DOC-001 포함)
- 브랜치: `main`. push하지 않았다.
- 다음 행동: 미확정 프로젝트 값(제품명, 식별자, 전시 개요)을 정한다. 목록은 [TODO](../../TODO.md)에 있다. 로컬 전용 에셋 4종이 새 PC에서도 필요하면 Asset Store에서 가져온다.
- 미해결 치명적 장애: 발견 없음 — 장애가 없음을 검증했다는 뜻은 아님. 목록은 [TroubleShooting](../TroubleShooting.md)에 있다.

## 진행 중 작업 / 파일 소유권

`진행 중` 행의 파일은 다른 에이전트가 편집하지 않는다. 이 표는 원자적 잠금이 아니므로 기본은 순차 인계다. 작업을 끝낼 때 상태를 바꾸고 소유권 해제를 명시한다.

| 작업 ID | 담당 | 상태 | 수정 범위 | 다음 담당 | 갱신 |
|---|---|---|---|---|---|
| SETUP-001 | Claude Code | 완료(소유권 해제) | .gitignore, AGENTS.md, TODO.md, docs/agents/HANDOFF.md, URP 렌더러 에셋 커밋 포함 | 없음 | 2026-10-19 14:50 +09:00 |
| DOC-001 | Claude Code | 완료(소유권 해제) | AGENTS.md, CLAUDE.md, TODO.md, CHANGELOG.md, docs/DEVELOPMENT_GUIDELINES.md, docs/TroubleShooting.md, docs/agents/HANDOFF.md | 사용자 검토 | 2026-10-19 14:45 +09:00 |

## 현재 프로젝트 확인값

조사일 2026-10-19, 조사자 Claude Code. 값이 바뀔 수 있으므로 작업할 때마다 실제 파일로 다시 확인한다.

| 항목 | 값 | 근거 |
|---|---|---|
| 개발 저장소(origin) | `https://github.com/twotop6357/Thai_03_AI-Generated_Pictures.git` | `git remote -v` |
| 수정 금지 저장소 | wonjeong97/Template, wonjeong97/0_Startup, CoplayDev/unity-mcp, GlitchEnzo/NuGetForUnity, OpenUPM 패키지, 개인 템플릿 저장소 | 사용자 확인 |
| 브랜치 정책 | `main` 직접 작업. 브랜치는 사용자가 요청할 때만 | 사용자 확인 |
| Unity | 2022.3.62f3 (96770f904ca7) | `ProjectSettings/ProjectVersion.txt` |
| 렌더 파이프라인 | URP 14.0.12 (URP-Performant) | `Packages/manifest.json`, `Assets/Settings` |
| 공통 코드 고정 커밋 | `com.huliacdev.template@a03665700c` (26.10.16-1) | `Library/PackageCache` |
| MCP for Unity 고정 커밋 | `com.coplaydev.unity-mcp@aa5fc638d6` | `Library/PackageCache` |
| 주 대상 플랫폼 | Windows Standalone x64 | 사용자 확인 |
| 빌드 씬 | `Assets/Scenes/0_Idle.unity` 하나 | `ProjectSettings/EditorBuildSettings.asset` |
| companyName / productName | `MyCompany` / `Unity-Startup-Template` (템플릿 기본값, 미변경) | `ProjectSettings/ProjectSettings.asset` |
| applicationIdentifier (Standalone) | `com.Unity-Technologies.com.unity.template.urp-blank` (Unity 기본값, 미변경) | 같은 파일 |
| bundleVersion | `26.10.16` (템플릿에서 넘어온 값) | 같은 파일 |
| 프로젝트 스크립트 | `Assets/Scripts/{App/GameLifetimeScope, Core/GameManager, Network/APIManager}.cs` | 파일 목록 |
| 런타임 설정 | `Assets/StreamingAssets/Settings.json`(`apiUrl` 비어 있음, `useInactivityTimer` false), `ShutdownSettings.json`(모든 요일 꺼짐) | 파일 내용 |
| 테스트 | Test Framework 1.4.6 설치. 프로젝트 테스트 어셈블리 없음 | `Assets` 검색 |
| 저장소 공개 여부 | 공개(비로그인 접근 시 HTTP 200) | `curl`, 2026-10-19 |
| 로컬 전용 에셋(미추적) | Particle Image, Text Animator(Febucci), vFolders, vHierarchy. 버전은 미기록. 2026-10-19 기준 프로젝트 코드는 이 에셋을 참조하지 않는다 | `.gitignore`, `Assets/Scripts` 검색 |
| 사용 에이전트 | Claude Code(`CLAUDE.md`), Codex(`AGENTS.md` 직접 읽음) | 사용자 확인 |
| 로그 언어 | 모든 프로그램 로그 영문 | 사용자 확인 |

## 보존할 기존 로컬 변경

DOC-001을 시작할 때(2026-10-19 14:30 +09:00) 이미 있던 변경이다. 의도를 확인하기 전에는 되돌리거나 커밋하지 않는다.

- `?? .vscode/` (`settings.json`): IDE 설정이다. `.gitignore`에 포함되지 않았고, 사용자 결정에 따라 커밋하지 않았다.
- (해소됨) `Assets/Settings/URP-Performant-Renderer.asset`의 URP 14 자동 업그레이드 추정 변경: 사용자 결정에 따라 SETUP-001 커밋에 포함했다.

## 기록 방법

새 기록은 [최신 작업 기록](#최신-작업-기록) 맨 위에 다음 양식으로 추가한다.

```markdown
### [YYYY-MM-DD HH:mm +09:00] 작업 ID · 담당 → 다음 담당 · 상태
- 요청/목표:
- 기준 브랜치/HEAD, 커밋 여부:
- 변경 파일/핵심 결정:
- 검증: 수행자, 환경, 시나리오/실행 방법, 결과, 증거 위치.
- 미검증/위험/차단:
- 다음 행동/파일 소유권 해제 여부:
- 장애: TroubleShooting 항목 링크 또는 이번 작업에서 발견 없음.
```

- 날짜 형식은 `YYYY-MM-DD HH:mm +09:00`이다.
- 작업 ID는 중복하지 않는다. 예: `DOC-001`, `FEAT-001`, `FIX-001`.
- 커밋 해시가 없으면 `미커밋`이라고 적는다.
- 상대 에이전트나 사용자가 실제로 확인하지 않은 승인·검증을 기록하지 않는다.

## 최신 작업 기록

### [2026-10-19 14:50 +09:00] SETUP-001 · Claude Code → 없음 · 완료
- 요청/목표: 활용 가능성이 있는 에셋을 추가한 상태를 포함해, 프로젝트 개발 준비 단계를 나타내는 영문 커밋을 만든다.
- 기준 브랜치/HEAD, 커밋 여부: `main` / `1c6ff95`. 이 기록을 포함해 커밋했다(해시는 `git log` 참조). push하지 않았다.
- 변경 파일/핵심 결정:
  - 사용자가 추가한 에셋 4종(Particle Image, Text Animator, vFolders, vHierarchy, 미추적 파일 약 680개)은 유료 Asset Store 에셋으로 보인다. 저장소가 공개 상태여서, 사용자 결정에 따라 `.gitignore`로 제외했다. 로컬 사본은 그대로 두었다.
  - AGENTS.md 3절에 로컬 전용 에셋 규칙과 목록을 추가했다.
  - URP 렌더러 에셋 자동 업그레이드 변경은 사용자 결정에 따라 포함했다. `.vscode/`는 제외했다.
  - 커밋에는 DOC-001 문서 체계도 포함된다.
- 검증: Claude Code. `git status`로 에셋 4종이 미추적 목록에서 빠지고 `.vscode/`만 남는 것을 확인했다. `.gitignore` diff는 11줄 추가뿐이다. `Assets/Scripts`에 에셋 참조가 없다. Unity 에디터 컴파일은 수행하지 않았다(에셋 가져오기 후 에디터 로그 미확인).
- 미검증/위험/차단: 각 에셋의 라이선스 원문과 정확한 버전은 확인하지 않았다. 새로 clone한 PC에는 이 에셋이 없다.
- 다음 행동/파일 소유권 해제 여부: 소유권 해제. push는 사용자가 요청할 때만 한다.
- 장애: 이번 작업에서 발견 없음.

### [2026-10-19 14:45 +09:00] DOC-001 · Claude Code → 사용자 검토 · 완료
- 요청/목표: Claude Code와 Codex가 같은 규칙으로 작업하도록 공통 지침 문서 체계를 도입한다.
- 기준 브랜치/HEAD, 커밋 여부: `main` / `1c6ff95`. 미커밋.
- 변경 파일/핵심 결정:
  - 신규: `AGENTS.md`(공통 규칙 원본), `docs/DEVELOPMENT_GUIDELINES.md`, `docs/TroubleShooting.md`.
  - 수정: `CLAUDE.md`를 진입 안내로 바꿨다. 기존 개요·연동·규칙 틀은 AGENTS.md "0. 프로젝트 개요"와 "3. 프로젝트 운영상 필수 조건"으로 옮겼다. 이 문서(HANDOFF)는 새 구조로 바꾸고 기존 기록을 아래 "이전 이력"으로 보존했다. `TODO.md`는 새 형식으로 바꾸고 기존 완료 항목을 템플릿 이력으로 보존했다. `CHANGELOG.md`에는 기존 항목이 템플릿 시절 이력이라는 안내 한 줄만 추가했다.
  - Codex는 AGENTS.md를 직접 읽으므로 별도 진입 파일을 만들지 않았다. Gemini·Cursor·Copilot은 사용하지 않아 진입 파일을 만들지 않았다.
  - README.md(템플릿 사용 가이드)와 docs/ORIGINAL_README.md는 범위 밖이라 그대로 두었다.
- 검증: Claude Code, 로컬 작업 트리. 문서 간 상대 링크와 앵커의 대상 파일·제목이 실제로 있는지 스크립트로 확인했다. 상대 링크 28개와 앵커 3개 모두 정상이다. DEVELOPMENT_GUIDELINES의 외부 공식 문서 URL 9개는 `curl`로 HTTP 200을 확인했다. 기재한 버전·경로·식별자를 실제 파일과 대조했다. 코드·에셋 변경이 없어 컴파일·빌드는 수행하지 않았다.
- 미검증/위험/차단: 제품명·식별자·전시 개요는 미정이다. Codex가 이 문서 체계를 실제로 읽는지는 확인하지 않았다.
- 다음 행동/파일 소유권 해제 여부: 소유권 해제. 사용자가 문서를 검토하고 커밋 여부를 결정한다.
- 장애: 이번 작업에서 발견 없음.

## 이전 이력 (템플릿 시절 기록 — 이 프로젝트의 검증 결과 아님)

아래는 이 저장소가 템플릿에서 복사되기 전, 템플릿 저장소에서 남긴 기록이다. 당시 양식(공통 스킬 `handoff`, Antigravity 검증)을 그대로 보존한다. 이 기록을 현재 프로젝트의 검증 근거로 쓰지 않는다.

### [2026-10-16 10:59] Claude → Antigravity · HuliacDev Template 26.10.16-1 업데이트 (main)
- 요청: Template을 최신으로 올리고, 바뀐 내용과 이 프로젝트에 걸리는 것이 있는지 확인.
- 변경: packages-lock.json 고정 커밋 1c5e127(26.10.12-1) → a036657(26.10.16-1, 항상 위 옵션 `alwaysOnTop`). Settings.json에 `"alwaysOnTop": false` 추가, bundleVersion 26.10.16, CHANGELOG에 10-12·10-16 운영자 영향 항목 추가.
- Claude 확인: 배치 모드로 a03665700c 패키지 받기·스크립트 컴파일, error CS 0, return code 0. 프로젝트 코드는 `ConfigureWindowFocus`·`ConfigureProjectMessageBrokers`·`GetTextWithRetryAsync` 를 override·호출하지 않음.
- agy (gemini-3.8-flash-high, 2묶음):
  - 프로젝트 코드와 바뀐 Template API 충돌, Settings.json 키 이름·기본 동작 → 통과
  - CHANGELOG 사실 일치, 배치 모드 로그, lock 해시·bundleVersion·README → 통과(로그 근거 줄 440·866 직접 확인)
