/* File: DatabaseWorker.cs
 * Author: Glenn Sutherland
 * Description: This manages the sql commands and database for the software. 
*/
using Spotify_Playlist_Manager.Models;
using System;
using Microsoft.Data.Sqlite;
namespace Spotify_Playlist_Manager.Models
{
    
    public static class DatabaseWorker
    {
        private static string db_path = Variables.DatabasePath;

        public static void Init()
        {
            using var conn = new SqliteConnection($"Data Source={db_path}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
        ";
            cmd.ExecuteNonQuery();
        }
    }
}