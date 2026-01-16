"""Database worker for sharing the existing Carillon SQLite database."""

from __future__ import annotations

__author__ = "ChatGPT Codex"

import os
import random
import sqlite3
import string
from dataclasses import dataclass
from itertools import islice
from pathlib import Path
from typing import Optional, TYPE_CHECKING

from appdirs import user_data_dir

if TYPE_CHECKING:
    from carillon.spotify_worker import SpotifyWorker


@dataclass
class DatabaseConfig:
    """Configuration for locating the shared SQLite database."""

    app_name: str = "SpotifyPlaylistManager"
    app_author: str = ""
    db_filename: str = "data.db"


class DatabaseWorker:
    """Thin wrapper around the shared SQLite database."""

    def __init__(self, config: DatabaseConfig | None = None) -> None:
        self._config = config or DatabaseConfig()
        self._connection: Optional[sqlite3.Connection] = None
        self._db_path = self._resolve_db_path()

    @property
    def db_path(self) -> Path:
        return self._db_path

    def _resolve_db_path(self) -> Path:
        base_dir = Path(user_data_dir(self._config.app_name, self._config.app_author))
        base_dir.mkdir(parents=True, exist_ok=True)
        return base_dir / self._config.db_filename

    def init(self) -> None:
        """Connect to the database and ensure the Settings table exists."""
        if self._connection is None:
            self._connection = sqlite3.connect(self._db_path)
            self._connection.execute("PRAGMA journal_mode=WAL;")

        self._connection.execute(
            """
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
            """
        )
        self._connection.commit()

    def get_setting(self, key: str) -> Optional[str]:
        if self._connection is None:
            raise RuntimeError("DatabaseWorker.init must be called before get_setting.")
        cursor = self._connection.execute(
            "SELECT Value FROM Settings WHERE Key = ?;",
            (key,),
        )
        row = cursor.fetchone()
        return row[0] if row else None

    def set_setting(self, key: str, value: str) -> None:
        if self._connection is None:
            raise RuntimeError("DatabaseWorker.init must be called before set_setting.")
        self._connection.execute(
            "INSERT OR REPLACE INTO Settings (Key, Value) VALUES (?, ?);",
            (key, value),
        )
        self._connection.commit()

    def close(self) -> None:
        if self._connection is not None:
            self._connection.close()
            self._connection = None

    def sync_from_spotify(self, spotify: "SpotifyWorker") -> None:
        """
        Syncs local database with Spotify data before sorting begins.
        Ensures local 'sorted' status is up to date.
        """
        if self._connection is None:
            self._connection = sqlite3.connect(self._db_path)
        self._connection.row_factory = sqlite3.Row

        separator = ";;"

        def ensure_schema() -> None:
            self._connection.executescript(
                """
                CREATE TABLE IF NOT EXISTS Settings (
                    Key TEXT PRIMARY KEY,
                    Value TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Playlists (
                    Id TEXT PRIMARY KEY,
                    Name TEXT,
                    ImageURL TEXT,
                    ImagePath TEXT,
                    Description TEXT,
                    SnapshotID TEXT,
                    TrackIDs TEXT
                );

                CREATE TABLE IF NOT EXISTS Albums (
                    Id TEXT PRIMARY KEY,
                    Name TEXT,
                    ImageURL TEXT,
                    ImagePath TEXT,
                    ArtistIDs TEXT
                );

                CREATE TABLE IF NOT EXISTS Tracks (
                    Id TEXT PRIMARY KEY,
                    SongID TEXT,
                    Name TEXT,
                    AlbumId TEXT,
                    ArtistIds TEXT,
                    DiscNumber INTEGER,
                    DurationMs INTEGER,
                    Explicit INTEGER,
                    PreviewUrl TEXT,
                    TrackNumber INTEGER
                );

                CREATE TABLE IF NOT EXISTS Artists (
                    Id TEXT PRIMARY KEY,
                    Name TEXT,
                    ImageURL TEXT,
                    ImagePath TEXT,
                    Genres TEXT
                );
                """
            )
            self._connection.execute("PRAGMA journal_mode=WAL;")
            self._connection.commit()

        def make_song_id(track_type: str = "SNG", length: int = 30) -> str:
            allowed_chars = string.ascii_uppercase + string.digits
            random_suffix = "".join(random.choice(allowed_chars) for _ in range(length))
            return f"CIID___{track_type}___{random_suffix}"

        def chunked(iterable, size: int):
            iterator = iter(iterable)
            while True:
                chunk = list(islice(iterator, size))
                if not chunk:
                    break
                yield chunk

        def fetch_playlist_track_ids(playlist_id: str) -> list[str]:
            track_ids: list[str] = []
            offset = 0
            while True:
                response = spotify.sp.playlist_items(
                    playlist_id,
                    limit=100,
                    offset=offset,
                    fields="items.track.id,next",
                )
                items = response.get("items", [])
                if not items:
                    break
                for item in items:
                    track = item.get("track") or {}
                    track_id = track.get("id")
                    if track_id:
                        track_ids.append(track_id)
                if response.get("next") is None:
                    break
                offset += 100
            return track_ids

        def ensure_track_placeholder(track_id: str) -> None:
            existing = self._connection.execute(
                "SELECT SongID FROM Tracks WHERE Id = ?;",
                (track_id,),
            ).fetchone()
            if existing:
                return
            self._connection.execute(
                """
                INSERT OR REPLACE INTO Tracks
                    (Id, SongID, Name, AlbumId, ArtistIds, DiscNumber, DurationMs, Explicit, PreviewUrl, TrackNumber)
                VALUES (?, ?, '', '', '', 0, 0, 0, '', 0);
                """,
                (track_id, make_song_id()),
            )

        def ensure_album_placeholder(album_id: str) -> None:
            existing = self._connection.execute(
                "SELECT Id FROM Albums WHERE Id = ?;",
                (album_id,),
            ).fetchone()
            if existing:
                return
            self._connection.execute(
                "INSERT OR REPLACE INTO Albums (Id, Name, ImageURL, ImagePath, ArtistIDs) VALUES (?, '', '', '', '');",
                (album_id,),
            )

        def ensure_artist_placeholder(artist_id: str) -> None:
            existing = self._connection.execute(
                "SELECT Id FROM Artists WHERE Id = ?;",
                (artist_id,),
            ).fetchone()
            if existing:
                return
            self._connection.execute(
                "INSERT OR REPLACE INTO Artists (Id, Name, ImageURL, ImagePath, Genres) VALUES (?, '', '', '', '');",
                (artist_id,),
            )

        ensure_schema()

        print("\n[Sync] Updating local database from Spotify...")

        # Sync playlists and their tracks
        print("[Sync] Playlists...")
        playlist_offset = 0
        playlists: list[dict] = []
        while True:
            response = spotify.sp.current_user_playlists(limit=50, offset=playlist_offset)
            items = response.get("items", [])
            if not items:
                break
            playlists.extend(items)
            if response.get("next") is None:
                break
            playlist_offset += 50

        playlist_snapshots = {
            row["Id"]: row["SnapshotID"]
            for row in self._connection.execute("SELECT Id, SnapshotID FROM Playlists;").fetchall()
        }

        for playlist in playlists:
            playlist_id = playlist["id"]
            details = spotify.sp.playlist(
                playlist_id,
                fields="name,images,description,snapshot_id",
            )
            snapshot_id = details.get("snapshot_id", "")
            if playlist_snapshots.get(playlist_id) == snapshot_id:
                continue
            image_url = ""
            images = details.get("images") or []
            if images:
                image_url = images[0].get("url", "")
            track_ids = fetch_playlist_track_ids(playlist_id)
            self._connection.execute(
                """
                INSERT OR REPLACE INTO Playlists
                    (Id, Name, ImageURL, ImagePath, Description, SnapshotID, TrackIDs)
                VALUES (?, ?, ?, '', ?, ?, ?);
                """,
                (
                    playlist_id,
                    details.get("name", ""),
                    image_url,
                    details.get("description", ""),
                    snapshot_id,
                    separator.join(track_ids),
                ),
            )
            for track_id in track_ids:
                ensure_track_placeholder(track_id)

        # Sync albums and their tracks
        print("[Sync] Albums...")
        album_offset = 0
        album_ids: list[str] = []
        while True:
            response = spotify.sp.current_user_saved_albums(limit=50, offset=album_offset)
            items = response.get("items", [])
            if not items:
                break
            for item in items:
                album = item.get("album") or {}
                album_id = album.get("id")
                if album_id:
                    album_ids.append(album_id)
            if response.get("next") is None:
                break
            album_offset += 50

        existing_albums = {
            row["Id"] for row in self._connection.execute("SELECT Id FROM Albums;").fetchall()
        }
        new_album_ids = [album_id for album_id in album_ids if album_id not in existing_albums]

        for album_batch in chunked(new_album_ids, 20):
            album_details = spotify.sp.albums(album_batch).get("albums", [])
            for album in album_details:
                if not album:
                    continue
                album_id = album.get("id")
                if not album_id:
                    continue
                artist_ids = [artist.get("id") for artist in album.get("artists", []) if artist.get("id")]
                track_ids: list[str] = []
                track_offset = 0
                while True:
                    tracks_resp = spotify.sp.album_tracks(album_id, limit=50, offset=track_offset)
                    track_items = tracks_resp.get("items", [])
                    if not track_items:
                        break
                    for track in track_items:
                        track_id = track.get("id")
                        if track_id:
                            track_ids.append(track_id)
                    if tracks_resp.get("next") is None:
                        break
                    track_offset += 50

                image_url = ""
                images = album.get("images") or []
                if images:
                    image_url = images[0].get("url", "")

                self._connection.execute(
                    """
                    INSERT OR REPLACE INTO Albums (Id, Name, ImageURL, ImagePath, ArtistIDs)
                    VALUES (?, ?, ?, '', ?);
                    """,
                    (
                        album_id,
                        album.get("name", ""),
                        image_url,
                        separator.join(artist_ids),
                    ),
                )
                for track_id in track_ids:
                    ensure_track_placeholder(track_id)
                for artist_id in artist_ids:
                    ensure_artist_placeholder(artist_id)

        # Sync liked songs
        print("[Sync] Liked songs...")
        liked_offset = 0
        while True:
            response = spotify.sp.current_user_saved_tracks(limit=50, offset=liked_offset)
            items = response.get("items", [])
            if not items:
                break
            for item in items:
                track = item.get("track") or {}
                track_id = track.get("id")
                if track_id:
                    ensure_track_placeholder(track_id)
            if response.get("next") is None:
                break
            liked_offset += 50

        # Sync track metadata
        print("[Sync] Track metadata...")
        track_rows = self._connection.execute("SELECT * FROM Tracks;").fetchall()
        missing_track_ids = []
        track_row_map = {}
        for row in track_rows:
            track_row_map[row["Id"]] = row
            if not row["Name"] or not row["AlbumId"] or not row["ArtistIds"] or not row["SongID"]:
                missing_track_ids.append(row["Id"])
                continue
            if row["DurationMs"] <= 0 or row["DiscNumber"] <= 0 or row["TrackNumber"] <= 0:
                missing_track_ids.append(row["Id"])

        for track_batch in chunked(missing_track_ids, 50):
            track_details = spotify.sp.tracks(track_batch).get("tracks", [])
            for track in track_details:
                if not track:
                    continue
                track_id = track.get("id")
                if not track_id:
                    continue
                row = track_row_map.get(track_id)
                song_id = row["SongID"] if row and row["SongID"] else make_song_id()
                artist_ids = [artist.get("id") for artist in track.get("artists", []) if artist.get("id")]
                album = track.get("album") or {}
                album_id = album.get("id", "")
                self._connection.execute(
                    """
                    INSERT OR REPLACE INTO Tracks
                        (Id, SongID, Name, AlbumId, ArtistIds, DiscNumber, DurationMs, Explicit, PreviewUrl, TrackNumber)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?);
                    """,
                    (
                        track_id,
                        song_id,
                        track.get("name", ""),
                        album_id,
                        separator.join(artist_ids),
                        track.get("disc_number") or 0,
                        track.get("duration_ms") or 0,
                        1 if track.get("explicit") else 0,
                        track.get("preview_url") or "",
                        track.get("track_number") or 0,
                    ),
                )
                if album_id:
                    ensure_album_placeholder(album_id)
                for artist_id in artist_ids:
                    ensure_artist_placeholder(artist_id)

        # Sync album metadata
        print("[Sync] Album metadata...")
        album_rows = self._connection.execute("SELECT * FROM Albums;").fetchall()
        missing_album_ids = [
            row["Id"]
            for row in album_rows
            if not row["Name"] or not row["ArtistIDs"]
        ]

        for album_batch in chunked(missing_album_ids, 20):
            album_details = spotify.sp.albums(album_batch).get("albums", [])
            for album in album_details:
                if not album:
                    continue
                album_id = album.get("id")
                if not album_id:
                    continue
                artist_ids = [artist.get("id") for artist in album.get("artists", []) if artist.get("id")]
                image_url = ""
                images = album.get("images") or []
                if images:
                    image_url = images[0].get("url", "")
                self._connection.execute(
                    """
                    INSERT OR REPLACE INTO Albums (Id, Name, ImageURL, ImagePath, ArtistIDs)
                    VALUES (?, ?, ?, '', ?);
                    """,
                    (
                        album_id,
                        album.get("name", ""),
                        image_url,
                        separator.join(artist_ids),
                    ),
                )
                for artist_id in artist_ids:
                    ensure_artist_placeholder(artist_id)

        # Sync artist metadata
        print("[Sync] Artist metadata...")
        artist_rows = self._connection.execute("SELECT * FROM Artists;").fetchall()
        missing_artist_ids = [
            row["Id"]
            for row in artist_rows
            if not row["Name"]
        ]

        for artist_batch in chunked(missing_artist_ids, 50):
            artist_details = spotify.sp.artists(artist_batch).get("artists", [])
            for artist in artist_details:
                if not artist:
                    continue
                artist_id = artist.get("id")
                if not artist_id:
                    continue
                images = artist.get("images") or []
                image_url = images[0].get("url", "") if images else ""
                self._connection.execute(
                    """
                    INSERT OR REPLACE INTO Artists (Id, Name, ImageURL, ImagePath, Genres)
                    VALUES (?, ?, ?, '', ?);
                    """,
                    (
                        artist_id,
                        artist.get("name", ""),
                        image_url,
                        ", ".join(artist.get("genres", []) or []),
                    ),
                )

        self._connection.commit()

        print("[Sync] Complete.")

    def __enter__(self) -> "DatabaseWorker":
        self.init()
        return self

    def __exit__(self, exc_type, exc, tb) -> None:
        self.close()
