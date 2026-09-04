// 팀 공동 작업 파일에서 네트워크 이동 관련 데이터와 메서드 발췌

using System.Collections.Generic;
using UnityEngine;
using FishNet.Component.Transforming;
using FishNet.Component.Transforming.Beta;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using NGPN.Core;

namespace NGPN.Gameplay
{
    // 한 tick의 오너 입력 전달 구조
    public struct MoveData : IReplicateData
    {
        public short mx;
        public short my;
        public bool run;
        public Vector3 facingHint;
        public bool jump;

        public bool moveLocked;
        public bool cameraLocked;

        public uint tick;

        public void Dispose()
        {
        }

        public uint GetTick()
        {
            return tick;
        }

        public void SetTick(uint value)
        {
            tick = value;
        }

        public static short ToS16(float value)
        {
            int scaled = Mathf.RoundToInt(Mathf.Clamp(value, -1f, 1f) * 32767f);
            return (short)Mathf.Clamp(scaled, -32767, 32767);
        }

        public static float FromS16(short value)
        {
            return Mathf.Clamp(value / 32767f, -1f, 1f);
        }
    }

    // 서버 권위 상태 복원 구조
    public struct MoveReconcile : IReconcileData
    {
        public PredictionRigidbody PRB;
        public float speed01;

        public float coyoteTimer;
        public float jumpBufferTimer;
        public bool hasJumpedThisAir;
        public bool wasGrounded;

        public Vector3 kbVel;
        public float kbHold;

        private uint _tick;

        public void Dispose()
        {
        }

        public uint GetTick()
        {
            return _tick;
        }

        public void SetTick(uint value)
        {
            _tick = value;
        }
    }

    public class MovementController : NetworkBehaviour
    {
        [Header("Camera")][SerializeField] private Camera cam;

        [Header("Observer Interpolation")]
        [SerializeField] private NetworkTickSmoother networkTickSmoother;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string jumpTrigger = "Jump";
        [SerializeField][Range(0.05f, 0.6f)] private float speedDampSec = 0.2f;

        [Header("Jump")]
        [SerializeField] private KeyCode jumpKey = KeyCode.Space;

        [Header("Interaction Lock (Hub)")]
        [SerializeField] private InteractionLockHub _lockHub;

        private bool _facingHintOverridden;
        private bool _jumpPressedLatched;
        private bool _externalControl;
        private Vector2 inputMove;
        private bool runPressed;
        private Vector3 _worldMove;
        private Vector3 _facingHint = Vector3.forward;
        private uint _tick;

        private Rigidbody rb;
        private PredictionRigidbody prb;
        private UniversalTickSmoother _smoother;
        private bool _kbVisualBoost;
        private float serverSpeed01;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private bool _hasJumpedThisAir;
        private bool _wasGrounded;
        private Vector3 _kbVel;
        private float _kbDrag = 12f;
        private float _kbHold;
        private float _kbCtrl = 0.2f;

        public PredictionRigidbody PRB => prb;

        private struct Snapshot
        {
            public uint tick;
            public Vector3 pos;
            public Quaternion rot;
            public Vector3 vel;
            public float speed01;
        }

        private readonly Queue<Snapshot> _snapshots = new();
        private uint _latestServerTick;
        private const int INTERP_DELAY_TICKS = 3;

        private Vector3 _renderVel;
        private float _renderSpeed01;
        private uint _jumpFlashActiveUntilTick;
        private float _jumpFlashVelY;

        private void Update()
        {
            // 비오너 애니메이션에 보간된 속도 반영
            if (!IsOwner && !IsServerInitialized && animator != null)
            {
                float dt = Time.deltaTime;
                Vector3 localVelocity = transform.InverseTransformDirection(
                    new Vector3(_renderVel.x, 0f, _renderVel.z));
                animator.SetFloat("MoveX", localVelocity.x, speedDampSec, dt);
                animator.SetFloat("MoveY", localVelocity.z, speedDampSec, dt);
                animator.SetFloat("Speed", _renderSpeed01);
            }

            if (IsOwner && !_externalControl && Input.GetKeyDown(jumpKey))
                _jumpPressedLatched = true;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            TimeManager.OnTick += OnTick_SendInputs;
            TimeManager.OnPostTick += OnPostTick_SendReconcile;
        }

        public override void OnStopNetwork()
        {
            TimeManager.OnTick -= OnTick_SendInputs;
            TimeManager.OnPostTick -= OnPostTick_SendReconcile;
        }

        private void OnTick_SendInputs()
        {
            if (!(IsOwner || IsServerInitialized)) return;
            ReplicateMove(CreateMoveData(), ReplicateState.Invalid, Channel.Unreliable);
        }

        private void OnPostTick_SendReconcile()
        {
            if (!IsServerInitialized) return;

            CreateReconcile();

            // 서버 tick이 포함된 관찰자 스냅샷 전송
            BroadcastSnapshot_ObserversRpc(
                transform.position,
                transform.rotation,
                prb.Rigidbody.linearVelocity,
                serverSpeed01,
                TimeManager.Tick
            );
        }

        [ObserversRpc(BufferLast = true)]
        private void BroadcastSnapshot_ObserversRpc(Vector3 pos, Quaternion rot, Vector3 vel, float speed01,
            uint serverTick)
        {
            if (IsOwner || IsServerInitialized) return;

            _latestServerTick = serverTick;
            _snapshots.Enqueue(new Snapshot
            {
                tick = serverTick,
                pos = pos,
                rot = rot,
                vel = vel,
                speed01 = speed01
            });

            // 보간 큐의 최대 크기 제한
            while (_snapshots.Count > 64)
                _snapshots.Dequeue();
        }

        private void FixedUpdate()
        {
            if (!IsOwner && !IsServerInitialized)
            {
                if (_snapshots.Count >= 2)
                {
                    // 최신 상태보다 일정 tick 이전을 재생할 목표로 선택
                    uint targetTick = _latestServerTick >= INTERP_DELAY_TICKS
                        ? _latestServerTick - (uint)INTERP_DELAY_TICKS
                        : _latestServerTick;

                    Snapshot a = default, b = default;
                    Snapshot prev = default;
                    bool hasPrev = false, found = false;

                    foreach (Snapshot s in _snapshots)
                    {
                        if (!hasPrev)
                        {
                            prev = s;
                            hasPrev = true;
                            continue;
                        }

                        if (prev.tick <= targetTick && targetTick <= s.tick)
                        {
                            a = prev;
                            b = s;
                            found = true;
                            break;
                        }

                        prev = s;
                    }

                    if (found)
                    {
                        float span = Mathf.Max(1, (int)(b.tick - a.tick));
                        float t = (float)(targetTick - a.tick) / span;

                        Vector3 pos = Vector3.Lerp(a.pos, b.pos, t);
                        Quaternion rot = Quaternion.Slerp(a.rot, b.rot, t);
                        Vector3 vel = Vector3.Lerp(a.vel, b.vel, t);
                        float spd01 = Mathf.Lerp(a.speed01, b.speed01, t);

                        // 점프 시작 직후 한 tick의 시각 지연 보정
                        if (_latestServerTick < _jumpFlashActiveUntilTick)
                            pos += Vector3.up * _jumpFlashVelY * Time.fixedDeltaTime * 0.5f;

                        rb.MovePosition(pos);
                        rb.MoveRotation(rot);
                        _renderVel = vel;
                        _renderSpeed01 = spd01;
                    }
                }

                return;
            }
        }

        public MoveData CreateMoveData()
        {
            if (!IsOwner) return default;

            _tick++;
            MoveData md = new();

            // 상호작용 잠금과 카메라 잠금 상태 수집
            bool moveLocked = _lockHub != null && _lockHub.MoveLocked;
            bool cameraLocked = _lockHub != null && _lockHub.CameraLocked;

            md.moveLocked = moveLocked;
            md.cameraLocked = cameraLocked;

            // 카메라 기준 평면 축 계산
            Vector3 camF = cam ? cam.transform.forward : Vector3.forward;
            Vector3 camR = cam ? cam.transform.right : Vector3.right;
            camF.y = 0;
            camR.y = 0;
            camF.Normalize();
            camR.Normalize();

            if (camF.sqrMagnitude > 1e-4f && !_facingHintOverridden)
                _facingHint = camF;

            // 이동 잠금 시 중립 입력 반환
            if (moveLocked)
            {
                md.mx = 0;
                md.my = 0;
                md.run = false;
                md.facingHint = _facingHint;
                md.jump = false;
                md.SetTick(_tick);
                return md;
            }

            // 입력 수집과 월드 이동 방향 계산
            inputMove = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
            runPressed = Input.GetKey(KeyCode.LeftShift);

            md.jump = _jumpPressedLatched;
            _jumpPressedLatched = false;

            _worldMove = camF * inputMove.y + camR * inputMove.x;
            if (_worldMove.sqrMagnitude > 1f) _worldMove.Normalize();

            // 전송용 입력 데이터 구성
            md.mx = MoveData.ToS16(_worldMove.x);
            md.my = MoveData.ToS16(_worldMove.z);
            md.run = runPressed;
            md.facingHint = _facingHint;
            md.SetTick(_tick);
            return md;
        }

        [Replicate]
        private void ReplicateMove(MoveData md, ReplicateState state = ReplicateState.Invalid,
            Channel channel = Channel.Unreliable)
        {
            if (!(IsOwner || IsServerInitialized)) return;
            float dt = (float)TimeManager.TickDelta;

            // 권한별 이동 잠금 판정
            bool nowLocked;

            if (IsServerInitialized)
            {
                if (_lockHub != null)
                    nowLocked = _lockHub.MoveLocked;
                else
                    nowLocked = false;
            }
            else
            {
                nowLocked = md.moveLocked;
            }

            if (nowLocked != _prevMoveLocked)
            {
                SetExternalControlLocal(nowLocked);
                _prevMoveLocked = nowLocked;
            }

            // 외부 상태에서도 예측 물리 유지
            if (isCaptured || isLaunched)
            {
                prb.Simulate();
                return;
            }

            // 스킬 등 외부 제어 상태 계산
            if (_externalControl)
            {
                bool isGrounded = CheckGrounded(out _);
                _coyoteTimer = isGrounded ? coyoteTime : Mathf.Max(0f, _coyoteTimer - dt);
                _jumpBufferTimer = Mathf.Max(0f, _jumpBufferTimer - dt);

                if (isGrounded && !_wasGrounded)
                    _hasJumpedThisAir = false;
                _wasGrounded = isGrounded;

                Vector3 vel = prb.Rigidbody.linearVelocity;

                if (_extHasForwardSpeed)
                {
                    Vector3 fwd = prb.Rigidbody.rotation * Vector3.forward;
                    vel.x = fwd.x * _extForwardSpeed + _kbVel.x;
                    vel.z = fwd.z * _extForwardSpeed + _kbVel.z;
                }
                else if (_extHasPlanarVel)
                {
                    vel.x = _extPlanarVel.x + _kbVel.x;
                    vel.z = _extPlanarVel.z + _kbVel.z;
                }
                else
                {
                    vel.x = _kbVel.x;
                    vel.z = _kbVel.z;
                }

                if (!prb.Rigidbody.isKinematic)
                    prb.Rigidbody.linearVelocity = vel;

                Vector3 f = md.facingHint.sqrMagnitude > 1e-4f ? md.facingHint : (_extHasFacing ? _extFacing : Vector3.zero);
                if (f.sqrMagnitude > 1e-4f)
                {
                    Quaternion goal = Quaternion.LookRotation(f, Vector3.up);
                    prb.Rigidbody.rotation = Quaternion.RotateTowards(prb.Rigidbody.rotation, goal, TurnRateEffective * dt);
                }

                prb.Simulate();
                float prevKbHold = _kbHold;
                _kbVel = Vector3.MoveTowards(_kbVel, Vector3.zero, _kbDrag * dt);
                _kbHold = Mathf.Max(0f, _kbHold - dt);

                if (IsServerInitialized && prevKbHold > 0f && _kbHold <= 0f)
                    SetKnockbackInterpolation_ObserversRpc(false);

                if (!isGrounded)
                {
                    float gBase = _gravity;
                    float gUp = gBase * Mathf.Max(0.1f, gravityScale);
                    float gDown = gUp * Mathf.Max(1f, fallMultiplier);

                    float targetG = prb.Rigidbody.linearVelocity.y < 0f ? gDown : gUp;
                    float extraG = Mathf.Max(0f, targetG - gBase);
                    if (extraG > 0f)
                        prb.AddForce(Vector3.down * extraG, ForceMode.Acceleration);
                }

                if (animator && (IsOwner || IsServerInitialized))
                {
                    Vector3 a = prb.Rigidbody.linearVelocity;
                    Vector3 lv = transform.InverseTransformDirection(new Vector3(a.x, 0f, a.z));
                    animator.SetFloat("MoveX", lv.x, speedDampSec, dt);
                    animator.SetFloat("MoveY", lv.z, speedDampSec, dt);
                    float s01 = Mathf.InverseLerp(0f, _run.Value, new Vector2(a.x, a.z).magnitude);
                    serverSpeed01 = Mathf.MoveTowards(serverSpeed01, s01, dt / Mathf.Max(0.0001f, speedDampSec));
                    animator.SetFloat("Speed", serverSpeed01);

                    if (IsServerInitialized)
                    {
                        _animMove.Value = new Vector2(lv.x, lv.z);
                        _animSpeed.Value = serverSpeed01;
                    }
                }

                return;
            }

            // 압축 입력 복원과 이동 정책 적용
            Vector3 camMove01 = new(
                Mathf.Clamp(MoveData.FromS16(md.mx), -1f, 1f), 0f,
                Mathf.Clamp(MoveData.FromS16(md.my), -1f, 1f));

            if (md.facingHint.sqrMagnitude > 1e-4f)
                _facingHint = md.facingHint.normalized;

            float baseSpeed = md.run ? _run.Value : _walk.Value;

            Vector3 wishVel = _policy != null
                ? _policy.ComputeVelocity(camMove01, _facingHint, transform, baseSpeed, md.run)
                : camMove01 * baseSpeed;

            Vector3 cur = prb.Rigidbody.linearVelocity;
            Vector3 curPlanar = new(cur.x, 0f, cur.z);
            Vector3 wishPlanar = new(wishVel.x, 0f, wishVel.z);

            float ctrl = _kbHold > 0f ? _kbCtrl : 1f;

            Vector3 goalPlanar = wishPlanar * ctrl + _kbVel;
            Vector3 dv = goalPlanar - curPlanar;

            prb.AddForce(dv, ForceMode.VelocityChange);

            bool grounded = CheckGrounded(out _);

            _coyoteTimer = grounded ? coyoteTime : Mathf.Max(0f, _coyoteTimer - dt);
            _jumpBufferTimer = Mathf.Max(0f, _jumpBufferTimer - dt);

            // 점프 버퍼와 코요테 타임 계산
            if (md.jump)
                _jumpBufferTimer = jumpBuffer;

            bool canJump = _jumpBufferTimer > 0f && _coyoteTimer > 0f && !_hasJumpedThisAir;

            if (canJump)
            {
                float gBase = _gravity;
                float gEff = gBase * Mathf.Max(0.1f, gravityScale);
                float v0 = Mathf.Sqrt(2f * gEff * Mathf.Max(0.01f, jumpHeight));

                Vector3 vel2 = prb.Rigidbody.linearVelocity;
                vel2.y = v0;
                prb.Rigidbody.linearVelocity = vel2;

                _hasJumpedThisAir = true;
                _jumpBufferTimer = 0f;
                _coyoteTimer = 0f;

                bool isReplayed = (state & ReplicateState.Replayed) != 0;

                if (!isReplayed && animator != null)
                    if (IsOwner)
                    {
                        if (IsClientOnlyInitialized)
                        {
                            animator.ResetTrigger(jumpTrigger);
                            animator.SetTrigger(jumpTrigger);
                        }
                        else if (IsServerInitialized)
                        {
                            animator.ResetTrigger(jumpTrigger);
                            animator.SetTrigger(jumpTrigger);
                        }
                    }

                if (IsServerInitialized)
                {
                    BroadcastJump_ObserversRpc();

                    JumpStarted_ObserversRpc(TimeManager.Tick, v0);
                }
            }

            if (grounded && !_wasGrounded)
                _hasJumpedThisAir = false;

            _wasGrounded = grounded;

            if (!grounded)
            {
                float gBase = _gravity;
                float gUp = gBase * Mathf.Max(0.1f, gravityScale);
                float gDown = gUp * Mathf.Max(1f, fallMultiplier);

                float targetG = prb.Rigidbody.linearVelocity.y < 0f ? gDown : gUp;
                float extraG = Mathf.Max(0f, targetG - gBase);
                if (extraG > 0f)
                    prb.AddForce(Vector3.down * extraG, ForceMode.Acceleration);
            }

            // 예측 Rigidbody 물리 반영
            prb.Simulate();

            bool alive = _health == null || _health.isAlive;
            if (alive && _facingHint.sqrMagnitude > 1e-4f)
            {
                Quaternion goal = Quaternion.LookRotation(_facingHint, Vector3.up);
                prb.Rigidbody.rotation = Quaternion.RotateTowards(prb.Rigidbody.rotation, goal, TurnRateEffective * dt);
            }

            if (animator != null && (IsOwner || IsServerInitialized))
            {
                Vector3 afterVec = prb.Rigidbody.linearVelocity;
                Vector3 lv = transform.InverseTransformDirection(new Vector3(afterVec.x, 0f, afterVec.z));
                animator.SetFloat("MoveX", lv.x, speedDampSec, dt);
                animator.SetFloat("MoveY", lv.z, speedDampSec, dt);

                float s01 = Mathf.InverseLerp(0f, _run.Value, new Vector2(afterVec.x, afterVec.z).magnitude);
                serverSpeed01 = Mathf.MoveTowards(serverSpeed01, s01, dt / Mathf.Max(0.0001f, speedDampSec));
                animator.SetFloat("Speed", serverSpeed01);

                if (IsServerInitialized)
                {
                    _animMove.Value = new Vector2(lv.x, lv.z);
                    _animSpeed.Value = serverSpeed01;
                }
            }

            // 넉백 감쇠와 종료 상태 반영
            float prevKbHold1 = _kbHold;

            _kbVel = Vector3.MoveTowards(_kbVel, Vector3.zero, _kbDrag * dt);

            _kbHold = Mathf.Max(0f, _kbHold - dt);

            if (IsServerInitialized)
                if (prevKbHold1 > 0f && _kbHold <= 0f)
                    SetKnockbackInterpolation_ObserversRpc(false);
        }

        public override void CreateReconcile()
        {
            // 서버 권위 상태 스냅샷 생성
            MoveReconcile rd = new()
            {
                PRB = prb,
                speed01 = serverSpeed01,
                coyoteTimer = _coyoteTimer,
                jumpBufferTimer = _jumpBufferTimer,
                hasJumpedThisAir = _hasJumpedThisAir,
                wasGrounded = _wasGrounded,
                kbVel = _kbVel,
                kbHold = _kbHold
            };

            rd.SetTick(TimeManager.Tick);
            ReconcileMove(rd, Channel.Unreliable);
        }

        [Reconcile]
        private void ReconcileMove(MoveReconcile rd, Channel channel = Channel.Unreliable)
        {
            // Rigidbody 상태 복원
            prb.Reconcile(rd.PRB);

            // 점프와 넉백 상태 복원
            _coyoteTimer = rd.coyoteTimer;
            _jumpBufferTimer = rd.jumpBufferTimer;
            _hasJumpedThisAir = rd.hasJumpedThisAir;
            _wasGrounded = rd.wasGrounded;

            _kbVel = rd.kbVel;
            _kbHold = rd.kbHold;
        }

        [ObserversRpc(RunLocally = true, BufferLast = true)]
        private void BroadcastJump_ObserversRpc()
        {
            if (IsOwner) return;
            if (animator == null) return;

            animator.ResetTrigger(jumpTrigger);
            animator.SetTrigger(jumpTrigger);
        }

        [ObserversRpc(RunLocally = true, BufferLast = true)]
        private void JumpStarted_ObserversRpc(uint serverTick, float initialYVel, Channel channel = Channel.Reliable)
        {
            if (IsOwner || IsServerInitialized) return;

            // 보간으로 늦게 보이는 점프 시작 프레임 보정
            _jumpFlashActiveUntilTick = serverTick + 1;
            _jumpFlashVelY = initialYVel;
        }

        [ObserversRpc(RunLocally = true, BufferLast = false)]
        private void SetKnockbackInterpolation_ObserversRpc(bool on)
        {
            if (networkTickSmoother is null) return;
            if (_smoother == null)
                _smoother = networkTickSmoother.SmootherController.UniversalSmoother;

            if (on)
            {
                _smoother.SetInterpolationValue(Constants.Interpolate.KNOCKBACK_INTERPOLATE_CONTROLLER, true);
                _smoother.SetInterpolationValue(Constants.Interpolate.KNOCKBACK_INTERPOLATE_SPECTATOR, false);
                _kbVisualBoost = true;
            }
            else
            {
                _smoother.SetInterpolationValue(Constants.Interpolate.DEFAULT_INTERPOLATE_CONTROLLER, true);
                _smoother.SetInterpolationValue(Constants.Interpolate.DEFAULT_INTERPOLATE_SPECTATOR, false);
                _kbVisualBoost = false;
            }

            // Controller와 spectator의 smoothing 설정 갱신
            _smoother.SetSmoothedProperties(TransformPropertiesFlag.Everything, true);
            _smoother.SetSmoothedProperties(TransformPropertiesFlag.Everything, false);
            _smoother.UpdateRealtimeInterpolation();
        }

        [Server]
        public void ApplyExplosionKnockback(Vector3 impulseVel, float drag = 12f, float hold = 0.2f,
            float ctrlScale = 0.2f)
        {
            float prevHold = _kbHold;

            Vector3 planar = new(impulseVel.x, 0f, impulseVel.z);
            _kbVel += planar;
            _kbDrag = Mathf.Max(0f, drag);
            _kbHold = Mathf.Max(_kbHold, hold);
            _kbCtrl = Mathf.Clamp01(ctrlScale);

            Vector3 v = PRB.Rigidbody.linearVelocity;
            v.y = Mathf.Max(v.y, impulseVel.y);
            PRB.Rigidbody.linearVelocity = v;

            // 넉백 시작 경계에서 전용 보간 설정 적용
            if (prevHold <= 0f && _kbHold > 0f)
                SetKnockbackInterpolation_ObserversRpc(true);
        }
    }
}
