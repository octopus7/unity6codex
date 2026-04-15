# Voxel Village Plan

## Goal

기존 샘플과 볼링 게임을 건드리지 않고, 새 경로와 새 씬에 다음 기능을 갖춘 복셀 마을 게임을 만든다.

- `32 x 32 x 32` 해상도 기준의 복셀 메시 캐릭터
- 플레이어 `WASD` 이동
- 색상만 다른 주민 NPC 다수
- 주민의 자율 이동
- 주민 근처에서 `F` 상호작용 프롬프트 노출
- `F` 입력 시 주민 머리 위 말풍선 대화
- 화면 고정 UI에서 `한국어 -> English -> 日本語` 순환 버튼 제공
- 현재 선택 언어에 따라 UI, 프롬프트, 대사 전체 동기화
- 외부 `JSON` 기반 대화 데이터 로드
- 외부 `JSON`에 언어별 번역 데이터 포함
- `npcId` 기준으로 여러 대화 세트를 재사용 가능한 구조
- 실행할 때마다 달라지는 마을 배치
- 건물 벽, 문, 자연물, 수풀, 꽃, 소품 생성
- 문 근처에서 `F`로 열기 / 닫기

## Isolation / Paths

- 루트: `Assets/Games/VoxelVillage/`
- 씬: `Assets/Games/VoxelVillage/Scenes/VoxelVillage.unity`
- 런타임 코드: `Assets/Games/VoxelVillage/Scripts/Runtime/`
- 데이터: `Assets/Games/VoxelVillage/Data/`
- 테스트: `Assets/Games/VoxelVillage/Tests/EditMode/`

기존 `Assets/Scenes/SampleScene.unity`, `Assets/Games/Bowling/`는 수정하지 않는다.  
초기 구현도 새 씬에서만 동작하도록 부트스트랩을 분리한다.

## Core Decisions

### 1. 월드는 런타임 생성으로 간다

- 마을의 도로, 건물 배치, 수풀/꽃/자연물은 런타임 시드 기반으로 생성한다.
- 기본값은 실행할 때마다 새 시드를 사용한다.
- 디버그 재현을 위해 고정 시드 옵션은 남긴다.

### 2. 화면 UI는 전부 Screen Space Overlay로 간다

- `F` 프롬프트는 스크린 UI다.
- 말풍선도 월드 스페이스 캔버스를 쓰지 않고, NPC 머리 위치를 `Camera.WorldToScreenPoint`로 변환해 오버레이 캔버스에 붙인다.

### 3. NPC 이동은 그리드 기반 보행 로직으로 간다

- 마을이 복셀 스타일이고 매 실행마다 구조가 바뀌므로, `NavMesh`보다는 점유 그리드 + A* 경로 탐색이 예측 가능하다.
- 건물, 벽, 닫힌 문, 장식물은 점유 셀을 막는다.
- 도로, 광장, 집 앞 셀은 보행 가능 셀로 유지한다.

### 4. 캐릭터는 "공용 바디 + 팔레트 변형"으로 간다

- 플레이어와 주민은 같은 복셀 바디 구조를 공유한다.
- NPC 차이는 우선 색상 팔레트, 이름, 대화, 이동 파라미터로 만든다.
- 필요 시 후속으로 모자/앞치마 같은 소품 슬롯만 추가한다.

### 5. 언어 시스템은 전역 상태 + 키 기반 조회로 간다

- 지원 언어는 `ko`, `en`, `ja` 세 가지로 고정한다.
- 화면에 고정된 언어 순환 버튼 하나가 현재 언어를 바꾼다.
- UI 문구, 상호작용 프롬프트, NPC 이름 표시, 대화 텍스트는 전부 같은 언어 상태를 참조한다.
- 하드코딩 문자열 대신 `localizationKey` 또는 언어별 텍스트 맵을 사용한다.

## Proposed Folder Layout

- `Assets/Games/VoxelVillage/Scenes/`
- `Assets/Games/VoxelVillage/Scripts/Runtime/Bootstrap/`
- `Assets/Games/VoxelVillage/Scripts/Runtime/World/`
- `Assets/Games/VoxelVillage/Scripts/Runtime/Characters/`
- `Assets/Games/VoxelVillage/Scripts/Runtime/Interaction/`
- `Assets/Games/VoxelVillage/Scripts/Runtime/Dialogue/`
- `Assets/Games/VoxelVillage/Scripts/Runtime/Localization/`
- `Assets/Games/VoxelVillage/Scripts/Runtime/UI/`
- `Assets/Games/VoxelVillage/Data/Dialogue/`
- `Assets/Games/VoxelVillage/Data/Localization/`
- `Assets/Games/VoxelVillage/Data/Npcs/`
- `Assets/Games/VoxelVillage/Tests/EditMode/`

## Runtime Architecture

### Bootstrap

- `VoxelVillageSceneBootstrap`
  - 현재 활성 씬이 `VoxelVillage`일 때만 게임을 초기화한다.
  - 기존 런타임 데모 오브젝트가 있으면 비활성화 또는 제거한다.
- `VoxelVillageGameController`
  - 월드 생성, 데이터 로드, 플레이어/NPC 생성, UI 초기화를 총괄한다.

### World Generation

- `TownGenerationSettings`
  - 마을 크기, 시드, 건물 수, 자연물 밀도, 도로 폭 등 설정 보관
- `TownOccupancyGrid`
  - 셀 점유 여부, 보행 가능 여부, 문 셀, 진입 가능 셀 관리
- `TownLayoutGenerator`
  - 광장, 주 도로, 보조 골목, 필지, 플레이어 시작점, NPC 스폰 포인트 계산
- `TownRuntimeBuilder`
  - 계산된 레이아웃을 실제 메시/오브젝트로 생성
- `BuildingGenerator`
  - 벽, 바닥, 지붕, 문틀, 문 생성
- `NatureScatterSystem`
  - 나무, 수풀, 꽃, 바위, 울타리, 상자 등을 가중치 기반으로 배치

### Character System

- `VoxelMeshBuilder`
  - `32^3` 논리 복셀 데이터에서 외곽 면만 추출해 메시를 만든다.
- `VoxelHumanoidFactory`
  - 머리, 몸통, 팔, 다리 파트를 생성하고 캐릭터 루트에 조립한다.
- `VoxelPalette`
  - 피부, 머리, 옷, 보조색 팔레트 정의
- `VoxelCharacterAnimator`
  - 걷기 시 팔/다리 스윙, 상체 바운스, 대기 시 미세 흔들림 제공

### Gameplay

- `PlayerController`
  - `WASD` 이동, 방향 회전, 상호작용 입력(`F`)
- `NpcAgent`
  - 현재 위치, 목표 셀, 이동 속도, 대기 시간, NPC ID 보유
- `NpcWanderBrain`
  - 광장, 도로, 집 앞 포인트 중 하나를 골라 순찰
- `DoorController`
  - 문 열림/닫힘 상태, 회전 애니메이션, 충돌 상태 업데이트

### Interaction / Dialogue / UI

- `IInteractable`
  - `CanInteract`, `GetPromptText`, `Interact` 인터페이스
- `InteractionDetector`
  - 플레이어 주변에서 가장 적합한 상호작용 대상(NPC/문) 선택
- `InteractionPromptPresenter`
  - 화면 하단 또는 중앙 하단에 `F` 프롬프트 표시
- `SpeechBubblePresenter`
  - NPC 머리 위를 따라다니는 오버레이 말풍선 렌더링
- `LanguageState`
  - 현재 언어(`ko`, `en`, `ja`) 보관 및 변경 이벤트 발행
- `LocalizationDatabase`
  - 공용 UI 문자열과 이름표 문자열을 JSON에서 로드
- `LanguageCycleButton`
  - 화면 고정 UI 버튼으로 언어를 순환 전환
- `LocalizedTextBinder`
  - 특정 UI 텍스트가 현재 언어 변경을 따라 자동 갱신되도록 연결
- `DialogueDatabase`
  - JSON 파싱 결과를 메모리 캐시
- `DialogueRunner`
  - 대화 세트 재생, 한 줄형 / 왕복형 처리, 다음 줄 타이밍 제어

## Character Plan

### 32^3 캐릭터 표현

- 기준 해상도는 캐릭터 한 명당 `32 x 32 x 32` 로컬 복셀 공간이다.
- 내부 구현은 "전신 단일 메시"보다 "파츠별 복셀 메시"가 유리하다.
  - 걷기 애니메이션이 쉬움
  - NPC와 플레이어가 같은 메시를 재사용 가능
  - 색상만 바꾼 변형 제작이 쉬움

### 초기 캐릭터 범위

- 플레이어 1종
- 주민 베이스 1종
- 주민 팔레트 변형 4~6종
- 이름, 팔레트, 대화 세트만 달라지는 구조

## Town Generation Plan

### 레이아웃 규칙

- 중앙 광장 1개는 항상 보장
- 광장에서 이어지는 메인 도로 루프 1개 구성
- 도로에 붙은 필지에 소형 주택 / 상점형 건물 배치
- 각 건물은 반드시 도로를 향한 정면 문 1개 보유
- 문 앞 1~2셀은 항상 비워 두어 상호작용 가능하게 유지

### 배치 규칙

- 플레이어 시작 위치는 광장 근처 고정
- NPC 순찰 포인트는 도로, 광장, 집 앞 우선
- 꽃/수풀/바위는 건물 외벽과 도로를 침범하지 않음
- 자연물은 최소 간격을 두고 가중치 랜덤 배치
- 매 실행마다 시드는 달라지지만, "길이 완전히 막히는 상황"은 금지

### 건물 규칙

- 벽: 복셀 블록 조합으로 외벽 생성
- 지붕: 단순 경사 또는 평지붕 중 랜덤 선택
- 문: 힌지 축 기준 회전형 문
- 창문: 시각용 블록으로만 시작, 상호작용은 제외

## NPC Behavior Plan

- NPC는 생성 시 `npcId`, 팔레트, 현재 대화 세트 목록을 받는다.
- 유휴 상태에서는 순찰 포인트 사이를 이동한다.
- 플레이어와 대화 중일 때는 정지하고 플레이어를 바라본다.
- 대화가 끝나면 짧은 쿨다운 후 다시 순찰한다.

초기 버전에서는 NPC가 문을 직접 열고 집 안으로 드나드는 행동은 제외한다.  
우선은 "마을 외부 공간에서 자연스럽게 걷는 주민"을 먼저 완성한다.

## Interaction Plan

### 공통 규칙

- 플레이어 주변 반경 내에서 가장 가까운 상호작용 대상 하나만 활성화
- 후보가 여러 개면 거리 + 바라보는 방향 점수로 우선순위 결정
- 동일 키 `F`로 NPC 대화, 문 열기/닫기 모두 처리
- 프롬프트 문구도 현재 언어에 맞춰 즉시 바뀐다.

### NPC 상호작용

- 프롬프트 예시: `F 대화하기`
- 예시 키는 내부적으로 `interaction.talk` 같은 로컬라이제이션 키를 사용한다.
- 입력 시 NPC 이동 중지
- 화면 오버레이 말풍선을 NPC 머리 위 위치에 표시
- 한 줄 대화와 왕복 대화 모두 지원

### 문 상호작용

- 프롬프트 예시: `F 문 열기`, `F 문 닫기`
- 실제 표시는 현재 언어에 따라 번역된 문구를 사용한다.
- 문이 열리면 충돌 박스를 갱신하고 보이는 상태를 변경
- 닫힌 문은 점유 셀로 취급

## Localization Plan

### 언어 범위

- 한국어 `ko`
- 영어 `en`
- 일본어 `ja`

### 동작 방식

- 화면 고정 HUD에 현재 언어 버튼을 둔다.
- 버튼을 누를 때마다 `ko -> en -> ja -> ko` 순서로 순환한다.
- 버튼 텍스트도 현재 선택 언어 표시와 다음 동작 의도를 같이 보여준다.
- 언어가 바뀌면 프롬프트, HUD 라벨, 대화 말풍선, NPC 표시 이름이 즉시 갱신된다.

### 데이터 구조

- 공용 UI 문자열은 별도 로컬라이제이션 JSON으로 관리한다.
- 대화 데이터는 각 줄마다 언어별 번역 묶음을 가진다.
- NPC 표시 이름도 문자열 하나가 아니라 언어별 이름 맵을 가질 수 있게 설계한다.

### 파일 분리

- `Assets/Games/VoxelVillage/Data/Resources/VoxelVillage/Localization/UiTextDatabase.json`
- `Assets/Games/VoxelVillage/Data/Resources/VoxelVillage/Npcs/NpcCatalog.json`
- `Assets/Games/VoxelVillage/Data/Resources/VoxelVillage/Dialogue/DialogueDatabase.json`

## Dialogue Data Plan

### 파일 분리

- `Assets/Games/VoxelVillage/Data/Resources/VoxelVillage/Localization/UiTextDatabase.json`
- `Assets/Games/VoxelVillage/Data/Resources/VoxelVillage/Npcs/NpcCatalog.json`
- `Assets/Games/VoxelVillage/Data/Resources/VoxelVillage/Dialogue/DialogueDatabase.json`

### 구조 의도

- `npcId`는 어떤 대화 세트를 사용할 수 있는지만 가진다.
- 실제 대화 본문은 `dialogueSetId` 기준으로 별도 관리한다.
- 같은 대화 세트를 여러 NPC가 공유할 수 있다.
- NPC 고유 대사도 별도 세트로 추가할 수 있다.
- 모든 표시 텍스트는 단일 `text` 대신 언어별 번역 맵을 가진다.

### JSON 예시

```json
{
  "languageOrder": ["ko", "en", "ja"],
  "entries": [
    {
      "key": "interaction.talk",
      "translations": {
        "ko": "F 대화하기",
        "en": "F Talk",
        "ja": "F 話す"
      }
    },
    {
      "key": "interaction.openDoor",
      "translations": {
        "ko": "F 문 열기",
        "en": "F Open Door",
        "ja": "F ドアを開く"
      }
    }
  ]
}
```

```json
{
  "npcs": [
    {
      "npcId": "villager_mina",
      "displayName": {
        "ko": "미나",
        "en": "Mina",
        "ja": "ミナ"
      },
      "paletteId": "npc_red",
      "dialogueSetIds": ["greeting_common", "market_smalltalk", "mina_personal"]
    }
  ]
}
```

```json
{
  "dialogueSets": [
    {
      "id": "greeting_common",
      "cooldownSeconds": 6,
      "lines": [
        {
          "speaker": "npc",
          "translations": {
            "ko": "오늘은 장터가 꽤 붐비네.",
            "en": "The market is pretty busy today.",
            "ja": "今日は市場がかなりにぎやかだね。"
          }
        }
      ]
    },
    {
      "id": "market_smalltalk",
      "lines": [
        {
          "speaker": "npc",
          "translations": {
            "ko": "꽃밭 쪽 길은 좀 한산해.",
            "en": "The path near the flower beds is quieter.",
            "ja": "花畑のほうの道は少し静かだよ。"
          }
        },
        {
          "speaker": "player",
          "translations": {
            "ko": "그쪽부터 둘러봐야겠네요.",
            "en": "I should start looking around there.",
            "ja": "まずはあちらから見て回ろうかな。"
          }
        },
        {
          "speaker": "npc",
          "translations": {
            "ko": "해 질 무렵엔 등이 켜져서 더 예뻐.",
            "en": "It looks even nicer when the lamps turn on at dusk.",
            "ja": "夕方に明かりがつくと、もっときれいだよ。"
          }
        }
      ]
    }
  ]
}
```

## Scene Composition Plan

- 새 씬 하나만 만든다: `VoxelVillage.unity`
- 씬에는 최소한의 카메라, 광원, 부트스트랩 루트만 둔다.
- 마을, 캐릭터, UI는 런타임에 생성한다.

이 방식이면 실행할 때마다 다른 배치를 만들기 쉽고, 기존 씬과 충돌도 줄어든다.

## Implementation Phases

1. 독립 폴더/씬/asmdef/부트스트랩 구성
2. 플레이어 이동, 카메라, 기본 오버레이 UI 구성
3. 언어 순환 버튼과 전역 로컬라이제이션 상태 구현
4. `32^3` 복셀 캐릭터 메시 생성기와 팔레트 변형 구현
5. 도로/광장/필지 기반 마을 레이아웃 생성기 구현
6. 건물 벽, 지붕, 문, 자연물 생성기 구현
7. NPC 순찰 AI와 상호작용 탐지 구현
8. 외부 JSON 대화 로더와 다국어 말풍선 시스템 구현
9. 문 상호작용과 충돌/점유 셀 동기화
10. 씬 진입 검증, 빌드 세팅 등록, 테스트 보강

## Validation Plan

### Edit Mode Tests

- `TownLayoutGeneratorTests`
  - 건물 문 앞이 막히지 않는지
  - 플레이어 시작점에서 광장/도로 접근이 가능한지
- `DialogueDatabaseTests`
  - JSON 파싱과 누락 필드 처리
  - `npcId -> dialogueSetId` 매핑 검증
- `LocalizationDatabaseTests`
  - `ko`, `en`, `ja` 번역 누락 검증
  - 언어 순환 순서 검증
- `InteractionDetectorTests`
  - NPC와 문이 동시에 있을 때 우선순위가 올바른지

### Manual Checks

- 실행할 때마다 마을 배치가 달라지는지
- NPC가 벽을 뚫지 않고 이동하는지
- `F` 프롬프트가 NPC/문 근처에서만 뜨는지
- 말풍선이 월드 스페이스가 아니라 화면 UI로 붙는지
- 문을 열고 닫을 때 충돌과 시각 상태가 일치하는지
- 언어 버튼을 누를 때 HUD, 프롬프트, 대사, NPC 이름이 즉시 함께 바뀌는지
- 한국어, 영어, 일본어에서 번역 누락 시 안전한 fallback이 동작하는지

## Risks / Notes

- 무작위 배치는 보기 좋은 결과보다 "길이 끊기지 않는 결과"를 먼저 보장해야 한다.
- `32^3` 복셀 메시를 캐릭터마다 매번 다시 빌드하면 비용이 커질 수 있으므로, 메시 캐시를 둔다.
- 자연물까지 전부 개별 오브젝트로 두면 드로우콜이 늘 수 있으니, 정적 배치는 묶을 수 있게 설계한다.
- 문이 많은 구조에서 NPC가 실내까지 완전하게 이동하도록 확장하면 경로 규칙이 복잡해지므로, 1차 목표에서는 외부 순찰 위주로 제한한다.
- 번역 데이터가 늘어나면 누락 키 관리가 어려워지므로, JSON 검증 테스트와 fallback 규칙을 초기에 넣는다.

## Current Status

### Planned

- 요구사항 정리 완료
- 시스템 분해 완료
- 파일 경로와 데이터 구조 초안 확정

### Implemented Prototype

- `VoxelVillage` 씬 생성 완료
- 언어 순환 버튼과 전역 언어 상태 구현 완료
- 한국어 / 영어 / 일본어 UI 번역 JSON 로드 완료
- NPC 대화와 문 프롬프트의 다국어 반영 확인 완료
- 편집 모드 로컬라이제이션 테스트 추가 완료

### Not Started

- 복셀 `32^3` 캐릭터 메시 생성
- 마을 랜덤 생성
- 주민 자율 이동
- 실제 문/건물/자연물 시스템 확장
