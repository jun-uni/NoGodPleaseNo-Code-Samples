// 궁극기 사용 검증과 서버 투사체 생성
// 투사체의 충돌·수명주기는 BarbarianUltimateProjectile에서 처리

using UnityEngine;
using FishNet.Object;
using NGPN.Combat;
using NGPN.Core;

namespace NGPN.Gameplay
{
    [DisallowMultipleComponent]
    public class BarbarianUltimate : NetworkBehaviour, IUltimateAbility
    {
        [Header("Projectile")]
        [SerializeField] private BarbarianUltimateProjectile bladeWavePrefab;
        [SerializeField] private Transform firePoint;

        [Header("Damage")]
        [SerializeField] private float damageMultiplier = 4f;
        [SerializeField] private float inUseDuration = 1f;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string ultimateTrigger = "Ultimate";

        [Header("References")]
        [SerializeField] private CharacterActor actor;
        [SerializeField] private CharacterHealth health;

        private bool _pendingFire;

        private void Awake()
        {
            if (actor == null)
                actor = GetComponent<CharacterActor>();
            if (health == null)
                health = GetComponent<CharacterHealth>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        public float ExecuteUltimate_Server()
        {
            // 서버 생존 상태와 투사체 설정 검증
            if (!IsServerInitialized) return 0f;
            if (health != null && !health.isAlive) return 0f;
            if (bladeWavePrefab == null) return 0f;

            // 애니메이션 이벤트까지 발사 요청 유지
            _pendingFire = true;
            PlayUltimate_AllClients_ObserversRpc();

            return Mathf.Max(0f, inUseDuration);
        }

        [ObserversRpc(BufferLast = false)]
        private void PlayUltimate_AllClients_ObserversRpc()
        {
            if (animator == null) return;

            animator.ResetTrigger(ultimateTrigger);
            animator.SetTrigger(ultimateTrigger);
        }

        [Server]
        public void AnimationEvent_FireUltimate_Server()
        {
            if (!_pendingFire) return;
            _pendingFire = false;

            // 캐릭터 전방 기준 발사 방향 계산
            Vector3 direction = transform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f)
                direction = Vector3.forward;
            direction.Normalize();

            Vector3 start = firePoint != null
                ? firePoint.position
                : transform.position + Vector3.up * 1.2f;

            float attack = 0f;
            float criticalChance = 0f;
            if (actor != null)
            {
                CharacterActor.StatSnapshot snapshot = actor.GetSnapshot();
                attack = snapshot.atk;
                criticalChance = snapshot.crt;
            }

            float rawDamage = Mathf.Max(1f, attack * Mathf.Max(0f, damageMultiplier));
            float damage = CritUtility.ApplyCrit_Server(rawDamage, criticalChance, out _);

            // 풀에서 생성 후 서버 초기 상태 전달
            NetworkObject projectileObject =
                NetworkManager.GetPooledInstantiated(bladeWavePrefab.NetworkObject, true);
            ServerManager.Spawn(projectileObject);

            BarbarianUltimateProjectile projectile =
                projectileObject.GetComponent<BarbarianUltimateProjectile>();
            projectile.ServerInit(start, direction, damage, Team.Players, NetworkObject);
        }
    }
}
