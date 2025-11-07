/* File: FuzzyMatchLogic.Cs
 * Author: Glenn Sutherland
 * Description: Finds songs that may be the same song and assigns them
 *              the same internal songID. If the system is unsure then
 *              it saves it and allows the user to decide for themselves.
 */

using System.Collections.Generic;
using System.Linq;

namespace Spotify_Playlist_Manager.Models
{
    public static class FuzzyMatchLogic
    {
        public static List<Variables.Track> SameTracks = new();
        public static void BasicMatch()
        {
            SameTracks.Clear();
            var tracks = DataCoordinator.GetAllTracks();
            HashSet<Variables.Track> CheckedTracks = new ();
            foreach (var MainTrack in tracks)
            {
                if (CheckedTracks.Contains(MainTrack))
                {
                    continue;
                }
                foreach (var OtherTrack in tracks)
                {
                    double score = 0;
                    
                    if (MainTrack.Name.Equals(OtherTrack.Name) && MainTrack.ArtistIds.Equals(OtherTrack.ArtistIds) && 
                        !MainTrack.Id.Equals(OtherTrack.Id))
                    {
                        score = 1;
                    }
                    else
                    {
                        score = ScoreSong(MainTrack, OtherTrack);
                    }

                    if (score >= 0.75)
                    {
                        Variables.Track temptrack = OtherTrack;
                        temptrack.SongID = MainTrack.SongID;
                        SameTracks.Add(temptrack);
                    }
                    else if (score >= 0.45)
                    {
                        DataCoordinator.SetMightBeSimilarAsync(MainTrack.SongID, OtherTrack.SongID);
                    }
                }
                CheckedTracks.Add(MainTrack);
            }

            foreach (var track in SameTracks)
            {
                DataCoordinator.SetTrackAsync(track);
            }
        }

        private static double ScoreSong(Variables.Track track1, Variables.Track track2)
        {
            track1 = new()
            {
                Id = track1.Id,
                Name = track1.Name,
                ArtistIds = track1.ArtistIds,
                SongID = track1.SongID,
                AlbumId = track1.AlbumId,
        DiscNumber = track1.DiscNumber,
        DurationMs = track1.DurationMs,
        Explicit = track1.Explicit,
        PreviewUrl = track1.PreviewUrl,
        TrackNumber = track1.TrackNumber,
                
            };
            track2 = new()
            {
                Id = track2.Id,
                Name = track2.Name,
                ArtistIds = track2.ArtistIds,
                SongID = track2.SongID,
                AlbumId = track2.AlbumId,
                DiscNumber = track2.DiscNumber,
                DurationMs = track2.DurationMs,
                Explicit = track2.Explicit,
                PreviewUrl = track2.PreviewUrl,
                TrackNumber = track2.TrackNumber,
            };
            track1.Name = track1.Name.ToLower().Replace(" ", "").Trim();
            track2.Name = track2.Name.ToLower().Replace(" ", "").Trim();
            string[] t1Artists =  track1.ArtistIds.Split(Variables.Seperator);
            string[] t2Artists = track2.ArtistIds.Split(Variables.Seperator);
            double score = 0;
            //check if they have already been marked
            if (track1.SongID.Equals(track2.SongID))
            {
                score = 1;
            }
            //do they have the same PreviewURL
            if (track1.PreviewUrl.Equals(track2.PreviewUrl))
            {
                score = 1;
            }
            //do they share an album
            if (track1.AlbumId.Equals(track2.AlbumId))
            {
                score+=0.1;
            }
            //do they share artists
            if (track1.ArtistIds.Equals(track2.ArtistIds))
            {
                score += 0.25;
            }
            else
            {
                foreach (var t1 in t1Artists)
                {
                    if (t2Artists.Contains(t1))
                    {
                        score += 0.1;
                    }
                }
            }

            //same disc
            if (track1.DiscNumber.Equals(track2.DiscNumber))
            {
                score += 0.05;
            }
            //same length
            if (track1.DurationMs.Equals(track2.DurationMs))
            {
                score += 0.05;
            }
            //share explicit
            if (track1.Explicit.Equals(track2.Explicit))
            {
                score += 0.05;
            }
            //same spot in album
            if (track1.TrackNumber.Equals(track2.TrackNumber))
            {
                score += 0.05;
            }
            //do they have similar names
            if (track1.Name.StartsWith(track2.Name) || track2.Name.StartsWith(track1.Name))
            {
                score += .5;
            }
            else if (track1.Name.Contains(track2.Name) || track2.Name.Contains(track1.Name))
            {
                score += .3;
            }
            return score;
        }
    }
}

