using Microsoft.AspNetCore.Http;

namespace ArtisanApi.Services;

public static class ChatAttachmentRules
{
    public const long MaxBytes = 15 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".gif",
            ".webp",
            ".bmp",
            ".heic",
            ".pdf",
            ".txt",
            ".csv",
            ".json",
            ".xml",
            ".zip",
            ".doc",
            ".docx",
            ".xls",
            ".xlsx",
            ".ppt",
            ".pptx",
            ".svg",
        };

    public static bool IsAllowedExtension(string ext) => AllowedExtensions.Contains(ext);

    public static string SanitizeOriginalName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "file";
        var n = Path.GetFileName(name.Trim());
        if (n.Length > 200)
            n = n[..200];
        return string.IsNullOrEmpty(n) ? "file" : n;
    }

    public static string GuessContentType(string ext, string? fromClient)
    {
        if (!string.IsNullOrWhiteSpace(fromClient) && fromClient != "application/octet-stream")
            return fromClient.Trim();
        return ext.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".txt" or ".csv" => "text/plain",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".zip" => "application/zip",
            _ => "application/octet-stream",
        };
    }

    public static string? ValidateFile(IFormFile file, out string ext)
    {
        ext = Path.GetExtension(file.FileName ?? "");
        if (string.IsNullOrEmpty(ext))
            ext = ".bin";
        if (!IsAllowedExtension(ext))
            return "File type not allowed. Use images, PDF, Office documents, zip, or text files.";
        if (file.Length <= 0)
            return "Empty file.";
        if (file.Length > MaxBytes)
            return $"File too large (max {MaxBytes / (1024 * 1024)} MB).";
        return null;
    }
}
