using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Core;
using UnityEngine;

namespace ProjectM.Auth
{
    // Unity Gaming Services Authentication 래퍼.
    // 게스트(Anonymous) / Unity Player Accounts 로그인을 제공한다.
    public class UnityAuthService : MonoBehaviour
    {
        private static Task initializeTask;
        private TaskCompletionSource<string> playerAccountSignInTcs;
        private bool playerAccountEventsRegistered;

        private void OnDestroy()
        {
            UnregisterPlayerAccountEvents();
        }

        public async Task InitializeAsync()
        {
            if (initializeTask != null)
            {
                await initializeTask;
                return;
            }

            initializeTask = UnityServices.InitializeAsync();
            await initializeTask;
        }

        public async Task<string> SignInAsGuestAsync()
        {
            await InitializeAsync();

            if (AuthenticationService.Instance.IsSignedIn)
                AuthenticationService.Instance.SignOut(true);

            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            return AuthenticationService.Instance.PlayerId;
        }

        public async Task<string> SignInWithPlayerAccountAsync()
        {
            await InitializeAsync();
            RegisterPlayerAccountEvents();

            if (AuthenticationService.Instance.IsSignedIn)
                AuthenticationService.Instance.SignOut(true);

            playerAccountSignInTcs = new TaskCompletionSource<string>();

            try
            {
                await PlayerAccountService.Instance.StartSignInAsync();
            }
            catch (Exception ex)
            {
                playerAccountSignInTcs = null;
                throw new InvalidOperationException($"Unity Player Accounts 로그인 시작 실패: {ex.Message}", ex);
            }

            return await playerAccountSignInTcs.Task;
        }

        private void RegisterPlayerAccountEvents()
        {
            if (playerAccountEventsRegistered) return;

            PlayerAccountService.Instance.SignedIn += OnPlayerAccountSignedIn;
            PlayerAccountService.Instance.SignInFailed += OnPlayerAccountSignInFailed;
            playerAccountEventsRegistered = true;
        }

        private void UnregisterPlayerAccountEvents()
        {
            if (!playerAccountEventsRegistered) return;

            PlayerAccountService.Instance.SignedIn -= OnPlayerAccountSignedIn;
            PlayerAccountService.Instance.SignInFailed -= OnPlayerAccountSignInFailed;
            playerAccountEventsRegistered = false;
        }

        private async void OnPlayerAccountSignedIn()
        {
            try
            {
                await Task.Delay(100);

                if (AuthenticationService.Instance.IsSignedIn)
                    AuthenticationService.Instance.SignOut(true);

                await AuthenticationService.Instance.SignInWithUnityAsync(PlayerAccountService.Instance.AccessToken);
                playerAccountSignInTcs?.TrySetResult(AuthenticationService.Instance.PlayerId);
            }
            catch (Exception ex)
            {
                playerAccountSignInTcs?.TrySetException(ex);
            }
            finally
            {
                playerAccountSignInTcs = null;
            }
        }

        private void OnPlayerAccountSignInFailed(Unity.Services.Core.RequestFailedException ex)
        {
            playerAccountSignInTcs?.TrySetException(ex);
            playerAccountSignInTcs = null;
        }
    }
}
