/* File: Program.cs
 * Author: ChatGPT Codex
 * Description: Console entry point for the headless Spotify sorter.
 */
using System;

namespace Spotify_Playlist_Manager;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        CliRunner.Run().GetAwaiter().GetResult();
    }

    /*
    // Original Avalonia UI startup code (disabled for headless CLI runs).
    public static void nMain(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    */
}
