# Network Movement

`MovementController.cs`는 여러 팀원이 함께 수정한 공동 작업 파일이며, 제가 구현하거나 주요하게 기여한 네트워크 이동 관련 데이터 구조·상태 필드·메서드를 정리했습니다.

이 폴더는 서버 권위 이동의 입력 생성과 상태 복원, 비오너 화면 보간, 캐릭터별 이동 정책 분리를 보여줍니다.

## 처리 흐름

`Owner 입력 생성 → Replicate 기반 예측·서버 시뮬레이션 → Reconcile 적용 → 입력 재실행`

`Server 스냅샷 전송 → Observer 큐 적재 → 3 tick 지연 상태 선택 → 위치·회전 보간`

## 파일별 설명

### [`MovementController.cs`](MovementController.cs)

주요 데이터 구조와 메서드는 다음과 같습니다.

- `MoveData`: 이동 축, 달리기, 점프, 방향 힌트와 이동 잠금 상태를 한 network tick의 입력으로 구성합니다.
- `ToS16`·`FromS16`: -1~1 범위의 이동 축을 16비트 정수로 압축하고 시뮬레이션 전에 복원합니다.
- `CreateMoveData`: 오너 권한을 확인하고 카메라 기준 이동 방향, 달리기, latch된 점프 입력을 수집합니다. 이동이 잠긴 경우 이동·점프 입력을 0으로 만듭니다.
- `ReplicateMove`: 오너 예측과 서버 권위 경로가 같은 tick 입력으로 이동·점프·넉백 상태를 시뮬레이션합니다. 공동 메서드에서 이 흐름을 확인하는 데 필요한 본문을 발췌했습니다.
- `MoveReconcile`: `PredictionRigidbody` 상태와 점프 타이머, 지면 상태, 넉백 상태를 서버 권위 데이터로 전달합니다.
- `CreateReconcile`: 서버의 physics tick 이후 복원할 상태를 생성해 오너에게 전송합니다.
- `ReconcileMove`: 서버 물리 상태를 적용한 뒤 점프·넉백 변수를 복원해 이후 입력을 재실행할 기준을 맞춥니다.
- `Snapshot`·`BroadcastSnapshot_ObserversRpc`: 서버 tick이 포함된 위치·회전·속도 상태를 비오너 큐에 최대 64개 보관합니다.
- `FixedUpdate`: 최신 상태보다 3 tick 이전을 목표로 두 스냅샷 사이의 위치와 회전을 보간합니다.
- `JumpStarted_ObserversRpc`: 점프 시작 tick과 초기 수직 속도를 신뢰성 채널로 전달해 첫 프레임의 시각 지연을 보정합니다.
- `SetKnockbackInterpolation_ObserversRpc`: 넉백 시작에는 전용 smoothing 값을 적용하고 넉백이 끝나면 기본값으로 복원합니다.

### [`IMovementPolicy.cs`](IMovementPolicy.cs)

카메라 기준 입력, 방향 힌트, 캐릭터 transform과 기본 속도를 받아 목표 이동 속도를 반환하는 정책 인터페이스입니다. 캐릭터별 이동 규칙을 `MovementController`의 조건문으로 누적하지 않도록 분리합니다.

### [`StandardMovementPolicy.cs`](StandardMovementPolicy.cs)

일반 캐릭터의 이동 정책입니다. 평면 입력의 크기를 1 이하로 정규화하고 기본 이동 속도를 적용합니다.

### [`BarbarianForwardOnlyPolicy.cs`](BarbarianForwardOnlyPolicy.cs)

입력 방향과 관계없이 캐릭터가 바라보는 전방으로만 이동하는 정책입니다. 방향 힌트가 없으면 캐릭터 transform의 전방을 사용합니다.
