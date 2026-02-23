# TopdownShooter 원격 배포/실행/모니터링 계획

## 1. 목적

- 현재 `server/TopdownShooter.Server`(.NET 10 TCP 서버)를 원격 호스트에 안정적으로 배포한다.
- Git 태그 기반 자동 배포 파이프라인을 구축한다.
- 서버 상태를 Grafana 대시보드로 모니터링하고, 로그는 journald로 관리한다.

## 2. 확정된 운영 원칙

- 구현 범위는 배포/실행/운영 자동화이며 게임 규칙/프로토콜 변경은 제외한다.
- 배포 단위는 `Native dotnet publish` 결과물로 고정한다.
- 배포 트리거는 `Git Tag (server-v*)`로 고정한다.
- 배포 시 30~60초 재시작 다운타임을 허용한다.
- 호스트 OS는 Ubuntu 계열을 기준으로 하고, Native Ubuntu/WSL2(systemd 활성화) 모두 지원한다.
- 게임 포트(기본 7777)는 허용 IP 목록만 접근 가능하게 제한한다.
- 모니터링은 자체 호스팅 Grafana + Prometheus로 구성한다.
- 알림 채널(Discord/Slack/Email)은 이번 범위에서 구성하지 않는다.
- 로그/메트릭 보존 기본값은 14일로 한다.

## 3. 현재 코드 기준 리스크

- `Program.cs`가 비대화형 입력(`ReadLine() == null`)에서 종료될 수 있어 서비스 실행 안정성이 떨어질 수 있다.
- 원격 배포 스크립트, CI/CD 워크플로우, systemd 유닛, 모니터링 설정 파일이 아직 없다.
- 운영 런북(배포/롤백/장애 대응 문서)이 없다.

## 4. 구현 산출물(문서 기준 설계)

1. 서버 실행 안정화
- `--headless` 실행 옵션을 추가한다.
- 비대화형 환경에서 콘솔 명령 루프를 비활성화한다.
- `SIGINT/SIGTERM/ProcessExit` graceful shutdown 경로를 확정한다.

2. 배포 아티팩트 생성
- `server/scripts/publish-linux.sh` 추가.
- 기본 publish 타깃: `linux-x64`, `Release`, self-contained single-file.

3. 원격 호스트 사전검증
- `server/scripts/preflight-host.sh` 추가.
- 점검 항목: systemd 사용 가능 여부, 포트 충돌, 디스크 여유, sudo 권한, WSL2 + systemd 여부.

4. 서비스 실행 단위
- `server/ops/systemd/topdown-shooter.service` 추가.
- 서비스명: `topdown-shooter.service`.
- 재시작 정책: `Restart=always`, `RestartSec=5`.
- 실행 경로: `/opt/topdown/current/TopdownShooter.Server --headless`.

5. 릴리스 디렉토리 표준
- `/opt/topdown/releases/<tag>` 릴리스 보관.
- `/opt/topdown/current` 심볼릭 링크로 활성 버전 관리.
- `/opt/topdown/shared/config/appsettings.json`을 릴리스에 링크해 설정 분리.

6. 원격 배포 스크립트
- `server/scripts/deploy-remote.sh` 추가.
- 절차: 업로드 -> 압축 해제 -> current 전환 -> service restart -> smoke test.
- 실패 시 이전 릴리스로 자동 롤백.

7. CI/CD
- `.github/workflows/server-release.yml` 추가.
- 트리거: `server-v*` 태그 push.
- 단계: test -> publish -> deploy.
- GitHub Environment 보호 규칙 적용.

8. 시크릿/환경변수 표준
- `DEPLOY_HOST`, `DEPLOY_PORT`, `DEPLOY_USER`, `DEPLOY_SSH_KEY`, `DEPLOY_KNOWN_HOSTS`, `DEPLOY_PATH`, `GRAFANA_ADMIN_PASSWORD`.

9. 네트워크/보안
- SSH(22)는 관리자 IP만 허용.
- 게임 포트(7777)는 허용 CIDR만 허용.
- Grafana(3000)는 외부 미노출, 로컬 바인딩 + SSH 터널 접근.

10. 모니터링 스택
- Prometheus, node_exporter, blackbox_exporter, Grafana를 systemd로 실행.
- 수집 대상: 호스트 리소스 + TCP probe(`127.0.0.1:7777`).
- 대시보드: CPU, 메모리, 디스크, 네트워크, TCP up/latency, 서비스 상태.

11. 로그 운영
- journald 보존 14일.
- 기본 용량 제한(`SystemMaxUse`) 적용.
- 운영 조회 명령: `journalctl -u topdown-shooter`.

12. 운영 문서
- `server/docs/remote-ops-runbook.md` 작성.
- 내용: 초기 설정, 릴리스 절차, 롤백, 장애 대응 체크리스트, WSL2 주의사항.

## 5. 테스트/검증 시나리오

1. `dotnet test server/TopdownShooter.sln` 통과.
2. headless 모드에서 서버가 즉시 종료되지 않고 유지.
3. 태그 배포 워크플로우 성공(test/publish/deploy).
4. 배포 직후 `topdown-shooter.service` active 상태 확인.
5. TCP 포트 7777 정상 응답 확인.
6. 실패 배포 시 자동 롤백 후 이전 버전 정상 기동 확인.
7. 서버 재부팅 후 자동 재기동 확인.
8. 허용 IP/비허용 IP 접근 정책 검증.
9. Grafana 대시보드 지표 갱신 확인.
10. journald 14일 보존 정책 적용 확인.

## 6. 기본값/가정

- 단일 서버 운영 기준.
- 무중단 배포는 범위 밖.
- 알림 채널은 현재 미구성.
- 게임 로직 및 프로토콜은 이번 계획에서 변경하지 않음.

