using UnityEngine;

namespace NGPN.Gameplay
{
    // 이동 정책 인터페이스
    // 입력과 방향 힌트 기반 속도 벡터 계산
    public interface IMovementPolicy
    {
        Vector3 ComputeVelocity(
            Vector3 cameraSpaceMove01,
            Vector3 facingHint,
            Transform actor,
            float baseSpeed,
            bool isRunning);
    }
}
