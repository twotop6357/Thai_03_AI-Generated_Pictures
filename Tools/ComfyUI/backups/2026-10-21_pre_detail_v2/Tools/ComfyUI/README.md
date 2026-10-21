# 식재료 블록 → 음식 사진

15종 중 인식된 **1~6개**를 입력해 한 접시의 **태국 음식 느낌** 사진을 생성한다. 재료별 큰 형태를 요리 표면에 보이게 지정해 가려짐과 누락을 줄인다. 별도 재료 그릇이나 재료 사진 합성은 사용하지 않는다.

코코넛 밀크가 있으면 태국식 코코넛 커리(쌀도 있으면 같은 접시의 커리밥), 쌀을 포함한 다른 조합은 카오팟 스타일 볶음밥, 오이를 포함하면 얌 스타일 샐러드, 나머지는 태국식 볶음 방향을 선택한다. 쌀만 선택하면 자스민 쌀밥, 코코넛 밀크만 선택하면 코코넛 크림 디저트 표현을 쓴다. 전통 레시피의 정확한 재현을 보장하는 분류가 아니라, 선택 재료를 살리는 생성 프롬프트 방향이다. 청화 도자기·티크 테이블·접시 아래 바나나 잎으로 분위기를 보강하며, 선택하지 않은 바질·라임·고추를 스타일 때문에 강제 추가하지 않는다.

## 워크플로 파일

- `Thai_03_Food_Ingredients.json`: ComfyUI에서 여는 기본 UI 워크플로. **512×512**, 배치 1.
- `Thai_03_Food_Ingredients.api.json`: 같은 그래프의 `/prompt` 제출용 JSON.
- `Thai_03_Food_Ingredients_640.json` / `.api.json`: **640×640** 비교용.
- `food_workflow.py`: 인식 재료에 맞는 프롬프트와 UI/API 워크플로를 만드는 Python 표준 라이브러리 도구.
- `verification.json`: 현 PC의 생성 시간, 입력 검증 및 시각 검토 기록.

기본 JSON에는 예시로 Rice, Egg, Shrimp, Garlic, Mushroom, Coconut milk 6개가 들어 있다. **6개가 필수라는 뜻이 아니다.** 1~5개 입력이면 그 재료만 프롬프트에 넣고, 나머지 슬롯을 임의 재료로 채우지 않는다. Unity의 ChiliPepper/CoconutMilk 이름도 입력할 수 있다.

현재 PC ComfyUI 사용자 워크플로 목록의 **`Thai_03_Food_Thai_Style`**이 태국 스타일 버전이다. 이전 `Thai_03_Food_Ingredients` 사본과 미저장 탭은 보존했다. 저장소의 기본/640 JSON은 태국 스타일로 갱신했다. JSON을 직접 끌어다 열어도 된다. `CLIPTextEncode`의 텍스트를 바꿀 수 있지만, 실제 블록 목록에 맞추려면 아래 도구로 다시 생성한다.

## 설정과 필요한 모델

이미 설치된 모델을 사용하며 새 모델/패키지/커스텀 노드를 설치하지 않았다.

| 항목 | 값 |
|---|---|
| 모델 | `diffusion_models/flux-2-klein-4b-fp8.safetensors` (distilled) |
| 텍스트 인코더 | `text_encoders/qwen_3_4b.safetensors`, type `flux2` |
| VAE | `vae/flux2-vae.safetensors` |
| 샘플링 | Euler, Flux2Scheduler, 4 steps, CFG 1 |
| 크기/배치 | 기본 512×512 / 1 |
| 후처리 | refiner·업스케일·추가 생성 없음 |
| 저장 | ComfyUI output의 `Thai03/Food_*.png` |

`base` 모델은 사용하지 않는다. 크기를 바꿀 때는 **EmptyFlux2LatentImage와 Flux2Scheduler를 함께** 바꾼다. 도구는 두 값을 함께 설정한다. 기본 `weight_dtype=default`를 유지하며 최신 GPU 전용 FP8 fast 옵션을 강제하지 않는다.

공식 [ComfyUI Klein 가이드](https://docs.comfy.org/tutorials/flux/flux-2-klein)와 [공식 워크플로](https://github.com/Comfy-Org/workflow_templates/blob/main/templates/image_flux2_klein_text_to_image.json)의 distilled 구성을 참고했다.

## 입력과 실행

저장소 루트에서 실행한다. Python 3만 필요하다.

```powershell
# 파일 생성만: 식재료 1개
python Tools/ComfyUI/food_workflow.py Rice

# 파일 생성만: 식재료 3개 (C# enum 이름도 지원)
python Tools/ComfyUI/food_workflow.py Shrimp Garlic CoconutMilk

# 로컬 ComfyUI에서 사진 1장 생성, 결과와 시간 기록
python Tools/ComfyUI/food_workflow.py Rice Egg Shrimp Garlic Mushroom "Coconut milk" --run

# 640 비교용 파일. 별도 경로에 내보내 기본 파일을 보존
python Tools/ComfyUI/food_workflow.py Rice Egg --width 640 --height 640 --output Temp/food_two_640 --run
```

`--run`이 없으면 JSON 파일만 만든다. `--seed`를 생략하면 매 호출 새로운 seed를 사용한다. 기본 출력 이름으로 재실행하면 프로젝트의 해당 JSON 두 개를 갱신하며, ComfyUI 사용자 폴더의 사본은 자동으로 덮어쓰지 않는다. 새 JSON을 열어 사용한다.

0개·7개 이상·중복·알 수 없는 재료는 거절한다. 실행은 localhost HTTP만 허용한다. 기존 작업이 큐에 있으면 제출하지 않는다. 모델/노드 존재를 확인하고 **한 번만** 제출한다. 기본 29초가 지나면 결과를 실패로 처리하며 재시도하지 않는다. 공유 큐 전체를 중지하지 않으므로 이미 제출된 GPU 작업은 완료될 수 있다. 제한 시간 검사는 속도 보장이나 이미지 내용 검증을 의미하지 않는다.

## Unity 연결

`Assets/Scenes/1_Game.unity`를 실행하고 왼쪽 RFID 테스트 패널에서 재료를 선택한 뒤 **숫자키 1(상단 또는 숫자패드)** 또는 빨간 생성 버튼을 누른다. 최소 1개가 있어야 동작한다. 즉시 LoadingPanel로 전환하고 선택한 1~6개의 WordBox만 순서대로 팝업한다. 생성 중에는 버튼/키 연타를 무시한다. 사진 수신과 게이지 100%·단어 팝이 끝나면 ResultPanel의 GeneratedImage와 WordBox에 결과를 표시한다. Cancel/Back으로 GamePanel에 복귀하면 새 선택을 시작한다.

`Canvas`의 `FoodGenerationController.GenerateFromDetectedBlocks()`가 공통 버튼 진입점이다. 화면 표시·DOTween·Sprite는 같은 Canvas의 `FoodGenerationPresentation`이 관리한다. 향후 아두이노 수신 콜백은 메인 스레드로 전달한 다음 생성 함수를 호출한다. 실제 아두이노/시리얼 연결은 아직 없다.

현재 재료 1~6개의 스냅샷 → localhost `/queue` 확인 → 요청 전용 `clientId`로 `/ws` 연결 → 같은 ID로 `/prompt` 1회 제출 → 해당 `prompt_id`의 진행 이벤트와 `/history` 확인 → `/view` 이미지 수신 순서다. Loading·Result는 같은 스냅샷을 사용한다. GamePanel 비활성화는 인식 상태를 초기화하지만 공통 Canvas의 요청은 유지된다. 취소·Canvas 비활성화 시 HTTP/소켓 대기와 늦은 결과 적용을 취소하고 소유 Sprite·Texture를 해제한다. 실패 시 GamePanel로 복귀해 상태를 표시한다. 총 29초 제한은 연결·다운로드·완료 애니메이션까지 포함한다. 취소·시간 초과 시 ComfyUI 서버의 기제출 GPU 작업을 전역 interrupt로 중지하지 않는다.

게이지는 **경과 시간의 정확한 비율이 아니라 고정 워크플로의 단계 진행률**이다. 다른 요청의 이벤트는 무시하고 `progress_state`/`progress`/`executing`/캐시·완료 이벤트를 사용한다. 준비 0~15%, 4단계 샘플링 15~80%(value/max 반영), VAE 디코드 80~90%, 저장 90~95%, 서버 완료 96%, 유효한 이미지 수신 후에만 100%가 된다. 서버 이벤트가 없으면 임의로 다음 단계로 진행하지 않는다. 연결 도중 소켓이 끊기면 진단 경고를 남기고 HTTP 완료·이미지 수신으로 마무리한다. 최초 소켓 연결 실패는 생성 요청을 제출하지 않고 복귀한다.

각 새 목표까지 DOTween 선형 보간(기본 1.2초). 이전 목표에 도착하기 전에 다음 목표가 오면 현재 표시값에서 새 목표까지 0.22초로 가속한다. 이전 트윈을 강제 완료하거나 게이지를 뒤로 돌리지 않는다. Canvas의 Presentation Inspector에서 팝 길이·단어 간격·보간/추격 시간을 조절한다. 이 단계 가중치는 위 노드 ID 1~13 구성 전용이므로 워크플로 구조를 바꾸면 `ComfyProgressClient` 매핑도 함께 갱신해야 한다.

서버 주소 기본값은 `http://127.0.0.1:8188`이다. `Canvas` 컴포넌트 Inspector에서 로컬 포트 변경이 가능하며 외부 호스트는 거절한다. 공통 운영 로그용 `Settings.json.apiUrl`과는 별도다. 새 Settings 키나 패키지는 추가하지 않았다.

Unity는 Python 프로세스를 실행하지 않고, 씬에 참조된 `Assets/Resources/ComfyFoodWorkflow.json`을 사용한다. 이 파일은 워크플로·15종 설명·요리 분기 문구·프롬프트 템플릿을 함께 담는다. Python 도구와 문구를 바꿨다면 아래처럼 내보낸 뒤 Unity 에셋을 갱신한다. 기본 워크플로를 바꾸지 않으려면 `--output`도 별도 경로로 지정한다.

```powershell
python Tools/ComfyUI/food_workflow.py Rice Egg --output Temp/food_export --export-unity Assets/Resources/ComfyFoodWorkflow.json
```

예시 재료는 내보낼 때 JSON 기본 프롬프트에만 들어가며 Unity는 생성 버튼을 누를 때 실제 인식 재료로 교체한다. Python/Unity 간 가능한 9,948개 조합의 프롬프트가 동일한지 확인했다. Unity 요청 seed는 매 요청 바뀌고, 저장 경로는 ComfyUI output의 `Thai03/UnityFood_*.png`다.

## Finish 사진 기록

`2_Finish`의 공통 Canvas에 `FinishGalleryController`가 연결되어 있다. `PictureSelectGroup`의 기존 Image 12칸을 사용하고 사진이 적으면 남는 칸을 숨긴다. 최신 사진부터 오래된 순서(좌→우, 위→아래)로 배치하며 최신은 항상 첫 칸이다. 디스크 기록은 기존 순서를 유지하고 표시할 때 역순으로 읽으므로 기존 사진에도 바로 적용된다. 좌우 화살표는 현재 선택을 강조하고 양 끝에서 순환한다. 숫자키 1/숫자패드 1로 `OtherPictureViewPanel`을 열어 같은 사진과 당시 선택한 1~6개 재료만 표시한다. 상세 화면의 1/Esc/Back으로 기존 선택 위치에 돌아온다. 사진이 없으면 선택은 무시한다.

향후 아두이노 버튼은 Unity 메인 스레드에서 `SelectPrevious()`, `SelectNext()`, `ConfirmSelection()`, `CloseDetail()`을 호출한다. 컨트롤러는 두 패널이 꺼져도 유지되는 공통 Canvas에 둔다. 실제 아두이노 수신은 아직 없다.

Game의 생성·이미지 표시까지 성공하면 PNG와 독립적인 재료 스냅샷을 `Application.streamingAssetsPath/Thai03FoodHistory/`(에디터: `Assets/StreamingAssets/Thai03FoodHistory/`)에 저장한다. `index.jsonl`과 UUID 이름 PNG를 사용하며 최대 12개다. 각 기록은 `ingredients`(enum 값)와 `ingredientNames`(당시 블록 표시 이름, 예: Rice/Egg/Coconut milk)를 포함해 PNG의 UUID와 연결된다. 이미지와 블록 텍스트는 같은 폴더에 저장된다. 13번째 저장은 가장 오래된 항목을 빼고 새 항목을 마지막에 추가한다. 기존 인덱스를 삭제하지 않고 임시 파일 작성·원자 교체하며, 파일 교체 IOException은 40ms 간격 최대 3회 재시도한다. 인덱스 반영 후 이 저장소가 소유한 오래된 UUID PNG만 제거한다. 다른 ComfyUI 원본 출력 폴더의 보관 정책은 별개다. 기록은 Game 씬 종료·앱 재실행 후에도 유지된다. Windows 빌드에서는 실행 파일 옆 `<제품명>_Data/StreamingAssets/Thai03FoodHistory/`를 사용하므로 해당 폴더에 쓰기 권한이 필요하다. 기존 persistentDataPath 기록은 현재 PC에서 새 폴더로 복사하고 원본을 복구용으로 보존했다. 런타임 사진·기록과 해당 폴더 meta는 Git에서 제외한다.

기존 로컬 `UnityFood` 사진 20장의 PNG 메타데이터에서 사용자 인식 재료를 확인했고, 최신 12장만 최초 기록으로 가져왔다. 순서와 재료를 추측해 가져오지 않았다. 현재 PC의 일회성 이관이며, 다른 PC의 과거 출력 자동 검색은 수행하지 않는다. 이후 Unity 생성 성공분은 자동 기록한다. 메타데이터가 없는 이미지에는 임의의 재료를 붙이지 않는다.

갤러리는 활성화 시 디스크를 읽고 12개 이하 Sprite·Texture만 소유한다. 상세 사진은 해당 썸네일의 Sprite를 공유한다. 잘못된 기록·누락 PNG·손상 이미지·2048 초과 이미지·16MiB 초과 파일은 진단 후 건너뛰며 재료와 이미지가 다른 항목으로 엇갈리지 않는다. 비활성화 때 로드 취소·세대 가드·Sprite/Texture 해제로 늦은 응답을 차단한다. 저장 실패는 오류 로그를 남기고 현재 완성 사진 표시를 유지한다. 디스크 권한·용량 문제는 운영자가 해결해야 한다.

`2_Finish` 내부 동작을 연결한 작업이며 Game→Finish 자동 씬 이동이나 배포용 Build Settings 변경은 포함하지 않는다.

## 30초 조건과 식별 조건

**RTX 3060 12GB의 30초 조건은 아직 실측되지 않았다.** 현 PC는 RTX 5070 Ti Laptop 12GB다. 최초 관측 640 생성은 7.265초, 수정된 최종 프롬프트의 1~6개 640 생성은 2.268~2.850초였다. 512 예시는 동일 프롬프트 인코딩 캐시를 재사용해 1.126초였다. 이 수치를 RTX 3060 수치로 환산하지 않는다.

설치 PC에서는 모델을 미리 불러오고 큐를 비운 상태에서 서로 다른 재료/seed의 첫 실행과 반복 실행을 측정해야 한다. Windows·다른 GPU 프로그램·전력 설정·RAM과 모델 교체/오프로딩도 지연에 영향을 준다. 이 워크플로는 512·4단계·한 장으로 비용을 줄였지만 대상 장비에서 시간 조건을 통과했다고 판정하지 않았다.

완성 음식 안에 식재료가 보이도록 마늘은 껍질 벗긴 통마늘, 고기는 서로 다른 조리 단면, 코코넛 밀크는 흰 소스로 표현한다. 최종 6종 예시에서 밥·달걀·새우·마늘·버섯·흰 소스의 형태를 확인했다. 흰 소스가 코코넛 밀크인지, 고기가 정확히 어떤 종인지는 사진만으로 증명할 수 없다. 생성 모델은 재료 누락, 다른 재료처럼 표현, 선택하지 않은 장식 추가를 일으킬 수 있다. 입력 조합 9,948개 프롬프트를 검사했어도 모든 조합/seed의 시각 통과를 의미하지 않는다.

**따라서 '모든 생성 결과에서 정확한 성분 확인'은 미충족 조건이다.** 실제 운영 전 대표 조합 검수와 승인 이미지 활용 등 별도 정책이 필요하다. VLM 검수나 재생성을 자동 추가하지 않았으므로 30초 예산을 숨겨서 늘리지 않는다. Unity 키보드·생성 버튼 연결은 구현했고, 실제 RFID/아두이노 장비 연결은 아직 없다.
