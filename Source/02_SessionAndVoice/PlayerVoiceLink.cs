using UnityEngine;
using Unity.Services.Authentication;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace NGPN.Gameplay
{
    public class PlayerVoiceLink : NetworkBehaviour
    {
        [Header("Avatar Root")][SerializeField] private Transform avatarRoot;

        // 바바리안의 음성채팅 패널티를 판별을 위한 bool 값
        private readonly SyncVar<bool> _isBarbarian = new();
        private readonly SyncVar<string> _vivoxPlayerId = new();

        public bool IsBarbarian => _isBarbarian.Value;
        public string VivoxPlayerId => _vivoxPlayerId.Value;

        private void Awake()
        {
            // 동기화 값 변경 구독
            _isBarbarian.OnChange += OnBarbarianChanged;
            _vivoxPlayerId.OnChange += OnVivoxIdChanged;
        }

        private void OnDestroy()
        {
            _isBarbarian.OnChange -= OnBarbarianChanged;
            _vivoxPlayerId.OnChange -= OnVivoxIdChanged;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // 오너의 UGS PlayerId 등록
            if (IsOwner)
            {
                string id = AuthenticationService.Instance.PlayerId;
                if (!string.IsNullOrEmpty(id))
                    ServerSetVivoxId(id);
            }

            TryRegisterToController();
            TryRegisterOrUpdateRole();

            if (IsOwner)
            {
                VivoxPositionUpdater up = GetComponent<VivoxPositionUpdater>();
                if (up != null)
                    VivoxBootstrap.Instance?.ForceReconnectLocalUpdater(up);
            }
        }

        [ServerRpc]
        private void ServerSetVivoxId(string id)
        {
            _vivoxPlayerId.Value = id;
        }

        [Server]
        private void SetBarbarian_Server(bool isBarbarian)
        {
            _isBarbarian.Value = isBarbarian;
            TryRegisterOrUpdateRole();
        }

        [ServerRpc]
        public void ServerSetBarbarian(bool isBarbarian)
        {
            _isBarbarian.Value = isBarbarian;
        }

        public void SetBarbarian(bool isBarbarian)
        {
            if (IsServer) SetBarbarian_Server(isBarbarian);
            else ServerSetBarbarian(isBarbarian);
        }

        private void OnBarbarianChanged(bool prev, bool next, bool asServer)
        {
            TryRegisterOrUpdateRole();
        }

        private void OnVivoxIdChanged(string prev, string next, bool asServer)
        {
            TryRegisterToController();
        }

        private void TryRegisterToController()
        {
            if (string.IsNullOrEmpty(VivoxPlayerId) || avatarRoot == null) return;

            // Vivox ID와 아바타 연결
            GameManager.Instance?.VoiceChat?.RegisterAvatar(VivoxPlayerId, avatarRoot, IsBarbarian);
        }

        private void TryRegisterOrUpdateRole()
        {
            if (string.IsNullOrEmpty(VivoxPlayerId)) return;
            GameManager.Instance?.VoiceChat?.SetPlayerRoleBarbarian(VivoxPlayerId, IsBarbarian);
        }
    }
}
