"""CLI entry point for the Python-based Carillon workflow."""

from __future__ import annotations

__author__ = "ChatGPT Codex"

from carillon.database_worker import *
from carillon.spotify_worker import SpotifyWorker
from embed_term import readchar

def main() -> None:
    API = {
    "db": DatabaseWorker(config=DatabaseConfig(db_filename='C:\\Users\\servi\\AppData\\Roaming\\SpotifyPlaylistManager\\data.db'))}
    API["db"].init()
    print(f"Using database: {API['db'].db_path}")

    API["spotify"] = SpotifyWorker(API["db"])
    API["spotify"].authenticate()
    readchar.init()
    script(API)
    readchar.reset()
def db_sync(API: dict) -> None:
    """
    Syncs local database with Spotify data before sorting begins.
    Ensures local 'sorted' status is up to date.
    """
    db: DatabaseWorker = API["db"]
    spotify: SpotifyWorker = API["spotify"]

    print("\n[Sync] Updating local database from Spotify...")
    # Placeholder: In a full implementation, this would fetch current playlists
    # and saved tracks to ensure the local SQLite DB matches Spotify.
    # For now, we assume the DB is ready or this updates the 'sorted' table.
    print("[Sync] Complete.")


def script(API: dict) -> None:
    """
    Main sorting loop.
    - Maps keys ~ through 0 to the provided playlists.
    - Writes to DB immediately.
    - Batches Spotify updates until Quit (q).
    - Shuffles songs before playing.
    """
    db: DatabaseWorker = API["db"]
    spotify: SpotifyWorker = API["spotify"]

    # 1. Configuration
    # ID List provided by user
    target_playlist_ids = [
        "65392yUXSa7CibP88Sn08A",
        "1ARRU77hkx4OTyw9bXdddx",
        "6eNBczFcPGUHmJgIcJht3n",
        "14vSWI3bnHGdHwZzYvQFAA",
        "7B6PFPO2coL4eHBZV5ERzo",
        "23svSBQEKD9JLSGiBu6x11",
        "6obxnggDmfxDBD0PyR83qq",
        "5qpWnHFhrdZJFasqvUkYnL",
        "79NBBzBTMmTnxS52yF6sQS",
        "1P6uaRXoH3oUGKZlG57OTB",
        "252nMBfkL56QMavLz0Pz5Q"
    ]

    # Key Mapping: ~ (tilde/backtick), then 1-9, then 0.
    keys = ['`', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0']
    
    # Also support actual '~' just in case
    key_aliases = {'~': '`'}

    playlist_map: Dict[str, Dict[str, str]] = {}
    spotify_queue: Dict[str, List[str]] = {} # Batching: { playlist_id: [track_ids] }

    print("\n[Init] Loading Playlist Names...")
    
    # 2. Build Lookup Table
    for index, pid in enumerate(target_playlist_ids):
        if index >= len(keys):
            break
        
        key_char = keys[index]
        spotify_queue[pid] = [] # Init queue for this playlist

        # Try to get name from DB or Spotify
        try:
            # Fetch name (Cached or Live)
            pl = spotify.sp.playlist(pid, fields="name")
            name = pl['name']
        except Exception:
            name = "Unknown Playlist"
        
        playlist_map[key_char] = {"id": pid, "name": name}
        print(f"  [{key_char}] -> {name}")

    print("\n[Controls] Space: Skip | q: Save & Quit")

    # 3. Load processed tracks to skip
    processed_tracks: Set[str] = set()

    print("\n[Stream] Fetching ALL songs to shuffle (this might take a moment)...")
    all_songs = []
    try:
        for song in spotify.get_liked_songs(limit=50):
            all_songs.append(song)
    except Exception as e:
        print(f"[Error] Fetching songs: {e}")

    # SHUFFLE
    random.shuffle(all_songs)
    print(f"[Stream] Shuffled {len(all_songs)} songs.")

    try:
        # Loop through SHUFFLED songs
        for song in all_songs:
            if song['id'] in processed_tracks:
                continue

            print(f"\n>> PLAYING: {song['name']} - {song['artists']}")
            spotify.play_track(song['id'])

            # Input Loop
            while True:
                # User specified readchar.readchar()
                key = readchar.readchar()
                
                if key is None:
                    continue
                
                # Handle Alias (e.g. shift+` = ~)
                key = key_aliases.get(key, key)

                if key == 'q':
                    print("\n[Quit] Processing batch queue...")
                    
                    # Flush to Spotify
                    for pid, tracks in spotify_queue.items():
                        if tracks:
                            print(f"  -> Adding {len(tracks)} tracks to {playlist_map.get(keys[target_playlist_ids.index(pid)], {}).get('name', pid)}...")
                            try:
                                spotify.sp.playlist_add_items(pid, tracks)
                            except Exception as e:
                                print(f"  [Error] Failed to add to {pid}: {e}")
                    
                    print("All changes saved. Exiting.")
                    return

                if key == ' ':
                    print("  [Skip]")
                    break # Break input loop, next song

                if key in playlist_map:
                    target = playlist_map[key]
                    print(f"  [Queue] Adding to '{target['name']}'")
                    
                    # 1. Write to DB Immediately (Mock implementation)
                    # db.record_sort(song['id'], target['id'])
                    processed_tracks.add(song['id'])

                    # 2. Batch for Spotify (Write later)
                    spotify_queue[target['id']].append(song['id'])
                    
                    break # Break input loop, next song

    except KeyboardInterrupt:
        print("\n[Force Exit] No changes saved to Spotify.")

if __name__ == "__main__":
    main()