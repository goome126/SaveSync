using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SaveSync.Services;

namespace SaveSync.Views;

public partial class AddGameDialog : Window
{
    private readonly IgdbService? _igdb;

    // Debounce: cancel the previous search task when the user keeps typing.
    private CancellationTokenSource? _searchCts;

    // Holds the IGDB id of whichever suggestion was last selected (null if none).
    private int? _selectedIgdbId;
    private string? _selectedCoverImageId;

    public AddGameDialog()
    {
        InitializeComponent();

        // Build IgdbService from saved config, falling back to env vars inside the service.
        var configService = new ConfigurationService();
        var config = configService.LoadConfig();
        var igdbSettings = config.IgdbSettings;

        _igdb = new IgdbService(igdbSettings.ClientId, igdbSettings.ClientSecret, igdbSettings.BearerToken);

        if (!_igdb.IsConfigured)
        {
            _igdb = null;
            IgdbNotConfiguredText.IsVisible = true;
        }

        GameNameBox.TextChanged += GameNameBox_TextChanged;
    }

    // -------------------------------------------------------------------------
    // IGDB search & suggestion handling
    // -------------------------------------------------------------------------

    private async void GameNameBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        // Reset selection when the user edits the text manually.
        _selectedIgdbId = null;

        if (_igdb == null) return;

        var query = GameNameBox.Text ?? string.Empty;

        // Hide suggestions and cancel any in-flight search.
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();

        if (query.Length < 2)
        {
            HideSuggestions();
            return;
        }

        var cts = _searchCts;

        // Debounce: wait 400 ms before firing the request.
        try
        {
            await Task.Delay(400, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        SearchingText.IsVisible = true;

        List<IgdbSearchResult> results;
        try
        {
            results = await _igdb.SearchGamesAsync(query, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            SearchingText.IsVisible = false;
            return;
        }

        if (cts.IsCancellationRequested) return;

        SearchingText.IsVisible = false;

        if (results.Count == 0)
        {
            HideSuggestions();
            return;
        }

        SuggestionsList.ItemsSource = results;
        SuggestionsPanel.IsVisible = true;

        // Load cover images in the background without blocking the UI.
        _ = LoadCoversAsync(results, cts.Token);
    }

    private async Task LoadCoversAsync(List<IgdbSearchResult> results, CancellationToken ct)
    {
        foreach (var result in results)
        {
            if (ct.IsCancellationRequested) return;
            // IgdbSearchResult.Cover is an [ObservableProperty], so setting it
            // automatically notifies the binding — no ItemsSource refresh needed.
            await _igdb!.LoadCoverAsync(result, ct);
        }
    }

    private void SuggestionsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SuggestionsList.SelectedItem is not IgdbSearchResult selected) return;

        _selectedIgdbId = selected.IgdbId;
        _selectedCoverImageId = selected.CoverImageId;

        // Fill the textbox and dismiss suggestions without re-triggering search.
        GameNameBox.TextChanged -= GameNameBox_TextChanged;
        GameNameBox.Text = selected.Name;
        GameNameBox.TextChanged += GameNameBox_TextChanged;

        HideSuggestions();
    }

    private void HideSuggestions()
    {
        SuggestionsPanel.IsVisible = false;
        SuggestionsList.ItemsSource = null;
        SuggestionsList.SelectedItem = null;
    }

    // -------------------------------------------------------------------------
    // Browse / confirm / cancel
    // -------------------------------------------------------------------------

    private async void BrowseSavePathButton_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Game Save Directory",
            AllowMultiple = false
        });

        if (folders.Count > 0)
            SavePathBox.Text = folders[0].Path.LocalPath;
    }

    private void AddGame_Click(object? sender, RoutedEventArgs e)
    {
        var name = GameNameBox.Text?.Trim();
        var path = SavePathBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
        {
            ErrorText.Text = "Please enter both a game name and a save directory path.";
            ErrorText.IsVisible = true;
            return;
        }

        Close((name, path, _selectedIgdbId, _selectedCoverImageId));
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}

