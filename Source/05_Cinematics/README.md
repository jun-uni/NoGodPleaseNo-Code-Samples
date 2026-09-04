# Cinematics and Scene Transitions

이 폴더는 씬 전환 UI와 게임 물리에서 분리된 로컬 시네마틱 물리 씬, 로비 시작 연출의 래그돌 시퀀스를 보여줍니다.

## 연출 미리보기

### 로비에서 게임 시작

로비에서 게임을 시작하면 캐릭터들이 여신상에 매달린 채 상승하고, 화면 전환과 함께 게임 씬으로 이동합니다.

![로비에서 게임 시작 전환 컷씬](Lobby%20Transition.gif)

### 승리 씬에서 로비 복귀

승리 연출이 끝난 뒤 캐릭터들을 보여주는 복귀 컷씬과 화면 전환을 거쳐 로비 씬으로 이동합니다.

![승리 씬에서 로비 복귀 전환 컷씬](Victory%20Scene%20Transition.gif)

## 처리 흐름

### 씬 전환

`로컬 조작 잠금 → Fade 또는 Iris 닫기 → 검은 화면 유지 → 씬 로드 완료 → Fade 열기 → 조작 잠금 해제`

Fade와 Iris는 `Time.unscaledDeltaTime`을 기준으로 진행합니다. Iris 중심은 대상의 월드 좌표를 현재 활성 카메라의 뷰포트 좌표로 변환해 매 프레임 갱신합니다.

### 로컬 시네마틱 물리

`LocalPhysicsMode.Physics3D 씬 생성 → 연출 오브젝트 이동 → unscaled time 누적 → 60 Hz 고정 스텝 시뮬레이션 → 씬 전환 시 잔여 루트 정리`

게임 씬의 네트워크 물리와 시네마틱 더미의 래그돌을 분리합니다. 각 클라이언트는 동일한 연출 순서를 로컬 물리 씬에서 재생하며, 전환 뒤 남은 오브젝트와 누적 시간을 초기화합니다.

### 로비 시작 연출

`직업·슬롯별 오프셋 적용 → 손 IK를 앵커에 연결 → 앵커를 현재 손 위치에 정렬 → 래그돌 활성화 → ConfigurableJoint 연결 → 짧은 물리 안정화 대기 → 여신상 상승·회전`

비동기 시퀀스는 `CancellationTokenSource`와 시퀀스 ID를 함께 확인합니다. 정지되거나 새 시퀀스가 시작되면 이전 작업의 후속 처리를 중단하고, 생성한 더미·카메라·조작 잠금·UI 상태를 정리합니다.

## 파일별 설명

### [`SceneTransitionFx.cs`](SceneTransitionFx.cs)

Fade와 Iris 닫힘·열림 처리부입니다. 화면 전환 중 로컬 플레이어의 조작과 UI 입력을 잠그고, 최소 검은 화면 유지 시간 이후 원래 상태로 복원합니다.

### [`CinematicPhysicsWorld.cs`](CinematicPhysicsWorld.cs)

3D 로컬 물리 씬을 생성하고 `Time.unscaledDeltaTime`을 60 Hz 고정 스텝으로 누적해 직접 시뮬레이션합니다. 연출 오브젝트를 전용 씬으로 이동시키며 일반 씬 전환 시 잔여 루트와 누적 시간을 정리합니다.

### [`Lobby Game Start/LobbyCinematicClientController.cs`](Lobby%20Game%20Start/LobbyCinematicClientController.cs)

로비 시작 연출에서 더미의 손 위치에 물리 앵커를 맞춘 뒤 래그돌과 조인트를 연결하고, 짧은 물리 안정화 대기 후 여신상 상승을 실행하는 비동기 시퀀스입니다.

### [`Lobby Game Start/LobbyCinematicJobTuning.cs`](Lobby%20Game%20Start/LobbyCinematicJobTuning.cs)

직업과 슬롯 조합별 더미 위치·회전·양손 IK 오프셋을 `ScriptableObject` 데이터로 분리합니다.

### [`Lobby Game Start/CinematicCharacterHandIK.cs`](Lobby%20Game%20Start/CinematicCharacterHandIK.cs)

양손 IK 목표를 슬롯 앵커에 맞추고 직업·슬롯별 위치와 회전 오프셋, Rig 가중치를 적용합니다.

### [`Lobby Game Start/CinematicCharacterRagdollController.cs`](Lobby%20Game%20Start/CinematicCharacterRagdollController.cs)

Animator와 Animation Rigging을 끄고 더미의 Rigidbody·Collider를 활성화해 키네마틱 포즈에서 래그돌 상태로 전환합니다.

### [`Lobby Game Start/ForearmLatch.cs`](Lobby%20Game%20Start/ForearmLatch.cs)

래그돌 팔의 손 위치를 기준으로 런타임 `ConfigurableJoint`를 생성해 여신상 아래의 키네마틱 앵커에 연결합니다.

### [`Lobby Game Start/StatueLift.cs`](Lobby%20Game%20Start/StatueLift.cs)

DOTween으로 상승과 회전 가속을 구성하고, 오브젝트 파괴와 함께 취소되는 지연 효과음을 재생합니다. 정지 시 Tween을 종료하고 위치와 회전 속도 상태를 초기화합니다.
