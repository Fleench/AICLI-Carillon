/* File: AsyncHelper.cs
 * Author: Glenn Sutherland, ChatGPT Codex
 * Description: Helpers for forcing async Tasks to run synchronously for pythonnet.
 */
using System.Threading.Tasks;

namespace Spotify_Playlist_Manager;

public static class AsyncHelper
{
    public static T Get<T>(Task<T> task)
    {
        return task.GetAwaiter().GetResult();
    }

    public static void Run(Task task)
    {
        task.GetAwaiter().GetResult();
    }
}
