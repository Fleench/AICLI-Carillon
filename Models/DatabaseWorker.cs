/* File: DatabaseWorker.cs
 * Author: Glenn Sutherland
 * Description: This manages the sql commands and database for the software. 
*/
using Spotify_Playlist_Manager.Models;
using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
namespace Spotify_Playlist_Manager.Models
{
    
    public static class DatabaseWorker
    {
        private static string _dbPath = Variables.DatabasePath;

        public static void Init()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();

            cmd.CommandText = """
                                  -- Settings table
                                  CREATE TABLE IF NOT EXISTS Settings (
                                      Key TEXT PRIMARY KEY,
                                      Value TEXT NOT NULL
                                  );

                                  -- Playlists table
                                  CREATE TABLE IF NOT EXISTS Playlists (
                                      Id TEXT PRIMARY KEY,               -- Spotify playlist ID
                                      Name TEXT,
                                      ImageURL TEXT,
                                      Description TEXT,
                                      SnapshotID TEXT,
                                      TrackIDs TEXT                      -- 'id1;;id2;;id3'
                                  );

                                  -- Albums table
                                  CREATE TABLE IF NOT EXISTS Albums (
                                      Id TEXT PRIMARY KEY,               -- Spotify album ID
                                      Name TEXT,
                                      ImageURL TEXT,
                                      ArtistIDs TEXT,                    -- 'id1;;id2;;id3'
                                      TrackIDs TEXT                      -- 'id1;;id2;;id3'
                                  );

                                  -- Tracks table (Spotify ID is anchor; SongID is internal secondary)
                                  CREATE TABLE IF NOT EXISTS Tracks (
                                      Id TEXT PRIMARY KEY,               -- Spotify track ID
                                      SongID TEXT,                       -- Carillon internal ID (CIID)
                                      Name TEXT,
                                      AlbumId TEXT,
                                      ArtistIds TEXT,
                                      DiscNumber INTEGER,
                                      DurationMs INTEGER,
                                      Explicit INTEGER,                  -- 0 = false, 1 = true
                                      PreviewUrl TEXT,
                                      TrackNumber INTEGER
                                  );

                                  -- Artists table
                                  CREATE TABLE IF NOT EXISTS Artists (
                                      Id TEXT PRIMARY KEY,               -- Spotify artist ID
                                      Name TEXT,
                                      ImageURL TEXT,
                                      Generes TEXT
                                  );
                                  -- Similar Table
                                  CREATE TABLE IF NOT EXISTS Similar (
                                      SongID TEXT,               -- Spotify similar ID
                                      SongID2 TEXT,
                                  )
                              """;

            cmd.ExecuteNonQuery();
        }


        public static void SetSetting(string key, string value)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES ($key, $value);";
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", value);
            cmd.ExecuteNonQuery();
        }

        public static string? GetSetting(string key)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Value FROM Settings WHERE Key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
            return cmd.ExecuteScalar() as string;
        }

        public static IEnumerable<(string key, string value)> GetSettings()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Key, Value FROM Settings;";

            using var reader = cmd.ExecuteReader();

            // Read each row and yield it as a tuple
            while (reader.Read())
            {
                string key = reader.GetString(0);   // column index 0 → Key
                string value = reader.GetString(1); // column index 1 → Value
                yield return (key, value);
            }
        }

        public static Variables.Track GetTrack(string id)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();

            // Check if we're dealing with a Carillon internal SongID or a Spotify ID
            if (id.StartsWith(Variables.Identifier)) // e.g., "CIID"
            {
                cmd.CommandText = "SELECT * FROM Tracks WHERE SongID = @id LIMIT 1;";
            }
            else
            {
                cmd.CommandText = "SELECT * FROM Tracks WHERE Id = @id LIMIT 1;";
            }

            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                // Safely convert columns to your Track object
                return new Variables.Track(
                    name: reader["Name"]?.ToString() ?? "",
                    id: reader["Id"]?.ToString() ?? "",
                    albumId: reader["AlbumId"]?.ToString() ?? "",
                    artistIds: reader["ArtistIds"]?.ToString() ?? "",
                    discNumber: reader["DiscNumber"] is DBNull ? 0 : Convert.ToInt32(reader["DiscNumber"]),
                    durationMs: reader["DurationMs"] is DBNull ? 0 : Convert.ToInt32(reader["DurationMs"]),
                    @explicit: reader["Explicit"] is DBNull ? false : Convert.ToInt32(reader["Explicit"]) == 1,
                    previewUrl: reader["PreviewUrl"]?.ToString() ?? "",
                    trackNumber: reader["TrackNumber"] is DBNull ? 0 : Convert.ToInt32(reader["TrackNumber"]),
                    songId: reader["SongID"]?.ToString() ?? ""
                );
            }

            return null; // No result found
        }


    }
}