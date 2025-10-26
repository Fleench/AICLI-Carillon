using Spotify_Playlist_Manager.Models.txt;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Spotify_Playlist_Manager.Models;
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
        string localID = "";
        await foreach (var item in SpotifyWorker.GetUserPlaylistsAsync())
        {
            localID = item.Id;
            Console.WriteLine(item.Name);
            break;
        }

        var tup = SpotifyWorker.GetPlaylistDataAsync(localID);
        foreach (string trackID in tup.Result.TrackIDs.Split(";;"))
        {
            try
            {
                Console.WriteLine(SpotifyWorker.GetSongDataAsync(trackID).Result.name);
            }
            catch
            {
                Console.WriteLine("Could not get song data for track ID: " + trackID);
            }
        }
    }    
}