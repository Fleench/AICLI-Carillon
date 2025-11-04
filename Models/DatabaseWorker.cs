/* File: DatabaseWorker.cs
 * Author: Glenn Sutherland, ChatGPT Codex
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
                                      ArtistIDs TEXT                     -- 'id1;;id2;;id3'
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
                                      Genres TEXT                        -- fixed typo: "Generes" → "Genres"
                                  );

                                  -- Similar table
                                  CREATE TABLE IF NOT EXISTS Similar (
                                      SongID TEXT,                       -- Spotify similar ID
                                      SongID2 TEXT                       -- removed trailing comma
                                  );
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

        public static IEnumerable<(string key, string value)> GetAllSettings()
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

        public static void SetPlaylist(Variables.PlayList playlist)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO Playlists (Id, Name, ImageURL, Description, SnapshotID, TrackIDs)
                                VALUES ($id, $name, $imageUrl, $description, $snapshotId, $trackIds);";

            cmd.Parameters.AddWithValue("$id", playlist.Id ?? string.Empty);
            cmd.Parameters.AddWithValue("$name", playlist.Name ?? string.Empty);
            cmd.Parameters.AddWithValue("$imageUrl", playlist.ImageURL ?? string.Empty);
            cmd.Parameters.AddWithValue("$description", playlist.Description ?? string.Empty);
            cmd.Parameters.AddWithValue("$snapshotId", playlist.SnapshotID ?? string.Empty);
            cmd.Parameters.AddWithValue("$trackIds", playlist.TrackIDs ?? string.Empty);

            cmd.ExecuteNonQuery();
        }

        public static Variables.PlayList? GetPlaylist(string id)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, ImageURL, Description, SnapshotID, TrackIDs FROM Playlists WHERE Id = $id LIMIT 1;";
            cmd.Parameters.AddWithValue("$id", id);

            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                return new Variables.PlayList
                {
                    Id = reader["Id"]?.ToString() ?? string.Empty,
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    ImageURL = reader["ImageURL"]?.ToString() ?? string.Empty,
                    Description = reader["Description"]?.ToString() ?? string.Empty,
                    SnapshotID = reader["SnapshotID"]?.ToString() ?? string.Empty,
                    TrackIDs = reader["TrackIDs"]?.ToString() ?? string.Empty
                };
            }

            return null;
        }

        public static IEnumerable<Variables.PlayList> GetAllPlaylists()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, ImageURL, Description, SnapshotID, TrackIDs FROM Playlists;";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                yield return new Variables.PlayList
                {
                    Id = reader["Id"]?.ToString() ?? string.Empty,
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    ImageURL = reader["ImageURL"]?.ToString() ?? string.Empty,
                    Description = reader["Description"]?.ToString() ?? string.Empty,
                    SnapshotID = reader["SnapshotID"]?.ToString() ?? string.Empty,
                    TrackIDs = reader["TrackIDs"]?.ToString() ?? string.Empty
                };
            }
        }

        public static void SetAlbum(Variables.Album album)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO Albums (Id, Name, ImageURL, ArtistIDs)
                                VALUES ($id, $name, $imageUrl, $artistIds);";

            cmd.Parameters.AddWithValue("$id", album.Id ?? string.Empty);
            cmd.Parameters.AddWithValue("$name", album.Name ?? string.Empty);
            cmd.Parameters.AddWithValue("$imageUrl", album.ImageURL ?? string.Empty);
            cmd.Parameters.AddWithValue("$artistIds", album.ArtistIDs ?? string.Empty);

            cmd.ExecuteNonQuery();
        }

        public static Variables.Album? GetAlbum(string id)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, ImageURL, ArtistIDs FROM Albums WHERE Id = $id LIMIT 1;";
            cmd.Parameters.AddWithValue("$id", id);

            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                var albumId = reader["Id"]?.ToString() ?? string.Empty;

                return new Variables.Album
                {
                    Id = albumId,
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    ImageURL = reader["ImageURL"]?.ToString() ?? string.Empty,
                    ArtistIDs = reader["ArtistIDs"]?.ToString() ?? string.Empty,
                    TrackIDs = GetAlbumTrackIds(conn, albumId)
                };
            }

            return null;
        }

        public static IEnumerable<Variables.Album> GetAllAlbums()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, ImageURL, ArtistIDs FROM Albums;";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var albumId = reader["Id"]?.ToString() ?? string.Empty;
                yield return new Variables.Album
                {
                    Id = albumId,
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    ImageURL = reader["ImageURL"]?.ToString() ?? string.Empty,
                    ArtistIDs = reader["ArtistIDs"]?.ToString() ?? string.Empty,
                    TrackIDs = GetAlbumTrackIds(conn, albumId)
                };
            }
        }

        private static string GetAlbumTrackIds(SqliteConnection connection, string albumId)
        {
            using var trackCmd = connection.CreateCommand();
            trackCmd.CommandText = "SELECT Id FROM Tracks WHERE AlbumId = $albumId;";
            trackCmd.Parameters.AddWithValue("$albumId", albumId);

            using var trackReader = trackCmd.ExecuteReader();
            List<string> trackIds = new();

            while (trackReader.Read())
            {
                var trackId = trackReader["Id"]?.ToString();
                if (!string.IsNullOrEmpty(trackId))
                {
                    trackIds.Add(trackId);
                }
            }

            return string.Join(Variables.Seperator, trackIds);
        }

        public static void SetTrack(Variables.Track track)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO Tracks (Id, SongID, Name, AlbumId, ArtistIds, DiscNumber, DurationMs, Explicit, PreviewUrl, TrackNumber)
                                VALUES ($id, $songId, $name, $albumId, $artistIds, $discNumber, $durationMs, $explicit, $previewUrl, $trackNumber);";

            cmd.Parameters.AddWithValue("$id", track.Id ?? string.Empty);
            cmd.Parameters.AddWithValue("$songId", track.SongID ?? string.Empty);
            cmd.Parameters.AddWithValue("$name", track.Name ?? string.Empty);
            cmd.Parameters.AddWithValue("$albumId", track.AlbumId ?? string.Empty);
            cmd.Parameters.AddWithValue("$artistIds", track.ArtistIds ?? string.Empty);
            cmd.Parameters.AddWithValue("$discNumber", track.DiscNumber);
            cmd.Parameters.AddWithValue("$durationMs", track.DurationMs);
            cmd.Parameters.AddWithValue("$explicit", track.Explicit ? 1 : 0);
            cmd.Parameters.AddWithValue("$previewUrl", track.PreviewUrl ?? string.Empty);
            cmd.Parameters.AddWithValue("$trackNumber", track.TrackNumber);

            cmd.ExecuteNonQuery();
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
                 return new Variables.Track() {
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    Id = reader["Id"]?.ToString() ?? string.Empty,
                    AlbumId = reader["AlbumId"]?.ToString() ?? string.Empty,
                    ArtistIds = reader["ArtistIds"]?.ToString() ?? string.Empty,
                    DiscNumber = reader["DiscNumber"] is DBNull ? 0 : Convert.ToInt32(reader["DiscNumber"]),
                    DurationMs = reader["DurationMs"] is DBNull ? 0 : Convert.ToInt32(reader["DurationMs"]),
                    Explicit = reader["Explicit"] is DBNull ? false : Convert.ToInt32(reader["Explicit"]) == 1,
                    PreviewUrl = reader["PreviewUrl"]?.ToString() ?? string.Empty,
                    TrackNumber = reader["TrackNumber"] is DBNull ? 0 : Convert.ToInt32(reader["TrackNumber"]),
                    SongID = reader["SongID"]?.ToString() ?? string.Empty
                };
            }

            return null; // No result found
        }

        public static IEnumerable<Variables.Track> GetAllTracks()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Tracks;";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                yield return new Variables.Track() {
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    Id = reader["Id"]?.ToString() ?? string.Empty,
                    AlbumId = reader["AlbumId"]?.ToString() ?? string.Empty,
                    ArtistIds = reader["ArtistIds"]?.ToString() ?? string.Empty,
                    DiscNumber = reader["DiscNumber"] is DBNull ? 0 : Convert.ToInt32(reader["DiscNumber"]),
                    DurationMs = reader["DurationMs"] is DBNull ? 0 : Convert.ToInt32(reader["DurationMs"]),
                    Explicit = reader["Explicit"] is DBNull ? false : Convert.ToInt32(reader["Explicit"]) == 1,
                    PreviewUrl = reader["PreviewUrl"]?.ToString() ?? string.Empty,
                    TrackNumber = reader["TrackNumber"] is DBNull ? 0 : Convert.ToInt32(reader["TrackNumber"]),
                    SongID = reader["SongID"]?.ToString() ?? string.Empty
                };
            }
        }

        public static void SetArtist(Variables.Artist artist)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO Artists (Id, Name, ImageURL, Genres)
                                VALUES ($id, $name, $imageUrl, $genres);";

            cmd.Parameters.AddWithValue("$id", artist.Id ?? string.Empty);
            cmd.Parameters.AddWithValue("$name", artist.Name ?? string.Empty);
            cmd.Parameters.AddWithValue("$imageUrl", artist.ImageURL ?? string.Empty);
            cmd.Parameters.AddWithValue("$genres", artist.Genres ?? string.Empty);

            cmd.ExecuteNonQuery();
        }

        public static Variables.Artist? GetArtist(string id)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, ImageURL, Genres FROM Artists WHERE Id = $id LIMIT 1;";
            cmd.Parameters.AddWithValue("$id", id);

            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                return new Variables.Artist
                {
                    Id = reader["Id"]?.ToString() ?? string.Empty,
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    ImageURL = reader["ImageURL"]?.ToString() ?? string.Empty,
                    Genres = reader["Genres"]?.ToString() ?? string.Empty
                };
            }

            return null;
        }

        public static IEnumerable<Variables.Artist> GetAllArtists()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, ImageURL, Genres FROM Artists;";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                yield return new Variables.Artist
                {
                    Id = reader["Id"]?.ToString() ?? string.Empty,
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    ImageURL = reader["ImageURL"]?.ToString() ?? string.Empty,
                    Genres = reader["Genres"]?.ToString() ?? string.Empty
                };
            }
        }

        public static void SetSimilar(string songId, string songId2)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO Similar (SongID, SongID2) VALUES ($songId, $songId2);";

            cmd.Parameters.AddWithValue("$songId", songId);
            cmd.Parameters.AddWithValue("$songId2", songId2);

            cmd.ExecuteNonQuery();
        }

        public static (string SongId, string SongId2)? GetSimilar(string songId, string songId2)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT SongID, SongID2 FROM Similar WHERE SongID = $songId AND SongID2 = $songId2 LIMIT 1;";
            cmd.Parameters.AddWithValue("$songId", songId);
            cmd.Parameters.AddWithValue("$songId2", songId2);

            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                return (
                    reader["SongID"]?.ToString() ?? string.Empty,
                    reader["SongID2"]?.ToString() ?? string.Empty
                );
            }

            return null;
        }

        public static IEnumerable<(string SongId, string SongId2)> GetAllSimilar()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT SongID, SongID2 FROM Similar;";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                yield return (
                    reader["SongID"]?.ToString() ?? string.Empty,
                    reader["SongID2"]?.ToString() ?? string.Empty
                );
            }
        }


    }
}