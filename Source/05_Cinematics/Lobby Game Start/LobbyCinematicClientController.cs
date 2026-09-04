// 래그돌·조인트·여신상 상승 시퀀스

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NGPN.Gameplay
{
    public sealed class LobbyCinematicClientController : MonoBehaviour
    {
        [SerializeField] private GameObject cinematicCameraGO;
        [SerializeField] private GameObject uiRootToDisable;
        [SerializeField] private Transform statueRoot;
        [SerializeField] private StatueLift statueLift;
        [SerializeField] private float preHold = 0.6f;
        [SerializeField] private float ikFadeOut = 0.2f;

        private Rigidbody[] _latchRightRbs;
        private Rigidbody[] _latchLeftRbs;
        private readonly List<GameObject> _spawned = new();
        private CancellationTokenSource _cinematicCts;
        private int _sequenceId;
        private bool _cinematicRunning;

        private void OnDestroy()
        {
            CancelCinematicSequence();
        }

        public void StopCinematic()
        {
            CancelCinematicSequence();
            _cinematicRunning = false;

            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                    Destroy(_spawned[i]);
            }

            _spawned.Clear();

            if (cinematicCameraGO != null)
                cinematicCameraGO.SetActive(false);

            InteractionLockHub hub = FindLocalPlayerHub();
            hub?.UnlockAllFor(InteractionLockHub.LockSource.LobbyCinematic);

            if (uiRootToDisable != null)
                uiRootToDisable.SetActive(true);
        }

        private void CancelCinematicSequence()
        {
            if (_cinematicCts != null)
            {
                _cinematicCts.Cancel();
                _cinematicCts.Dispose();
                _cinematicCts = null;
            }
        }

        private void EnsureLatchAnchors(int count)
        {
            _latchRightRbs ??= new Rigidbody[count];
            _latchLeftRbs ??= new Rigidbody[count];

            if (_latchRightRbs.Length != count) _latchRightRbs = new Rigidbody[count];
            if (_latchLeftRbs.Length != count) _latchLeftRbs = new Rigidbody[count];
        }

        private Rigidbody GetOrCreateLatchRb(int slotIndex, bool right)
        {
            Rigidbody[] arr = right ? _latchRightRbs : _latchLeftRbs;
            if (arr == null || slotIndex < 0 || slotIndex >= arr.Length) return null;

            if (arr[slotIndex] != null) return arr[slotIndex];

            GameObject go = new(right ? $"Latch_{slotIndex}_R" : $"Latch_{slotIndex}_L");
            if (statueRoot != null) go.transform.SetParent(statueRoot, true);

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            arr[slotIndex] = rb;
            return rb;
        }

        private async UniTaskVoid PlayLiftSequenceAsync(int seqId, CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(preHold), cancellationToken: ct);

                if (ct.IsCancellationRequested || seqId != _sequenceId) return;

                // 현재 손 위치에 앵커를 맞춘 뒤 래그돌과 조인트 연결
                for (int i = 0; i < _spawned.Count; i++)
                {
                    GameObject go = _spawned[i];
                    if (go == null) continue;

                    CinematicCharacterHandIK ik = go.GetComponentInChildren<CinematicCharacterHandIK>(true);
                    if (ik == null) continue;

                    bool useRight = ik.RightTarget != null;
                    bool useLeft = !useRight && ik.LeftTarget != null;

                    Transform targetTf = useRight ? ik.RightTarget : useLeft ? ik.LeftTarget : null;
                    if (targetTf == null) continue;

                    Rigidbody latchRb = GetOrCreateLatchRb(i, useRight);
                    if (latchRb == null) continue;

                    ForearmLatch latch = go.GetComponentInChildren<ForearmLatch>(true);
                    if (latch == null) continue;

                    latchRb.transform.position = latch.GetWorldAttachPoint();
                    latchRb.transform.rotation = latch.GetWorldAttachRotation();

                    CinematicCharacterRagdollController rag =
                        go.GetComponentInChildren<CinematicCharacterRagdollController>(true);
                    rag?.EnableRagdoll(true, true);

                    latch.LatchTo(latchRb);
                }

                // 래그돌 물리 안정화 대기 후 여신상 상승
                float t = 0f;
                while (t < ikFadeOut)
                {
                    if (ct.IsCancellationRequested || seqId != _sequenceId) return;

                    t += Time.deltaTime;
                    float w = 1f - Mathf.Clamp01(t / ikFadeOut);

                    foreach (GameObject go in _spawned)
                    {
                        if (go == null) continue;
                        CinematicCharacterHandIK ik = go.GetComponentInChildren<CinematicCharacterHandIK>(true);
                        if (ik != null) ik.SetRigWeight(w);
                    }

                    await UniTask.Yield();
                }

                if (ct.IsCancellationRequested || seqId != _sequenceId) return;
                statueLift?.Lift();
            }
            finally
            {
                if (seqId == _sequenceId)
                {
                    _cinematicRunning = false;
                    CancelCinematicSequence();
                }
            }
        }

        private InteractionLockHub FindLocalPlayerHub()
        {
            InteractionLockHub[] hubs = FindObjectsOfType<InteractionLockHub>(true);
            foreach (InteractionLockHub h in hubs)
            {
                if (h != null && h.IsOwner)
                    return h;
            }

            return null;
        }
    }
}
