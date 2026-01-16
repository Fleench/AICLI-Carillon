/* File: CliRunner.cs
 * Author: ChatGPT Codex
 * Description: Headless CLI runner that triages liked songs into playlists.
 */
using System;
using System.Collections.Generic;
using Spotify_Playlist_Manager.Models;

namespace Spotify_Playlist_Manager
{
    public static class CliRunner
    {
        private static readonly string[] PlaylistIds =
        {
            "2KjzmceL3Ql46eG3Bnc0gY"
        };

        public static async System.Threading.Tasks.Task Run()
        {
            Variables.Init();
            DatabaseWorker.Init();
            SpotifyWorker.Init();
            await SpotifyWorker.AuthenticateAsync();

            var playlistLookup = BuildPlaylistLookup();
            if (playlistLookup.Count == 0)
            {
                Console.WriteLine("No playlists are available for triage. Run a sync to load the target playlist into the local database.");
                return;
            }

            var playlistTrackIds = BuildPlaylistTrackIndex(playlistLookup);

            await foreach (var song in SpotifyWorker.GetLikedSongsAsync())
            {
                if (playlistTrackIds.Contains(song.Id))
                {
                    continue;
                }

                Console.Clear();
                string displayArtists = FormatArtists(song.Artists);
                Console.WriteLine($"NOW PLAYING: {song.Name} — {displayArtists}");
                var playbackResult = await SpotifyWorker.PlayTrack(song.Id);
                if (!playbackResult.Success)
                {
                    Console.WriteLine();
                    Console.WriteLine(playbackResult.Message);
                    Console.WriteLine("Press any key to continue.");
                    Console.ReadKey(true);
                    continue;
                }

                Console.WriteLine();
                Console.WriteLine("Controls:");
                foreach (var playlist in playlistLookup.Values)
                {
                    Console.WriteLine($"Playlist: {playlist.Name} ({playlist.Id})");
                }

                Console.WriteLine("Enter a playlist ID to add the track, press Enter to skip, or type q to quit.");

                while (true)
                {
                    string? input = Console.ReadLine();

                    if (string.Equals(input, "q", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(input))
                    {
                        break;
                    }

                    if (playlistLookup.TryGetValue(input.Trim(), out var selection))
                    {
                        Console.WriteLine($"Selected: {selection.Name} ({selection.Id})");
                        await SpotifyWorker.AddTracksToPlaylistAsync(selection.Id, new List<string> { song.Id });
                        playlistTrackIds.Add(song.Id);
                        break;
                    }

                    Console.WriteLine("Unknown playlist ID. Enter a valid playlist ID, press Enter to skip, or type q to quit.");
                }
            }
        }

        private static Dictionary<string, Variables.PlayList> BuildPlaylistLookup()
        {
            var lookup = new Dictionary<string, Variables.PlayList>(StringComparer.OrdinalIgnoreCase);

            foreach (var playlistId in PlaylistIds)
            {
                var playlist = DataCoordinator.GetPlaylist(playlistId);
                if (playlist == null)
                {
                    Console.WriteLine($"Playlist {playlistId} is not cached locally. Run a sync to load playlist metadata into the database.");
                    continue;
                }

                if (playlist.MissingInfo())
                {
                    Console.WriteLine($"Playlist {playlistId} metadata is incomplete. Run a sync to refresh playlist details in the database.");
                }

                lookup[playlistId] = playlist;
            }

            return lookup;
        }

        private static HashSet<string> BuildPlaylistTrackIndex(Dictionary<string, Variables.PlayList> playlistLookup)
        {
            var trackIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var playlist in playlistLookup.Values)
            {
                if (string.IsNullOrWhiteSpace(playlist.TrackIDs))
                {
                    continue;
                }

                foreach (var trackId in playlist.TrackIDs.Split(new[] { Variables.Seperator }, StringSplitOptions.RemoveEmptyEntries))
                {
                    trackIds.Add(trackId);
                }
            }

            return trackIds;
        }

        private static string FormatArtists(string artists)
        {
            if (string.IsNullOrWhiteSpace(artists))
            {
                return "Unknown Artist";
            }

            return string.Join(", ", artists.Split(new[] { Variables.Seperator }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
