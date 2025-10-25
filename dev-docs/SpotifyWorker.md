# SpotifyWorker Class Documentation

## Introduction

The `SpotifyWorker` is a static class that serves as the sole communication layer between the application and the Spotify API. It is designed to handle all aspects of the Spotify authentication process and provide a simplified interface for retrieving user data, such as liked songs and playlists. This class encapsulates the complexities of the SpotifyAPI.Web library, ensuring that the rest of the application does not need to interact with it directly.

## Key Responsibilities

-   **Authentication:** Manages the OAuth 2.0 authentication flow, including token refreshing.
-   **Data Retrieval:** Provides methods to fetch user-specific data from the Spotify API.
-   **API Abstraction:** Acts as a wrapper around the SpotifyAPI.Web library, simplifying its usage.

## Usage

To use the `SpotifyWorker`, you must first initialize it with your Spotify application's credentials. After initialization, you can authenticate the user and then call the data retrieval methods. The following example demonstrates the basic workflow from within a `temp-main` context.

### 1. Initialization

Before making any calls to the Spotify API, you must initialize the `SpotifyWorker` with your client ID and client secret. You can obtain these from the Spotify Developer Dashboard.

```csharp
string clientID = "YOUR_CLIENT_ID";
string clientSecret = "YOUR_CLIENT_SECRET";

// Optionally, you can provide existing access and refresh tokens
// to avoid re-authentication.
string accessToken = ""; // Provide a saved access token, if available
string refreshToken = ""; // Provide a saved refresh token, if available

SpotifyWorker.Init(clientID, clientSecret, accessToken, refreshToken);
```

### 2. Authentication

After initialization, you must authenticate the user. The `AuthenticateAsync` method handles both the initial authentication and token refreshing. If a valid refresh token is available, it will be used to get a new access token; otherwise, it will initiate the full authentication flow, which may require user interaction.

```csharp
var (accessToken, refreshToken) = await SpotifyWorker.AuthenticateAsync();

// It is recommended to save the new access and refresh tokens
// for future sessions to avoid repeated user logins.
```

### 3. Data Retrieval

Once authenticated, you can use the various data retrieval methods to get information from the user's Spotify account. The following examples show how to get the user's liked songs and playlists.

#### Getting Liked Songs

The `GetLikedSongsAsync` method returns an `IAsyncEnumerable` of `SimpleLikedTrack` objects, each containing the ID, name, and artists of a liked song.

```csharp
await foreach (var song in SpotifyWorker.GetLikedSongsAsync())
{
    Console.WriteLine($"ID: {song.Id}, Name: {song.Name}, Artists: {song.Artists}");
}
```

#### Getting User Playlists

The `GetUserPlaylistsAsync` method returns an `IAsyncEnumerable` of `SimplePlaylist` objects, each containing the ID, name, and track count of a user's playlist.

```csharp
await foreach (var playlist in SpotifyWorker.GetUserPlaylistsAsync())
{
    Console.WriteLine($"ID: {playlist.Id}, Name: {playlist.Name}, Tracks: {playlist.TrackCount}");
}
```

## Full Example

The following is a complete example of how to use the `SpotifyWorker` class from within a `temp-main` file.

```csharp
using System;
using System.Threading.Tasks;
using Spotify_Playlist_Manager.Models;

public class TempProgram
{
    static async Task Main(string[] args)
    {
        // 1. Initialization
        string clientID = "YOUR_CLIENT_ID";
        string clientSecret = "YOUR_CLIENT_SECRET";
        string accessToken = ""; // Load from a secure location, if available
        string refreshToken = ""; // Load from a secure location, if available

        SpotifyWorker.Init(clientID, clientSecret, accessToken, refreshToken);

        // 2. Authentication
        var (newAccessToken, newRefreshToken) = await SpotifyWorker.AuthenticateAsync();

        // Save the new tokens for future use
        // For example, write them to a secure configuration file
        Console.WriteLine("Authentication successful!");
        Console.WriteLine($"New Access Token: {newAccessToken}");
        Console.WriteLine($"New Refresh Token: {newRefreshToken}");

        // 3. Data Retrieval
        Console.WriteLine("\nFetching liked songs...");
        int songCount = 0;
        await foreach (var song in SpotifyWorker.GetLikedSongsAsync())
        {
            Console.WriteLine($"- {song.Name} by {song.Artists}");
            if (++songCount >= 20) break; // Limit to the first 20 songs for this example
        }

        Console.WriteLine("\nFetching user playlists...");
        await foreach (var playlist in SpotifyWorker.GetUserPlaylistsAsync())
        {
            Console.WriteLine($"- {playlist.Name} ({playlist.TrackCount} tracks)");
        }
    }
}
```

This example demonstrates the complete, recommended workflow for using the `SpotifyWorker` class. By following this pattern, you can ensure that the application is properly authenticated and can interact with the Spotify API as needed.
