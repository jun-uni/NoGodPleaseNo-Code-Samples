using System;
using System.Threading;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Vivox;
using Cysharp.Threading.Tasks;

namespace NGPN.Gameplay
{
    // UGS 인증과 Vivox 초기화, 포지셔널 채널 참가
    public class VivoxBootstrap : MonoBehaviour
    {
        public static VivoxBootstrap Instance { get; private set; }


        private string server;
        private string domain;
        private string issuer;
        private string lambdaUrl;
        private string lambdaApiKey;

        [Header("Vivox Settings")] [SerializeField]
        private VivoxServiceConfig vivoxConfig;

        private readonly SemaphoreSlim _readyGate = new(1, 1);
        private bool _initialized;
        private bool _loggedIn;

        // 조인/업데이터 결합 상태
        private string _pendingChannelName;
        private float _pendingInterval;
        private bool _channelJoined;
        private VivoxPositionUpdater _localUpdater;
        private bool _configuredOnce;

        private bool _shuttingDown = false;
        private bool _joiningChannel = false;
        public bool IsShuttingDown => _shuttingDown;

        public event Action<string> OnVoiceChannelJoined;
        public event Action OnVoiceChannelLeft;

        public bool IsAnyChannelJoined => _channelJoined;


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            server = vivoxConfig.Server;
            domain = vivoxConfig.Domain;
            issuer = vivoxConfig.Issuer;
            lambdaUrl = vivoxConfig.LambdaUrl;
            lambdaApiKey = vivoxConfig.AppKey;

            Application.runInBackground = true;
        }


        // UGS와 Vivox 초기화 및 로그인
        public async UniTask EnsureVivoxReadyAsync(CancellationToken ct)
        {
            if (_shuttingDown)
                return;

            await _readyGate.WaitAsync(ct);
            try
            {
                if (_shuttingDown)
                    return;

                // Unity Services API 접근은 메인 스레드에서 실행
                await UniTask.SwitchToMainThread(ct);

                if (!_initialized)
                {
                    InitializationOptions initOpts = new InitializationOptions()
                        .SetVivoxCredentials(server, domain, issuer);

                    await UnityServices.InitializeAsync(initOpts);

                    // Vivox 초기화 전에 Authentication 사용자 확정
                    if (!AuthenticationService.Instance.IsSignedIn)
                        await AuthenticationService.Instance.SignInAnonymouslyAsync();

                    LambdaTokenProvider tp = new(
                        (lambdaUrl ?? string.Empty).Trim().Trim('\"', '\''),
                        string.IsNullOrWhiteSpace(lambdaApiKey) ? null : lambdaApiKey,
                        issuer, domain
                    );
                    VivoxService.Instance.SetTokenProvider(tp);

                    await VivoxService.Instance.InitializeAsync();
                    _initialized = true;
                }
                else if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                await LoginAsync(ct);
            }
            finally
            {
                _readyGate.Release();
            }
        }

        private async UniTask LoginAsync(CancellationToken ct)
        {
            if (VivoxService.Instance.IsLoggedIn)
            {
                _loggedIn = true;
                return;
            }

            _loggedIn = false;
            ct.ThrowIfCancellationRequested();

            string displayName;
            try
            {
                string pid = AuthenticationService.Instance.PlayerId;
                displayName =
                    $"Player-{(string.IsNullOrEmpty(pid) ? "Anon" : pid.Substring(0, Math.Min(6, pid.Length)))}";
            }
            catch
            {
                displayName = "Player-Anon";
            }

            LoginOptions loginOptions = new() { DisplayName = displayName };
            await VivoxService.Instance.LoginAsync(loginOptions);

            if (!VivoxService.Instance.IsLoggedIn)
                throw new InvalidOperationException("Vivox login completed without entering the logged-in state.");

            _loggedIn = true;
        }

        // 포지셔널 음성 채널 참가
        public async UniTask JoinVoiceChannelAsync(string channelName, CancellationToken ct = default)
        {
            // 종료 중 재접속 차단
            if (_shuttingDown)
            {
                Debug.LogWarning("[VivoxBootstrap] JoinVoiceChannelAsync called while shutting down. Ignored.");
                return;
            }

            // 중복 채널 참가 차단
            if (_joiningChannel)
            {
                Debug.LogWarning(
                    "[VivoxBootstrap] JoinVoiceChannelAsync called while another join is in progress. Ignored.");
                return;
            }

            _joiningChannel = true;
            try
            {
                if (string.IsNullOrWhiteSpace(channelName))
                    throw new ArgumentException("channelName is empty");

                // Vivox 초기화와 로그인 보장
                await EnsureVivoxReadyAsync(ct);

                if (_shuttingDown)
                    return;

                // 기존 채널 정리
                if (VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn)
                    try
                    {
                        await VivoxService.Instance.LeaveAllChannelsAsync();
                    }
                    catch
                    {
                        // 이전 채널이 없는 경우 정리 실패 허용
                    }

                // 비동기 초기화 이후 종료 상태 재확인
                if (_shuttingDown)
                    return;

                // 채널 연결 상태 초기화
                _pendingChannelName = channelName;
                _pendingInterval = 0.3f;
                _channelJoined = false;
                _configuredOnce = false;

                // 포지셔널 채널 참가
                Channel3DProperties props = new(
                    90,
                    3,
                    1.3f,
                    AudioFadeModel.LinearByDistance
                );

                await VivoxService.Instance.JoinPositionalChannelAsync(
                    channelName,
                    ChatCapability.AudioOnly,
                    props,
                    null
                );

                GameManager.Instance?.VoiceChat?.SetChannelName(channelName);
                GameManager.Instance?.VoiceChat?.ResetAllStates();
                GameManager.Instance?.VoiceChat?.ClearAllAvatarMappings();


                _channelJoined = true;
                TryConfigureLocalUpdater();
                OnVoiceChannelJoined?.Invoke(channelName);
            }
            finally
            {
                _joiningChannel = false;
            }
        }

        // 로컬 플레이어의 위치 갱신기 연결
        public void OnLocalUpdaterReady(VivoxPositionUpdater up)
        {
            if (up == null) return;
            _localUpdater = up;
            TryConfigureLocalUpdater();
        }

        // 채널 참가와 위치 갱신기 준비 이후 단일 구성
        private void TryConfigureLocalUpdater()
        {
            if (_configuredOnce) return;
            if (!_channelJoined) return;
            if (_localUpdater == null) return;
            if (string.IsNullOrEmpty(_pendingChannelName)) return;

            _localUpdater.enabled = true;
            _localUpdater.ConfigureForChannel(_pendingChannelName, _pendingInterval);

            // 채널 참가 직후 초기 위치 전송
            VivoxService.Instance.Set3DPosition(_localUpdater.gameObject, _pendingChannelName, allowPanning: false);

            _configuredOnce = true;
        }
    }
}
