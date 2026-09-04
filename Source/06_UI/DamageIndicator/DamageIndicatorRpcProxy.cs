// 서버 피해 이벤트를 공격자 클라이언트로 전달

using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace NGPN.Gameplay
{
    public class DamageIndicatorRpcProxy : NetworkBehaviour
    {
        [Server]
        public void ShowToAttacker(
            NetworkConnection attackerConnection,
            NetworkObject target,
            float amount,
            bool isCritical)
        {
            if (attackerConnection == null || target == null)
                return;

            DamageStyleType styleType = isCritical
                ? DamageStyleType.Critical
                : DamageStyleType.Normal;

            // 공격자 클라이언트에만 표시 요청 전달
            Target_ShowDamage(
                attackerConnection,
                target,
                amount,
                isCritical,
                styleType
            );
        }

        [TargetRpc]
        private void Target_ShowDamage(
            NetworkConnection _,
            NetworkObject target,
            float amount,
            bool isCritical,
            DamageStyleType styleType)
        {
            if (target == null)
                return;

            DamageIndicatorManager.Instance?.Show(new DamageShowArgs(
                amount,
                isCritical,
                target.transform,
                styleType
            ));
        }
    }
}
