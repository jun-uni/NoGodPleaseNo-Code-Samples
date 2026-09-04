using UnityEngine;

namespace NGPN.Gameplay
{
    // Vivox와 Lambda 토큰 서버 설정
    [CreateAssetMenu(fileName = "VivoxServiceConfig",
        menuName = "Game/Networking/Vivox Service Config", order = 0)]
    public class VivoxServiceConfig : ScriptableObject
    {
        [Header("Lambda Token Server")]
        [Tooltip("토큰 발급 Lambda의 HTTPS 엔드포인트")]
        [SerializeField]
        private string lambdaUrl;

        [Tooltip("x-game-key (긴급 차단과 사용량 제어용, 사용자 인증 수단 아님)")] [SerializeField]
        private string appKey;

        [Header("Vivox")] [Tooltip("VIVOX_ISSUER")] [SerializeField]
        private string issuer;

        [Tooltip("VIVOX_DOMAIN")] [SerializeField]
        private string domain;

        [Tooltip("VIVOX_SERVER AppConfig URL")] [SerializeField]
        private string server;

        // Lambda 토큰 발급 endpoint
        public string LambdaUrl => lambdaUrl;

        // 긴급 차단과 사용량 제어용 x-game-key 값
        public string AppKey => appKey;

        // Vivox issuer ID
        public string Issuer => issuer;

        // Vivox 서비스 domain
        public string Domain => domain;

        // Vivox AppConfig URL
        public string Server => server;
    }
}
