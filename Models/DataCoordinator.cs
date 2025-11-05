/* File: DataCoordinator.cs
 * Author: Glenn Sutherland, ChatGPT Codex
 * Description: Connects the SpotifyWorker to the DatabaseWorker
 */

using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using Spotify_Playlist_Manager.Models;
namespace Spotify_Playlist_Manager.Models
{
    public enum SyncEntryPoint
    {
        Playlists = 0,
        Albums = 1,
        LikedSongs = 2,
        TrackMetadata = 3,
        AlbumMetadata = 4,
        ArtistMetadata = 5
    }

    static class DataCoordinator
    {
        private static readonly TimeSpan RateLimitRetryThreshold = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan MinimumRetryDelay = TimeSpan.FromSeconds(1);

        public static async Task SlowSync()
        {
            /* Playlists → Playlist Tracks

            Fetch all user playlists.

            For each playlist, pull its track list and save locally.

            Albums → Album Tracks

            Fetch all saved albums.
    
            For each album, pull its track list and save locally.
    
            Liked Songs

            Fetch all saved (liked) tracks.

            Save them and include extra Spotify API fields that liked tracks provide (like added_at).

            Missing Song Data

            Scan for any tracks without full metadata and request those track details.

            Unique Albums

            From all gathered tracks, create a unique album list that hasn’t already been stored.

            Fetch missing album metadata.

            Unique Artists

            From all gathered tracks and albums, create a unique artist list.

            Fetch and save their metadata.
            */
             //1. Set playlists
            await foreach(var item in SpotifyWorker.GetUserPlaylistsAsync())
            {
                //Console.WriteLine($"{item.Id} - {item.Name}");
                var data = await SpotifyWorker.GetPlaylistDataAsync(item.Id);
                Variables.PlayList playlist = new()
                {
                    Id = data.Id,
        Name = data.name,
        ImageURL = data.imageURL,
        Description = data.Description,
        SnapshotID=data.SnapshotID,
        TrackIDs = data.TrackIDs,
                };
                Variables.PlayList tp = DatabaseWorker.GetPlaylist(playlist.Id);
                if (tp == null || tp.SnapshotID != playlist.SnapshotID)
                {
                    await DatabaseWorker.SetPlaylist(playlist);
                    foreach (string id in SplitIds(playlist.TrackIDs, Variables.Seperator))
                    {
                        await DatabaseWorker.SetTrack(new Variables.Track() {Id = id});
                    }
                }
            }
            Console.WriteLine("Got Playlists");
            //2. Set albums
            await foreach (var item in SpotifyWorker.GetUserAlbumsAsync())
            {
                if (DatabaseWorker.GetAlbum(item.Id) is null)
                {
                    var data = await SpotifyWorker.GetAlbumDataAsync(item.Id);
                    Variables.Album album = new()
                    {
                        Id = data.Id,
                        Name = data.name,
                        ImageURL = data.imageURL,
                        ArtistIDs = data.artistIDs,
                    };
                    foreach (string id in SplitIds(data.TrackIDs, Variables.Seperator))
                    {
                        await DatabaseWorker.SetTrack(new Variables.Track() {Id = id});
                    }
                    await DatabaseWorker.SetAlbum(album); 
                }
                
            }
            Console.WriteLine("Got Albums");
            //3. set liked songs
            await foreach (var item in SpotifyWorker.GetLikedSongsAsync())
            {
                await DatabaseWorker.SetTrack(new Variables.Track() {Id = item.Id});
            }
            Console.WriteLine("Got Liked Songs");
            //update track data
            foreach (var item in DatabaseWorker.GetAllTracks())
            {
                string albumID = item.AlbumId;
                var artistIDs = SplitArtistIds(item.ArtistIds);
                if (item.MissingInfo())
                {
                    var data = await SpotifyWorker.GetSongDataAsync(item.Id);
                    await DatabaseWorker.SetTrack(new Variables.Track()
                    {
                        Id = item.Id,
                        Name = data.name,
                        AlbumId = data.albumID,
                        ArtistIds = data.artistIDs,
                        DiscNumber = data.discnumber,
                        TrackNumber = data.tracknumber,
                        Explicit = data.Explicit,
                        DurationMs = data.durrationms,
                        PreviewUrl = data.previewURL,
                        SongID = item.SongID
                    });
                    albumID = data.albumID;
                    artistIDs = SplitArtistIds(data.artistIDs);
                }

                await DatabaseWorker.SetAlbum(new Variables.Album() {Id= albumID });
                foreach (string id in artistIDs)
                {
                    await DatabaseWorker.SetArtist(new Variables.Artist(){Id = id});
                }
            }
            Console.WriteLine("Got Track Dara");
            foreach (var item in DatabaseWorker.GetAllAlbums())
            {
                var artistIDs = SplitIds(item.ArtistIDs, Variables.Seperator);
                if (item.MissingInfo())
                {
                    var data = await SpotifyWorker.GetAlbumDataAsync(item.Id);
                    await DatabaseWorker.SetAlbum(new Variables.Album()
                    {
                        Id = item.Id,
                        Name = data.name,
                        ImageURL = data.imageURL,
                        ArtistIDs = data.artistIDs
                    });
                    artistIDs = SplitIds(data.artistIDs, Variables.Seperator);
                }
                foreach (string id in artistIDs)
                {
                    await DatabaseWorker.SetArtist(new Variables.Artist(){Id = id});
                }
            }
            Console.WriteLine("Got Album Data");
            foreach (var item in DatabaseWorker.GetAllArtists())
            {
                if (item.MissingInfo())
                {
                    var data = await SpotifyWorker.GetArtistDataAsync(item.Id);
                    await DatabaseWorker.SetArtist(new Variables.Artist()
                    {
                        Id = item.Id,
                        Genres = data.Genres,
                        Name = data.Name,
                        ImageURL = data.ImageUrl
                    });
                }
            }
            Console.WriteLine("Got Artist Data");
        }

        public static async Task Sync(SyncEntryPoint startFrom = SyncEntryPoint.Playlists)
        {
            if (startFrom <= SyncEntryPoint.Playlists)
            {
                await ExecuteWithRetryAsync(SyncPlaylistsAsync, "playlist synchronization");
            }

            if (startFrom <= SyncEntryPoint.Albums)
            {
                await ExecuteWithRetryAsync(SyncAlbumsAsync, "album synchronization");
            }

            if (startFrom <= SyncEntryPoint.LikedSongs)
            {
                await ExecuteWithRetryAsync(SyncLikedSongsAsync, "liked songs synchronization");
            }

            if (startFrom <= SyncEntryPoint.TrackMetadata)
            {
                await ExecuteWithRetryAsync(SyncTrackMetadataAsync, "track metadata synchronization");
            }

            if (startFrom <= SyncEntryPoint.AlbumMetadata)
            {
                await ExecuteWithRetryAsync(SyncAlbumMetadataAsync, "album metadata synchronization");
            }

            if (startFrom <= SyncEntryPoint.ArtistMetadata)
            {
                await ExecuteWithRetryAsync(SyncArtistMetadataAsync, "artist metadata synchronization");
            }
        }

        private static List<string> SplitIds(string? ids, params string[] separators)
        {
            if (string.IsNullOrWhiteSpace(ids))
            {
                return new List<string>();
            }

            var parts = ids.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            List<string> results = new();

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    results.Add(trimmed);
                }
            }

            return results;
        }

        private static List<string> SplitArtistIds(string? ids)
        {
            return SplitIds(ids, "::", Variables.Seperator);
        }

        private static async Task ExecuteWithRetryAsync(Func<Task> operation, string operationName)
        {
            while (true)
            {
                try
                {
                    await operation();
                    return;
                }
                catch (APITooManyRequestsException ex) when (ShouldRetry(ex))
                {
                    var delay = ex.RetryAfter;

                    if (delay <= TimeSpan.Zero)
                    {
                        delay = MinimumRetryDelay;
                    }

                    Console.WriteLine($"Rate limit encountered during {operationName}. Waiting {delay} before retrying.");
                    await Task.Delay(delay);
                }
            }
        }

        private static bool ShouldRetry(APITooManyRequestsException ex)
        {
            var retryAfter = ex.RetryAfter;

            if (retryAfter < TimeSpan.Zero)
            {
                return false;
            }

            return retryAfter < RateLimitRetryThreshold;
        }

        private static async Task SyncPlaylistsAsync()
        {
            var playlistSummaries = new List<(string Id, string Name, int TrackCount)>();
            await foreach (var playlist in SpotifyWorker.GetUserPlaylistsAsync())
            {
                playlistSummaries.Add(playlist);
            }

            var playlistDetails = await SpotifyWorker.GetPlaylistDataBatchAsync(playlistSummaries.Select(p => p.Id));

            foreach (var summary in playlistSummaries)
            {
                if (!playlistDetails.TryGetValue(summary.Id, out var data))
                {
                    continue;
                }

                Variables.PlayList playlist = new()
                {
                    Id = data.Id ?? summary.Id,
                    Name = data.name ?? string.Empty,
                    ImageURL = data.imageURL ?? string.Empty,
                    Description = data.Description ?? string.Empty,
                    SnapshotID = data.SnapshotID ?? string.Empty,
                    TrackIDs = data.TrackIDs ?? string.Empty
                };

                Variables.PlayList? existing = DatabaseWorker.GetPlaylist(playlist.Id);

                if (existing == null || existing.SnapshotID != playlist.SnapshotID)
                {
                    await DatabaseWorker.SetPlaylist(playlist);

                    foreach (string id in SplitIds(playlist.TrackIDs, Variables.Seperator))
                    {
                        await DatabaseWorker.SetTrack(new Variables.Track() { Id = id });
                    }
                }
            }

            Console.WriteLine("Got Playlists");
        }

        private static async Task SyncAlbumsAsync()
        {
            var albumSummaries = new List<(string Id, string Name, int TrackCount, string Artists)>();
            await foreach (var album in SpotifyWorker.GetUserAlbumsAsync())
            {
                albumSummaries.Add(album);
            }

            var newAlbumIds = albumSummaries
                .Where(summary => DatabaseWorker.GetAlbum(summary.Id) is null)
                .Select(summary => summary.Id)
                .ToList();

            var newAlbumDetails = await SpotifyWorker.GetAlbumDataBatchAsync(newAlbumIds);

            foreach (var albumId in newAlbumIds)
            {
                if (!newAlbumDetails.TryGetValue(albumId, out var data))
                {
                    continue;
                }

                Variables.Album album = new()
                {
                    Id = data.Id ?? albumId,
                    Name = data.name ?? string.Empty,
                    ImageURL = data.imageURL ?? string.Empty,
                    ArtistIDs = data.artistIDs ?? string.Empty
                };

                foreach (var trackId in SplitIds(data.TrackIDs, Variables.Seperator))
                {
                    await DatabaseWorker.SetTrack(new Variables.Track() { Id = trackId });
                }

                await DatabaseWorker.SetAlbum(album);
            }

            Console.WriteLine("Got Albums");
        }

        private static async Task SyncLikedSongsAsync()
        {
            await foreach (var liked in SpotifyWorker.GetLikedSongsAsync())
            {
                await DatabaseWorker.SetTrack(new Variables.Track() { Id = liked.Id });
            }

            Console.WriteLine("Got Liked Songs");
        }

        private static async Task SyncTrackMetadataAsync()
        {
            var trackRecords = DatabaseWorker.GetAllTracks().ToList();
            var missingTrackIds = trackRecords.Where(t => t.MissingInfo()).Select(t => t.Id).ToList();
            var missingTrackDetails = await SpotifyWorker.GetSongDataBatchAsync(missingTrackIds);

            var hydratedTracks = new List<Variables.Track>(trackRecords.Count);

            foreach (var track in trackRecords)
            {
                var current = track;

                if (track.MissingInfo() && missingTrackDetails.TryGetValue(track.Id, out var data))
                {
                    current = new Variables.Track()
                    {
                        Id = track.Id,
                        Name = data.name ?? string.Empty,
                        AlbumId = data.albumID ?? string.Empty,
                        ArtistIds = data.artistIDs ?? string.Empty,
                        DiscNumber = data.discnumber,
                        TrackNumber = data.tracknumber,
                        Explicit = data.Explicit,
                        DurationMs = data.durrationms,
                        PreviewUrl = data.previewURL ?? string.Empty,
                        SongID = string.IsNullOrEmpty(track.SongID) ? Variables.MakeId() : track.SongID
                    };

                    await DatabaseWorker.SetTrack(current);
                }

                hydratedTracks.Add(current);
            }

            foreach (var track in hydratedTracks)
            {
                if (!string.IsNullOrEmpty(track.AlbumId))
                {
                    await DatabaseWorker.SetAlbum(new Variables.Album() { Id = track.AlbumId });
                }

                foreach (var artistId in SplitArtistIds(track.ArtistIds))
                {
                    await DatabaseWorker.SetArtist(new Variables.Artist() { Id = artistId });
                }
            }

            Console.WriteLine("Got Track Data");
        }

        private static async Task SyncAlbumMetadataAsync()
        {
            var albumRecords = DatabaseWorker.GetAllAlbums().ToList();
            var albumsNeedingDetails = albumRecords.Where(a => a.MissingInfo()).Select(a => a.Id).ToList();
            var albumDetails = await SpotifyWorker.GetAlbumDataBatchAsync(albumsNeedingDetails);

            foreach (var album in albumRecords)
            {
                if (albumDetails.TryGetValue(album.Id, out var data))
                {
                    if (album.MissingInfo())
                    {
                        var updatedAlbum = new Variables.Album()
                        {
                            Id = album.Id,
                            Name = data.name ?? string.Empty,
                            ImageURL = data.imageURL ?? string.Empty,
                            ArtistIDs = data.artistIDs ?? string.Empty
                        };

                        await DatabaseWorker.SetAlbum(updatedAlbum);

                        album.Name = updatedAlbum.Name;
                        album.ImageURL = updatedAlbum.ImageURL;
                        album.ArtistIDs = updatedAlbum.ArtistIDs;
                    }

                    foreach (var artistId in SplitIds(data.artistIDs, Variables.Seperator))
                    {
                        await DatabaseWorker.SetArtist(new Variables.Artist() { Id = artistId });
                    }
                }
                else
                {
                    foreach (var artistId in SplitIds(album.ArtistIDs, Variables.Seperator))
                    {
                        await DatabaseWorker.SetArtist(new Variables.Artist() { Id = artistId });
                    }
                }
            }

            Console.WriteLine("Got Album Data");
        }

        private static async Task SyncArtistMetadataAsync()
        {
            var artistRecords = DatabaseWorker.GetAllArtists().ToList();
            var artistsNeedingDetails = artistRecords.Where(a => a.MissingInfo()).Select(a => a.Id).ToList();
            var artistDetails = await SpotifyWorker.GetArtistDataBatchAsync(artistsNeedingDetails);

            foreach (var artist in artistRecords)
            {
                if (artist.MissingInfo() && artistDetails.TryGetValue(artist.Id, out var data))
                {
                    await DatabaseWorker.SetArtist(new Variables.Artist()
                    {
                        Id = data.Id ?? artist.Id,
                        Genres = data.Genres ?? string.Empty,
                        Name = data.Name ?? string.Empty,
                        ImageURL = data.ImageUrl ?? string.Empty
                    });
                }
            }

            Console.WriteLine("Got Artist Data");
        }
    }
}

