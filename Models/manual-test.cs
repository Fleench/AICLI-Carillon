using Spotify_Playlist_Manager.Models.txt;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Spotify_Playlist_Manager.Models;
using System.Collections.Concurrent;
using System.Threading;
public class TempProgram
{
    static async Task Main(string[] args)
    {
        // --- Example Usage ---

        string myFile = "settings.txt";
        //old code
        /*
        FileHelper.ModifySpecificLine(myFile,4,"null"); //client token
        FileHelper.ModifySpecificLine(myFile,1,"null"); //client secret
        FileHelper.ModifySpecificLine(myFile,2,"null"); //token
        FileHelper.ModifySpecificLine(myFile,3,"null"); //refresh token
        */
        string clientID = FileHelper.ReadSpecificLine(myFile, 4);
        if (clientID == "" || clientID == null)
        {
            Console.WriteLine("Could not read settings.txt file for ClientID");
            clientID = Console.ReadLine();
        }

        string  clientSecret = FileHelper.ReadSpecificLine(myFile, 1);;
        
        if(clientSecret == "" || clientSecret == null)
        {
            Console.WriteLine("Could not read settings.txt file for ClientSecret");
            clientSecret = Console.ReadLine();
            
        }
        string token = "";
        string refreshToken = "";
        if (FileHelper.ReadSpecificLine(myFile, 2) != "null")
        {
            token = FileHelper.ReadSpecificLine(myFile, 2);
        }
        if (FileHelper.ReadSpecificLine(myFile, 3) != "null") 
        {
            refreshToken = FileHelper.ReadSpecificLine(myFile, 3);
        }
        SpotifyWorker.Init(clientID, clientSecret, token, refreshToken);
        var (at,rt) = await SpotifyWorker.AuthenticateAsync();
        FileHelper.ModifySpecificLine(myFile, 4, clientID);
        FileHelper.ModifySpecificLine(myFile,1, clientSecret);
        FileHelper.ModifySpecificLine(myFile, 2, at);
        FileHelper.ModifySpecificLine(myFile, 3, rt);
        Console.WriteLine("YO WE DONE with AUTHENTICATED!");
        //HOW MANY SONGS I GOT
        /*
        string localID = "";
        List<string> trackIDs = [];
        int trackcounter = 0;
        int change = 0;
        await foreach (var item in SpotifyWorker.GetUserPlaylistsAsync())
        {
            trackIDs.AddRange(SpotifyWorker.GetPlaylistDataAsync(item.Id).Result.TrackIDs.Split(";;"));
            change = trackIDs.Count - trackcounter;
            for (int i = 0; i <= change; i++)
            {
                trackcounter++;
                Console.Write($"\r Tracks Found: {trackcounter}");
            }
            
        }
        await foreach (var item in SpotifyWorker.GetUserAlbumsAsync())
        {
            trackIDs.AddRange(SpotifyWorker.GetAlbumDataAsync(item.Id).Result.TrackIDs.Split(";;"));
            change = trackIDs.Count - trackcounter;
            for (int i = 0; i <= change; i++)
            {
                trackcounter++;
                Console.Write($"\r Tracks Found: {trackcounter}");
            }
        }

        await foreach (var item in SpotifyWorker.GetLikedSongsAsync())
        {
            trackIDs.Add(item.Id);
            trackcounter++;
            Console.Write($"\r Tracks Found: {trackcounter}");
        }

        var uniqueTrackIDs = trackIDs.Distinct().ToList();
        Console.WriteLine($"\n\rYou have {uniqueTrackIDs.Count} unique tracks from a pulled list of {trackIDs.Count} tracks.");
        */


// adjustable concurrency (Spotify seems happy at 8–10)
const int MAX_CONCURRENT_REQUESTS = 8;

var trackIDs = new ConcurrentBag<string>();
int trackCounter = 0;
var semaphore = new SemaphoreSlim(MAX_CONCURRENT_REQUESTS);

// --- Helper method for progress-safe increment ---
void PrintProgress()
{
    Console.Write($"\rTracks Found: {trackCounter}");
}

// --- Playlists ---
var playlistTasks = new List<Task>();
await foreach (var playlist in SpotifyWorker.GetUserPlaylistsAsync())
{
    await semaphore.WaitAsync();
    playlistTasks.Add(Task.Run(async () =>
    {
        try
        {
            var data = await SpotifyWorker.GetPlaylistDataAsync(playlist.Id);
            foreach (var id in data.TrackIDs.Split(";;", StringSplitOptions.RemoveEmptyEntries))
            {
                trackIDs.Add(id);
                Interlocked.Increment(ref trackCounter);
                PrintProgress();
            }
        }
        finally
        {
            semaphore.Release();
        }
    }));
}
await Task.WhenAll(playlistTasks);

// --- Albums ---
var albumTasks = new List<Task>();
await foreach (var album in SpotifyWorker.GetUserAlbumsAsync())
{
    await semaphore.WaitAsync();
    albumTasks.Add(Task.Run(async () =>
    {
        try
        {
            var data = await SpotifyWorker.GetAlbumDataAsync(album.Id);
            foreach (var id in data.TrackIDs.Split(";;", StringSplitOptions.RemoveEmptyEntries))
            {
                trackIDs.Add(id);
                Interlocked.Increment(ref trackCounter);
                PrintProgress();
            }
        }
        finally
        {
            semaphore.Release();
        }
    }));
}
await Task.WhenAll(albumTasks);

// --- Liked Songs (sequential, already rate-safe) ---
await foreach (var song in SpotifyWorker.GetLikedSongsAsync())
{
    trackIDs.Add(song.Id);
    Interlocked.Increment(ref trackCounter);
    PrintProgress();
}

// --- Deduplicate ---
var uniqueTrackIDs = trackIDs.Distinct().ToList();
Console.WriteLine($"\nYou have {uniqueTrackIDs.Count} unique tracks from a pulled list of {trackIDs.Count} total tracks.");

    }    
}