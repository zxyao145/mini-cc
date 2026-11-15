namespace MiniCc.Api.Shared.Config;

public class LibraryOptions
{
    public int TrashRetentionDays { get; set; } = 30;
    public int UndoWindowSeconds { get; set; } = 10;
}
