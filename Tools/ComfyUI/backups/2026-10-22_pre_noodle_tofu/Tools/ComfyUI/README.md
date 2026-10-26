# 식재료 블록 → 음식 사진

15종 중 인식된 **1~6개**를 입력해 한 접시의 **태국 음식 느낌** 사진을 생성한다. 재료별 큰 형태를 요리 표면에 보이게 지정해 가려짐과 누락을 줄인다. 별도 재료 그릇이나 재료 사진 합성은 사용하지 않는다.

코코넛 밀크가 있으면 태국식 코코넛 커리(쌀도 있으면 같은 접시의 커리밥), 쌀을 포함한 다른 조합은 카오팟 스타일 볶음밥, 오이를 포함하면 얌 스타일 샐러드, 나머지는 태국식 볶음 방향을 선택한다. v2는 재료 하나일 때 단일 재료 전용 문구를 사용한다. 쌀만 선택하면 자스민 쌀밥, 코코넛 밀크만 선택하면 고형 토핑 없는 크림을 표현한다. 전통 레시피의 정확한 재현을 보장하는 분류가 아니라, 선택 재료를 살리는 생성 프롬프트 방향이다. 청화 도자기·티크 테이블·접시 아래 바나나 잎으로 분위기를 보강하며, 선택하지 않은 바질·라임·고추를 스타일 때문에 강제 추가하지 않는다.

## 워크플로 파일

- `Thai_03_Food_Thai_Focus_v4.json` / `.api.json`: **현재 테스트 버전**. 배경 없이 음식 중심, 태국식 달걀/커리 질감과 속이 찬 얇은 편마늘 단면. 640×640/4단계.
- `Thai_03_Food_Thai_Prepared_v3.json` / `.api.json`: 보존한 이전 테스트 버전. 마늘 슬라이스·자른 달걀·손질한 고기/해산물·채소와 태국식 조리 표현.
- `Thai_03_Food_Detail_v2.json` / `.api.json`: 새 기본 UI/API 워크플로. **640×640**, 4단계, 배치 1. 조리 표면·단면·수분·빛 표현 강화.
- `Thai_03_Food_Ingredients.json` / `.api.json`: 보존한 이전 **512×512** 버전.
- `Thai_03_Food_Ingredients_640.json` / `.api.json`: 보존한 이전 **640×640** 비교 버전(v2와 프롬프트가 다름).
- `backups/2026-10-21_pre_detail_v2/`: 변경 전 Python·4개 워크플로·Unity JSON/meta·이전 ComfyUI 사용자 워크플로의 체크섬 백업.
- `compare_detail_v2.py`: 동일 재료/시드 비교와 `--final` 최종 조합 검사.
- `reports/2026-10-21_detail_v2/`: 생성 시간·최종 검증 기록과 예시 이미지.
- `food_workflow.py`: 인식 재료에 맞는 프롬프트와 UI/API 워크플로를 만드는 Python 표준 라이브러리 도구.
- `verification.json`: 현 PC의 생성 시간, 입력 검증 및 시각 검토 기록.

기본 JSON에는 예시로 Rice, Egg, Shrimp, Garlic, Mushroom, Coconut milk 6개가 들어 있다. **6개가 필수라는 뜻이 아니다.** 1~5개 입력이면 그 재료만 프롬프트에 넣고, 나머지 슬롯을 임의 재료로 채우지 않는다. Unity의 ChiliPepper/CoconutMilk 이름도 입력할 수 있다.

현재 PC ComfyUI 사용자 목록에서 새 **`Thai_03_Food_Detail_v2`**를 연다. 이전 `Thai_03_Food_Thai_Style`도 그대로 남아 있다. 이전 `Thai_03_Food_Ingredients` 사본과 미저장 탭은 보존했다. 이전 기본/640 JSON은 보존하고 새 파일명으로 v2를 추가했다. JSON을 직접 끌어다 열어도 된다. `CLIPTextEncode`의 텍스트를 바꿀 수 있지만, 실제 블록 목록에 맞추려면 아래 도구로 다시 생성한다.

## 설정과 필요한 모델

이미 설치된 모델을 사용하며 새 모델/패키지/커스텀 노드를 설치하지 않았다.

| 항목 | 값 |
|---|---|
| 모델 | `diffusion_models/flux-2-klein-4b-fp8.safetensors` (distilled) |
| 텍스트 인코더 | `text_encoders/qwen_3_4b.safetensors`, type `flux2` |
| VAE | `vae/flux2-vae.safetensors` |
| 샘플링 | Euler, Flux2Scheduler, 4 steps, CFG 1 |
| 크기/배치 | v2 기본 640×640 / 1 (legacy 512×512) |
| 후처리 | refiner·업스케일·추가 생성 없음 |
| 저장 | v2 도구: `Thai03/FoodDetailV2_*.png`, Unity: `Thai03/UnityFood_*.png` |

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

# 이전 문구/512 설정으로 비교. 별도 출력 경로를 사용
python Tools/ComfyUI/food_workflow.py Rice Egg --profile legacy --output Temp/food_two_legacy --run
```

기본 프로필은 `detail-v2`(640)이며 `--profile legacy`(512)로 이전 문구를 쓴다. `--width/--height`로 크기를 지정할 수 있다. `--run`이 없으면 JSON 파일만 만든다. `--seed`를 생략하면 매 호출 새로운 seed를 사용한다. 기본 출력 이름으로 재실행하면 프로젝트의 해당 JSON 두 개를 갱신하며, ComfyUI 사용자 폴더의 사본은 자동으로 덮어쓰지 않는다. 새 JSON을 열어 사용한다.

0개·7개 이상·중복·알 수 없는 재료는 거절한다. 실행은 localhost HTTP만 허용한다. 기존 작업이 큐에 있으면 제출하지 않는다. 모델/노드 존재를 확인하고 **한 번만** 제출한다. 기본 29초가 지나면 결과를 실패로 처리하며 재시도하지 않는다. 공유 큐 전체를 중지하지 않으므로 이미 제출된 GPU 작업은 완료될 수 있다. 제한 시간 검사는 속도 보장이나 이미지 내용 검증을 의미하지 않는다.

## Unity 연결

Game 씬 루트의 **`FoodWorkflowSettings` → `FoodWorkflowSwitch` → `Is Test`(`isTest`)**로 전환한다. **false는 기존 Detail v2**, true는 `Assets/Resources/ComfyFoodWorkflowTest.json`의 **Thai Focus v4**다. Inspector에서 편집 중 또는 Play 중 변경할 수 있다. 생성 버튼을 누르는 시점에 선택을 고정하므로 생성 도중 토글해도 해당 요청에는 영향을 주지 않고 다음 생성부터 적용한다. 기본 `ComfyFoodWorkflow.json`과 GUID는 그대로 유지했다. 테스트 에셋이 없거나 손상되면 제출 전에 오류 안내를 표시하고 Game에 머문다. 스위치 참조가 없는 기존 씬은 원래 기본 워크플로로 동작한다.

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

예시 재료는 내보낼 때 JSON 기본 프롬프트에만 들어가며 Unity는 생성 버튼을 누를 때 실제 인식 재료로 교체한다. v2와 백업 legacy 각각 9,948개 조합(총 19,896건)의 Python/Unity 프롬프트 일치를 확인했다. 단일 재료 옵션이 없는 기존 공유 JSON도 호환된다. Unity 요청 seed는 매 요청 바뀌고, 저장 경로는 ComfyUI output의 `Thai03/UnityFood_*.png`다.

## v2 비교와 복구

기존 512, 새 문구 512/640/768을 같은 시드로 비교했다. 640에서 새우 마디·닭고기 단면·당근 단면·버섯 결·달걀 가장자리의 미세 질감을 확인했고, 768은 비용 증가에 비해 이 표본에서 뚜렷한 추가 이점이 적어 640을 선택했다. 해상도가 달라지면 같은 시드라도 노이즈 배치·구도가 달라지므로 완전히 같은 사진의 단순 확대 비교는 아니다. 텍스트 인코딩 캐시도 시간에 영향을 준다.

최종 1/2/3/4/5/6 재료 및 쌀·코코넛 밀크 단독 8회는 현 RTX 5070 Ti Laptop에서 **이미지 다운로드까지 2.237~3.005초**였다. 초기 단일 재료 검사에서 추가 채소/고형물이 보인 실패를 기록하고 단일 재료 전용 문구로 보강해 다시 검사했다. 모든 시드/조합의 추가 음식·누락 방지를 보장하지 않으며, 2재료 코코넛 소스 표본의 흰 고형물·마늘의 통구근 같은 형태는 여전히 주의할 표현이다. 사진으로 코코넛 밀크 성분을 증명할 수 없는 기존 제약도 유지된다.

재실행: `python Tools/ComfyUI/compare_detail_v2.py`(A/B), `python Tools/ComfyUI/compare_detail_v2.py --final`(최종 조합). 큐가 비었을 때 한 번씩 로컬 제출하며 29초를 넘으면 중단한다. 비교 출력은 별도 ComfyUI `Thai03/DetailV2Tests`와 프로젝트 `Temp/ComfyFoodDetailV2`에 저장해 관람객의 12장 기록을 교체하지 않는다. Unity 연결 검사도 격리된 임시 기록을 사용했다. 노드 1~13·4단계 구조를 유지해 기존 진행 게이지와 호환된다.

복구하려면 `backups/2026-10-21_pre_detail_v2/manifest.json`의 source/backup 대응대로 복사한다. 특히 `Assets/Resources/ComfyFoodWorkflow.json`을 이전 파일로 복원하면 Unity도 512 이전 문구로 돌아간다. 기존 `.meta` GUID는 유지한다. 새 C#의 단일 재료 옵션은 이전 JSON에 없으면 사용하지 않으므로 추가 코드 복구 없이 이전 데이터가 동작한다. ComfyUI의 이전 사용자 워크플로 원본은 덮어쓰지 않았다.

## 손질·태국식 v3 테스트

2026-10-22 작업 전 상태는 `backups/2026-10-22_pre_thai_prepared_v3/manifest.json`에 13개 파일 SHA256과 복구 경로를 기록했다. v2 UI/API·Unity 기본 JSON/meta·현재 생성 코드/Game 씬·Python·기존 ComfyUI 사용자 워크플로 사본을 보관한다. v2 기본 데이터와 기존 ComfyUI 사용자 파일은 바꾸지 않았다. ComfyUI 목록에는 **Thai_03_Food_Thai_Prepared_v3**를 별도로 저장했다.

재료 1~6개는 그대로 지원한다. 카오팟 볶음밥·gaeng kati 커리·얌 샐러드·태국식 볶음을 선택하고, 통마늘 대신 슬라이스, 달걀 대신 자른 오믈렛, 손질한 새우·한입 크기 고기·채 썬 당근/양파를 요청한다. 별도 모델 학습/추가 weights 다운로드는 없으며 Klein4B FP8·Qwen3 4B·Flux2 VAE·640·4단계·CFG1·노드1~13 그대로다. 진행률 게이지와 호환된다. CLI 기본 프로필도 여전히 detail-v2다.

```powershell
# 테스트용 데이터 내보내기: 기본 리소스를 덮어쓰지 않는다.
python Tools/ComfyUI/food_workflow.py Rice Egg Shrimp Garlic Mushroom "Coconut milk" --profile thai-prepared-v3 --seed 6102201 --export-unity Temp/ComfyFoodWorkflowPreparedV3.json

# v2/v3 동일 크기·seed 비교 및 v3 1~6재료 검사
python Tools/ComfyUI/compare_prepared_v3.py
python Tools/ComfyUI/compare_prepared_v3.py --final
```

현 ComfyUI0.42.0/RTX5070Ti Laptop12GB 최종8회는 PNG 다운로드까지 **2.775~4.318초**, Unity의 실제 false/true 생성은 각각 **6.692/5.609초**(UI·다운로드·격리 저장 포함)였다. 3060에서의 30초 조건은 여전히 미검증이다. 기본·테스트 각각9948조합 Python/C# 프롬프트 SHA256 일치, 기존 legacy/v2 각9948개와 대표 그래프가 백업과 동일함을 검사했다. 생성 도중 토글 변경·연타·0재료·누락 테스트 에셋·반복 복귀도 Unity에서 확인했다.

`reports/2026-10-22_thai_prepared_v3`에 비교/최종 JSON·예시 PNG·summary, `Temp/ComfyThaiPreparedV3`에 요청 그래프·전체 사진·Unity 실행 기록을 둔다. 초기 결과의 intact omelet/큰 고기 조각을 보고 짧은 달걀 스트립·작은 조각 표현으로 조정 후 최종8회 검사를 했다. 마늘/달걀/당근 손질 표현은 개선됐으나 일부 사진에 선택하지 않은 고추·마늘 같은 장식이 여전히 추가되고, 쌀 단독에 황금색 스트립도 보인다. 모든 재료 식별·정확한 성분·태국식 재현을 보장하는 버전이 아니므로 **실험용으로 유지**하며 기본값으로 승격하지 않았다. 관람객 사진12장과 설문 데이터는 시험 응답/생성에 사용하지 않았다.

원상태로 비교하려면 `isTest=false`만 선택하면 된다. 전체 코드/씬 복구가 필요할 때만 2026-10-22 백업 manifest의 source/backup 대응대로 복사한다. 2026-10-21 백업은 v2 이전 버전이므로 혼동하지 않는다.

## 음식 집중·편마늘 v4 수정

현재 `isTest=true`는 **Thai Focus v4**다. 사용자가 시장 배경을 원하지 않아 최종 버전에서 시장/식당/인물/소품 배경을 제거하고, 단색 배경에 음식이 화면의 대부분을 채우게 요청한다. 청화 접시·반듯한 지단 대신 단순한 접시, 부풀고 바삭한 Thai khai jiao 달걀, 카오팟 볶음밥·윤기 있는 태국식 볶음·코코넛 커리를 강조한다. 고추와 코코넛 밀크가 모두 선택될 때만 선택적 `curry_with_chili` 문구로 붉은 gaeng phet 커리를 요청하며, 기존 JSON은 이 키가 없으면 이전 문구를 그대로 사용한다.

마늘은 구멍 난 고리/두꺼운 반쪽/균일한 원판을 줄이기 위해 5개 문구를 단독·새우 혼합으로 비교했다. 최종 문구는 **0.5mm 얇은 세로 편, 속이 찬 아이보리 단면, 희미한 연녹색 중심 싹 선, 비대칭으로 뾰족한 가장자리**다. 일부 조합에서는 여전히 두껍거나 균일하게 표현되며, 사진만으로 실제 식재료 단면/성분의 정확성을 보장하지 않는다. 지시와 달리 숟가락·팬이나 미선택 음식 유사 조각이 간혹 남는 제약도 기록했다.

```powershell
python Tools/ComfyUI/food_workflow.py Rice Egg Shrimp Garlic Mushroom "Coconut milk" --profile thai-focus-v4 --seed 6102201 --export-unity Assets/Resources/ComfyFoodWorkflowTest.json
python Tools/ComfyUI/compare_thai_focus_v4.py --final --revision garlic-germ
```

`backups/2026-10-22_pre_thai_street_v4/manifest.json`은 수정 전 v3·현재 기본 JSON·기존 C# 등 11파일 체크섬 백업이다(폴더명은 사용자가 시장 배경 제외를 요청하기 전 초기 후보명). 이전 v3 UI/API와 Comfy 사용자 워크플로는 그대로 보존하고, 새 **Thai_03_Food_Thai_Focus_v4**를 Comfy 목록에 추가했다. 테스트 리소스의 파일명과 GUID를 유지했으므로 씬 연결은 그대로다. 기존 v2 데이터·모델·640/4단계·진행률 구조 유지.

최종 1~6재료 6회는 현5070Ti Laptop에서 다운로드까지 **2.521~2.990초**, Unity v4 실제 생성은 **5.000초**였다. 기본/테스트 각9948조합 Python/C# 일치, 이전 legacy/v2/v3 각9948문구 불변 확인. 3060 실측은 미수행이다. 최종 보고/예시 PNG는 `reports/2026-10-22_thai_focus_v4`, 전체 로컬 후보/실행 자료는 `Temp/ComfyThaiStreetV4`(거절된 배경 후보 포함)·`Temp/ComfyThaiFocusV4`에 있다. 초기 후보를 다시 생성하지 않고 현재 음식 전용 버전만 테스트하려면 위 `--final` 명령을 사용한다.

## Finish 사진 기록

`2_Finish`의 공통 Canvas에 `FinishGalleryController`가 연결되어 있다. `PictureSelectGroup`의 기존 Image 12칸을 사용하고 사진이 적으면 남는 칸을 숨긴다. 최신 사진부터 오래된 순서(좌→우, 위→아래)로 배치하며 최신은 항상 첫 칸이다. 디스크 기록은 기존 순서를 유지하고 표시할 때 역순으로 읽으므로 기존 사진에도 바로 적용된다. 좌우 화살표는 현재 선택을 강조하고 양 끝에서 순환한다. 숫자키 1/숫자패드 1로 `OtherPictureViewPanel`을 열어 같은 사진과 당시 선택한 1~6개 재료만 표시한다. 상세 화면의 1/Esc/Back으로 기존 선택 위치에 돌아온다. 사진이 없으면 선택은 무시한다.

향후 아두이노 버튼은 Unity 메인 스레드에서 `SelectPrevious()`, `SelectNext()`, `ConfirmSelection()`, `CloseDetail()`을 호출한다. 컨트롤러는 두 패널이 꺼져도 유지되는 공통 Canvas에 둔다. 실제 아두이노 수신은 아직 없다.

Game의 생성·이미지 표시까지 성공하면 PNG와 독립적인 재료 스냅샷을 `Application.streamingAssetsPath/Thai03FoodHistory/`(에디터: `Assets/StreamingAssets/Thai03FoodHistory/`)에 저장한다. `index.jsonl`과 UUID 이름 PNG를 사용하며 최대 12개다. 각 기록은 `ingredients`(enum 값)와 `ingredientNames`(당시 블록 표시 이름, 예: Rice/Egg/Coconut milk)를 포함해 PNG의 UUID와 연결된다. 이미지와 블록 텍스트는 같은 폴더에 저장된다. 13번째 저장은 가장 오래된 항목을 빼고 새 항목을 마지막에 추가한다. 기존 인덱스를 삭제하지 않고 임시 파일 작성·원자 교체하며, 파일 교체 IOException은 40ms 간격 최대 3회 재시도한다. 인덱스 반영 후 이 저장소가 소유한 오래된 UUID PNG만 제거한다. 다른 ComfyUI 원본 출력 폴더의 보관 정책은 별개다. 기록은 Game 씬 종료·앱 재실행 후에도 유지된다. Windows 빌드에서는 실행 파일 옆 `<제품명>_Data/StreamingAssets/Thai03FoodHistory/`를 사용하므로 해당 폴더에 쓰기 권한이 필요하다. 기존 persistentDataPath 기록은 현재 PC에서 새 폴더로 복사하고 원본을 복구용으로 보존했다. 런타임 사진·기록과 해당 폴더 meta는 Git에서 제외한다.

기존 로컬 `UnityFood` 사진 20장의 PNG 메타데이터에서 사용자 인식 재료를 확인했고, 최신 12장만 최초 기록으로 가져왔다. 순서와 재료를 추측해 가져오지 않았다. 현재 PC의 일회성 이관이며, 다른 PC의 과거 출력 자동 검색은 수행하지 않는다. 이후 Unity 생성 성공분은 자동 기록한다. 메타데이터가 없는 이미지에는 임의의 재료를 붙이지 않는다.

갤러리는 활성화 시 디스크를 읽고 12개 이하 Sprite·Texture만 소유한다. 상세 사진은 해당 썸네일의 Sprite를 공유한다. 잘못된 기록·누락 PNG·손상 이미지·2048 초과 이미지·16MiB 초과 파일은 진단 후 건너뛰며 재료와 이미지가 다른 항목으로 엇갈리지 않는다. 비활성화 때 로드 취소·세대 가드·Sprite/Texture 해제로 늦은 응답을 차단한다. 저장 실패는 오류 로그를 남기고 현재 완성 사진 표시를 유지한다. 디스크 권한·용량 문제는 운영자가 해결해야 한다.

`2_Finish` 내부 동작을 연결한 작업이며 Game→Finish 자동 씬 이동이나 배포용 Build Settings 변경은 포함하지 않는다.

## 30초 조건과 식별 조건

**RTX 3060 12GB의 30초 조건은 아직 실측되지 않았다.** 현 PC는 RTX 5070 Ti Laptop 12GB다. 최초 관측 640 생성은 7.265초, 수정된 최종 프롬프트의 1~6개 640 생성은 2.268~2.850초였다. 512 예시는 동일 프롬프트 인코딩 캐시를 재사용해 1.126초였다. 이 수치를 RTX 3060 수치로 환산하지 않는다.

설치 PC에서는 모델을 미리 불러오고 큐를 비운 상태에서 서로 다른 재료/seed의 첫 실행과 반복 실행을 측정해야 한다. Windows·다른 GPU 프로그램·전력 설정·RAM과 모델 교체/오프로딩도 지연에 영향을 준다. v2는 640·4단계·한 장으로 비용을 제한했지만 대상 장비에서 시간 조건을 통과했다고 판정하지 않았다.

완성 음식 안에 식재료가 보이도록 마늘은 껍질 벗긴 통마늘, 고기는 서로 다른 조리 단면, 코코넛 밀크는 흰 소스로 표현한다. 최종 6종 예시에서 밥·달걀·새우·마늘·버섯·흰 소스의 형태를 확인했다. 흰 소스가 코코넛 밀크인지, 고기가 정확히 어떤 종인지는 사진만으로 증명할 수 없다. 생성 모델은 재료 누락, 다른 재료처럼 표현, 선택하지 않은 장식 추가를 일으킬 수 있다. 입력 조합 9,948개 프롬프트를 검사했어도 모든 조합/seed의 시각 통과를 의미하지 않는다.

**따라서 '모든 생성 결과에서 정확한 성분 확인'은 미충족 조건이다.** 실제 운영 전 대표 조합 검수와 승인 이미지 활용 등 별도 정책이 필요하다. VLM 검수나 재생성을 자동 추가하지 않았으므로 30초 예산을 숨겨서 늘리지 않는다. Unity 키보드·생성 버튼 연결은 구현했고, 실제 RFID/아두이노 장비 연결은 아직 없다.
