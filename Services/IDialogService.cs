namespace SaveSync.Services;

public interface IDialogService
{
    /// <summary>
    /// Shows a confirmation dialog and returns true if the user chose to proceed.
    /// </summary>
    Task<bool> ConfirmAsync(string title, string message);
}
