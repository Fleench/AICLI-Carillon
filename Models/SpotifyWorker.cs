using System.Security.Cryptography;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Spotify_Playlist_Manager.Models
{
    /// <summary>
    /// A static class to handle Spotify authentication using the SpotifyAPI.Web library.
    /// It manages the OAuth 2.0 authentication flow, including token refreshing.
    /// </summary>
    static class SpotifyWorker
    {
        /// <summary>
        /// The Spotify application client ID.
        /// </summary>
        private static string ClientID;

        /// <summary>
        /// The Spotify application client secret.
        /// </summary>
        private static string ClientSecret;

        /// <summary>
        /// The access token obtained from Spotify.
        /// </summary>
        private static string AccessToken;

        /// <summary>
        /// The refresh token obtained from Spotify, used to get a new access token.
        /// </summary>
        private static string RefreshToken;

        /// <summary>
        /// The redirect URI for the OAuth flow.
        /// </summary>
        private static string Uri = "http://127.0.0.1:5543/callback";

        /// <summary>
        /// The port for the local server used in the OAuth flow.
        /// </summary>
        private static int port = 5543;

        /// <summary>
        /// The expiration date and time of the access token.
        /// </summary>
        private static DateTime Expires = DateTime.MinValue;

        /// <summary>
        /// The server that listens for the OAuth callback.
        /// </summary>
        private static EmbedIOAuthServer _server;

        /// <summary>
        /// Initializes the SpotifyWorker with necessary credentials.
        /// This method should be called before any other methods in this class.
        /// </summary>
        /// <param name="ck">The client key (ID) for your Spotify application.</param>
        /// <param name="cs">The client secret for your Spotify application.</param>
        /// <param name="at">An optional existing access token.</param>
        /// <param name="rt">An optional existing refresh token.</param>
        /// <param name="e">An optional expiration date for the existing access token.</param>
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

        /// <summary>
        /// Authenticates the user with Spotify.
        /// If a refresh token is available, it will try to refresh the access token.
        /// Otherwise, it will start the full OAuth 2.0 authentication flow.
        /// </summary>
        /// <returns>A tuple containing the access token and refresh token.</returns>
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

        /// <summary>
        /// Refreshes the access token if it is expired or about to expire.
        /// </summary>
        /// <param name="clientId">The Spotify application client ID.</param>
        /// <param name="clientSecret">The Spotify application client secret.</param>
        /// <param name="accessToken">The current access token.</param>
        /// <param name="refreshToken">The refresh token.</param>
        /// <param name="expiresAtUtc">The UTC expiration time of the current access token.</param>
        /// <param name="refreshThreshold">The time before expiration to refresh the token. Defaults to 5 minutes.</param>
        /// <returns>A tuple containing the new access token, refresh token, and new expiration time.</returns>
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

        /// <summary>
        /// Performs the full OAuth 2.0 authorization code flow.
        /// It starts a local server to listen for the callback from Spotify,
        /// opens a browser for the user to authorize the application,
        /// and exchanges the authorization code for an access token and refresh token.
        /// </summary>
        /// <param name="clientId">The Spotify application client ID.</param>
        /// <param name="clientSecret">The Spotify application client secret.</param>
        /// <returns>A tuple containing the access token, refresh token, and expiration time.</returns>
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