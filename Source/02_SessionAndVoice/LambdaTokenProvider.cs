using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Unity.Services.Vivox;
using Unity.Services.Authentication;

namespace NGPN.Gameplay
{
    // Lambda 기반 Vivox 토큰 공급자
    public class LambdaTokenProvider : IVivoxTokenProvider
    {
        private readonly string _endpoint;
        private readonly string _apiKey;
        private readonly string _issuerHint;
        private readonly string _domainHint;

        // Lambda 응답 구조
        [Serializable]
        private class TokenResponse
        {
            public string token;
            public string action;
            public long exp;
            public long vxi;
        }

        // Lambda 요청 구조
        [Serializable]
        private class TokenRequest
        {
            public string action;
            public string f;
            public string t;
            public string sub;
            public string userId;
        }

        public LambdaTokenProvider(string endpoint, string apiKey, string issuerHint, string domainHint)
        {
            if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("Lambda endpoint is empty.");

            endpoint = endpoint.Trim().Trim('\"', '\'');
            StringBuilder sb = new(endpoint.Length);
            foreach (char ch in endpoint)
                if (!char.IsControl(ch) && ch != '\u200B' && ch != '\u200E' && ch != '\u200F')
                    sb.Append(ch);
            endpoint = sb.ToString().TrimEnd('/');

            if (!Uri.IsWellFormedUriString(endpoint, UriKind.Absolute))
                throw new ArgumentException($"Lambda endpoint invalid: '{endpoint}'");
            if (!endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Lambda endpoint must start with https://");

            _endpoint = endpoint;
            _apiKey = apiKey;
            _issuerHint = issuerHint ?? "";
            _domainHint = domainHint ?? "";
        }

        // Vivox action별 JWT 요청
        public async Task<string> GetTokenAsync(
            string issuer = null,
            TimeSpan? expiration = null,
            string targetUserUri = null,
            string action = null,
            string channelUri = null,
            string fromUserUri = null,
            string realm = null)
        {
            // issuer와 domain 결정
            string issuerUse = !string.IsNullOrWhiteSpace(_issuerHint) ? _issuerHint : issuer ?? "";
            string domainUse = !string.IsNullOrWhiteSpace(_domainHint)
                ? _domainHint
                : ExtractDomainFromUri(channelUri) ?? ExtractDomainFromUri(fromUserUri) ?? "";

            // UGS PlayerId 확보
            string playerId = null;
            try
            {
                if (AuthenticationService.Instance?.IsSignedIn == true)
                    playerId = AuthenticationService.Instance.PlayerId;
            }
            catch
            {
            }

            if (string.IsNullOrEmpty(playerId))
                playerId = SystemInfo.deviceUniqueIdentifier ?? Guid.NewGuid().ToString("N");

            // Vivox action 정규화
            string act = (action ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(act))
                // URI 유무에 따른 기본 action 결정
                act = string.IsNullOrWhiteSpace(channelUri) ? "login" : "join";

            // Vivox SIP 요청 값 구성
            string f, t, sub;

            if (act == "login")
            {
                f = string.IsNullOrWhiteSpace(fromUserUri)
                    ? BuildUserSip(issuerUse, playerId, domainUse)
                    : EnsureDotBeforeAt(fromUserUri);

                t = null;
                sub = null;
            }
            else
            {
                f = string.IsNullOrWhiteSpace(fromUserUri)
                    ? BuildUserSip(issuerUse, playerId, domainUse)
                    : EnsureDotBeforeAt(fromUserUri);

                t = NormalizeChannelSip(channelUri);
                sub = string.IsNullOrWhiteSpace(targetUserUri) ? null : EnsureDotBeforeAt(targetUserUri);
            }

            TokenRequest req = new()
            {
                action = act,
                f = f,
                t = t,
                sub = sub,
                userId = playerId
            };
            string json = JsonUtility.ToJson(req);

            using UnityWebRequest www = new(_endpoint, "POST");
            www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("content-type", "application/json");
            if (!string.IsNullOrEmpty(_apiKey)) www.SetRequestHeader("x-game-key", _apiKey);

            string idToken = AuthenticationService.Instance?.IsSignedIn == true
                ? AuthenticationService.Instance.AccessToken
                : null;
            if (!string.IsNullOrEmpty(idToken))
                www.SetRequestHeader("authorization", $"Bearer {idToken}");

            UnityWebRequestAsyncOperation op = www.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (www.result != UnityWebRequest.Result.Success)
                throw new Exception(
                    $"Token server HTTP error: {www.responseCode} {www.error} {www.downloadHandler.text}");

            TokenResponse resp = JsonUtility.FromJson<TokenResponse>(www.downloadHandler.text);
            if (resp == null || string.IsNullOrWhiteSpace(resp.token))
                throw new Exception("Token server returned empty token.");

            return resp.token;
        }


        // 사용자 SIP URI 구성
        private static string BuildUserSip(string issuer, string userId, string domain)
        {
            return $"sip:.{issuer}.{userId}.@{domain}";
        }

        // SIP 구분자 보정
        private static string EnsureDotBeforeAt(string sip)
        {
            if (string.IsNullOrWhiteSpace(sip)) return sip;
            string s = sip.Trim();
            int at = s.IndexOf('@');
            if (at > 0 && s[at - 1] != '.') s = s.Insert(at, ".");
            return s;
        }

        // 채널 SIP URI 검증
        private static string NormalizeChannelSip(string sip)
        {
            if (string.IsNullOrWhiteSpace(sip)) return null;
            string s = sip.Trim();
            if (!s.StartsWith("sip:confctl-d-") || !s.Contains("@")) return null;

            return s;
        }

        // SIP URI domain 추출
        private static string ExtractDomainFromUri(string sip)
        {
            if (string.IsNullOrWhiteSpace(sip)) return null;
            int at = sip.IndexOf('@');
            if (at < 0) return null;
            return sip.Substring(at + 1).TrimEnd('>');
        }
    }
}
