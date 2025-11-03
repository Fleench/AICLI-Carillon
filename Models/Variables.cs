/* File: Variables.cs
 * Author: Glenn Sutherland
 * Description: Variables and Classes used by the program
 */
using System;
using System.IO;
using System.Linq;

namespace Spotify_Playlist_Manager.Models
{


    public static class Variables
    {
        // App Info
        public const string AppName = "SpotifyPlaylistManager";
        public static readonly string AppVersion = "0.0.0";

        // Base Directories
        public static readonly string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

        public static readonly string ConfigPath = Path.Combine(AppDataPath, "config.json");
        public static readonly string DatabasePath = Path.Combine(AppDataPath, "data.db");

        public static readonly string CachePath = Path.Combine(AppDataPath, "/cache");

        public static readonly string Seperator = ";;";
        public static readonly string Identifier = "CIID";
        public static readonly string IdentifierDelimiter = "___";
        public static readonly Random RNG = new Random();
        // Initialize any needed directories
        public static void Init()
        {
            Directory.CreateDirectory(AppDataPath);
            Directory.CreateDirectory(CachePath);
            File.Create(DatabasePath).Close();
            Console.WriteLine(DatabasePath);
        }

        public class PlayList
        {
            public string Name;
            public string ImageURL;
            public string Id;
            public string Description;
            public string SnapshotID;
            public string TrackIDs;
        }
        public class Album
        {
            public string Name;
            public string ImageURL;
            public string Id;
            public string ArtistIDs;
            public string TrackIDs;
        }

        public class Track
        {
            public string Name;
            public string Id;
            public string AlbumId;
            public string ArtistIds;
            public int DiscNumber;
            public int DurationMs;
            public bool Explicit;
            public string PreviewUrl;
            public int TrackNumber;
            public string SongID;

            public Track()
            {
                
                    SongID = Variables.MakeId();
            }
        }

        public class Artist
        {
            public string Name;
            public string ImageURL;
            public string Id;
            public string Generes;
        }
        public static class Settings
        {
            public static string SW_AccessToken = "SW_AccessToken";
            public static string SW_RefreshToken = "SW_RefreshToken";
            public static string SW_ClientToken = "SW_ClientToken";
            public static string SW_ClientSecret = "SW_ClientSecret";
        }

        public static string MakeId( string type = "SNG", int length = 30)
        {
            string AllowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            string songId = Variables.Identifier + Variables.IdentifierDelimiter + type +  Variables.IdentifierDelimiter;
            songId += new string(Enumerable.Range(0, length)
                .Select(_ => AllowedChars[RNG.Next(AllowedChars.Length)])
                .ToArray());
            return songId;
        }
    }

}