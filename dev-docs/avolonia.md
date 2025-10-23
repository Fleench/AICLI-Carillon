# Avalonia Project Structure

This document explains the purpose of each `.cs` file in the project.

## Root Directory

### `Program.cs`

This is the main entry point for the application. It initializes the Avalonia framework and starts the application.

### `App.axaml.cs`

This file is the code-behind for `App.axaml`. It handles application-level events and sets up the main window.

### `ViewLocator.cs`

This class is responsible for locating and creating views for a given view model. It allows for a convention-based approach to connecting views and view models.

## ViewModels Directory

### `ViewModelBase.cs`

This is a base class for all view models in the application. It typically implements `INotifyPropertyChanged` to support data binding.

### `MainWindowViewModel.cs`

This is the view model for the main window. It contains the logic and data that the main window will display.

## Views Directory

### `MainWindow.axaml.cs`

This is the code-behind for the main window (`MainWindow.axaml`). It's responsible for handling UI events and interacting with the view model.
