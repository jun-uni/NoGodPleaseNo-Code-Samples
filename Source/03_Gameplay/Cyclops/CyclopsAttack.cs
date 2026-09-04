// 사이클롭스 공격의 서버 히트 윈도우와 넉백 판정

using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using NGPN.Core;
using NGPN.Combat;

namespace NGPN.Gameplay
{
    public class CyclopsAttack : NetworkBehaviour
    {
        [Header("Attack")]
        [SerializeField] private AttackProfile normalAttackProfile;
        [SerializeField] private Transform attackTransform;

        [Header("Knockback")]
        [SerializeField] private float knockbackPowerPhase1 = 16f;

        private readonly HashSet<NetworkObject> _hitOnce = new();
        private float _atk;
        private float _attackRange;
        private bool _windowOpen;

        public Team Team => Team.Monsters;
        public AttackProfile CurrentAttackProfile => normalAttackProfile;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!IsServerInitialized) return;

            // 서버 전투 스탯 캐시
            MonsterStatsDefinition statsDefinition = GetComponent<MonsterStatsDefinition>();
            if (statsDefinition != null)
                InitializeStats(statsDefinition);
        }

        private void FixedUpdate()
        {
            // 히트 윈도우 동안 서버 판정 반복
            if (IsServerInitialized && _windowOpen)
                Server_DoHitTick();
        }

        public void InitializeStats(MonsterStatsDefinition statsDefinition)
        {
            _atk = statsDefinition.atk.Value;
            _attackRange = normalAttackProfile != null
                ? Mathf.Max(0.1f, normalAttackProfile.range)
                : 3f;
        }

        [Server]
        public void AnimationEvent_OpenHitWindow()
        {
            if (normalAttackProfile == null || attackTransform == null) return;

            // 히트 윈도우 시작과 대상 기록 초기화
            _windowOpen = true;
            _hitOnce.Clear();
        }

        [Server]
        public void AnimationEvent_CloseHitWindow()
        {
            _windowOpen = false;
        }

        [Server]
        public void Server_DoHitTick()
        {
            if (!_windowOpen || normalAttackProfile == null || attackTransform == null) return;

            int hits = 0;
            Server_OverlapSphere(out hits);

            if (normalAttackProfile.maxHits > 0 && hits >= normalAttackProfile.maxHits)
                AnimationEvent_CloseHitWindow();
        }

        [Server]
        private void Server_OverlapSphere(out int hits)
        {
            hits = 0;
            // 공격 프로필 기준 범위 판정
            Collider[] colliders = Physics.OverlapSphere(
                attackTransform.position + attackTransform.forward * _attackRange * 0.5f,
                normalAttackProfile.radius,
                normalAttackProfile.hittableMask,
                QueryTriggerInteraction.Collide);

            foreach (Collider collider in colliders)
            {
                Vector3 point = collider.ClosestPoint(attackTransform.position);
                Vector3 delta = point - attackTransform.position;
                Vector3 normal = delta.sqrMagnitude > 1e-4f
                    ? delta.normalized
                    : -attackTransform.forward;

                Server_TryApply(collider, point, normal, ref hits);
                if (normalAttackProfile.maxHits > 0 && hits >= normalAttackProfile.maxHits)
                    break;
            }
        }

        [Server]
        private void Server_TryApply(Collider collider, Vector3 hitPosition, Vector3 hitNormal, ref int hits)
        {
            if (collider == null) return;

            // NetworkObject 기준 중복 타격 차단
            NetworkObject targetObject = collider.GetComponentInParent<NetworkObject>();
            if (targetObject == null || !_hitOnce.Add(targetObject)) return;

            IDamageable target = collider.GetComponentInParent<IDamageable>();
            if (target == null || !target.isAlive) return;

            bool friendly = target.GetTeam() == Team;
            if (friendly && !normalAttackProfile.allowFriendlyFire) return;

            hits++;

            // 공격력과 방어력 기반 서버 피해 계산
            float rawDamage = _atk * normalAttackProfile.damageMultiplier;
            float finalDamage = Mathf.Max(1f, rawDamage - Mathf.Max(0f, target.GetTargetDef()));

            DamageContext context = new()
            {
                damage = finalDamage,
                hitPosition = hitPosition,
                hitNormal = hitNormal,
                attacker = this,
                attackerTeam = Team,
                friendlyFire = friendly
            };

            target.ApplyDamage(in context);

            IKnockbackReceiver knockback = collider.GetComponentInParent<IKnockbackReceiver>();
            if (knockback == null) return;

            Vector3 direction = collider.transform.position - attackTransform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f)
                direction = attackTransform.forward;
            direction.Normalize();

            // 피격 방향 기준 넉백 적용
            Vector3 impulse =
                direction * knockbackPowerPhase1 + Vector3.up * knockbackPowerPhase1 * 0.8f;
            knockback.ApplyExplosionKnockback(impulse, 12f, 0.2f, 0.2f);
        }
    }
}
