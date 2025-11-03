/* File: DataCoordinator.cs
 * Author: Glenn Sutherland
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
             HashSet<string> tracks = new();
            await foreach(var item in SpotifyWorker.GetUserPlaylistsAsync())
            {
                var data = SpotifyWorker.GetPlaylistDataAsync(item.Id).Result;
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
                    DatabaseWorker.SetPlaylist(playlist);
                    foreach(string id in playlist.TrackIDs.Split(Variables.Seperator))
                    {
                        DatabaseWorker.SetTrack(new Variables.Track() {Id = id});
                    }
                }
            }
            //2. Set albums
            await foreach (var item in SpotifyWorker.GetUserAlbumsAsync())
            {
                var data = SpotifyWorker.GetAlbumDataAsync(item.Id).Result;
                Variables.Album album = new()
                {
                    Id = data.Id,
                    Name = data.name,
                    ImageURL = data.imageURL,
                    ArtistIDs = data.artistIDs,
                };
                foreach(string id in data.TrackIDs.Split(Variables.Seperator))
                {
                    DatabaseWorker.SetTrack(new Variables.Track() {Id = id});
                }
                DatabaseWorker.SetAlbum(album);
            }
            
        }
    }
}

