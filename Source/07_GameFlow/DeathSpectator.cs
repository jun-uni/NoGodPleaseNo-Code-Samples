// 사망 관전 시작, 대상 갱신과 리스폰 복구

using System.Collections.Generic;
using UnityEngine;
using FishNet.Connection;
using FishNet.Object;
using NGPN.Combat;
using NGPN.Gameplay.UI;

namespace NGPN.Gameplay
{
    public sealed class DeathSpectator : NetworkBehaviour, IRespawnable
    {
        [SerializeField] private KeyCode cycleKey = KeyCode.Mouse1;
        [SerializeField] private string anchorName = "DeathAnchor";
        [SerializeField] private float delayAfterDeath = 2f;

        private ThirdPersonCamera _tpcam;
        private CharacterActor _actor;
        private DeathSpectateUIController deathSpectateUIController;

        private readonly List<Transform> _targets = new();
        private int _index = 0;
        private bool _active = false;
        private Transform _anchor;
        private float _cycleUnlockTime = 0f;
        private bool _guideShown = false;

        private void Awake()
        {
            _actor = GetComponent<CharacterActor>();
            if (_actor != null && _actor.PlayerCamera != null)
                _tpcam = _actor.PlayerCamera.GetComponent<ThirdPersonCamera>();
        }

        private void Update()
        {
            if (!IsOwner || !_active) return;

            if (Time.unscaledTime < _cycleUnlockTime)
                return;

            if (!_guideShown)
            {
                _guideShown = true;
                if (deathSpectateUIController == null)
                    deathSpectateUIController = FindFirstObjectByType<DeathSpectateUIController>();
                deathSpectateUIController?.ShowHint();
                deathSpectateUIController?.UpdateDeathSpectatorTarget(null);
            }

            // 직업 교체나 접속 종료로 사라진 대상 반영
            if (_targets.Count == 0 || _targets[_index] == null)
            {
                RebuildTargets();
                ApplyCurrentTargetToCameraAndUI();
            }

            if (Input.GetKeyDown(cycleKey)) Cycle(1);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!IsOwner)
                return;

            if (_actor == null)
                _actor = GetComponent<CharacterActor>();

            if (_actor != null && _actor.PlayerCamera != null)
                _tpcam = _actor.PlayerCamera.GetComponent<ThirdPersonCamera>();

            if (deathSpectateUIController == null)
                deathSpectateUIController = FindFirstObjectByType<DeathSpectateUIController>();

            deathSpectateUIController?.CloseContent();
        }

        private void Cycle(int step)
        {
            // 대상 전환 시 현재 플레이어 목록 재구성
            RebuildTargets();

            if (_targets.Count == 0 || _tpcam == null) return;

            _index = (_index + step + _targets.Count) % _targets.Count;
            ApplyCurrentTargetToCameraAndUI();
        }

        [Server]
        public void StartSpectate_Server(Vector3 deathPosition, NetworkObject[] allyCandidates)
        {
            TargetBeginSpectate(Owner, deathPosition, allyCandidates);
        }

        [TargetRpc]
        private void TargetBeginSpectate(NetworkConnection conn, Vector3 deathPos, NetworkObject[] allyCandidates)
        {
            if (_tpcam == null) return;

            // 사망 지점에 로컬 관전 앵커 생성
            if (_anchor == null)
            {
                _anchor = new GameObject(anchorName).transform;
                _anchor.gameObject.hideFlags = HideFlags.DontSave;
            }

            _anchor.position = deathPos;
            _active = true;
            _cycleUnlockTime = Time.unscaledTime + delayAfterDeath;
            _guideShown = false;

            if (deathSpectateUIController == null)
                deathSpectateUIController = FindFirstObjectByType<DeathSpectateUIController>();

            deathSpectateUIController?.UpdateDeathSpectatorTarget(null);
            deathSpectateUIController?.OpenContent();

            RebuildTargets();
            _index = 0;
            ApplyCurrentTargetToCameraAndUI();
        }

        [Server]
        public void StopSpectate_Server()
        {
            TargetEndSpectate(Owner);
        }

        [TargetRpc]
        private void TargetEndSpectate(NetworkConnection conn)
        {
            _active = false;

            deathSpectateUIController?.UpdateDeathSpectatorTarget(null);
            deathSpectateUIController?.CloseContent();
            _guideShown = false;

            // 리스폰한 로컬 캐릭터로 카메라 복구
            if (_tpcam != null)
                _tpcam.RestoreTarget();

            if (_anchor != null)
            {
                Destroy(_anchor.gameObject);
                _anchor = null;
            }

            _targets.Clear();
            _index = 0;
        }

        [Server]
        public void OnAfterRespawn_Server()
        {
            StopSpectate_Server();
        }

        private void RebuildTargets()
        {
            _targets.Clear();

            // 첫 번째 대상은 사망 지점 앵커
            if (_anchor != null)
                _targets.Add(_anchor);

            CharacterActor[] actors =
                FindObjectsByType<CharacterActor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (CharacterActor actor in actors)
            {
                if (actor == null) continue;
                if (actor == _actor) continue;
                if (!actor.GraphicRoot) continue;

                _targets.Add(actor.GraphicRoot);
            }

            if (_targets.Count == 0)
                _index = 0;
            else
                _index = Mathf.Clamp(_index, 0, _targets.Count - 1);
        }

        private void ApplyCurrentTargetToCameraAndUI()
        {
            if (_tpcam == null || _targets.Count == 0) return;

            Transform t = _targets[Mathf.Clamp(_index, 0, _targets.Count - 1)];
            if (t == null)
                return;

            _tpcam.FollowTarget(t);

            CharacterActor targetActor = null;
            if (t != _anchor)
                targetActor = t.GetComponentInParent<CharacterActor>();

            deathSpectateUIController?.UpdateDeathSpectatorTarget(targetActor);
        }
    }
}
