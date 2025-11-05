    /* File: CacheWorker.cs
     * Author: Glenn Sutherland
     * Description: A basic manager for the local cache of images of items from spotify.
     */
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;
    namespace Spotify_Playlist_Manager.Models
    {
        public static class CacheWorker
        {
            public static string Cachepath = Variables.CachePath;
            public static async Task DownloadImageAsync(string url, ImageType type, string itemId)
            {
                using HttpClient client = new();
    
                // Use HttpCompletionOption.ResponseHeadersRead to get headers before downloading the full content
                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                // 1. Get the MIME type from the Content-Type header
                string contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

                // 2. Map the MIME type to a file extension
                string extension = GetFileExtensionFromMimeType(contentType);
    
                // 3. Construct the full path with the determined extension
                string fileName = type.ToString() + "_" + itemId + extension;
                string path = Path.Combine(Cachepath, fileName);
    
                Console.WriteLine(path);

                // 4. Download and save the content (response.Content is still available)
                await using var fileStream = new FileStream(path, FileMode.Create);
                await response.Content.CopyToAsync(fileStream);
            }
            private static string GetFileExtensionFromMimeType(string mimeType)
            {
                // Convert to lowercase for case-insensitive comparison
                mimeType = mimeType.ToLowerInvariant(); 

                // Handle the common image types for Spotify
                if (mimeType.Contains("image/jpeg"))
                {
                    return ".jpg";
                }
                if (mimeType.Contains("image/png"))
                {
                    return ".png";
                }
                if (mimeType.Contains("image/gif"))
                {
                    return ".gif";
                }

                // Fallback: If the type is unknown, use a generic binary extension or skip
                return ".bin"; 
            }
            public enum ImageType
            {
                Artist,
                Album,
                Playlist,
            }

            public static string GetImagePath(ImageType type, string itemId)
            {
                // 1. Create the base filename (without extension)
                string baseFileName = type.ToString() + "_" + itemId;
                string searchPattern = baseFileName + ".*"; // e.g., "Album_123.*"

                // 2. Search the Cachepath directory for any file matching the pattern
                // The search option ensures it returns only files, not directories.
                // The search option TopDirectoryOnly means it only looks in the Cachepath folder itself.
                string[] files = Directory.GetFiles(
                    Cachepath, 
                    searchPattern, 
                    SearchOption.TopDirectoryOnly
                );

                // 3. Check the results
                if (files.Length > 0)
                {
                    // The file is found! Return the full path of the first match (e.g., "C:\cache\Album_123.jpg")
                    return files[0];
                }
                else
                {
                    // The file is not found. Return null to indicate it needs to be downloaded.
                    return null;
                }
            }
        }
    }

