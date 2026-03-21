using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SaveSync.Views;

public partial class ConfirmationDialog : Window
{
    public ConfirmationDialog(string title, string message)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
    }

    private void Proceed_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
