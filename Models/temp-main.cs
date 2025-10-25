using Spotify_Playlist_Manager.Models.txt;
using System;
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
        FileHelper.ModifySpecificLine(myFile,0,"null"); //client token
        FileHelper.ModifySpecificLine(myFile,1,"null"); //client secrey
        FileHelper.ModifySpecificLine(myFile,2,"null"); //token
        FileHelper.ModifySpecificLine(myFile,3,"null"); //refresh token 
        */
        string clientID = "82074d3d33b4464c8be925c455fc64b4";
        string clientSecret = "e6ed0a06b29846208ac2ee24e8d54943";
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
        FileHelper.ModifySpecificLine(myFile, 2, at);
        FileHelper.ModifySpecificLine(myFile, 3, rt);
        //Console.WriteLine(FileHelper.ReadAllLines(myFile));
    }
}