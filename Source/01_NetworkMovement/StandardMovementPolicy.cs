using UnityEngine;

namespace NGPN.Gameplay
{
    [CreateAssetMenu(menuName = "Movement/Policy/Standard")]
    public class StandardMovementPolicy : ScriptableObject, IMovementPolicy
    {
        public Vector3 ComputeVelocity(Vector3 camMove01, Vector3 facingHint, Transform actor, float baseSpeed,
            bool isRunning)
        {
            Vector3 dir = camMove01;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            return dir * baseSpeed;
        }
    }
}
