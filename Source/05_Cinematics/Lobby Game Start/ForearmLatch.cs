using UnityEngine;

namespace NGPN.Gameplay
{
    // 래그돌 팔과 이동 앵커를 런타임 조인트로 연결
    public sealed class ForearmLatch : MonoBehaviour
    {
        [SerializeField] private Rigidbody forearmRb;
        [SerializeField] private Transform handBone;

        private ConfigurableJoint _joint;

        public void LatchTo(Rigidbody anchorRb, float breakForce = Mathf.Infinity)
        {
            if (forearmRb == null || anchorRb == null)
                return;

            Unlatch();

            _joint = forearmRb.gameObject.AddComponent<ConfigurableJoint>();
            _joint.connectedBody = anchorRb;

            _joint.autoConfigureConnectedAnchor = false;
            if (handBone != null)
                _joint.anchor = forearmRb.transform.InverseTransformPoint(handBone.position);
            else
                _joint.anchor = Vector3.zero;

            _joint.connectedAnchor = Vector3.zero;

            _joint.xMotion = ConfigurableJointMotion.Locked;
            _joint.yMotion = ConfigurableJointMotion.Locked;
            _joint.zMotion = ConfigurableJointMotion.Locked;


            _joint.angularXMotion = ConfigurableJointMotion.Locked;
            _joint.angularYMotion = ConfigurableJointMotion.Locked;
            _joint.angularZMotion = ConfigurableJointMotion.Locked;

            _joint.breakForce = breakForce;
            _joint.breakTorque = breakForce;
        }

        public void Unlatch()
        {
            if (_joint != null)
            {
                Destroy(_joint);
                _joint = null;
            }
        }

        public Vector3 GetWorldAttachPoint()
        {
            if (handBone != null) return handBone.position;
            return forearmRb != null ? forearmRb.worldCenterOfMass : transform.position;
        }

        public Quaternion GetWorldAttachRotation()
        {
            if (handBone != null) return handBone.rotation;
            return forearmRb != null ? forearmRb.rotation : transform.rotation;
        }
    }
}
