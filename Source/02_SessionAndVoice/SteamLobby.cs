// 팀 공동 작업 파일에서 Steam 세션 수명주기 관련 데이터와 메서드 발췌

using System;
using UnityEngine;
using Steamworks;
using FishNet;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using Cysharp.Threading.Tasks;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace NGPN.Gameplay
{
    public enum NetworkExitReason
    {
        None = 0,
        UserRequested,
        HostDisconnected
    }

    public class SteamLobby : MonoBehaviour
    {
        private CSteamID lobbyId;
        private NetworkManager _networkManager;
        protected Callback<LobbyCreated_t> lobbyCreated;
        protected Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
        protected Callback<LobbyEnter_t> lobbyEnter;

        [SerializeField] private string lobbySceneName = "Game Lobby";
        [SerializeField] private string mainMenuSceneName = "Main Menu";

        private bool _isReturningToMenu;

        public static NetworkExitReason LastExitReason { get; internal set; } = NetworkExitReason.None;

        public bool IsLocalLobbyOwner =>
            lobbyId.IsValid() && SteamMatchmaking.GetLobbyOwner(lobbyId) == SteamUser.GetSteamID();

        private void Start()
        {
            _networkManager = FindAnyObjectByType<NetworkManager>();
            if (_networkManager == null)
            {
                Debug.LogError("NetworkManager not found.");
                return;
            }

            if (!SteamManager.Initialized)
            {
                Debug.LogError("SteamManager not initialized.");
                return;
            }

            lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            lobbyEnter = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        }

        public void CreateLobby()
        {
            if (_networkManager.ServerManager.Started)
            {
                Debug.LogWarning("Server already running.");
                return;
            }

            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 3);
        }

        private void OnLobbyCreated(LobbyCreated_t cb)
        {
            if (_isReturningToMenu) return;
            OnLobbyCreatedAsync(cb).Forget();
        }

        private async UniTask OnLobbyCreatedAsync(LobbyCreated_t cb)
        {
            if (cb.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogError($"CreateLobby failed: {cb.m_eResult}");
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.SceneTransitionFx != null)
                await GameManager.Instance.SceneTransitionFx.FadeOutAndHoldAsync();

            lobbyId = new CSteamID(cb.m_ulSteamIDLobby);
            SetLobbyJoinable_HostOnly(true);

            if (GameManager.Instance != null && GameManager.Instance.SettingsManager != null)
                GameManager.Instance.SettingsManager.SetSteamLobbyHost(true);

            // FishNet 호스트 시작
            _networkManager.ServerManager.StartConnection();
            _networkManager.ClientManager.StartConnection();

            int which = await UniTask.WhenAny(
                UniTask.WaitUntil(() => _networkManager.ServerManager.Started),
                UniTask.Delay(TimeSpan.FromSeconds(6))
            );

            if (which == 1)
            {
                Debug.LogError("Server failed to start (timeout).");
                return;
            }

            // 로비 씬의 global load
            SceneLoadData sld = new(lobbySceneName)
            {
                ReplaceScenes = ReplaceOption.All
            };
            InstanceFinder.SceneManager.LoadGlobalScenes(sld);
        }

        // Steam 초대 수락 후 대상 로비 참가
        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
        {
            if (_networkManager != null &&
                (_networkManager.ServerManager.Started || _networkManager.ClientManager.Started))
                return;

            SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
        }

        private void OnLobbyEntered(LobbyEnter_t callback)
        {
            OnLobbyEnteredAsync(callback).Forget();
        }

        // Steam 로비 정보로 FishNet과 Vivox 세션 연결
        private async UniTaskVoid OnLobbyEnteredAsync(LobbyEnter_t callback)
        {
            if (_isReturningToMenu) return;

            if (GameManager.Instance != null && GameManager.Instance.SceneTransitionFx != null)
                await GameManager.Instance.SceneTransitionFx.FadeOutAndHoldAsync();

            lobbyId = new CSteamID(callback.m_ulSteamIDLobby);

            bool steamHost = IsLocalLobbyOwner;
            if (GameManager.Instance != null && GameManager.Instance.SettingsManager != null)
                GameManager.Instance.SettingsManager.SetSteamLobbyHost(steamHost);

            if (steamHost)
                SetLobbyJoinable_HostOnly(true);

            if (!steamHost)
            {
                CSteamID hostId = SteamMatchmaking.GetLobbyOwner(lobbyId);
                _networkManager.ClientManager.StartConnection(hostId.ToString());
            }

            int networkWaitResult = await UniTask.WhenAny(
                UniTask.WaitUntil(() =>
                    steamHost
                        ? _networkManager.ServerManager.Started && _networkManager.ClientManager.Started
                        : _networkManager.ClientManager.Started),
                UniTask.Delay(TimeSpan.FromSeconds(6))
            );

            if (networkWaitResult == 1)
            {
                Debug.LogError("FishNet session failed to start (timeout).");
                return;
            }

            // FishNet 연결 이후 Steam 로비 ID를 공통 음성 채널로 사용
            if (VivoxBootstrap.Instance != null && !VivoxBootstrap.Instance.IsShuttingDown)
            {
                try
                {
                    await VivoxBootstrap.Instance.EnsureVivoxReadyAsync(default);
                    if (!VivoxBootstrap.Instance.IsShuttingDown)
                        await VivoxBootstrap.Instance.JoinVoiceChannelAsync(lobbyId.ToString());
                }
                catch (Exception e)
                {
                    Debug.LogError($"Vivox join failed: {e}");
                }
            }
        }

        public async UniTask ReturnToMainMenuAsync()
        {
            if (_isReturningToMenu) return;

            if (GameManager.Instance != null && GameManager.Instance.SceneTransitionFx != null)
                await GameManager.Instance.SceneTransitionFx.FadeOutAndHoldAsync();

            LastExitReason = NetworkExitReason.UserRequested;
            _isReturningToMenu = true;

            if (GameManager.Instance != null && GameManager.Instance.SettingsManager != null)
                GameManager.Instance.SettingsManager.SetSteamLobbyHost(false);

            // Vivox 채널 종료
            if (VivoxBootstrap.Instance != null)
                try
                {
                    await VivoxBootstrap.Instance.LeaveAllChannelAsync();
                    await UniTask.Delay(TimeSpan.FromMilliseconds(600));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SteamLobby] Vivox leave failed: {e}");
                }

            // FishNet과 transport의 순차 종료
            if (_networkManager != null)
            {
                bool wasClient = _networkManager.ClientManager.Started;
                bool wasServer = _networkManager.ServerManager.Started;

                if (wasClient) _networkManager.ClientManager.StopConnection();
                if (wasServer) _networkManager.ServerManager.StopConnection(true);

                UniTask waitFishNet = UniTask.WaitUntil(() =>
                    !_networkManager.ClientManager.Started && !_networkManager.ServerManager.Started
                );

                Transport transport = _networkManager.TransportManager.Transport;
                UniTask waitTransport = UniTask.WaitUntil(() =>
                    transport.GetConnectionState(false) == LocalConnectionState.Stopped &&
                    transport.GetConnectionState(true) == LocalConnectionState.Stopped
                );

                UniTask timeout = UniTask.Delay(TimeSpan.FromSeconds(3));

                await UniTask.WhenAny(
                    UniTask.WhenAll(waitFishNet, waitTransport),
                    timeout
                );
            }

            FindFirstObjectByType<ConnectionLimiter>()?.Unlock();

            // Steam 로비 종료
            if (lobbyId.IsValid())
            {
                SteamMatchmaking.LeaveLobby(lobbyId);
                lobbyId = default;
            }

            if (!string.IsNullOrEmpty(mainMenuSceneName))
                UnitySceneManager.LoadScene(mainMenuSceneName);

            await UniTask.NextFrame();

            if (GameManager.Instance != null && GameManager.Instance.SceneTransitionFx != null)
                await GameManager.Instance.SceneTransitionFx.FadeInWithMinimumHoldAsync();
        }

        public void SetLobbyJoinable_HostOnly(bool joinable)
        {
            if (!SteamManager.Initialized) return;
            if (!lobbyId.IsValid() || lobbyId.m_SteamID == 0) return;
            if (!IsLocalLobbyOwner) return;

            SteamMatchmaking.SetLobbyJoinable(lobbyId, joinable);
        }

        public static void TrySetLobbyJoinable_HostOnly(bool joinable)
        {
            SteamLobby sl = FindFirstObjectByType<SteamLobby>(FindObjectsInactive.Include);
            sl?.SetLobbyJoinable_HostOnly(joinable);
        }
    }
}
