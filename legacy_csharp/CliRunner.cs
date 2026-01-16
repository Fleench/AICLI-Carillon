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
            "65392yUXSa7CibP88Sn08A",
"1ARRU77hkx4OTyw9bXdddx",
"6eNBczFcPGUHmJgIcJht3n",
"14vSWI3bnHGdHwZzYvQFAA",
"7B6PFPO2coL4eHBZV5ERzo",
"23svSBQEKD9JLSGiBu6x11",
"26pTF75NRXl5MHibLHYgx7",
"6obxnggDmfxDBD0PyR83qq",
"5qpWnHFhrdZJFasqvUkYnL",
"79NBBzBTMmTnxS52yF6sQS",
"1P6uaRXoH3oUGKZlG57OTB",
"252nMBfkL56QMavLz0Pz5Q"
        };

        public static async System.Threading.Tasks.Task Run()
        {
            Variables.Init();
            DatabaseWorker.Init();
            SpotifyWorker.Init();
            await SpotifyWorker.AuthenticateAsync();
            await DataCoordinator.Sync();

            var playlistLookup = BuildPlaylistLookup();
            if (playlistLookup.Count == 0)
            {
                Console.WriteLine("No playlists are available for triage. Update PlaylistIds in CliRunner.cs and ensure they are synced into the local database.");
                return;
            }

            var playlistTrackIds = BuildPlaylistTrackIndex(playlistLookup);
            var playlistKeyMap = BuildPlaylistKeyMap(playlistLookup);
            var likedSongs = await LoadLikedSongsAsync();
            ShuffleLikedSongs(likedSongs);

            foreach (var song in likedSongs)
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
                Console.WriteLine("Configure playlist IDs in CliRunner.PlaylistIds.");
                foreach (var entry in playlistKeyMap)
                {
                    var playlist = entry.Value;
                    Console.WriteLine($"Press {entry.Key} to add to {playlist.Name} ({playlist.Id})");
                }

                Console.WriteLine("Press the key for a playlist, press Enter to skip, or press Q to quit.");

                while (true)
                {
                    var keyInfo = Console.ReadKey(true);

                    if (keyInfo.Key == ConsoleKey.Q)
                    {
                        return;
                    }

                    if (keyInfo.Key == ConsoleKey.Enter)
                    {
                        break;
                    }

                    if (playlistKeyMap.TryGetValue(keyInfo.Key, out var selection))
                    {
                        Console.WriteLine($"Selected: {selection.Name} ({selection.Id})");
                        await SpotifyWorker.AddTracksToPlaylistAsync(selection.Id, new List<string> { song.Id });
                        playlistTrackIds.Add(song.Id);
                        break;
                    }

                    Console.WriteLine("Unknown playlist key. Press a listed key, press Enter to skip, or press Q to quit.");
                }
            }
        }

        private static async System.Threading.Tasks.Task<List<(string Id, string Name, string Artists)>> LoadLikedSongsAsync()
        {
            var likedSongs = new List<(string Id, string Name, string Artists)>();

            await foreach (var song in SpotifyWorker.GetLikedSongsAsync())
            {
                likedSongs.Add(song);
            }

            return likedSongs;
        }

        private static void ShuffleLikedSongs(List<(string Id, string Name, string Artists)> likedSongs)
        {
            for (var i = likedSongs.Count - 1; i > 0; i--)
            {
                var swapIndex = Variables.RNG.Next(i + 1);
                (likedSongs[i], likedSongs[swapIndex]) = (likedSongs[swapIndex], likedSongs[i]);
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

        private static Dictionary<ConsoleKey, Variables.PlayList> BuildPlaylistKeyMap(Dictionary<string, Variables.PlayList> playlistLookup)
        {
            var map = new Dictionary<ConsoleKey, Variables.PlayList>();
            var availableKeys = BuildAvailablePlaylistKeys();
            var index = 0;

            foreach (var playlist in playlistLookup.Values)
            {
                if (index >= availableKeys.Count)
                {
                    Console.WriteLine("Not enough keys to map all playlists. Remove extra playlist IDs or extend key mapping.");
                    break;
                }

                map[availableKeys[index]] = playlist;
                index++;
            }

            return map;
        }

        private static List<ConsoleKey> BuildAvailablePlaylistKeys()
        {
            var keys = new List<ConsoleKey>
            {
                ConsoleKey.D1,
                ConsoleKey.D2,
                ConsoleKey.D3,
                ConsoleKey.D4,
                ConsoleKey.D5,
                ConsoleKey.D6,
                ConsoleKey.D7,
                ConsoleKey.D8,
                ConsoleKey.D9,
                ConsoleKey.D0
            };

            for (var key = ConsoleKey.A; key <= ConsoleKey.Z; key++)
            {
                keys.Add(key);
            }

            return keys;
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
