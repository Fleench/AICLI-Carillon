/* File: DataCoordinator.cs
 * Author: Glenn Sutherland, ChatGPT Codex
 * Description: Connects the SpotifyWorker to the DatabaseWorker
 */

using SQLitePCL;
using System;
using System.Collections.Generic;
using Spotify_Playlist_Manager.Models;
namespace Spotify_Playlist_Manager.Models
{
    static class DataCoordinator
    {
        public async static void Sync()
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
            //2. Set albums
            await foreach (var item in SpotifyWorker.GetUserAlbumsAsync())
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
            //3. set liked songs
            await foreach (var item in SpotifyWorker.GetLikedSongsAsync())
            {
                await DatabaseWorker.SetTrack(new Variables.Track() {Id = item.Id});
            }
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
    }
}

