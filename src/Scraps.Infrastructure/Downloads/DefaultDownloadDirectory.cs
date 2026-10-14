namespace Scraps.Infrastructure.Downloads;

public static class DefaultDownloadDirectory
{
    public static string GetPath(string? userProfilePath = null)
    {
        if (userProfilePath is not null) ArgumentException.ThrowIfNullOrWhiteSpace(userProfilePath);
        var profile = userProfilePath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(profile))
            throw new InvalidOperationException("The current user's profile directory could not be determined.");
        return Path.Combine(profile, "Downloads", "Scraps");
    }
}
