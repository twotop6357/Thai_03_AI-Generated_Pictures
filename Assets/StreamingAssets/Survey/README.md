# 만족도 설문

- `questions.json`: 문항을 배열 순서로 표시. `version`은 1, 문항은 1~64개. 현재 SurveyPanel 제목을 첫 문항으로 사용한다. `text`를 수정하거나 문항을 추가한 뒤 패널에 다시 진입하면 반영된다.
- `id`는 누적 집계의 연결 키다. 소문자 영문·숫자·`_`·`-` 1~64자, 중복 불가. 기존 통계를 유지하려면 문구/순서를 바꿔도 ID를 유지한다. 삭제한 문항 집계도 파일에서 보존한다.
- 답변 순서 고정: Very Dissatisfied / Dissatisfied / Neutral / Satisfied / Very Satisfied. 처음 선택은 Neutral, 좌/상 이전·우/하 다음(순환), 숫자 1 또는 숫자패드 1로 확정. 키를 계속 누른 상태는 추가 응답이 되지 않는다.
- `statistics.json`: 첫 답변 저장 시 이 폴더에 자동 생성. 문항 ID 아래 다섯 option ID별 **실제 정수 응답 수**를 저장한다. 이전 파일을 임시 파일로 원자적 교체한다. 실행 중 수동 편집하지 않는다. 손상/쓰기 실패 시 기존 통계를 덮어쓰거나 초기화하지 않고 오류·재시도 안내를 표시한다. 누락된 문항 파일도 재시도 안내를 표시한다.
- 응답 0개이면 다섯 값 각각 20%. 이후 `해당 답변 수 / 해당 문항 전체 응답 수 × 100`. 가상 응답/미리 반올림한 %는 저장하지 않는다. 예: Satisfied 1회만 응답하면 0/0/0/100/0%. 표시 반올림은 향후 그래프에서 결정한다.
- 한 패널 세션에서 문항당 한 번 저장. 저장 중/완료 후 연타 무시. 완료 시 감사 문구만 표시하고 `Completed` 이벤트를 발행한다. 새 화면으로의 자동 이동은 아직 없다. 패널을 껐다 켜면 새 응답자 세션이 된다. 화면 이탈 전 원자적 저장이 이미 끝났다면 그 답변은 유지된다.
- 응답별 개인정보·원문 기록 없이 집계 수와 최근 128개 제출 UUID(중복 재시도 방지용)만 보관. 로컬 통계는 Git에서 제외한다. Windows 전시 PC에서 StreamingAssets 폴더에 쓰기 권한이 필요하다.

## 다음 그래프 패널 연결

`SurveyPanelController.Statistics`는 로드된 모든 문항의 읽기 전용 집계, `CurrentStatistics`는 현재 문항 집계다. 각 `SurveyQuestionStatistics`의 `Counts`, `Percentages`, `TotalResponses`, `QuestionId` 사용. `StatisticsChanged`는 최초 로드·답변 저장 후 발생한다. 완료 시점에는 모든 답변이 디스크에 저장되어 있다.

별도 그래프 화면은 `new SurveyStore(Path.Combine(Application.streamingAssetsPath, SurveyStore.DirectoryName))`를 만들고 `LoadQuestionsAsync(token)`, `GetStatisticsAsync(questionId, token)`으로 앱 재시작 후에도 읽을 수 있다. 키보드/추후 아두이노는 메인 스레드에서 `SelectPrevious()`, `SelectNext()`, `ConfirmSelection()`을 공통 사용한다. SurveyPanel 활성 중에는 Finish 사진 목록 입력을 차단한다.
