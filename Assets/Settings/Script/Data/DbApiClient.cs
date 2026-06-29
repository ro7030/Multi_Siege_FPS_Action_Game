using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ProjectM.Data
{
    /// <summary>
    /// 백엔드 API Layer와 통신하는 HTTP 클라이언트.
    /// UnityWebRequest 기반. 모든 요청은 비동기 코루틴.
    /// baseUrl이 비어있으면 자동으로 실패 처리되어 ResultUploader가 로컬 폴백을 사용한다.
    /// </summary>
    public class DbApiClient : MonoBehaviour
    {
        [SerializeField] private string baseUrl = ""; // 예: "http://127.0.0.1:8080"
        [SerializeField] private int timeoutSeconds = 5;
        [SerializeField] private string authToken = ""; // 로그인 후 발급된 Bearer 토큰

        public string BaseUrl { get => baseUrl; set => baseUrl = value; }
        public string AuthToken { get => authToken; set => authToken = value; }
        public bool IsConfigured => !string.IsNullOrEmpty(baseUrl);

        public IEnumerator PostJson(string endpoint, object body, Action<string> onSuccess, Action<string> onError)
        {
            if (!IsConfigured)
            {
                onError?.Invoke("baseUrl 미설정");
                yield break;
            }

            string url = JoinUrl(baseUrl, endpoint);
            string json = body == null ? "{}" : JsonUtility.ToJson(body);

            using var req = new UnityWebRequest(url, "POST");
            byte[] payload = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(payload);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(authToken))
                req.SetRequestHeader("Authorization", $"Bearer {authToken}");
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke(req.downloadHandler.text);
            }
            else
            {
                onError?.Invoke($"{req.responseCode} {req.error}");
            }
        }

        public IEnumerator GetJson(string endpoint, Action<string> onSuccess, Action<string> onError)
        {
            if (!IsConfigured) { onError?.Invoke("baseUrl 미설정"); yield break; }
            string url = JoinUrl(baseUrl, endpoint);

            using var req = UnityWebRequest.Get(url);
            if (!string.IsNullOrEmpty(authToken))
                req.SetRequestHeader("Authorization", $"Bearer {authToken}");
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success) onSuccess?.Invoke(req.downloadHandler.text);
            else onError?.Invoke($"{req.responseCode} {req.error}");
        }

        private static string JoinUrl(string baseUrl, string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint)) return baseUrl;
            if (baseUrl.EndsWith("/") && endpoint.StartsWith("/")) return baseUrl + endpoint.Substring(1);
            if (!baseUrl.EndsWith("/") && !endpoint.StartsWith("/")) return baseUrl + "/" + endpoint;
            return baseUrl + endpoint;
        }
    }
}
