"""CLI entry point for the Python-based Carillon workflow."""

from __future__ import annotations

__author__ = "ChatGPT Codex"

from carillon.database_worker import DatabaseWorker
from carillon.spotify_worker import SpotifyWorker


def run_cli() -> None:
    db = DatabaseWorker()
    db.init()
    print(f"Using database: {db.db_path}")

    spotify = SpotifyWorker(db)
    spotify.authenticate()

    liked_keep = []
    liked_skip = []

    print("Starting liked songs triage. Press 1=keep, 2=skip, 3=quit.")

    for song in spotify.get_liked_songs():
        track_id = song.get("id")
        if not track_id:
            continue

        print("\nNow playing: {name} — {artists}".format(**song))
        try:
            spotify.play_track(track_id)
        except Exception as exc:  # spotipy may raise SpotifyException subclasses
            print(f"Playback failed: {exc}")
            continue

        while True:
            choice = input("Select (1=keep, 2=skip, 3=quit): ").strip()
            if choice == "1":
                liked_keep.append(track_id)
                break
            if choice == "2":
                liked_skip.append(track_id)
                break
            if choice == "3":
                print("Quitting triage.")
                print(f"Kept: {len(liked_keep)} | Skipped: {len(liked_skip)}")
                return
            print("Invalid input. Please press 1, 2, or 3.")

    print("No more liked songs to triage.")
    print(f"Kept: {len(liked_keep)} | Skipped: {len(liked_skip)}")


if __name__ == "__main__":
    run_cli()
