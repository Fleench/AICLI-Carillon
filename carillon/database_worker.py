"""Database worker for sharing the existing Carillon SQLite database."""

from __future__ import annotations

__author__ = "ChatGPT Codex"

import os
import sqlite3
from dataclasses import dataclass
from pathlib import Path
from typing import Optional

from appdirs import user_data_dir


@dataclass
class DatabaseConfig:
    """Configuration for locating the shared SQLite database."""

    app_name: str = "SpotifyPlaylistManager"
    app_author: str = "Fleench"
    db_filename: str = "music.db"


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

    def __enter__(self) -> "DatabaseWorker":
        self.init()
        return self

    def __exit__(self, exc_type, exc, tb) -> None:
        self.close()
