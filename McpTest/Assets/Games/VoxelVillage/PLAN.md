# VoxelVillage Plan

이 문서는 `VoxelVillage`의 최초 계획 문서다.
구현 현황, 완료 여부, 진행 로그는 이 파일에 누적하지 않고 별도 문서에서 관리한다.

## Goal

기존 샘플과 다른 게임을 건드리지 않고, `Assets/Games/VoxelVillage/` 아래에 독립적인 마을형 복셀 게임을 만든다.

핵심 목표:

- `32 x 32 x 32` 기준의 복셀 메시 캐릭터
- 플레이어 `WASD` 이동
- 주민 목표 수 12명
- 주민 자율 이동
- 주민 근처에서 `F` 상호작용 프롬프트 노출
- 스크린 UI 기반 말풍선 대화
- 외부 JSON 기반 대화 데이터
- 언어 전환 버튼으로 `ko / en / ja` 순환
- UI와 대사가 현재 언어를 따라 즉시 갱신
- 실행할 때마다 다른 마을 배치
- 건물 벽과 `F`로 여닫는 문

## Scope

### 포함

- 새 씬 `VoxelVillage.unity`
- 새 런타임 스크립트, 테스트, JSON 데이터
- 플레이어 1명과 주민 다수
- 복셀 스타일 캐릭터와 마을 오브젝트
- 화면 고정 HUD와 스크린 공간 상호작용 UI
- 언어 전환, 대화, 문 상호작용

### 제외

- 기존 게임/샘플 씬 직접 수정
- 월드 스페이스 UI
- 실사풍 모델/텍스처 파이프라인
- 네트워크, 저장/로드, 퀘스트 시스템

## Folder Structure

```text
Assets/Games/VoxelVillage/
  Scenes/
    VoxelVillage.unity
  Scripts/
    Runtime/
      Localization/
      Dialogue/
      UI/
      Voxel/
      World/
    Editor/
  Data/
    Resources/
      VoxelVillage/
        Localization/
        Dialogue/
        Npcs/
  Tests/
    EditMode/
```

## Scene Composition

- 씬은 하나만 사용한다: `VoxelVillage.unity`
- 카메라, 광원, EventSystem, HUD는 런타임 생성 가능 구조로 둔다
- 마을, 캐릭터, 상호작용 오브젝트는 코드 중심으로 생성한다
- 기존 씬 의존성 없이 단독 실행 가능해야 한다

## Core Systems

### Player

- 입력: `W`, `A`, `S`, `D`
- 카메라는 플레이어를 따라가되 너무 가깝지 않게, 마을 전경이 충분히 보이는 내려다보기 시점을 유지한다
- 플레이어는 주민과 문, 벽을 관통하지 않아야 한다

### NPC

- 주민 수는 12명을 목표로 한다
- 주민마다 `npcId`를 가진다
- 공용 바디 구조를 공유하되 색상, 소품, 역할명, 체형 비율, 대사 톤으로 구분한다
- 점유 그리드 기반으로 순찰 또는 배회한다
- 플레이어와 대화 중인 주민은 대화 중 이동을 멈출 수 있어야 한다
- 주민별 고유 이름과 고유 대화 세트를 가질 수 있어야 한다

### Interaction

- 상호작용 대상이 가까우면 스크린 UI 프롬프트를 띄운다
- `F` 입력으로 NPC 대화 또는 문 상호작용을 처리한다
- 프롬프트는 문구 길이에 맞춰 크기가 조절되는 구조를 우선한다

### Dialogue

- 데이터는 외부 JSON에서 읽는다
- `npcId` 기준으로 대화 세트를 연결한다
- 단답형, 왕복형 모두 가능해야 한다
- 말풍선은 주민 머리 위 위치를 화면 좌표로 투영해 표시한다

### Localization

- HUD에 언어 순환 버튼을 둔다
- 전역 언어 상태는 `ko -> en -> ja` 순환 구조
- UI 텍스트, 프롬프트, NPC 이름, 역할명, 대사를 모두 현재 언어에 맞춰 갱신한다

## Data Design

### UI Text JSON

```json
{
  "entries": [
    {
      "key": "interaction.talk",
      "translations": {
        "ko": "F 대화하기",
        "en": "F Talk",
        "ja": "F 話す"
      }
    }
  ]
}
```

### NPC Catalog JSON

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
      "roleName": {
        "ko": "빵집 주인",
        "en": "Bread Seller",
        "ja": "パン屋"
      },
      "paletteId": "npc_red",
      "dialogueSetIds": [
        "mina_market_chat"
      ]
    }
  ]
}
```

### Dialogue JSON

```json
{
  "dialogueSets": [
    {
      "id": "mina_market_chat",
      "lines": [
        {
          "speaker": "npc",
          "translations": {
            "ko": "오늘은 광장이 꽤 분주하네.",
            "en": "The plaza is busy today.",
            "ja": "今日は広場がにぎやかだね。"
          }
        },
        {
          "speaker": "player",
          "translations": {
            "ko": "점심 시간이 가까워져서 그런가 봐.",
            "en": "Maybe lunch time is getting close.",
            "ja": "お昼が近いからかな。"
          }
        }
      ]
    }
  ]
}
```

## Voxel Character Direction

- 캐릭터는 `32 x 32 x 32` 로컬 복셀 공간을 기준으로 만든다
- 플레이어와 NPC는 같은 기본 바디 구조를 공유한다
- 주민 차이는 색상, 액세서리, 체형 비율, 역할명, 대사로 만든다
- 메시는 가능한 한 캐시해서 반복 빌드를 줄인다

## Procedural Village Direction

- 실행 시 시드 기반으로 마을 레이아웃을 생성한다
- 마을은 답답하지 않게 넓은 footprint를 가지며, 광장과 외곽 주거 구역이 충분히 분리되어 보이도록 한다
- 광장, 중심 도로, 보조 도로, 건물 슬롯, 자연물 슬롯을 먼저 계산한다
- 건물, 문, 나무, 수풀, 꽃은 그 결과를 바탕으로 생성한다
- 문 위치와 건물 충돌은 점유 그리드와 함께 관리한다

우선 규칙:

- 광장은 중앙에 둔다
- 도로는 광장과 건물을 연결한다
- 건물은 도로에 면하도록 둔다
- 자연물은 최소 간격을 두고 랜덤 배치한다

## Buildings And Doors

- 건물 외벽은 복셀 블록 조합으로 만든다
- 문은 개별 pivot을 가진다
- `F` 입력으로 열고 닫을 수 있어야 한다
- 문 열림/닫힘 상태는 충돌 또는 점유 그리드와 동기화해야 한다

## UI Direction

- HUD는 `Screen Space Overlay`
- 언어 버튼은 화면 고정 위치
- 언어 버튼 근처에 조작법 버튼을 두고, 조작법 패널은 기본 닫힘 + 토글 구조로 둔다
- NPC 말풍선은 라운딩과 꼬리가 있는 스크린 UI
- 말풍선 배경은 필요 시 이미지 에셋 대신 코드 생성형 UI 그래픽으로 처리 가능해야 한다
- 상호작용 프롬프트는 작은 패널로 텍스트를 타이트하게 감싼다
- 불필요한 상시 타이틀 패널은 두지 않고 필요한 안내만 남긴다

## Implementation Phases

1. 독립 폴더, 씬, 런타임 부트스트랩 구성
2. 플레이어 이동, 카메라, 기본 HUD 구성
3. 언어 버튼과 전역 로컬라이제이션 상태 구현
4. JSON 로더와 UI/대사 다국어 구조 구현
5. `32^3` 복셀 캐릭터 메시 생성기 구현
6. 주민 데이터 구조와 주민별 고유 대사 구조 구현
7. 시드 기반 마을 레이아웃 생성기 구현
8. 건물, 문, 자연물 생성기 구현
9. 주민 자율 이동과 경로 탐색 구현
10. 상호작용, 말풍선, 문 열기/닫기 통합
11. EditMode 테스트와 수동 검수 보강

## Validation Plan

### EditMode Tests

- 로컬라이제이션 JSON 파싱과 언어 키 검증
- `npcId -> dialogueSetId` 매핑 검증
- 복셀 캐릭터 메시 생성 검증
- 말풍선 UI 메시 생성 검증
- 점유 그리드와 문 상태 변경 검증
- 절차 생성 레이아웃 기본 제약 검증

### Manual Checks

- 실행할 때마다 마을 배치가 달라지는지
- 주민이 벽과 문을 뚫고 지나가지 않는지
- 플레이어가 주민을 관통하지 않는지
- `F` 프롬프트가 올바른 대상 근처에서만 뜨는지
- 언어 버튼을 누르면 HUD/프롬프트/대사/NPC 이름이 즉시 바뀌는지
- 문을 열고 닫을 때 시각 상태와 이동 가능 상태가 일치하는지

## Risks

- 복셀 메시를 매번 재생성하면 비용이 크므로 캐시 전략이 필요하다
- 랜덤 배치는 보기 좋은 결과보다 먼저 충돌 없는 결과를 보장해야 한다
- 문이 많아질수록 점유 그리드와 경로 탐색 규칙의 정합성이 중요해진다
- 번역 데이터 누락 시 fallback 규칙과 테스트가 필요하다
