# SpotifyWorker Documentation

The `SpotifyWorker` is a static class responsible for handling the authentication process with the Spotify API. It uses the [SpotifyAPI.Web](https://github.com/JohnnyCrazy/SpotifyAPI-NET) library to manage the OAuth 2.0 flow.

## Purpose

The main purpose of the `SpotifyWorker` is to abstract the complexity of the Spotify authentication process. It provides a simple way to authenticate the user and get the access and refresh tokens required to make calls to the Spotify API. It also handles token refreshing automatically.

## Authentication Flow

The `SpotifyWorker` implements the Authorization Code Flow with PKCE, which is the recommended OAuth 2.0 flow for applications that can securely store a client secret. The flow is as follows:

1.  **Initialization**: The `SpotifyWorker` is initialized with your Spotify application's credentials. You can also provide existing access and refresh tokens if you have them.
2.  **Authentication**: When `AuthenticateAsync` is called, the `SpotifyWorker` checks if it has a valid refresh token.
    *   **Token Refresh**: If a valid refresh token exists, the `SpotifyWorker` will attempt to use it to get a new access token from Spotify without requiring user interaction. This is a silent process.
    *   **Full Authentication**: If there is no valid refresh token, the `SpotifyWorker` will start the full authentication flow:
        1.  It starts a local server to listen for a callback from Spotify.
        2.  It opens a browser window and directs the user to the Spotify login and authorization page.
        3.  After the user grants permission, Spotify redirects them back to the local server with an authorization code.
        4.  The `SpotifyWorker` exchanges this code for an access token and a refresh token.
3.  **Token Management**: The `SpotifyWorker` stores the tokens and will automatically refresh the access token when it's about to expire.

## How to Use

To use the `SpotifyWorker`, you first need to initialize it with your Spotify application's client ID and client secret. You can also provide existing tokens if you have them stored from a previous session.

### 1. Initialization

Call the `Init` method at the start of your application.

```csharp
// Your Spotify application credentials
string clientId = "YOUR_CLIENT_ID";
string clientSecret = "YOUR_CLIENT_SECRET";

// Previously stored tokens (if available)
string storedAccessToken = ""; // Load from storage if you have it
string storedRefreshToken = ""; // Load from storage if you have it

// Initialize the SpotifyWorker
SpotifyWorker.Init(clientId, clientSecret, storedAccessToken, storedRefreshToken);
```

### 2. Authentication

After initialization, call the `AuthenticateAsync` method to get the access and refresh tokens.

```csharp
// This will either refresh the token or start the full authentication flow
var (newAccessToken, newRefreshToken) = await SpotifyWorker.AuthenticateAsync();

// It's important to store the new tokens for future sessions
// For example, you could save them to a file:
FileHelper.ModifySpecificLine("settings.txt", 2, newAccessToken);
FileHelper.ModifySpecificLine("settings.txt", 3, newRefreshToken);

```

### 3. Using the Spotify API

Once you have the access token, you can use it to create a `SpotifyClient` and make requests to the Spotify API.

```csharp
var spotify = new SpotifyClient(newAccessToken);

// Example: Get the current user's profile
var user = await spotify.UserProfile.Current();
Console.WriteLine($"Welcome, {user.DisplayName}!");
```

### Complete Example

Here is a complete example of how you might use the `SpotifyWorker` in a console application:

```csharp
using System;
using System.Threading.Tasks;
using Spotify_Playlist_Manager.Models;
using Spotify_Playlist_Manager.Models.txt; // For FileHelper

public class ExampleProgram
{
    static async Task Main(string[] args)
    {
        string settingsFile = "settings.txt";
        string clientId = "YOUR_CLIENT_ID";
        string clientSecret = "YOUR_CLIENT_SECRET";

        // Read stored tokens from a file
        string token = FileHelper.ReadSpecificLine(settingsFile, 2) ?? "";
        string refreshToken = FileHelper.ReadSpecificLine(settingsFile, 3) ?? "";

        // Initialize the SpotifyWorker
        SpotifyWorker.Init(clientId, clientSecret, token, refreshToken);

        try
        {
            // Authenticate and get the latest tokens
            var (newAccessToken, newRefreshToken) = await SpotifyWorker.AuthenticateAsync();

            // Save the new tokens for the next session
            FileHelper.ModifySpecificLine(settingsFile, 2, newAccessToken);
            FileHelper.ModifySpecificLine(settingsFile, 3, newRefreshToken);

            // Create a SpotifyClient and make an API call
            var spotify = new SpotifyClient(newAccessToken);
            var user = await spotify.UserProfile.Current();
            Console.WriteLine($"Successfully authenticated as {user.DisplayName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Authentication failed: {ex.Message}");
        }
    }
}
```
