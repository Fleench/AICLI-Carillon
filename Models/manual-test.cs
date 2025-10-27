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

        string clientSecret = FileHelper.ReadSpecificLine(myFile, 1);
        ;

        if (clientSecret == "" || clientSecret == null)
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
        var (at, rt) = await SpotifyWorker.AuthenticateAsync();
        FileHelper.ModifySpecificLine(myFile, 4, clientID);
        FileHelper.ModifySpecificLine(myFile, 1, clientSecret);
        FileHelper.ModifySpecificLine(myFile, 2, at);
        FileHelper.ModifySpecificLine(myFile, 3, rt);
        Console.WriteLine("YO WE DONE with AUTHENTICATED!");
        // HOW MANY SONGS I GOT
        /*
        string localID = "";
        HashSet<string> trackIDs = new(); // ensures uniqueness
        int trackcounter = 0;

        await foreach (var item in SpotifyWorker.GetUserPlaylistsAsync())
        {
            var ids = SpotifyWorker.GetPlaylistDataAsync(item.Id).Result.TrackIDs.Split(";;");
            foreach (var id in ids)
            {
                if (trackIDs.Add(id)) // only true if new ID added
                {
                    trackcounter++;
                    Console.Write($"\r Tracks Found: {trackcounter}");
                }
            }
        }

        await foreach (var item in SpotifyWorker.GetUserAlbumsAsync())
        {
            var ids = SpotifyWorker.GetAlbumDataAsync(item.Id).Result.TrackIDs.Split(";;");
            foreach (var id in ids)
            {
                if (trackIDs.Add(id))
                {
                    trackcounter++;
                    Console.Write($"\r Tracks Found: {trackcounter}");
                }
            }
        }

        await foreach (var item in SpotifyWorker.GetLikedSongsAsync())
        {
            if (trackIDs.Add(item.Id))
            {
                trackcounter++;
                Console.Write($"\r Tracks Found: {trackcounter}");
            }
        }

        trackIDs.Remove("");
        Console.WriteLine("");
        HashSet<string> albumIDs = new();
        int albumcounter = 0;
        foreach (var track in trackIDs)
        {
            if (albumIDs.Add(SpotifyWorker.GetSongDataAsync(track).Result.albumID))
            {
                albumcounter++;
                Console.Write($"\r Albums Found: {albumcounter}");
            }
        }
        HashSet<string> artistIDs = new();
        Console.WriteLine("");
        int artistcounter = 0;
        foreach (var track in trackIDs)
        {
            string[] data = SpotifyWorker.GetSongDataAsync(track).Result.artistIDs.Split(";;");
            foreach (var artist in data)
            {
                if (albumIDs.Add(artist))
                {
                    artistcounter++;
                    Console.Write($"\r Artists Found: {artistcounter}");
                }
            }
        }
        Console.WriteLine($"{trackcounter} tracks found from {albumcounter} albums by {artistcounter} artists");
        */
        // ---- CONFIG ----
        int maxConcurrentCalls = 6; // Hard limit (change if needed)
        var semaphore = new SemaphoreSlim(maxConcurrentCalls);
        const int maxRetries = 5;   // Retry limit for transient errors

        // ---- STEP 1: COLLECT ALL TRACK IDS ----
        var trackIDs = new HashSet<string>();
        int trackcounter = 0;

        Console.WriteLine("Collecting all track IDs...\n");
        
        await foreach (var item in SpotifyWorker.GetUserPlaylistsAsync())
        {
            try
            {
                var ids = SpotifyWorker.GetPlaylistDataAsync(item.Id).Result.TrackIDs.Split(";;");
                foreach (var id in ids)
                    if (trackIDs.Add(id))
                        trackcounter++;

                Console.Write($"\rTracks Found: {trackcounter}");
            }
            catch
            {
                
            }
            
        }

        await foreach (var item in SpotifyWorker.GetUserAlbumsAsync())
        {
            try
            {

            }
            catch
            {
                var ids = SpotifyWorker.GetAlbumDataAsync(item.Id).Result.TrackIDs.Split(";;");
                foreach (var id in ids)
                    if (trackIDs.Add(id))
                        trackcounter++;

                Console.Write($"\rTracks Found: {trackcounter}");
            }
            
        }

        await foreach (var item in SpotifyWorker.GetLikedSongsAsync())
        {
            try
            {

            }
            catch
            {
                if (trackIDs.Add(item.Id))
                    trackcounter++;

                Console.Write($"\rTracks Found: {trackcounter}"); 
            }
            
        }

        trackIDs.Remove("");
        Console.WriteLine($"\n\nTotal unique tracks collected: {trackIDs.Count}\n");

        // ---- STEP 2: FETCH SONG METADATA CONCURRENTLY ----
        var songData = new ConcurrentDictionary<string, (string AlbumID, string[] ArtistIDs)>();
        int completed = 0;

        Console.WriteLine($"Fetching song metadata (max {maxConcurrentCalls} concurrent calls)...\n");

        var tasks = trackIDs.Select(async id =>
        {
            await semaphore.WaitAsync();
            try
            {
                int retryCount = 0;
                while (true)
                {
                    try
                    {
                        var data = await SpotifyWorker.GetSongDataAsync(id);
                        songData[id] = (data.albumID, data.artistIDs.Split(";;"));
                        int done = Interlocked.Increment(ref completed);

                        if (done % 25 == 0)
                            Console.Write($"\rSongs Processed: {done}/{trackIDs.Count}");
                        break; // Success, exit retry loop
                    }
                    catch (SpotifyAPI.Web.APITooManyRequestsException ex) // SpotifyAPI.Web specific
                    {
                        int waitTime = (int)ex.RetryAfter.TotalSeconds + 1;
                        Console.WriteLine($"\n[429] Rate limited. Waiting {waitTime}s...");
                        await Task.Delay(waitTime * 1000);
                    }
                    /*catch
                    {
                        retryCount++;
                        if (retryCount > maxRetries)
                        {
                            Console.WriteLine($"\n[WARN] Skipping {id} after {maxRetries} failed retries (network issues).");
                            break;
                        }

                        int waitTime = retryCount * 2; // exponential backoff
                        Console.WriteLine($"\n[Retry] Network error fetching {id}. Waiting {waitTime}s before retry {retryCount}/{maxRetries}...");
                        await Task.Delay(waitTime * 1000);
                    }*/
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\n[ERROR] {id}: {ex.Message}");
                        break; // Skip bad data
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        Console.WriteLine("\nSong data fetch complete.\n");

        // ---- STEP 3: COUNT ALBUMS & ARTISTS ----
        var albumIDs = new HashSet<string>();
        var artistIDs = new HashSet<string>();

        foreach (var kv in songData.Values)
        {
            albumIDs.Add(kv.AlbumID);
            foreach (var a in kv.ArtistIDs)
                artistIDs.Add(a);
        }

        Console.WriteLine("------------------------------------");
        Console.WriteLine($"Tracks : {trackIDs.Count}");
        Console.WriteLine($"Albums : {albumIDs.Count}");
        Console.WriteLine($"Artists: {artistIDs.Count}");
        Console.WriteLine("------------------------------------\n");
    }
}