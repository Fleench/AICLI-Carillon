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
        private static readonly Dictionary<char, (string Name, string PlaylistId)> PlaylistMap = new()
        {
            { '1', ("Chill Playlist", "PLAYLIST_ID_1") },
            { '2', ("Focus Playlist", "PLAYLIST_ID_2") },
            { '3', ("Workout Playlist", "PLAYLIST_ID_3") },
            { 'd', ("Delete/Skip", string.Empty) }
        };

        public static async System.Threading.Tasks.Task Run()
        {
            Variables.Init();
            DatabaseWorker.Init();
            SpotifyWorker.Init();
            await SpotifyWorker.AuthenticateAsync();

            await foreach (var song in SpotifyWorker.GetLikedSongsAsync())
            {
                Console.Clear();
                string displayArtists = FormatArtists(song.Artists);
                Console.WriteLine($"NOW PLAYING: {song.Name} — {displayArtists}");
                await SpotifyWorker.PlayTrack(song.Id);

                Console.WriteLine();
                Console.WriteLine("Controls:");

                foreach (var control in PlaylistMap)
                {
                    Console.WriteLine($"[{control.Key}] -> {control.Value.Name}");
                }

                Console.WriteLine("[space] -> Skip");
                Console.WriteLine("[q] -> Quit");

                while (true)
                {
                    char key = Console.ReadKey(true).KeyChar;

                    if (key == 'q')
                    {
                        return;
                    }

                    if (key == ' ')
                    {
                        break;
                    }

                    if (PlaylistMap.TryGetValue(key, out var selection))
                    {
                        Console.WriteLine($"Selected: {selection.Name}");

                        if (!string.IsNullOrWhiteSpace(selection.PlaylistId))
                        {
                            await SpotifyWorker.AddTracksToPlaylistAsync(selection.PlaylistId, new List<string> { song.Id });
                        }

                        break;
                    }
                }
            }
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
