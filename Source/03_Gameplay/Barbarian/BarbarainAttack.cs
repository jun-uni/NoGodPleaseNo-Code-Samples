// 바바리안 기본 공격의 서버 검증과 히트 판정

using System.Collections.Generic;
using UnityEngine;
using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NGPN.Core;
using NGPN.Combat;
using Random = UnityEngine.Random;

namespace NGPN.Gameplay
{
    public class BarbarianAttack : NetworkBehaviour
    {
        [Header("Input")]
        [SerializeField] private KeyCode attackKey = KeyCode.Mouse0;

        [Header("Attack")]
        [SerializeField] private List<AttackProfileEntry> entries = new();
        [SerializeField] private AttackType selectedAttackType = AttackType.Light;
        [SerializeField] private Transform attackTransform;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string attackTrigger = "Attack";
        [SerializeField] private string attackVariantParam = "AttackVariant";
        [SerializeField][Range(0f, 1f)] private float avoidRepeatBias = 0.7f;

        [Header("State")]
        [SerializeField] private InteractionLockHub interactionLockHub;
        [SerializeField] private UltimateCharge ultimateCharge;

        [Header("Passive")]
        [SerializeField] private float passiveDamagePercentPerSec = 2f;
        [SerializeField] private float passiveDamagePercentMax = 100f;

        private readonly Dictionary<AttackType, AttackProfile> profiles = new();
        private readonly Dictionary<AttackType, float> cooldowns = new();
        private readonly HashSet<NetworkObject> hitOnce = new();

        private AttackProfile currentAttackProfile;
        private Collider[] _ownColliders;
        private float _atk;
        private float _crt;
        private float _passiveStartUptime;
        private bool _passiveActive;
        private bool isAttacking;
        private bool windowOpen;
        private int lastAttackVariant = -1;
        private readonly SyncVar<bool> _jobBusy = new();

        private Team Team => Team.Players;
        private AttackProfile CurrentAttackProfile => currentAttackProfile;

        private void Awake()
        {
            // 공격 프로필과 자기 Collider 캐시
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            _ownColliders = GetComponentsInChildren<Collider>(true);
            BuildProfilesFromEntries();
            profiles.TryGetValue(selectedAttackType, out currentAttackProfile);

            CharacterActor actor = GetComponent<CharacterActor>();
            if (actor != null)
            {
                CharacterActor.StatSnapshot snapshot = actor.GetSnapshot();
                _atk = snapshot.atk;
                _crt = snapshot.crt;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // 웨이브 수명주기에 패시브 타이머 연결
            DefenseGameManager.WaveStartedServer += OnWaveStarted_Server;
            DefenseGameManager.WaveEndedServer += OnWaveEnded_Server;

            _passiveActive = DefenseGameManager.IsWaveActive;
            ResetPassiveTimer_Server();
        }

        public override void OnStopServer()
        {
            DefenseGameManager.WaveStartedServer -= OnWaveStarted_Server;
            DefenseGameManager.WaveEndedServer -= OnWaveEnded_Server;

            base.OnStopServer();
        }

        [Server]
        private void OnWaveStarted_Server()
        {
            _passiveActive = true;
            ResetPassiveTimer_Server();
        }

        [Server]
        private void OnWaveEnded_Server()
        {
            _passiveActive = false;
            ResetPassiveTimer_Server();
        }

        [Server]
        private void ResetPassiveTimer_Server()
        {
            _passiveStartUptime = InstanceFinder.TimeManager.ServerUptime;
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (interactionLockHub != null && interactionLockHub.AttackLocked) return;

            bool ultimateInUse = ultimateCharge && ultimateCharge.InUse;
            // 오너 입력을 서버 요청으로 전달
            if (Input.GetKeyDown(attackKey) && !ultimateInUse)
                TryBeginAttack_ServerRpc(selectedAttackType);
        }

        private void FixedUpdate()
        {
            if (IsServerInitialized && windowOpen)
                DoHitTick();
        }

        private void BuildProfilesFromEntries()
        {
            profiles.Clear();
            if (entries == null) return;

            foreach (AttackProfileEntry entry in entries)
                if (entry.profile != null && !profiles.ContainsKey(entry.type))
                    profiles.Add(entry.type, entry.profile);
        }

        [Server]
        private void SetJobBusy_Server(bool value)
        {
            _jobBusy.Value = value;
        }

        [ServerRpc]
        private void TryBeginAttack_ServerRpc(AttackType type)
        {
            TryBeginAttack_Server(type);
        }

        [Server]
        private void TryBeginAttack_Server(AttackType type)
        {
            // 생존·잠금·쿨다운 서버 검증
            if (TryGetComponent(out CharacterHealth health) && !health.isAlive) return;
            if (interactionLockHub != null && interactionLockHub.AttackLocked) return;
            if (ultimateCharge && ultimateCharge.InUse) return;
            if (isAttacking) return;
            if (!profiles.TryGetValue(type, out AttackProfile profile) || profile == null) return;

            if (!cooldowns.TryGetValue(type, out float lastUse))
                lastUse = -999f;
            if (TimeManager.ServerUptime - lastUse < profile.attackCooldown) return;

            // 서버 공격 상태와 재사용 시각 기록
            currentAttackProfile = profile;
            cooldowns[type] = TimeManager.ServerUptime;
            isAttacking = true;
            SetJobBusy_Server(true);

            if (animator != null && !string.IsNullOrEmpty(attackTrigger))
                PlayAttack_AllClients_ObserversRpc(PickVariant(2));
        }

        [ObserversRpc(BufferLast = false)]
        private void PlayAttack_AllClients_ObserversRpc(int variant)
        {
            if (animator == null) return;

            animator.SetInteger(attackVariantParam, variant);
            animator.ResetTrigger(attackTrigger);
            animator.SetTrigger(attackTrigger);
        }

        private int PickVariant(int count)
        {
            if (count <= 1) return 0;

            int variant = Random.Range(0, count);
            if (lastAttackVariant >= 0 && Random.value < avoidRepeatBias)
                variant = 1 - lastAttackVariant;

            lastAttackVariant = variant;
            return variant;
        }

        [Server]
        public void AnimationEvent_OpenHitWindow()
        {
            if (CurrentAttackProfile == null || attackTransform == null) return;

            // 히트 윈도우 시작과 대상 기록 초기화
            windowOpen = true;
            hitOnce.Clear();
        }

        [Server]
        public void AnimationEvent_CloseHitWindow()
        {
            windowOpen = false;
            isAttacking = false;
            SetJobBusy_Server(false);
        }

        [Server]
        public void DoHitTick()
        {
            if (!windowOpen || CurrentAttackProfile == null || attackTransform == null) return;

            int hits = 0;

            // OverlapSphere 기반 근접 공격 판정
            OverlapSphere(out hits);

            if (CurrentAttackProfile.maxHits > 0 && hits >= CurrentAttackProfile.maxHits)
                AnimationEvent_CloseHitWindow();
        }

        [Server]
        private void OverlapSphere(out int hits)
        {
            hits = 0;
            Collider[] colliders = Physics.OverlapSphere(
                attackTransform.position + attackTransform.forward * CurrentAttackProfile.range * 0.5f,
                CurrentAttackProfile.radius,
                CurrentAttackProfile.hittableMask,
                QueryTriggerInteraction.Collide);

            foreach (Collider collider in colliders)
            {
                if (IsOwnCollider(collider)) continue;

                Vector3 point = collider.ClosestPoint(attackTransform.position);
                Vector3 delta = point - attackTransform.position;
                Vector3 normal = delta.sqrMagnitude > 1e-4f
                    ? delta.normalized
                    : -attackTransform.forward;

                TryApply(collider, point, normal, ref hits);
                if (CurrentAttackProfile.maxHits > 0 && hits >= CurrentAttackProfile.maxHits)
                    break;
            }
        }

        private bool IsOwnCollider(Collider collider)
        {
            if (collider == null) return false;

            if (_ownColliders != null)
                for (int i = 0; i < _ownColliders.Length; i++)
                    if (_ownColliders[i] == collider)
                        return true;

            return collider.transform.root == transform.root;
        }

        [Server]
        private void TryApply(Collider collider, Vector3 hitPosition, Vector3 hitNormal, ref int hits)
        {
            if (collider == null) return;

            // 한 히트 윈도우에서 대상별 1회 판정
            NetworkObject targetObject = collider.GetComponentInParent<NetworkObject>();
            if (targetObject == null || !hitOnce.Add(targetObject)) return;

            IDamageable target = collider.GetComponentInParent<IDamageable>();
            if (target == null || !target.isAlive) return;

            bool friendly = target.GetTeam() == Team;
            if (friendly && !CurrentAttackProfile.allowFriendlyFire) return;

            hits++;

            // 패시브·치명타·방어력 서버 계산
            float rawDamage = _atk * CurrentAttackProfile.damageMultiplier;
            if (_passiveActive)
            {
                float elapsed = Mathf.Max(0f, InstanceFinder.TimeManager.ServerUptime - _passiveStartUptime);
                float elapsedSeconds = Mathf.Floor(elapsed);
                float bonusPercent = Mathf.Min(
                    Mathf.Max(0f, passiveDamagePercentMax),
                    elapsedSeconds * Mathf.Max(0f, passiveDamagePercentPerSec));

                rawDamage *= (100f + bonusPercent) * 0.01f;
            }

            rawDamage = CritUtility.ApplyCrit_Server(rawDamage, _crt, out bool isCritical);
            float finalDamage = Mathf.Max(1f, rawDamage - Mathf.Max(0f, target.GetTargetDef()));

            // 공통 피해 컨텍스트 적용
            DamageContext context = new()
            {
                damage = finalDamage,
                hitPosition = hitPosition,
                hitNormal = hitNormal,
                attacker = this,
                attackerTeam = Team,
                friendlyFire = friendly,
                isCritical = isCritical
            };

            target.ApplyDamage(in context);

            if (ultimateCharge != null && !friendly && target.GetTeam() == Team.Monsters)
                ultimateCharge.AddDamage_Server(finalDamage);
        }
    }
}
