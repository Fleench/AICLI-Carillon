using System.Security.Cryptography;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Spotify_Playlist_Manager.Models
{
    static class SpotifyWorker
    {
        private static string ClientID;
        private static string ClientSecret;
        private static string AccessToken;
        private static string RefreshToken;
        private static string Uri = "http://127.0.0.1:5543/callback";
        private static int port = 5543;
        private static DateTime Expires = DateTime.MinValue;
        private static EmbedIOAuthServer _server;
        //set up the module
        public static void Init(string ck, string cs, string at = "", string rt = "", DateTime e = new DateTime())
        {
            ClientID = ck;
            ClientSecret = cs;
            if (at != "")
            {
                AccessToken = at;
            }

            if (rt != "")
            {
                RefreshToken = rt;
            }

            if (e != DateTime.MinValue)
            {
                Expires = e;
            }
        }

        //Authorize with Spotify
        public static async Task<(string AccessToken, string RefreshToken)> AuthenticateAsync()
        {
            if (string.IsNullOrEmpty(RefreshToken) || string.IsNullOrEmpty(AccessToken))
            {
                var (accessToken, refreshToken, expiresAt) = await AuthenticateFlowAsync(ClientID, ClientSecret);

                AccessToken = accessToken;
                RefreshToken = refreshToken;
                Expires = expiresAt;
            }
            else
            {
                var (accessToken, refreshToken, expiresAt) = await RefreshTokensIfNeededAsync(
                    ClientID,
                    ClientSecret,
                    AccessToken,
                    RefreshToken,
                    Expires
                );

                AccessToken = accessToken;
                RefreshToken = refreshToken;
                Expires = expiresAt;
            }

            return (AccessToken, RefreshToken);
        }

        //refresh if needed
        public static async Task<(string accessToken, string refreshToken, DateTime expiresAt)> RefreshTokensIfNeededAsync(
            string clientId,
            string clientSecret,
            string accessToken,
            string refreshToken,
            DateTime expiresAtUtc,
            TimeSpan? refreshThreshold = null)
        {
            // Default to refreshing if token expires within the next 5 minutes
            var threshold = refreshThreshold ?? TimeSpan.FromMinutes(5);

            if (expiresAtUtc == DateTime.MinValue)
            {
                // Token expiration is unknown, force a refresh
            }
            else if (DateTime.UtcNow < expiresAtUtc - threshold)
            {
                // Token is still valid — no need to refresh
                return (accessToken, refreshToken, expiresAtUtc);
            }

            // Token is expired or about to expire — refresh it
            var config = SpotifyClientConfig.CreateDefault();
            var tokenResponse = await new OAuthClient(config).RequestToken(
                new AuthorizationCodeRefreshRequest(clientId, clientSecret, refreshToken)
            );

            var newAccessToken = tokenResponse.AccessToken;
            var newRefreshToken = tokenResponse.RefreshToken ?? refreshToken;
            var newExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            return (newAccessToken, newRefreshToken, newExpiresAt);
        }


        public static async Task<(string accessToken, string refreshToken, DateTime expiresAt)> AuthenticateFlowAsync(string clientId, string clientSecret)
        {
            _server = new EmbedIOAuthServer(new Uri(Uri), port);
            await _server.Start();

            var tcs = new TaskCompletionSource<(string, string, DateTime)>();

            _server.AuthorizationCodeReceived += async (sender, response) =>
            {
                await _server.Stop();

                var config = SpotifyClientConfig.CreateDefault();
                var tokenResponse = await new OAuthClient(config).RequestToken(
                    new AuthorizationCodeTokenRequest(clientId, clientSecret, response.Code, new Uri(Uri))
                );

                var expiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                tcs.SetResult((tokenResponse.AccessToken, tokenResponse.RefreshToken, expiresAt));
            };

            _server.ErrorReceived += async (sender, error, state) =>
            {
                await _server.Stop();
                tcs.SetException(new Exception($"Spotify auth error: {error}"));
            };

            var request = new LoginRequest(_server.BaseUri, clientId, LoginRequest.ResponseType.Code)
            {
                Scope = new List<string> { Scopes.UserReadEmail }
            };
            BrowserUtil.Open(request.ToUri());

            return await tcs.Task;
        }

    }
}