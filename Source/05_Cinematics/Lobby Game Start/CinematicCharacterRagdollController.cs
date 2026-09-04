using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace NGPN.Gameplay
{
    // 시네마틱 더미의 Animator·Rig 정지와 래그돌 전환
    public sealed class CinematicCharacterRagdollController : MonoBehaviour
    {
        [Header("Optional refs (recommended)")] [SerializeField]
        private Animator animator;

        [SerializeField] private RigBuilder rigBuilder;
        [SerializeField] private Rig rig;

        private Rigidbody[] _rbs;
        private Collider[] _cols;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (rigBuilder == null) rigBuilder = GetComponentInChildren<RigBuilder>(true);
            if (rig == null) rig = GetComponentInChildren<Rig>(true);

            _rbs = GetComponentsInChildren<Rigidbody>(true);
            _cols = GetComponentsInChildren<Collider>(true);
        }

        public void EnableRagdoll(bool disableAnimator = true, bool disableRigging = true)
        {
            if (disableRigging)
            {
                if (rig != null) rig.weight = 0f;
                if (rigBuilder != null) rigBuilder.enabled = false;
            }

            if (disableAnimator && animator != null)
                animator.enabled = false;

            if (_rbs != null)
                foreach (Rigidbody rb in _rbs)
                {
                    if (rb == null) continue;
                    rb.isKinematic = false;
                    rb.detectCollisions = true;
                }

            if (_cols != null)
                foreach (Collider c in _cols)
                {
                    if (c == null) continue;
                    c.enabled = true;
                }
        }

    }
}
