using System;
using System.IO;
namespace Spotify_Playlist_Manager.Models
{


    public static class Variables
    {
        // App Info
        public const string AppName = "SpotifyPlaylistManager";
        public static readonly string AppVersion = "1.0.0";

        // Base Directories
        public static readonly string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

        public static readonly string ConfigPath = Path.Combine(AppDataPath, "config.json");
        public static readonly string DatabasePath = Path.Combine(AppDataPath, "data.db");

        public static readonly string CachePath = Path.Combine(AppDataPath, "/cache");
        // Initialize any needed directories
        static Variables()
        {
            Directory.CreateDirectory(AppDataPath);
            Directory.CreateDirectory(CachePath);
        }

        public class PlayList
        {
            public string name;
            public string ImageURL;
            public string Id;
            public string Description;
            public string SnapshotID;
        }
    }

}