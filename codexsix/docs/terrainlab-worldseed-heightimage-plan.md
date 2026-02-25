# WorldSeed HeightImage 기반 절차적 지형 + 연속 평면도/미니맵 계획

## 요약
- 결정론적 생성 기준: `월드 시드 + 월드 절대 그리드 좌표`
- 높이값은 청크별 PNG로 저장하고, 메시는 항상 저장된 Height 이미지에서 복원
- 현재 월드는 물리적으로 중앙 청크 `(0,0)` 1개만 생성
- 에디터 전용 툴에서 청크 이미지를 NxN으로 이어 붙인 평면도 확인
- 미니맵은 플레이어 중심 고정 윈도우 방식이며 경계 근처에서 인접 청크 자동 포함
- 변경 범위는 `Assets/Labs/TerrainMeshMovementLab` 내부로 제한 (`Assets/Tests` 미사용)

## 공개 인터페이스/타입
1. `TerrainLabWorldConfig` (ScriptableObject)
2. `TerrainLabHeightCodec`
3. `TerrainLabHeightChunkStore`
4. `TerrainLabHeightGenerator`
5. `TerrainLabChunkMeshGenerator`
6. `TerrainLabMapViewerWindow` (EditorWindow)
7. `TerrainLabMinimapController`
8. 메뉴
   - `Tools > Terrain Lab > Create Terrain Movement Scene (Safe)`
   - `Tools > Terrain Lab > Height Map Viewer`

## 핵심 설계
- 청크 규칙: `chunkCells=64`, `chunkVertices=65`, `cellSize=1.0`
- 원점 정렬: 중앙 청크 중심이 월드 원점
- 경계 연속성: 높이 샘플은 반드시 월드 절대 그리드 기준으로 계산
- 높이 저장 포맷: PNG(`RGBA32`) 2채널 패킹으로 실질 16bit 높이 정밀도 사용
  - `ushort(0..65535) <-> Color32(R,G,0,255)`
- 파일 규칙: `height_s{seed}_cx{x}_cz{z}_v{verts}_p1.png`
- 이미지 크기: `(chunkVertices + 2) x (chunkVertices + 2)` (패딩 1픽셀)
- 메시 노멀 계산: 패딩 포함 높이값으로 중앙차분 계산

## Height 생성/저장 파이프라인
1. 입력: `seed`, `chunkCoord`, `world config`
2. 샘플 영역: 내부 렌더 정점 + 외곽 1픽셀 패딩
3. 생성 직후 PNG 저장
4. 임포트 설정 강제
   - `sRGB=false`
   - `Compression=None`
   - `Mipmap=false`
   - `FilterMode=Point`
   - `Wrap=Clamp`
   - `Read/Write=true`
5. 메시/미니맵은 동일 Height PNG를 공용 소스로 사용

## 중앙 청크 메시 생성
- 씬 시작 시 `(0,0)` 청크 PNG 로드
- 없으면 생성 후 저장, 저장본으로 메시 구성
- 버텍스 높이/삼각형/노멀/MeshCollider 동기화
- `R` 재생성 시 동일 파이프라인 재실행 후 플레이어 지면 스냅

## 에디터 연속 평면도 툴
- 입력: `seed`, `centerChunk`, `range(NxN)`
- 누락 청크 자동 생성+저장 기본 ON
- 스티치 규칙: 각 청크 내부 렌더 영역만 타일링 (패딩은 경계/노멀 계산 전용)
- 시각화: 높이 컬러램프(저지대 -> 고지대)
- 기능: 현재 뷰를 미니맵용 컬러맵 PNG로 내보내기

## 미니맵
- 소스: 저장 Height PNG 기반
- 표시: 플레이어 중심 고정 윈도우 샘플링
- 경계 근처에서 이웃 청크 자동 샘플링으로 자연스러운 연결
- 지형 재생성 시 즉시 갱신

## 저장 경로
- Height 생성 자산: `Assets/Labs/TerrainMeshMovementLab/Generated/Heightmaps`

## 검증 시나리오
1. 같은 seed, 같은 청크 PNG 2회 생성 시 바이트 단위 동일
2. 인접 청크 `(0,0)` vs `(1,0)` 경계 높이 오차 허용치 이내
3. 중앙 청크 메시가 저장 PNG만으로 재구성되고 시각적 seam 없음
4. Viewer NxN 스티치에서 타일 경계 끊김 없음
5. 플레이어 경계 근처에서 미니맵에 인접 청크 포함 표시
6. 변경 파일이 Labs 격리 경로에만 생성

## 가정/기본값
- 월드 물리 청크는 현재 1개 `(0,0)`
- 인접 청크는 데이터(Height 이미지) 필요 시 생성
- 자동 테스트 코드는 추가하지 않음 (`Assets/Tests` 미사용)
- 기본 시드 `12345`, Viewer 기본 범위 `3x3`
