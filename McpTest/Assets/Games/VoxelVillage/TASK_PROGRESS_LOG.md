# VoxelVillage Task Progress Log

Timezone: `Asia/Seoul (KST, UTC+09:00)`

This file accumulates task-by-task progress summaries with timestamps.
New entries should be appended to the bottom of the `Live Timeline` table.

## Backfilled Snapshot

Snapshot recorded at `2026-04-15 12:47:20 KST`.

| Task | Status | Summary |
| --- | --- | --- |
| Project structure and scene isolation | Done | `Assets/Games/VoxelVillage/` 경로와 전용 씬 기반으로 독립 구성 완료 |
| Localization vertical slice | Done | 언어 순환 버튼, `ko/en/ja` 전역 상태, UI/프롬프트/대사 동기화, 외부 JSON 번역 연결 완료 |
| NPC roster and dialogue | Done | 주민 6명 구성, 주민별 고유 역할/고유 대사, `npcId` 기반 데이터 구조 연결 완료 |
| Camera and village scale pass | Done | 카메라 거리 확장, 마을 가로/세로 규모 확대 완료 |
| Player and villager collision pass | Done | 플레이어가 주민을 관통하지 않도록 평면 충돌 보정 추가 |
| Speech bubble polish | Done | 화면 UI 말풍선에 라운딩과 꼬리 추가 완료 |
| HUD cleanup | Done | 좌측 상단 제목 패널 제거 완료 |
| Primitive prototype replacement | In Progress | 플레이어/NPC를 실제 `32^3` 복셀 메시 캐릭터로 교체하는 작업 진행 중 |
| Randomized village generation | Pending | 실행마다 달라지는 마을 배치 시스템 미구현 |
| Villager autonomous movement | Pending | 주민 순찰/경로 탐색/자율 이동 시스템 미구현 |
| Voxelized buildings and props | Pending | 건물/문/자연물의 실제 복셀 메시화 미구현 |

## Live Timeline

| Timestamp | Task | Status | Summary |
| --- | --- | --- | --- |
| 2026-04-15 12:47:20 KST | Progress log initialized | Done | 누적형 진행 로그 파일 생성 및 현재 상태 스냅샷 기록 |
| 2026-04-15 12:47:20 KST | `32^3` voxel character migration | In Progress | 프리미티브 기반 플레이어/NPC를 재사용 가능한 복셀 메시 캐릭터로 교체하는 작업 시작 |
