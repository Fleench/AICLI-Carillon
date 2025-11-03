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
             
            await foreach(var item in SpotifyWorker.GetUserPlaylistsAsync())
            {
                var data = SpotifyWorker.GetPlaylistDataAsync(item.Id).Result;
                Variables.PlayList playlist = new Variables.PlayList()
                {
                    Id = item.Id,
        public string Name;
        public string ImageURL;
        public string Description;
        public string SnapshotID;
        public string TrackIDs;
                };
            }
        }
    }
}

