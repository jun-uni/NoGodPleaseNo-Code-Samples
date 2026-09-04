# UI Feedback Systems

이 폴더는 서버에서 확인한 피해 이벤트를 공격자 클라이언트의 로컬 UI로 전달하는 Damage Indicator와, 여러 UI Tween을 공통 설정으로 조합하는 애니메이션 구조를 보여줍니다.

## 처리 흐름

### Damage Indicator

`서버 피해 판정 → 공격자 연결 선택 → TargetRpc → 공격자 로컬 ObjectPool → 숫자 초기화 → 카메라 기준 배치·빌보드·거리 스케일 → 수명 종료 후 반환`

피해량과 치명타 여부는 서버에서 공격자에게만 전달합니다. 일반·치명타 스타일은 `DamageStyleType`으로 구분합니다. 화면 표현은 해당 클라이언트가 담당하며, 미리 생성한 인디케이터를 재사용하고 활성 목록을 역순으로 일괄 갱신합니다. 생성 시 대상의 Collider와 Renderer를 캐싱하고, 해당 Bounds의 카메라 방향 표면과 높이 편향을 기준으로 위치를 계산합니다.

### UI Animation

`개별 Tween 생성 → 공통 duration·delay·ease·time scale 적용 → 병렬 또는 순차 Sequence 조합 → 재생·일시정지·되감기·종료`

애니메이션 제어 규약은 `IUIAnimation`으로 통일하고, 공통 Tween 설정과 반복 재생·비활성화 수명 제어는 `UIAnimationBase`에 모았습니다. `UIAnimationGroup`은 여러 애니메이션을 병렬 또는 순차로 조합하며, 비활성화될 때 실행 중인 Sequence를 정리합니다.

## 파일별 설명

### [`DamageIndicator/DamageIndicatorRpcProxy.cs`](DamageIndicator/DamageIndicatorRpcProxy.cs)

서버가 공격자 연결을 지정해 피해 표시 데이터를 `TargetRpc`로 전달하고, 수신한 클라이언트에서 로컬 표시 매니저를 호출합니다.

### [`DamageIndicator/DamageIndicatorManager.cs`](DamageIndicator/DamageIndicatorManager.cs)

Unity `ObjectPool`을 미리 채우고 활성 인디케이터를 한 곳에서 갱신합니다. 생성 시 캐싱한 대상 Bounds와 카메라 방향으로 표시 위치를 계산하고, 빌보드 회전과 거리·스타일·애니메이션 곡선을 결합해 최종 크기를 적용합니다.

### [`DamageIndicator/DamageIndicator.cs`](DamageIndicator/DamageIndicator.cs) · [`DamageIndicator/DamageStyle.cs`](DamageIndicator/DamageStyle.cs)

개별 표시의 수명과 TextMesh Pro 상태를 보관하고, 글꼴·머티리얼·색상·크기 곡선을 데이터로 분리합니다.

### [`UIAnimation/IUIAnimation.cs`](UIAnimation/IUIAnimation.cs) · [`UIAnimation/UIAnimationBase.cs`](UIAnimation/UIAnimationBase.cs)

Tween 생성은 파생 클래스에 맡기고 재생·정지·되감기·완료·종료와 공통 설정 적용은 동일한 API로 제공합니다.

### [`UIAnimation/UIAnimationGroup.cs`](UIAnimation/UIAnimationGroup.cs)

여러 UI 애니메이션의 Tween을 하나의 DOTween Sequence에 병렬 또는 순차로 배치하고 그룹 단위로 제어합니다.

### [`UIAnimation/UIFadeAnimation.cs`](UIAnimation/UIFadeAnimation.cs)

`CanvasGroup`의 알파를 보간하면서 현재 투명도에 맞춰 상호작용과 Raycast 차단 상태를 함께 갱신합니다.
