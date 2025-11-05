    /* File: CacheWorker.cs
     * Author: Glenn Sutherland, ChatGPT Codex
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
            public static async Task<string?> DownloadImageAsync(string url, ImageType type, string itemId)
            {
                if (string.IsNullOrWhiteSpace(url))
                {
                    return null;
                }

                try
                {
                    Directory.CreateDirectory(Cachepath);
                }
                catch (Exception directoryException) when (directoryException is IOException or UnauthorizedAccessException)
                {
                    Console.Error.WriteLine($"Failed to create cache directory '{Cachepath}': {directoryException}");
                    return null;
                }

                try
                {
                    using HttpClient client = new();

                    using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    string contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                    string extension = GetFileExtensionFromMimeType(contentType);
                    string fileName = type.ToString() + "_" + itemId + extension;
                    string path = Path.Combine(Cachepath, fileName);

                    try
                    {
                        await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                        await response.Content.CopyToAsync(fileStream).ConfigureAwait(false);
                        return path;
                    }
                    catch (Exception fileException) when (fileException is IOException or UnauthorizedAccessException)
                    {
                        Console.Error.WriteLine($"Failed to write cached image '{path}': {fileException}");
                        return null;
                    }
                }
                catch (HttpRequestException httpException)
                {
                    Console.Error.WriteLine($"Failed to download image from '{url}': {httpException}");
                    return null;
                }
                catch (TaskCanceledException canceledException)
                {
                    Console.Error.WriteLine($"Image download timed out for '{url}': {canceledException}");
                    return null;
                }
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

            public static string? GetImagePath(ImageType type, string itemId)
            {
                try
                {
                    string baseFileName = type.ToString() + "_" + itemId;
                    string searchPattern = baseFileName + ".*"; // e.g., "Album_123.*"

                    string[] files = Directory.GetFiles(
                        Cachepath,
                        searchPattern,
                        SearchOption.TopDirectoryOnly
                    );

                    if (files.Length > 0)
                    {
                        return files[0];
                    }
                }
                catch (Exception ioException) when (ioException is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
                {
                    Console.Error.WriteLine($"Failed to read cache directory '{Cachepath}': {ioException}");
                }

                return null;
            }
        }
    }

