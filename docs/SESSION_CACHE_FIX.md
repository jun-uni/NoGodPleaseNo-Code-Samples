# FishySteamworks 세션 상태 초기화 누락 수정

## 결론

이 문제는 FishNet용 Steam transport인 FishySteamworks 4.1.1에서 서버를 종료할 때 내부 연결 맵을 정리하지 않아, 다음 세션의 최대 인원 판정에 이전 연결 상태가 포함될 수 있었던 문제였습니다.

- 확인 환경: FishNet 4.6.12, FishySteamworks 4.1.1
- 수정 위치: FishySteamworks `ServerSocket`, 양방향 Dictionary

## 증상

호스트가 게임을 종료해 메인 메뉴로 돌아간 뒤 새 Steam 세션을 만들면 실제 접속자 수와 최대 인원 판정이 일치하지 않는 경우가 있었습니다. 새 클라이언트가 정상 인원인데도 거절되어, 이전 세션의 접속 상태가 남은 것처럼 동작했습니다.

## 조사 범위

1. FishNet `ServerManager.Clients`의 서버 종료 처리
2. FishySteamworks `ServerSocket`의 최대 인원 판정과 종료 처리
3. Steam connection과 FishNet connection ID의 양방향 맵
4. connection ID 재사용 큐와 로컬 호스트 패킷 큐

## 원인

FishNet 코어는 서버가 완전히 정지하면 `ServerManager.Clients`와 내부 클라이언트 목록을 비우고 있었습니다. 반면 FishySteamworks는 신규 연결을 받을 때 다음과 같이 transport 내부 연결 맵의 개수로 최대 인원을 판단했습니다.

```csharp
if (_steamConnections.Count >= GetMaximumClients())
{
    // 신규 연결 거절
}
```

FishySteamworks의 서버 종료 코드는 Steam 리슨 소켓을 닫고 원격 연결 콜백과 대기 중인 변경 목록을 정리했지만, `_steamConnections`와 `_steamIds`는 비우지 않았습니다. Steam 리슨 소켓을 닫으면 기존 연결도 종료되지만, 콜백이 함께 해제되므로 transport 객체에 저장된 양방향 맵은 종료 이벤트를 통해 정리되지 않을 수 있었습니다.

그 결과 두 레이어의 상태가 다음처럼 달라질 수 있었습니다.

```text
FishNet ServerManager.Clients = 0
FishySteamworks _steamConnections = 이전 세션 연결 수 유지
```

최대 인원 오류의 직접 원인은 `_steamConnections`가 이전 세션의 연결 수를 유지한 것이었습니다. `_steamIds`는 같은 connection ID를 역방향으로 조회하는 대응 맵이므로 두 맵의 일관성을 위해 함께 정리했습니다.

다음 상태는 직접적인 최대 인원 판정 원인은 아니지만, 같은 transport 객체를 다음 세션에서 재사용할 때 이전 세션 상태가 섞이지 않도록 방어적으로 초기화했습니다.

- 재사용 가능한 connection ID 큐
- 로컬 호스트가 받을 패킷 큐
- 호스트 클라이언트 시작 여부

FishySteamworks 4.1.1도 `_cachedConnectionIds`는 서버 시작 시 이미 비우고 있습니다. 로컬 패치에서 이 큐를 종료 경계에도 포함한 것은 직접 원인 수정이 아니라 세션 정리 시점을 명확히 하기 위한 방어적 처리입니다.

## 수정

양방향 Dictionary에 두 내부 Dictionary를 함께 비우는 `Clear()`를 추가했습니다. 한쪽 Dictionary만 비우면 역방향 조회 상태가 달라질 수 있으므로 두 맵을 하나의 연산으로 초기화했습니다.

FishySteamworks의 서버 시작과 종료 경계에서는 세션에 종속된 transport 상태를 초기화했습니다. 종료 시 정리를 기본으로 하고, 시작 시 초기화도 방어적으로 수행했습니다.

```csharp
// 최대 인원 판정과 양방향 대응 관계 초기화
_steamConnections.Clear();
_steamIds.Clear();

// 나머지 세션 단위 상태의 방어적 초기화
_cachedConnectionIds.Clear();
_clientHostIncoming.Clear();
_clientHostStarted = false;
```

## 적용 범위

이 수정은 팀 프로젝트에 포함된 FishySteamworks 소스에만 적용한 로컬 패치입니다. FishySteamworks 공식 저장소에 동일 문제를 이슈로 등록하거나 수정 코드를 PR로 제출하지는 않았습니다.

## 수정 평가와 한계

관찰된 문제에 대한 수정 방향은 타당했습니다.

- 최대 인원 판정에 사용되는 연결 맵을 세션 종료 시 비움
- 양방향 Dictionary의 두 방향을 함께 초기화해 대응 관계 유지
- connection ID 큐와 로컬 호스트 상태는 방어적으로 초기화해 세션 간 상태 혼입 방지
- 서버 시작 시에도 연결 맵을 초기화해 비정상 종료 경로 보완

다만 외부 패키지에 직접 적용한 로컬 패치이므로 패키지를 갱신하면 덮어써질 수 있습니다. 실제 서비스에서 유지하려면 별도 fork나 패치 파일로 버전을 고정해야 합니다.

## 최신 공식판 확인

2026년 8월 31일 기준 FishNet의 최신 안정판은 4.7.2R이지만, FishySteamworks는 FishNet과 별도로 배포되는 transport이며 최신 공식판도 4.1.1입니다. 공식 FishySteamworks의 현재 `ServerSocket.StopConnection()`에는 `_steamConnections`와 `_steamIds` 초기화가 없고, 양방향 Dictionary에도 `Clear()`가 추가되지 않았습니다. 따라서 FishNet만 최신 버전으로 올리는 것으로는 이 문제가 해결되지 않습니다.

- [FishNet 공식 릴리스](https://github.com/FirstGearGames/FishNet/releases)
- [FishySteamworks 공식 릴리스](https://github.com/FirstGearGames/FishySteamworks/releases)
- [FishySteamworks ServerSocket 공식 소스](https://github.com/FirstGearGames/FishySteamworks/blob/main/FishNet/Plugins/FishySteamworks/Core/ServerSocket.cs)
- [Steamworks Networking Sockets 문서](https://partner.steamgames.com/doc/api/ISteamNetworkingSockets)
