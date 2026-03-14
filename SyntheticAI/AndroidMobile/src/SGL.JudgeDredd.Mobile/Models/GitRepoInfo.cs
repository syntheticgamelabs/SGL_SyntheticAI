namespace SGL.JudgeDredd.Mobile.Models;

public class GitRepoInfo
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string CloneUrl { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public int Stars { get; set; }
    public int Forks { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string OwnerAvatarUrl { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public bool IsPrivate { get; set; }
    public long SizeKb { get; set; }
    public string DefaultBranch { get; set; } = "main";
    public bool IsLocal { get; set; }
    public string LocalPath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class GitFileItem
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = "file";
    public long Size { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public bool IsDirectory => Type == "dir";
}
