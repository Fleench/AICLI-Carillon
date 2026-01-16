from __future__ import annotations
from typing import Optional, List, Dict, Any, Generator

import spotipy
from spotipy.oauth2 import SpotifyOAuth
from carillon.database_worker import DatabaseWorker

class SpotifyWorker:
    """
    Handles Spotify API interactions using spotipy.
    Manages authentication tokens via the shared SQLite database.
    """
    
    # Constants for DB keys - must match C# Variables.Settings
    KEY_CLIENT_ID = "SW_ClientToken"
    KEY_CLIENT_SECRET = "SW_ClientSecret"
    KEY_ACCESS_TOKEN = "SW_AccessToken"
    KEY_REFRESH_TOKEN = "SW_RefreshToken"
    
    # Scopes matching the C# app
    SCOPES = [
        "user-read-email",
        "playlist-modify-private",
        "playlist-modify-public",
        "playlist-read-collaborative",
        "playlist-read-private",
        "user-library-read",
        "user-library-modify",
        "user-modify-playback-state",
        "user-read-private"
    ]
    
    REDIRECT_URI = "http://127.0.0.1:5543/callback"

    def __init__(self, db_worker: DatabaseWorker):
        self.db = db_worker
        self.sp: Optional[spotipy.Spotify] = None
        self.client_id: Optional[str] = None
        self.client_secret: Optional[str] = None

    def authenticate(self) -> None:
        """
        Authenticates with Spotify using tokens from the DB if available.
        Otherwise, triggers the OAuth flow and saves new tokens.
        """
        # 1. Load Credentials
        self.client_id = self.db.get_setting(self.KEY_CLIENT_ID)
        self.client_secret = self.db.get_setting(self.KEY_CLIENT_SECRET)
        
        if not self.client_id or not self.client_secret:
            raise ValueError("Client ID or Secret missing from Database. Please set SW_ClientToken and SW_ClientSecret.")

        # 2. Try to load existing tokens
        access_token = self.db.get_setting(self.KEY_ACCESS_TOKEN)
        refresh_token = self.db.get_setting(self.KEY_REFRESH_TOKEN)
        
        # 3. Initialize the Auth Manager
        # We use a custom cache handler or manually manage tokens to integrate with DB
        auth_manager = SpotifyOAuth(
            client_id=self.client_id,
            client_secret=self.client_secret,
            redirect_uri=self.REDIRECT_URI,
            scope=" ".join(self.SCOPES),
            open_browser=True
        )

        token_info = None

        # 4. Check if we have a valid refresh token to perform a silent refresh
        if refresh_token:
            print("Found existing refresh token in DB. Attempting refresh...")
            try:
                # refresh_access_token returns the full token info dict
                token_info = auth_manager.refresh_access_token(refresh_token)
            except Exception as e:
                print(f"Failed to refresh token: {e}. Falling back to full auth.")
                token_info = None

        # 5. If refresh failed or no tokens existed, do the full flow
        if not token_info:
            print("No valid tokens found. Starting Browser Auth...")
            # get_access_token handles the local server and browser interaction
            # It normally tries to cache to a file, but we will intercept the result
            code = auth_manager.get_auth_response()
            token_info = auth_manager.get_access_token(code, as_dict=True)

        # 6. Save the fresh tokens back to the DB
        if token_info:
            self._save_tokens(token_info)
            # Initialize the client with the fresh access token
            self.sp = spotipy.Spotify(auth=token_info['access_token'])
            print("Authentication Successful.")
        else:
            raise ConnectionError("Failed to retrieve valid tokens.")

    def _save_tokens(self, token_info: dict) -> None:
        """Saves access and refresh tokens to the SQLite DB."""
        if 'access_token' in token_info:
            self.db.set_setting(self.KEY_ACCESS_TOKEN, token_info['access_token'])
        
        if 'refresh_token' in token_info:
            self.db.set_setting(self.KEY_REFRESH_TOKEN, token_info['refresh_token'])
            
        print("Tokens saved to Database.")

    def play_track(self, track_id: str) -> None:
        """Starts playback of the given track ID on the active device."""
        if not self.sp:
            raise ConnectionError("Not authenticated.")
            
        uri = f"spotify:track:{track_id}"
        try:
            self.sp.start_playback(uris=[uri])
        except spotipy.SpotifyException as e:
            print(f"Playback Error: {e}")

    def add_to_playlist(self, playlist_id: str, track_id: str) -> None:
        """Adds a track to a playlist."""
        if not self.sp:
            raise ConnectionError("Not authenticated.")
            
        try:
            self.sp.playlist_add_items(playlist_id, [track_id])
            print(f"Added {track_id} to {playlist_id}")
        except spotipy.SpotifyException as e:
            print(f"Add Error: {e}")

    def get_liked_songs(self, limit: int = 50) -> Generator[Dict[str, Any], None, None]:
        """Yields liked songs from the user's library."""
        if not self.sp:
            raise ConnectionError("Not authenticated.")

        offset = 0
        while True:
            results = self.sp.current_user_saved_tracks(limit=limit, offset=offset)
            items = results.get('items', [])
            
            if not items:
                break
                
            for item in items:
                track = item['track']
                yield {
                    'id': track['id'],
                    'name': track['name'],
                    'artists': ", ".join(a['name'] for a in track['artists'])
                }
                
            offset += limit
            if results['next'] is None:
                break