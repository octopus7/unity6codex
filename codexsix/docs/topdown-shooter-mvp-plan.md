# Unity6 + .NET10 TCP 탑다운 슈터 MVP 계획 (무한 세션 / 코인 루프 / 상점)

## 요약
- 목표: Unity 6 클라이언트와 .NET 10 TCP 데디케이티드 서버 기반 2~8인 탑다운 슈터를 구현한다.
- 세션: 매치 종료 없음, 킬카운트 목표 없음, 서버가 살아있는 동안 무한 진행.
- 코어 루프: 전투로 적 처치 -> 피해자 코인 드랍 -> 코인 루팅 -> 상점 구매 -> 사망 시 코인 리셋.
- 카메라: Perspective 고정 60도, 플레이 중 회전 없음.
- 권한 모델: 하이브리드(이동 예측+보정, 전투 판정 서버 확정).

## 확정된 게임 규칙
- 플레이어 HP: 100.
- 총알 데미지: 25.
- 리스폰: 사망 후 5초.
- 코인 보유: 생존 중 누적, 사망 시 보유 코인 0.
- 코인 드랍 트리거: 플레이어에게 처치당한 경우만.
- 드랍량: `max(1, 피해자 보유 코인)`.
- 드랍 분할: 최대 5스택 랜덤 분할.
- 코인 획득: 근접 자동획득, 누구나 즉시 획득.
- 코인 유지시간: 시간 소멸 없음(영구 유지).
- 월드 코인 상한: 총액 5000, 초과 시 가장 오래된 코인 스택부터 제거.
- HUD 핵심 지표: 현재 소지 코인 + 서버 상위 코인 랭크.
- 조작: WASD 이동, 마우스 조준, 좌클릭 발사.

## 상점 시스템 규칙
- 상점은 비전투 구역.
- 상점 구역 내 규칙: 발사 금지, 피격 금지.
- 포탈 구조: 맵 입구 포탈 2개, 상점 내 출구 포탈 1개.
- 구매 UX: 아이템 위에 올라간 상태에서 발사키 입력 시 구매 요청.
- 체류 제한: 없음.
- 상점 재진입: 맵의 상점 입구 포탈을 통해서만 가능(리스폰 후에도 동일).
- MVP 상품 2개:
  - `Heal50`: 5코인, 현재 HP에 +50, 최대 HP 100 초과 불가.
  - `MoveSpeed+20%`: 8코인, 현재 생명 동안 유지, 최대 2중첩(+40%).
- 2중첩 상태 재구매: 구매 거부.
- 사망 시 상점 버프: 모두 제거.

## 네트워크/프로토콜 계획 (TCP 고정)
- 전송: 길이 프리픽스 바이너리 프레임.
- 서버 틱: 30Hz.
- 스냅샷 전송: 20Hz.
- 입력 업로드: 30Hz.
- 프레임 헤더:
  - `uint32 lengthLE`, `uint16 messageType`, `uint16 protocolVersion=1`, `uint32 sequence`.
- C2S 메시지:
  - `Hello(nickname)`.
  - `InputFrame(move, aim, fireHeld, inputSeq, clientTick)`.
  - `ShopPurchaseRequest(shopItemId, requestSeq)`.
  - `Ping(clientUnixMs)`.
- S2C 메시지:
  - `Welcome(playerId, rates, maxPlayers)`.
  - `Snapshot(serverTick, players, projectiles, coinStacks, portals, shopState, lastProcessedInputSeq)`.
  - `EventBatch(ShotFired, Damage, Death, Respawn, CoinDropped, CoinPicked, ShopPurchased, PurchaseRejected)`.
  - `Pong`.
  - `Error`.
- 제거되는 이벤트:
  - `MatchEnd` 미사용.
  - 킬 목표 관련 패킷/필드 미사용.

## 중요 API/인터페이스/타입
- 클라이언트 전송 인터페이스:
```csharp
public interface IGameTransport
{
    Task ConnectAsync(string host, int port, string nickname, CancellationToken ct);
    void Disconnect();
    void SendInput(in ClientInputFrame input);
    void SendShopPurchase(in ShopPurchaseRequest request);
    event Action<ServerWelcome> WelcomeReceived;
    event Action<ServerSnapshot> SnapshotReceived;
    event Action<ServerEventBatch> EventReceived;
    event Action<ConnectionState> ConnectionStateChanged;
}
```
- 서버 상태 타입:
```csharp
public sealed record PlayerState(
    int PlayerId, Vector2 Pos, Vector2 AimDir, int Hp, int CarriedCoins,
    int SpeedBuffStacks, bool IsAlive, bool InShopZone);

public sealed record CoinStackState(
    int CoinStackId, Vector2 Pos, int Amount, long CreatedTick);

public sealed record ShopItemDef(
    byte ItemId, string Name, int Cost);
```

## 구현 단계
1. `docs/network-protocol.md`와 `docs/game-rules.md`를 새 규칙 기준으로 먼저 고정한다.
2. `server/TopdownShooter.Server` 생성 후 TCP 세션/프레임 파서/메시지 라우팅 구현.
3. 서버 시뮬레이션 루프에 이동/발사/피격/사망/리스폰/코인/상점 처리 추가.
4. 코인 스택 시스템 구현:
   - 드랍 생성.
   - 자동 획득 판정.
   - 월드 총액 5000 초과 시 오래된 스택 정리.
5. 상점/포탈 시스템 구현:
   - 입구 2개, 출구 1개, 안전구역 판정.
   - 구매 요청 검증, 코인 차감, 효과 적용/거부 이벤트.
6. Unity 씬 구성:
   - 전투 맵, 장애물, 상점 존, 포탈, 코인 프리팹, 플레이어/총알 프리팹.
7. Unity 클라 동기화:
   - 입력 30Hz, 로컬 이동 예측/서버 보정, 원격 보간, 코인/상점 이벤트 반영.
8. HUD/UI:
   - 접속 패널, HP, 소지 코인, 상위 코인 랭크, 핑, 구매 피드백.
9. 테스트/부하 검증:
   - 서버 단위/통합 테스트 + Unity 플레이모드 테스트 + 장시간 soak 테스트.

## 테스트 케이스 및 시나리오
- 코인 규칙:
  - 보유 0인 피해자를 처치하면 정확히 1코인 드랍.
  - 보유 N인 피해자를 처치하면 정확히 N코인 가치 드랍.
  - 드랍은 최대 5스택으로 분할되고 합계는 드랍량과 일치.
- 획득/정리:
  - 플레이어가 픽업 반경 진입 시 즉시 코인 획득.
  - 월드 총 코인 > 5000 시 오래된 스택부터 제거되어 <=5000 유지.
- 상점:
  - 상점 구역 내 발사/피격 모두 무효.
  - 코인 부족 시 구매 거부 이벤트 수신.
  - 이속 버프 2중첩 상태에서 재구매 거부.
  - 사망 후 버프 제거 확인.
- 네트워크:
  - 부분 수신/패킷 경계 분리 상황에서 파싱 정상.
  - 2~8명 접속 시 스냅샷/이벤트 일관성 유지.
- 장시간:
  - 30분 이상 무한 세션에서 서버 메모리/틱시간이 안정 범위 유지.

## 수용 기준
- 2~8명이 동일 서버에 접속해 전투/사망/리스폰이 끊김 없이 동작.
- 킬카운트/매치종료 없이 세션이 지속된다.
- 처치 시 코인 드랍과 루팅이 규칙대로 동작한다.
- 상점 입장/구매/퇴장이 규칙대로 동작하고 비전투 구역 보호가 적용된다.
- 월드 코인 상한 정책으로 무한 세션에서도 성능 저하가 통제된다.

## 명시적 기본값/가정
- 플랫폼: Windows PC 우선.
- 닉네임 기반 게스트 접속, 계정/DB 영속화 없음.
- 코인은 MVP에서 상점 구매와 위험-보상 루프 용도만 제공.
- 픽업 반경 기본값: 1.2 유닛.
- 상위 코인 랭크 표시는 Top 3 플레이어 기준.
- 서버 런타임은 사용자 지정대로 .NET 10 고정.
