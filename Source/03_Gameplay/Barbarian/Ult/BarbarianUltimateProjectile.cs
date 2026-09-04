using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using FishNet.Object;
using Cysharp.Threading.Tasks;
using NGPN.Combat;
using NGPN.Core;

namespace NGPN.Gameplay
{
    [DisallowMultipleComponent]
    public class BarbarianUltimateProjectile : NetworkBehaviour
    {
        [Header("Tuning")] [SerializeField] private float speed = 45f;
        [SerializeField] private float lifetime = 1.0f;
        [SerializeField] private float radius = 0.45f;
        [SerializeField] private LayerMask hittableMask;

        [Header("VFX")] [SerializeField] private ParticleSystem bladeVfx;
        [SerializeField] private bool playVfxWithChildren = true;

        [Header("SFX")] [SerializeField] private AudioSource audioSrc;
        [SerializeField] private AudioClip spawnSfx;
        [SerializeField] private AudioClip hitSfx;
        [SerializeField] [Range(0f, 1f)] private float spawnVolume = 1f;
        [SerializeField] [Range(0f, 1f)] private float hitVolume = 1f;
        [SerializeField] private Vector2 hitPitchJitter = new(0.98f, 1.02f);

        [Header("Hit SFX Throttle")] [SerializeField] [Tooltip("히트 SFX 최소 간격(초). 군중 타격 시 소리 과밀 방지")]
        private float minHitSfxInterval = 0.07f;

        private double _nextHitSfxAt;

        [Header("Despawn Linger")] [SerializeField] [Range(0f, 3f)]
        private float lingerAfterLife = 3.0f;

        [SerializeField] private GameObject visualRoot;

        private CancellationTokenSource _cts;
        private bool _lingering;

        [Header("Hit Options")] [SerializeField]
        private bool allowFriendlyFire = false;

        private Vector3 _vel;
        private double _despawnAt;
        private bool _inited;
        private bool _willDespawn;
        private float _damage;
        private Team _attackerTeam;
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly HashSet<NetworkObject> _hitOnce = new();
        private NetworkObject _owner;


        private void OnEnable()
        {
            // 풀 재사용 상태 초기화
            _willDespawn = false;
            _hitOnce.Clear();

            _cts = new CancellationTokenSource();
            _lingering = false;

            if (visualRoot) visualRoot.SetActive(true);
        }

        private void OnDisable()
        {
            ResetSettings();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            _lingering = false;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            PlaySpawnSfx_Local();
        }

        private void PlaySpawnSfx_Local()
        {
            if (!audioSrc || !spawnSfx) return;
            audioSrc.PlayOneShot(spawnSfx, spawnVolume);
        }

        private void ResetSettings()
        {
            _inited = false;
            _willDespawn = false;
            _vel = Vector3.zero;
            _damage = 0f;
            _despawnAt = 0;
            _hitOnce.Clear();
            _nextHitSfxAt = 0;
            _owner = null;
        }

        private void RestartVfxLocal()
        {
            if (!bladeVfx) return;

            // 풀 재사용 대비: 항상 초기화 후 재생
            bladeVfx.Clear(true);
            bladeVfx.Play(playVfxWithChildren);
        }

        [Server]
        public void ServerInit(Vector3 pos, Vector3 dir, float dmg, Team attackerTeam, NetworkObject owner)
        {
            ResetSettings();

            // 서버 이동·피해·소유자 상태 설정
            Vector3 d = dir;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-6f) d = Vector3.forward;
            d.Normalize();

            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(d, Vector3.up));

            _vel = d * speed;
            _damage = dmg;
            _owner = owner;
            _attackerTeam = attackerTeam;

            _despawnAt = TimeManager.ServerUptime + lifetime;
            _inited = true;

            if (visualRoot) visualRoot.SetActive(true);

            // 서버 로컬 VFX 재생
            RestartVfxLocal();

            // 관찰자 초기 상태 전달
            Rpc_ClientInit(pos, d, lifetime);
        }

        [ObserversRpc(BufferLast = false, RunLocally = false)]
        private void Rpc_ClientInit(Vector3 pos, Vector3 dir, float life)
        {
            if (IsServerInitialized) return;

            // 관찰자 시각 이동 상태 초기화
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir, Vector3.up));
            _vel = dir.normalized * speed;

            lifetime = life;
            _despawnAt = TimeManager.ServerUptime + lifetime;

            _inited = true;
            _willDespawn = false;

            if (visualRoot) visualRoot.SetActive(true);

            RestartVfxLocal();
        }

        private void FixedUpdate()
        {
            if (!_inited) return;
            if (_willDespawn) return;

            // 소유자 소멸 시 추가 판정 차단
            if (IsServerInitialized && (_owner == null || !_owner.IsSpawned))
            {
                StartServerLingerAndDespawn();
                return;
            }

            float dt = (float)TimeManager.TickDelta;

            Vector3 prev = transform.position;
            Vector3 moveDist = _vel * dt;

            if (IsServerInitialized)
            {
                // 프레임 간 이동 구간의 연속 충돌 판정
                int n = Physics.SphereCastNonAlloc(
                    prev,
                    radius,
                    moveDist.sqrMagnitude > 1e-6f ? moveDist.normalized : transform.forward,
                    _hits,
                    moveDist.magnitude,
                    hittableMask,
                    QueryTriggerInteraction.Ignore);

                if (n > 0)
                    for (int i = 0; i < n; i++)
                    {
                        RaycastHit h = _hits[i];
                        if (!h.collider) continue;

                        TryApplyDamageOnce_Server(in h);
                    }
            }

            transform.position = prev + moveDist;

            if (TimeManager.ServerUptime >= _despawnAt)
            {
                if (IsServerInitialized)
                    StartServerLingerAndDespawn();
                return;
            }
        }

        [Server]
        private void TryApplyDamageOnce_Server(in RaycastHit hit)
        {
            if (_owner == null || !_owner.IsSpawned) return;

            Collider col = hit.collider;
            if (!col) return;

            IDamageable d = col.GetComponentInParent<IDamageable>();
            if (d == null || !d.isAlive) return;

            NetworkObject no = col.GetComponentInParent<NetworkObject>();
            // 네트워크 대상별 1회 피해 제한
            if (no != null && !_hitOnce.Add(no))
                return;

            if (no == _owner)
                return;

            bool friendly = d.GetTeam() == _attackerTeam;
            if (friendly && !allowFriendlyFire) return;

            float def = Mathf.Max(0f, d.GetTargetDef());
            float finalDmg = Mathf.Max(1f, _damage - def);

            // 방어력 반영 후 서버 피해 적용
            DamageContext ctx = new()
            {
                damage = finalDmg, attacker = _owner, attackerTeam = _attackerTeam, friendlyFire = friendly
            };

            d.ApplyDamage(ctx);

            // 히트 SFX 전송 간격 제한
            double now = TimeManager.ServerUptime;
            if (now >= _nextHitSfxAt)
            {
                _nextHitSfxAt = now + minHitSfxInterval;

                Vector3 pos = hit.point;
                Rpc_PlayHitSfx(pos);
            }
        }

        [ObserversRpc(BufferLast = false)]
        private void Rpc_PlayHitSfx(Vector3 pos)
        {
            if (!audioSrc || !hitSfx) return;

            audioSrc.pitch = UnityEngine.Random.Range(hitPitchJitter.x, hitPitchJitter.y);
            audioSrc.PlayOneShot(hitSfx, hitVolume);
        }


        [Server]
        private void StartServerLingerAndDespawn()
        {
            if (_lingering) return;
            _lingering = true;

            // 이동과 추가 판정 종료
            _willDespawn = true;

            // 오디오 잔여 재생 동안 비주얼 비활성화
            Rpc_SetVisualEnabled(false);
            if (visualRoot) visualRoot.SetActive(false);

            LingerThenDespawnAsync().Forget();
        }

        [ObserversRpc(BufferLast = false)]
        private void Rpc_SetVisualEnabled(bool enabled)
        {
            if (visualRoot) visualRoot.SetActive(enabled);
        }

        private async UniTaskVoid LingerThenDespawnAsync()
        {
            // 지연 종료 취소와 풀 반환
            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(lingerAfterLife),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    _cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (this && IsServerInitialized && IsSpawned)
            {
                ResetSettings();
                ServerManager.Despawn(NetworkObject, DespawnType.Pool);
            }
        }
    }
}
