using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace NGPN.Gameplay
{
    // 손 목표를 슬롯 앵커에 맞추는 IK 보조 컴포넌트
    public sealed class CinematicCharacterHandIK : MonoBehaviour
    {
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Transform rightHandTarget;
        [SerializeField] private Rig rig;

        [Header("Offsets")] [SerializeField] private Vector3 leftHandPosOffset;
        [SerializeField] private Vector3 leftHandEulerOffset;
        [SerializeField] private Vector3 rightHandPosOffset;
        [SerializeField] private Vector3 rightHandEulerOffset;

        private Transform _leftAnchor;
        private Transform _rightAnchor;

        public Transform LeftTarget => leftHandTarget;
        public Transform RightTarget => rightHandTarget;


        private void LateUpdate()
        {
            if (leftHandTarget != null && _leftAnchor != null)
            {
                leftHandTarget.position = _leftAnchor.position + _leftAnchor.rotation * leftHandPosOffset;
                leftHandTarget.rotation = _leftAnchor.rotation * Quaternion.Euler(leftHandEulerOffset);
            }

            if (rightHandTarget != null && _rightAnchor != null)
            {
                rightHandTarget.position = _rightAnchor.position + _rightAnchor.rotation * rightHandPosOffset;
                rightHandTarget.rotation = _rightAnchor.rotation * Quaternion.Euler(rightHandEulerOffset);
            }
        }


        public void BindToAnchors(Transform leftAnchor, Transform rightAnchor)
        {
            _leftAnchor = leftAnchor;
            _rightAnchor = rightAnchor;
        }

        public void SetRigWeight(float w)
        {
            if (rig != null) rig.weight = Mathf.Clamp01(w);
        }

        public void SetHandOffsets(Vector3 leftPos, Vector3 leftEuler, Vector3 rightPos, Vector3 rightEuler)
        {
            leftHandPosOffset = leftPos;
            leftHandEulerOffset = leftEuler;
            rightHandPosOffset = rightPos;
            rightHandEulerOffset = rightEuler;
        }
    }
}
