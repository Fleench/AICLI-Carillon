"""CLI entry point for the Python-based Carillon workflow."""

from __future__ import annotations

__author__ = "ChatGPT Codex"

from carillon.database_worker import *
from carillon.spotify_worker import SpotifyWorker


def main() -> None:
    API = {
    "db": DatabaseWorker(config=DatabaseConfig(db_filename='C:\\Users\\servi\\AppData\\Roaming\\SpotifyPlaylistManager\\data.db'))}
    API["db"].init()
    print(f"Using database: {API["db"].db_path}")

    API["spotify"] = SpotifyWorker(API["db"])
    API["spotify"].authenticate()

    


if __name__ == "__main__":
    main()
