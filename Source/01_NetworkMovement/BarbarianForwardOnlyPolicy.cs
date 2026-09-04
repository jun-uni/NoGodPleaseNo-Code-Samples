using UnityEngine;

namespace NGPN.Gameplay
{
    [CreateAssetMenu(menuName = "Movement/Policy/BarbarianForwardOnly")]
    public class BarbarianForwardOnlyPolicy : ScriptableObject, IMovementPolicy
    {
        public Vector3 ComputeVelocity(Vector3 camMove01, Vector3 facingHint, Transform actor,
            float baseSpeed, bool isRunning)
        {
            // 이동에 사용할 전방 계산
            Vector3 fwd = facingHint.sqrMagnitude > 1e-4f
                ? Vector3.ProjectOnPlane(facingHint, Vector3.up).normalized
                : Vector3.ProjectOnPlane(actor.forward, Vector3.up).normalized;

            // 입력이 있으면 전방 이동
            float forward01 = camMove01.sqrMagnitude > 1e-6f ? 1f : 0f;

            return fwd * (forward01 * baseSpeed);
        }
    }
}
