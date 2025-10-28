/* File: SpotifyWorker2.cs
 * Author: ChatGPT Codex
 * Description: A guarded wrapper for the SpotifyAPI.Web module that mirrors
 *              SpotifyWorker functionality while coordinating access through
 *              a singleton Spotify session.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;

namespace Spotify_Playlist_Manager.Models
{
    static class SpotifyWorker2
    {
        private static string ClientID;
        private static string ClientSecret;
        private static readonly string Uri = "http://127.0.0.1:5543/callback";
        private static readonly int Port = 5543;

        public static void Init(string ck, string cs, string at = "", string rt = "", DateTime e = new DateTime())
        {
            ClientID = ck;
            ClientSecret = cs;

            DateTime? expires = e == DateTime.MinValue ? null : e;

            SpotifySession.Instance.Initialize(
                ck,
                cs,
                string.IsNullOrEmpty(at) ? null : at,
                string.IsNullOrEmpty(rt) ? null : rt,
                expires
            );
        }

        public static async Task<(string AccessToken, string RefreshToken)> AuthenticateAsync()
        {
            if (string.IsNullOrEmpty(ClientID) || string.IsNullOrEmpty(ClientSecret))
            {
                throw new InvalidOperationException("SpotifyWorker2.Init must be called before AuthenticateAsync.");
            }

            var session = SpotifySession.Instance;

            if (!session.HasTokens)
            {
                var (accessToken, refreshToken, expiresAt) = await AuthenticateFlowAsync(ClientID, ClientSecret);
                session.Initialize(ClientID, ClientSecret, accessToken, refreshToken, expiresAt);
            }
            else
            {
                await session.GetClientAsync();
            }

            var snapshot = session.GetTokenSnapshot();
            return (snapshot.AccessToken, snapshot.RefreshToken);
        }

        public static async Task<(string accessToken, string refreshToken, DateTime expiresAt)> RefreshTokensIfNeededAsync(
            string clientId,
            string clientSecret,
            string accessToken,
            string refreshToken,
            DateTime expiresAtUtc,
            TimeSpan? refreshThreshold = null)
        {
            var threshold = refreshThreshold ?? TimeSpan.FromMinutes(5);

            if (expiresAtUtc != DateTime.MinValue && DateTime.UtcNow < expiresAtUtc - threshold)
            {
                return (accessToken, refreshToken, expiresAtUtc);
            }

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
            using var server = new EmbedIOAuthServer(new Uri(Uri), Port);
            await server.Start();

            var tcs = new TaskCompletionSource<(string, string, DateTime)>();

            server.AuthorizationCodeReceived += async (sender, response) =>
            {
                await server.Stop();

                var config = SpotifyClientConfig.CreateDefault();
                var tokenResponse = await new OAuthClient(config).RequestToken(
                    new AuthorizationCodeTokenRequest(clientId, clientSecret, response.Code, new Uri(Uri))
                );

                var expiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                tcs.TrySetResult((tokenResponse.AccessToken, tokenResponse.RefreshToken, expiresAt));
            };

            server.ErrorReceived += async (sender, error, state) =>
            {
                await server.Stop();
                tcs.TrySetException(new Exception($"Spotify auth error: {error}"));
            };

            var request = new LoginRequest(server.BaseUri, clientId, LoginRequest.ResponseType.Code)
            {
                Scope = new List<string>
                {
                    Scopes.UserReadEmail,
                    Scopes.PlaylistModifyPrivate,
                    Scopes.PlaylistModifyPublic,
                    Scopes.PlaylistReadCollaborative,
                    Scopes.PlaylistReadPrivate,
                    Scopes.UserLibraryRead,
                    Scopes.UserLibraryModify,
                    Scopes.UserReadPrivate
                }
            };

            BrowserUtil.Open(request.ToUri());

            return await tcs.Task.ConfigureAwait(false);
        }

        public static async IAsyncEnumerable<(string Id, string Name, string Artists)> GetLikedSongsAsync()
        {
            var spotify = await SpotifySession.Instance.GetClientAsync();
            var page = await spotify.Library.GetTracks(new LibraryTracksRequest { Limit = 50 });

            while (page != null && page.Items.Count > 0)
            {
                foreach (var item in page.Items)
                {
                    var track = item.Track;

                    if (track == null)
                    {
                        continue;
                    }

                    yield return (
                        track.Id,
                        track.Name,
                        string.Join(";;", track.Artists.Select(a => a.Id))
                    );
                }

                try
                {
                    page = await spotify.NextPage(page);
                }
                catch
                {
                    break;
                }
            }
        }

        public static async IAsyncEnumerable<(string Id, string Name, int TrackCount)> GetUserPlaylistsAsync()
        {
            var spotify = await SpotifySession.Instance.GetClientAsync();
            var page = await spotify.Playlists.CurrentUsers(new PlaylistCurrentUsersRequest { Limit = 50 });

            while (page != null && page.Items.Count > 0)
            {
                foreach (var playlist in page.Items)
                {
                    yield return (
                        playlist.Id,
                        playlist.Name,
                        playlist.Tracks?.Total ?? 0
                    );
                }

                try
                {
                    page = await spotify.NextPage(page);
                }
                catch
                {
                    break;
                }
            }
        }

        public static async IAsyncEnumerable<(string Id, string Name, int TrackCount, string Artists)> GetUserAlbumsAsync()
        {
            var spotify = await SpotifySession.Instance.GetClientAsync();
            var page = await spotify.Library.GetAlbums(new LibraryAlbumsRequest { Limit = 50 });

            while (page != null && page.Items.Count > 0)
            {
                foreach (var album in page.Items)
                {
                    yield return (
                        album.Album.Id,
                        album.Album.Name,
                        album.Album.Tracks?.Total ?? 0,
                        string.Join(";;", album.Album.Artists.Select(a => a.Id))
                    );
                }

                try
                {
                    page = await spotify.NextPage(page);
                }
                catch
                {
                    break;
                }
            }
        }

        public static async Task<(string? name, string? imageURL, string? Id, string? Description, string? SnapshotID, string? TrackIDs)> GetPlaylistDataAsync(string id)
        {
            var spotify = await SpotifySession.Instance.GetClientAsync();
            FullPlaylist playlist = await spotify.Playlists.Get(id);
            string trackIDs = string.Empty;
            string? imageURL = string.Empty;

            try
            {
                imageURL = playlist.Images[0].Url;
            }
            catch
            {
                imageURL = string.Empty;
            }

            foreach (PlaylistTrack<IPlayableItem> item in playlist.Tracks.Items)
            {
                if (item.Track is FullTrack track)
                {
                    trackIDs += track.Id + ";;";
                }
            }

            return (playlist.Name, imageURL, playlist.Id, playlist.Description, playlist.SnapshotId, trackIDs);
        }

        public static async Task<(string? name, string? imageURL, string? Id, string TrackIDs, string artistIDs)> GetAlbumDataAsync(string id)
        {
            var spotify = await SpotifySession.Instance.GetClientAsync();
            FullAlbum album = await spotify.Albums.Get(id);
            string trackIDs = string.Empty;
            string imageURL = string.Empty;
            string artistIDs = string.Empty;

            try
            {
                imageURL = album.Images[0].Url;
            }
            catch
            {
                imageURL = string.Empty;
            }

            foreach (SimpleTrack track in album.Tracks.Items)
            {
                trackIDs += track.Id + ";;";
            }

            foreach (SimpleArtist artist in album.Artists)
            {
                artistIDs += artist.Id + ";;";
            }

            return (album.Name, imageURL, album.Id, trackIDs, artistIDs);
        }

        public static async Task<(string name, string id, string albumID, string artistIDs, int discnumber, int durrationms, bool Explicit, string previewURL, int tracknumber)> GetSongDataAsync(string id)
        {
            var spotify = await SpotifySession.Instance.GetClientAsync();
            FullTrack track = await spotify.Tracks.Get(id);
            string artistIDs = string.Join("::", track.Artists.Select(a => a.Id));
            return (
                track.Name,
                track.Id,
                track.Album.Id,
                artistIDs,
                track.DiscNumber,
                track.DurationMs,
                track.Explicit,
                track.PreviewUrl,
                track.TrackNumber
            );
        }

        public static async Task<(string Id, string Name, string? ImageUrl, string Genres)> GetArtistDataAsync(string id)
        {
            var spotify = await SpotifySession.Instance.GetClientAsync();
            var artist = await spotify.Artists.Get(id);
            string? imageUrl = artist.Images?.FirstOrDefault()?.Url;
            string genres = artist.Genres != null && artist.Genres.Any()
                ? string.Join(";;", artist.Genres)
                : string.Empty;

            return (artist.Id, artist.Name, imageUrl, genres);
        }

        public static async Task AddTracksToPlaylistAsync(string playlistId, List<string> trackIds)
        {
            var spotify = await SpotifySession.Instance.GetClientAsync();
            var uris = trackIds.ConvertAll(id => $"spotify:track:{id}");
            var request = new PlaylistAddItemsRequest(uris);
            await spotify.Playlists.AddItems(playlistId, request);
        }
    }

    sealed class SpotifySession
    {
        private static readonly Lazy<SpotifySession> _instance = new(() => new SpotifySession());

        private SpotifyClient? _client;
        private string? _accessToken;
        private string? _refreshToken;
        private DateTime _expiresAt = DateTime.MinValue;
        private string? _clientId;
        private string? _clientSecret;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _configured;

        public static SpotifySession Instance => _instance.Value;

        private SpotifySession()
        {
        }

        public bool HasTokens => !string.IsNullOrEmpty(_accessToken) && !string.IsNullOrEmpty(_refreshToken);

        public void Initialize(string clientId, string clientSecret, string? accessToken = null, string? refreshToken = null, DateTime? expiresAt = null)
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
            _configured = true;

            if (!string.IsNullOrEmpty(accessToken))
            {
                _accessToken = accessToken;
                _client = new SpotifyClient(accessToken);
            }

            if (!string.IsNullOrEmpty(refreshToken))
            {
                _refreshToken = refreshToken;
            }

            if (expiresAt.HasValue && expiresAt.Value != DateTime.MinValue)
            {
                _expiresAt = expiresAt.Value;
            }
        }

        public (string AccessToken, string RefreshToken, DateTime ExpiresAt) GetTokenSnapshot()
        {
            return (
                _accessToken ?? string.Empty,
                _refreshToken ?? string.Empty,
                _expiresAt
            );
        }

        public async Task<SpotifyClient> GetClientAsync()
        {
            if (!_configured)
            {
                throw new InvalidOperationException("SpotifySession has not been configured. Call SpotifyWorker2.Init first.");
            }

            await _lock.WaitAsync().ConfigureAwait(false);

            try
            {
                if (string.IsNullOrEmpty(_accessToken) || string.IsNullOrEmpty(_refreshToken))
                {
                    throw new InvalidOperationException("SpotifySession is missing tokens. Call SpotifyWorker2.AuthenticateAsync to establish a session.");
                }

                var threshold = TimeSpan.FromMinutes(5);
                bool needsRefresh = _expiresAt == DateTime.MinValue || DateTime.UtcNow >= _expiresAt - threshold;

                if (needsRefresh)
                {
                    if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
                    {
                        throw new InvalidOperationException("SpotifySession is missing client credentials required for token refresh.");
                    }

                    var (newAccessToken, newRefreshToken, newExpiresAt) = await SpotifyWorker2.RefreshTokensIfNeededAsync(
                        _clientId,
                        _clientSecret,
                        _accessToken,
                        _refreshToken,
                        _expiresAt,
                        threshold
                    ).ConfigureAwait(false);

                    _accessToken = newAccessToken;
                    _refreshToken = newRefreshToken;
                    _expiresAt = newExpiresAt;
                    _client = new SpotifyClient(_accessToken);
                }
                else if (_client == null)
                {
                    _client = new SpotifyClient(_accessToken);
                }

                return _client;
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
