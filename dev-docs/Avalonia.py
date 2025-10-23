# What is Avalonia?

Avalonia is a free and open-source .NET cross-platform UI framework. It is inspired by Windows Presentation Foundation (WPF) and allows developers to create user interfaces for desktop, mobile, and web applications from a single codebase.

## Key Features

*   **Cross-Platform:** Avalonia supports a wide range of platforms, including:
    *   Windows
    *   macOS
    *   Linux
    *   iOS
    *   Android
    *   WebAssembly
*   **XAML-Based:** Avalonia uses the Extensible Application Markup Language (XAML) for defining user interfaces, which is familiar to developers with experience in WPF, UWP, or Xamarin.Forms.
*   **Independent Rendering Engine:** Unlike many other UI frameworks that rely on native platform controls, Avalonia uses its own rendering engine (powered by Skia or Direct2D). This ensures that the application's UI looks and behaves identically across all supported platforms, providing a consistent user experience.
*   **Flexible Styling System:** Avalonia has a powerful and flexible styling system, similar to CSS, that allows for extensive customization of the application's appearance.
*   **.NET Integration:** As a .NET framework, Avalonia allows developers to write application logic in C# or any other .NET language, leveraging the full power and ecosystem of the .NET platform.

## How it Works

Avalonia's architecture consists of a platform-agnostic core layer and a platform-specific integration layer. The core layer handles UI controls, layout, styling, data binding, and input. The rendering engine is part of this core, drawing the UI pixel by pixel, which is how it achieves its cross-platform consistency. The platform integration layer handles the interaction with the underlying operating system for things like windowing, input, and other platform-specific services.
