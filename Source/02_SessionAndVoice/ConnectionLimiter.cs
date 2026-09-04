using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;

namespace NGPN.Gameplay
{
    public class ConnectionLimiter : MonoBehaviour
    {
        [SerializeField] private int maxPlayers = 3;
        [SerializeField] private string mainMenuSceneName = "Main Menu";
        [SerializeField] private string gameLobbySceneName = "Game Lobby";

        private NetworkManager _nm;
        private FishySteamworks.FishySteamworks _fishy;
        private bool _lockedToCurrent = false;
        private int _lockedPlayerCount = 0;

        private void Awake()
        {
            _nm = InstanceFinder.NetworkManager;
            if (_nm == null)
            {
                Debug.LogError("[ConnectionLimiter] NetworkManager not found.");
                return;
            }

            _fishy = _nm.TransportManager.Transport as FishySteamworks.FishySteamworks;
            if (_fishy == null)
            {
                Debug.LogError("[ConnectionLimiter] FishySteamworks transport not found.");
                return;
            }

        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (_fishy != null)
                _fishy.OnRemoteConnectionState += OnRemoteConnectionState;

            if (_nm != null)
                _nm.ServerManager.OnServerConnectionState += OnServerConnectionState;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (_fishy != null)
                _fishy.OnRemoteConnectionState -= OnRemoteConnectionState;

            if (_nm != null)
                _nm.ServerManager.OnServerConnectionState -= OnServerConnectionState;
        }

        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            // 서버 종료 시 잠금 상태 초기화
            if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                _lockedToCurrent = false;
                _lockedPlayerCount = 0;
            }
        }

        public void LockToCurrentPlayers()
        {
            if (_nm == null || !_nm.ServerManager.Started)
                return;

            // 게임 시작 시 현재 인원 고정
            int current = _nm.ServerManager.Clients.Count;
            _lockedPlayerCount = current;
            _lockedToCurrent = true;
        }

        public void Unlock()
        {
            _lockedToCurrent = false;
            _lockedPlayerCount = 0;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == mainMenuSceneName || scene.name == gameLobbySceneName) Unlock();
        }

        private void OnRemoteConnectionState(RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Started)
                return;

            if (_nm == null || !_nm.ServerManager.Started)
                return;

            int total = _nm.ServerManager.Clients.Count;

            // 로비의 기본 최대 인원 검사
            if (!_lockedToCurrent)
            {
                if (total > maxPlayers)
                {
                    _fishy.StopConnection(args.ConnectionId, true);
                    return;
                }

                return;
            }

            // 게임 시작 시점 이후 추가 접속 차단
            if (total > _lockedPlayerCount)
            {
                _fishy.StopConnection(args.ConnectionId, true);
                return;
            }
        }
    }
}
