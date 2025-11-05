/* File: FuzzyMatchLogic.Cs
 * Author: Glenn Sutherland
 * Description: Finds songs that may be the same song and assigns them
 *              the same internal songID. If the system is unsure then
 *              it saves it and allows the user to decide for themselves.
 */

using System.Collections.Generic;

namespace Spotify_Playlist_Manager.Models
{
    public static class FuzzyMatchLogic
    {
        public static List<Variables.Track> SameTracks = new();
        public static void BasicMatch()
        {
            SameTracks.Clear();
            var tracks = DataCoordinator.GetAllTracks();
            foreach (var MainTrack in tracks)
            {
                foreach (var OtherTrack in tracks)
                {
                    if (MainTrack.Name.Equals(OtherTrack.Name) && MainTrack.ArtistIds.Equals(OtherTrack.ArtistIds) && 
                        !MainTrack.Id.Equals(OtherTrack.Id) && MainTrack is not null && OtherTrack is not null)
                    {
                        Variables.Track temptrack = OtherTrack;
                        temptrack.SongID = MainTrack.SongID;
                        SameTracks.Add(temptrack);
                    }
                }
            }

            foreach (var track in SameTracks)
            {
                DataCoordinator.SetTrackAsync(track);
            }
        }
    }
}

