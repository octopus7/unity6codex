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
| NPC roster and dialogue | Done | 주민 12명 구성, 주민별 고유 역할/고유 대사, `npcId` 기반 데이터 구조 연결 완료 |
| Camera and village scale pass | Done | 카메라 거리 확장, 마을 가로/세로 규모 확대 완료 |
| Player and villager collision pass | Done | 플레이어가 주민을 관통하지 않도록 평면 충돌 보정 추가 |
| Speech bubble polish | Done | 화면 UI 말풍선에 라운딩과 꼬리 추가 완료 |
| HUD cleanup | Done | 좌측 상단 제목 패널 제거 완료 |
| Primitive prototype replacement | Done | 플레이어/NPC를 실제 `32^3` 복셀 메시 캐릭터로 교체 완료 |
| Randomized village generation | Pending | 실행마다 달라지는 마을 배치 시스템 미구현 |
| Villager autonomous movement | Pending | 주민 순찰/경로 탐색/자율 이동 시스템 미구현 |
| Voxelized buildings and props | Pending | 건물/문/자연물의 실제 복셀 메시화 미구현 |

## Live Timeline

| Timestamp | Task | Status | Summary |
| --- | --- | --- | --- |
| 2026-04-15 12:47:20 KST | Progress log initialized | Done | 누적형 진행 로그 파일 생성 및 현재 상태 스냅샷 기록 |
| 2026-04-15 12:47:20 KST | `32^3` voxel character migration | In Progress | 프리미티브 기반 플레이어/NPC를 재사용 가능한 복셀 메시 캐릭터로 교체하는 작업 시작 |
| 2026-04-15 12:59:55 KST | `32^3` voxel character migration | Done | 플레이어와 주민 생성 경로를 프리미티브에서 복셀 메시 팩토리로 전환하고 `32^3` 메시 캐시/테스트를 추가 |
| 2026-04-15 12:59:55 KST | Villager roster expansion | Done | 주민 수를 6명에서 12명으로 확장하고 `NpcCatalog/Dialogue/UI` JSON과 스폰 배치를 전부 갱신 |
| 2026-04-15 13:03:55 KST | Verification pass | Done | `McpTest.VoxelVillage.EditModeTests` 15개 전체 통과, 리소스 데이터와 복셀 캐릭터 팩토리 검증 완료 |
| 2026-04-15 13:07:02 KST | Controls toggle and help text fix | Done | 조작 설명 패널을 우측 상단 버튼 토글로 변경하고, 잘못된 `RectTransform` 적용과 다국어 폰트 문제를 함께 수정 |
| 2026-04-15 13:14:00 KST | Interaction prompt slimming | Done | 상호작용 프롬프트 패널을 고정 폭에서 텍스트 기준 동적 폭으로 바꾸고 여백을 줄여 더 날렵하게 조정 |
| 2026-04-15 13:43:26 KST | Multi-agent world integration | Done | Worker-authored layout/grid generation replaced fixed placement and single-door assumptions with seed-based layout and multi-door runtime state |
| 2026-04-15 13:43:26 KST | Autonomous villager pass | Done | Twelve villagers now patrol with pathfinding, pause while speaking, and keep separation against the player and one another |
| 2026-04-15 13:43:26 KST | Voxel environment pass | Done | Houses, doors, fountain, foliage, and decorative rocks now build from voxel meshes while runtime door state updates the occupancy grid |
| 2026-04-15 13:43:26 KST | Verification pass | Done | Added procedural layout and dialogue-set tests; `McpTest.VoxelVillage.EditModeTests` passes after the world integration changes |
| 2026-04-15 15:31:20 KST | Open fence pass | Done | Added open U-shaped fence paths per building, connected voxel fence meshes, fence occupancy tests, and cache guards needed for stable EditMode factory tests |
| 2026-04-15 16:58:08 KST | Monster invader visual spec | Done | Added `MONSTER_INVADER_VISUAL_SPEC.md` to lock the squid-type village pursuer silhouette, scale ratios, module split, and runtime prefab hierarchy before implementation |
| 2026-04-15 17:15:45 KST | Monster invader prototype pass | Done | Added the first `MukhaengTracker` runtime slice with modular voxel meshes, procedural pose controller, pond-side prototype spawn, and focused EditMode factory coverage |
| 2026-04-15 17:17:14 KST | Monster eye bloom pass | Done | Split `EyeCluster` onto a dedicated red emissive material set and raised runtime emission so the tracker eyes hit HDR bloom much harder without retinting the rest of the body |
| 2026-04-15 17:24:40 KST | Monster foot planting pass | Done | Replaced leg wobble-only posing with world-space planted feet, step state transitions, ground contact sampling, and simple IK-driven leg solving for the `MukhaengTracker` prototype |
