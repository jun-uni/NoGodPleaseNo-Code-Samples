using UnityEngine;
using Unity.Services.Vivox;
using FishNet.Object;
using FishNet.Connection;

namespace NGPN.Gameplay
{
    [DisallowMultipleComponent]
    public class VivoxPositionUpdater : NetworkBehaviour
    {
        private string _channelName;
        [SerializeField] private float _updateInterval = 0.3f;
        private float _next;

        // Vivox 위치 기준 객체
        private GameObject _sourceGO;

        // Bootstrap 중복 알림 방지
        private bool _notified;

        private void Awake()
        {
            _sourceGO = gameObject;
        }

        // 채널과 위치 갱신 주기 설정
        public void ConfigureForChannel(string channelName, float interval = 0.3f)
        {
            _channelName = channelName;
            _updateInterval = Mathf.Max(0.1f, interval);
            enabled = IsOwner;
            _next = 0f;

            // 채널 참가 직후 위치 전송
            if (IsOwner && !string.IsNullOrEmpty(_channelName))
                VivoxService.Instance.Set3DPosition(_sourceGO ?? gameObject, _channelName, allowPanning: false);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            enabled = IsOwner;
            NotifyBootstrapOnce();
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            enabled = IsOwner;
            NotifyBootstrapOnce();

            if (IsOwner && !string.IsNullOrEmpty(_channelName))
                VivoxService.Instance.Set3DPosition(_sourceGO ?? gameObject, _channelName, allowPanning: false);
            else if (!IsOwner)
                _channelName = null;
        }

        private void NotifyBootstrapOnce()
        {
            if (!_notified && IsOwner)
            {
                _notified = true;
                VivoxBootstrap.Instance?.OnLocalUpdaterReady(this);
            }
            else if (!IsOwner)
            {
                _notified = false;
            }
        }

        private void Update()
        {
            if (VivoxBootstrap.Instance != null && VivoxBootstrap.Instance.IsShuttingDown)
                return;
            if (!IsOwner || string.IsNullOrEmpty(_channelName)) return;

            if (Time.time < _next) return;

            VivoxService.Instance.Set3DPosition(_sourceGO ?? gameObject, _channelName, allowPanning: false);
            _next = Time.time + _updateInterval;
        }

        // Vivox 출력 위치 변경
        public void OverrideSource(GameObject newSource)
        {
            _sourceGO = newSource ? newSource : gameObject;

            if (IsOwner && !string.IsNullOrEmpty(_channelName))
                VivoxService.Instance.Set3DPosition(_sourceGO, _channelName, allowPanning: false);
        }

        // Vivox 출력 위치 복원
        public void ClearOverrideSource()
        {
            _sourceGO = gameObject;

            if (IsOwner && !string.IsNullOrEmpty(_channelName))
                VivoxService.Instance.Set3DPosition(_sourceGO, _channelName, allowPanning: false);
        }
    }
}
