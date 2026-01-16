"""Spotify worker that mirrors the C# SpotifyWorker behavior."""

from __future__ import annotations

__author__ = "ChatGPT Codex"

from dataclasses import dataclass
import os
from typing import Generator, Optional

from dotenv import load_dotenv
import spotipy
from spotipy.cache_handler import CacheHandler
from spotipy.oauth2 import SpotifyOAuth

from carillon.database_worker import DatabaseWorker


@dataclass
class SpotifySettings:
    """Database keys for shared Spotify settings."""

    access_token_key: str = "SW_AccessToken"
    refresh_token_key: str = "SW_RefreshToken"
    client_id_key: str = "SW_ClientToken"
    client_secret_key: str = "SW_ClientSecret"


class DatabaseTokenCacheHandler(CacheHandler):
    """Cache handler that persists tokens in the shared SQLite database."""

    def __init__(self, db: DatabaseWorker, settings: SpotifySettings) -> None:
        self._db = db
        self._settings = settings

    def get_cached_token(self) -> Optional[dict]:
        access_token = self._db.get_setting(self._settings.access_token_key)
        refresh_token = self._db.get_setting(self._settings.refresh_token_key)
        if not access_token and not refresh_token:
            return None
        return {
            "access_token": access_token or "",
            "refresh_token": refresh_token or "",
            "expires_at": 0,
        }

    def save_token_to_cache(self, token_info: dict) -> None:
        access_token = token_info.get("access_token")
        refresh_token = token_info.get("refresh_token")
        if access_token:
            self._db.set_setting(self._settings.access_token_key, access_token)
        if refresh_token:
            self._db.set_setting(self._settings.refresh_token_key, refresh_token)


class SpotifyWorker:
    """Wrapper around spotipy that reuses the existing token storage."""

    def __init__(self, db: DatabaseWorker, settings: SpotifySettings | None = None) -> None:
        self._db = db
        self._settings = settings or SpotifySettings()
        self._spotify: Optional[spotipy.Spotify] = None
        self._auth_manager: Optional[SpotifyOAuth] = None

    def _build_auth_manager(self) -> SpotifyOAuth:
        load_dotenv()
        client_id = self._db.get_setting(self._settings.client_id_key) or os.getenv(
            "SPOTIPY_CLIENT_ID"
        )
        client_secret = self._db.get_setting(self._settings.client_secret_key) or os.getenv(
            "SPOTIPY_CLIENT_SECRET"
        )
        redirect_uri = os.getenv("SPOTIPY_REDIRECT_URI", "http://127.0.0.1:8888/callback")

        if not client_id or not client_secret:
            raise RuntimeError(
                "Spotify client credentials are missing. Set SW_ClientToken and "
                "SW_ClientSecret in the database or provide SPOTIPY_CLIENT_ID and "
                "SPOTIPY_CLIENT_SECRET in your environment."
            )

        cache_handler = DatabaseTokenCacheHandler(self._db, self._settings)
        scope = " ".join(
            [
                "user-library-read",
                "user-modify-playback-state",
                "user-read-playback-state",
                "user-read-private",
            ]
        )
        return SpotifyOAuth(
            client_id=client_id,
            client_secret=client_secret,
            redirect_uri=redirect_uri,
            scope=scope,
            cache_handler=cache_handler,
            open_browser=True,
        )

    def authenticate(self) -> None:
        """Authenticate with Spotify and persist tokens in the shared database."""
        self._auth_manager = self._build_auth_manager()
        token_info = self._auth_manager.get_cached_token()

        if token_info and not self._auth_manager.is_token_expired(token_info):
            pass
        elif token_info and token_info.get("refresh_token"):
            token_info = self._auth_manager.refresh_access_token(token_info["refresh_token"])
        else:
            auth_url = self._auth_manager.get_authorize_url()
            print("Open the following URL in your browser and authorize access:")
            print(auth_url)
            response = input("Paste the full redirect URL here: ").strip()
            code = self._auth_manager.parse_response_code(response)
            token_info = self._auth_manager.get_access_token(code, as_dict=True)

        if not token_info:
            raise RuntimeError("Failed to obtain Spotify tokens.")

        self._spotify = spotipy.Spotify(auth_manager=self._auth_manager)

    def play_track(self, track_id: str) -> None:
        if not self._spotify:
            raise RuntimeError("SpotifyWorker.authenticate must be called first.")
        if not track_id:
            raise ValueError("track_id must be a non-empty string.")
        uri = f"spotify:track:{track_id}"
        self._spotify.start_playback(uris=[uri])

    def get_liked_songs(self) -> Generator[dict, None, None]:
        """Yield liked tracks from the user's library."""
        if not self._spotify:
            raise RuntimeError("SpotifyWorker.authenticate must be called first.")

        results = self._spotify.current_user_saved_tracks(limit=50)
        while results and results.get("items"):
            for item in results["items"]:
                track = item.get("track")
                if not track:
                    continue
                artists = ", ".join(artist["name"] for artist in track.get("artists", []))
                yield {
                    "id": track.get("id"),
                    "name": track.get("name"),
                    "artists": artists,
                }
            results = self._spotify.next(results) if results.get("next") else None
