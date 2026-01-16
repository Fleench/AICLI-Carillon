import sys
import clr

# Add build output to sys.path so pythonnet can locate the DLL.
sys.path.append("./bin/Debug/net8.0")

# Load the compiled assembly (Spotify Playlist Manager.dll).
clr.AddReference("Spotify Playlist Manager")

from Spotify_Playlist_Manager.Models import SpotifyWorker, DatabaseWorker, DataCoordinator, Variables
from Spotify_Playlist_Manager import AsyncHelper

# Initialize core workers.
DatabaseWorker.Init()
SpotifyWorker.Init()

# Authenticate (run async task synchronously from Python).
AsyncHelper.Run(SpotifyWorker.AuthenticateAsync())

# Demonstrate capability: read a setting from the database.
print("Client Token setting:", DataCoordinator.GetSetting(Variables.Settings.SW_ClientToken))

# Fetch playlist data (replace with a real playlist ID).
playlist_id = "your_playlist_id_here"
playlist_data = AsyncHelper.Get(SpotifyWorker.GetPlaylistDataAsync(playlist_id))
print("Playlist data:", playlist_data)

# Play a track (replace with a real track ID).
track_id = "your_track_id_here"
play_result = AsyncHelper.Get(SpotifyWorker.PlayTrack(track_id))
print("Play result:", play_result)
