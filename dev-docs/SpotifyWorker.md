# SpotifyWorker Documentation

The `SpotifyWorker` is a static class responsible for handling the authentication process with the Spotify API. It uses the [SpotifyAPI.Web](https://github.com/JohnnyCrazy/SpotifyAPI-NET) library to manage the OAuth 2.0 flow.

## Purpose

The main purpose of the `SpotifyWorker` is to abstract the complexity of the Spotify authentication process. It provides a simple way to authenticate the user and get the access and refresh tokens required to make calls to the Spotify API.

## Authentication Flow

The `SpotifyWorker` implements the Authorization Code Flow, which is the recommended OAuth 2.0 flow for applications that can securely store a client secret. The flow is as follows:

1.  The application requests authorization from the user.
2.  The user is redirected to the Spotify website to log in and grant permission to the application.
3.  After the user grants permission, Spotify redirects the user back to the application with an authorization code.
4.  The application exchanges the authorization code for an access token and a refresh token.
5.  The access token is used to make requests to the Spotify API.
6.  When the access token expires, the refresh token is used to get a new access token without requiring the user to log in again.

The `SpotifyWorker` handles this flow by:

*   Starting a local server to listen for the callback from Spotify.
*   Opening a browser for the user to authorize the application.
*   Exchanging the authorization code for an access token and a refresh token.
*   Storing the access token, refresh token, and expiration date.
*   Refreshing the access token when it's about to expire.

## How to Use

To use the `SpotifyWorker`, you first need to initialize it with your Spotify application's client ID and client secret. This is done by calling the `Init` method:

```csharp
SpotifyWorker.Init("YOUR_CLIENT_ID", "YOUR_CLIENT_SECRET");
```

After initializing the `SpotifyWorker`, you can authenticate the user by calling the `AuthenticateAsync` method:

```csharp
var (accessToken, refreshToken) = await SpotifyWorker.AuthenticateAsync();
```

This method will either start the full authentication flow or refresh the access token if a valid refresh token is available. It returns a tuple containing the access token and the refresh token.

You can then use the access token to create a `SpotifyClient` and make requests to the Spotify API:

```csharp
var spotify = new SpotifyClient(accessToken);
var user = await spotify.UserProfile.Current();
```
