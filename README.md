# No God! Please No! — Portfolio Code Samples

Unity와 FishNet으로 개발한 최대 3인 온라인 협동 디펜스 RPG <strong>No God! Please No!</strong>에서 제가 구현하거나 주요하게 기여한 코드를 정리한 채용 포트폴리오용 저장소입니다.

- Steam 출시: [No God! Please No!](https://store.steampowered.com/app/4179710/No_God_Please_No/)
- 게임 플레이 영상: [YouTube](https://www.youtube.com/watch?v=b4sUalKoP0s)
- 개발 기간: 2025.09–2026.03
- 담당: Unity 클라이언트·네트워크 프로그래밍
- 주요 기술: C#, Unity, FishNet, FishySteamworks, Steamworks.NET, Vivox, Unity Gaming Services, AWS Lambda/API Gateway, UniTask, DOTween

## 빠르게 살펴보기

| 영역 | 주요 코드 | 확인할 내용 |
|---|---|---|
| 네트워크 이동 | [`코드 설명`](Source/01_NetworkMovement/README.md) · [`MovementController 발췌`](Source/01_NetworkMovement/MovementController.cs) | 입력 생성, 서버 권위 상태 복원, 이동 정책 분리 |
| 세션 수명주기 | [`코드 설명`](Source/02_SessionAndVoice/README.md) · [`SteamLobby 발췌`](Source/02_SessionAndVoice/SteamLobby.cs) · [`ConnectionLimiter.cs`](Source/02_SessionAndVoice/ConnectionLimiter.cs) | Steam 로비 생성, FishNet 호스트 시작, 게임 시작 시 인원 잠금과 순차 종료 |
| 음성 인증 | [`코드 설명`](Source/02_SessionAndVoice/README.md) · [`LambdaTokenProvider.cs`](Source/02_SessionAndVoice/LambdaTokenProvider.cs) · [`VivoxBootstrap.cs`](Source/02_SessionAndVoice/VivoxBootstrap.cs) | UGS 인증을 결합한 서버리스 토큰 발급 프로토타입과 Vivox 초기화·채널 참가 |
| 음성 제어 UI | [`PlayerVoiceChatList.cs`](Source/02_SessionAndVoice/PlayerVoiceChatList.cs) · [`PlayerVoiceChatControlPanel.cs`](Source/02_SessionAndVoice/PlayerVoiceChatControlPanel.cs) | 참가자 목록 동기화, 개인 음량·뮤트, 표시 이름과 접속 상태 갱신 |
| 캐릭터 전투 | [`코드 설명`](Source/03_Gameplay/README.md) · [`BarbarainAttack 발췌`](Source/03_Gameplay/Barbarian/BarbarainAttack.cs) · [`궁극기 투사체`](Source/03_Gameplay/Barbarian/Ult/BarbarianUltimateProjectile.cs) | 서버 요청 검증, 히트 윈도우, 대상 중복 방지, 패시브 피해와 서버 투사체 |
| 보스 전투 | [`코드 설명`](Source/03_Gameplay/README.md) · [`CyclopsAttack 발췌`](Source/03_Gameplay/Cyclops/CyclopsAttack.cs) | 서버 히트 윈도우, 대상별 1회 피해와 넉백 적용 |
| 도전과제 | [`코드 설명`](Source/04_Achievements/README.md) · [`AchievementNetRelay.cs`](Source/04_Achievements/AchievementNetRelay.cs) · [`SteamAchievements 발췌`](Source/04_Achievements/Steam/SteamAchievements.cs) | 플랫폼 추상화, 서버 스킬 지표 판정, 오너 대상 해금 RPC, Steam 표시 데이터 변환 |
| 씬 전환·시네마틱 | [`코드 설명`](Source/05_Cinematics/README.md) · [`SceneTransitionFx 발췌`](Source/05_Cinematics/SceneTransitionFx.cs) · [`CinematicPhysicsWorld.cs`](Source/05_Cinematics/CinematicPhysicsWorld.cs) | 비동기 Fade/Iris 연출과 클라이언트별 독립 물리 씬 |
| UI·피드백 | [`코드 설명`](Source/06_UI/README.md) · [`UIAnimationGroup.cs`](Source/06_UI/UIAnimation/UIAnimationGroup.cs) · [`DamageIndicatorManager.cs`](Source/06_UI/DamageIndicator/DamageIndicatorManager.cs) | TargetRpc 기반 로컬 피해 표시 풀링과 재사용 UI 애니메이션 조합 |
| 사망 관전 | [`코드 설명`](Source/07_GameFlow/README.md) · [`DeathSpectator.cs`](Source/07_GameFlow/DeathSpectator.cs) · [`DeathSpectateUIController.cs`](Source/07_GameFlow/DeathSpectateUIController.cs) | 관전 대상 재구성, 직업 교체·접속 종료 예외 처리, 리스폰 카메라 복구 |
| 로비 직업 선택 | [`코드 설명`](Source/07_GameFlow/README.md) · [`LobbyJobSelectUIController.cs`](Source/07_GameFlow/LobbyJobSelectUIController.cs) | 선택 상태, 로컬라이징, 서버 직업 변경 요청 |
| 에디터 도구 | [`기능 소개`](Source/08_EditorTools/README.md) | 웨이브·스폰 이벤트의 시간축 편집, Snap과 데이터 검증 |

## 주요 문제 해결 사례

### 1. 서버 권위 이동과 화면 보간의 역할 분리

처음에는 서버 권위 위치를 단순 전파하면서 호스트의 점프와 급격한 위치 변화가 클라이언트 화면에서 끊겨 보였습니다. 이를 다음 두 경로로 분리했습니다.

- 오너: 입력을 `Replicate`로 전송하고 서버 상태를 `Reconcile`로 복원
- 비오너: 서버 tick이 포함된 위치·회전·속도 스냅샷을 큐에 저장해 3 tick 지연 재생하고, FishNet `NetworkTickSmoother`로 화면 움직임을 보간
- 점프 시작: 일반 위치 보간만으로 늦게 보이는 첫 프레임을 별도 `ObserversRpc`로 보완
- 넉백: 일반 이동과 다른 보간 설정을 적용하고 넉백 종료 시 기본값으로 복원

관련 자료: [`네트워크 이동 코드 설명`](Source/01_NetworkMovement/README.md). 공동 작업한 `MovementController`에서 입력 예측·상태 복원·관찰자 보간과 관련된 데이터 구조와 메서드를 확인할 수 있습니다.

### 2. 세션 재생성 후 최대 인원 상태가 남는 문제

메인 메뉴와 로비, 게임 씬을 반복 이동한 뒤 새 세션에서 정상 인원의 접속이 거절되는 현상을 조사했습니다. FishNet 코어의 서버 접속자 목록은 종료 시 정상적으로 초기화됐지만, FishySteamworks transport가 최대 인원 판정에 사용하는 Steam 연결 맵은 같은 수명주기에 맞춰 비워지지 않았습니다. 이 때문에 새 세션의 접속자 수에 이전 세션의 연결 상태가 포함될 수 있었습니다.

- 최대 인원 판정에 직접 사용되는 `_steamConnections`와 역방향 대응 맵 `_steamIds`를 함께 비우도록 `Clear()` 추가
- connection ID 재사용 큐와 로컬 호스트 큐·시작 플래그는 세션 경계의 방어적 초기화로 분리

이는 FishNet 코어가 아니라 FishNet용 Steam transport인 FishySteamworks 4.1.1의 세션 상태 정리 누락에 대한 프로젝트 내부 로컬 패치입니다. FishySteamworks 공식 저장소에 이슈나 PR을 제출한 upstream 기여 사례는 아닙니다. 원인 분석과 수정 범위는 [`docs/SESSION_CACHE_FIX.md`](docs/SESSION_CACHE_FIX.md)에 설명했습니다.

### 3. 플랫폼과 게임 규칙을 분리한 도전과제

게임 로직이 Steam API에 직접 의존하지 않도록 `IAchievements`와 플랫폼 구현을 분리했습니다. 스킬별 지표는 서버에서 설정 데이터와 비교하고 세션 중복을 제거한 뒤, 해당 플레이어에게만 `TargetRpc`로 해금을 전달합니다. 팀 구성 업적은 동기화된 플레이어 목록을 시작·승리 시점에 비교해 동일 멤버와 단일 직업 유지 여부를 확인합니다.

관련 코드: [`Source/04_Achievements`](Source/04_Achievements)

### 4. 클라이언트마다 달라지던 시네마틱 물리

씬 전환 연출의 래그돌과 물리가 클라이언트에서 일관되게 재생되지 않는 문제를 해결하기 위해, 네트워크 게임 물리와 분리된 로컬 `PhysicsScene`을 만들고 unscaled time 기준으로 직접 시뮬레이션했습니다.

관련 코드: [`CinematicPhysicsWorld.cs`](Source/05_Cinematics/CinematicPhysicsWorld.cs)

### 5. 반복 편집을 줄인 웨이브 타임라인 도구

웨이브 데이터를 Inspector의 중첩 목록으로 직접 수정하는 대신, 스폰 이벤트를 시간축 위에서 배치하고 그룹별 겹침과 유효성을 확인할 수 있는 `EditorWindow`를 구현했습니다. 확대·축소, 스크롤, 이벤트 선택, 세부 값 편집과 검증을 한 화면에서 처리합니다.

관련 자료: [`웨이브 타임라인 기능 소개`](Source/08_EditorTools/README.md)

## 디렉터리 구성

```text
Source/
├─ 01_NetworkMovement/   # 공동 MovementController 발췌와 전체 이동 정책
├─ 02_SessionAndVoice/   # Steam 세션, 접속 제한, Vivox·Lambda 인증과 음성 제어 UI
├─ 03_Gameplay/          # 바바리안과 사이클롭스 전투
├─ 04_Achievements/      # 크로스 플랫폼 도전과제와 Steam 구현
├─ 05_Cinematics/        # 씬 전환 효과와 독립 물리 씬 기반 로비 연출
├─ 06_UI/                # UI 애니메이션과 Damage Indicator
├─ 07_GameFlow/          # 사망 관전 카메라·UI와 로비 직업 선택
└─ 08_EditorTools/       # 웨이브 데이터 편집·검증용 Unity Editor 도구
```

## 공개 및 저작권

이 저장소는 팀 프로젝트 **No God! Please No!**의 채용 포트폴리오용 코드 모음입니다.

- 팀원 전원의 공개 동의를 바탕으로 제가 직접 구현하거나 주요하게 기여한 C# 코드를 정리했습니다.
- 이 저장소에는 별도의 오픈소스 라이선스를 부여하지 않습니다.
