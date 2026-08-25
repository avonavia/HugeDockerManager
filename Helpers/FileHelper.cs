using System.Text;
using Entities;

namespace Helpers;

public static class FileHelper
{
    public static Task<string> GetContentType(string extension)
    {
        return Task.FromResult(extension.ToLowerInvariant() switch
        {
            ".txt" => "text/plain",
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".zip" => "application/zip",
            ".exe" => "application/octet-stream",
            ".doc" or ".docx" => "application/msword",
            ".xls" or ".xlsx" => "application/vnd.ms-excel",
            _ => "application/octet-stream"
        });
    }

    public static async Task<List<FileItem>> GetDirectoryContents(string path)
    {
        var items = new List<FileItem>();

        try
        {
            var dirInfo = new DirectoryInfo(path);

            if (!await IsRootOrDrive(path))
            {
                var parent = dirInfo.Parent?.FullName;
                if (parent != null)
                {
                    items.Add(new FileItem
                    {
                        Name = "..",
                        FullPath = parent,
                        IsDirectory = true,
                        Size = 0,
                        LastModified = dirInfo.LastWriteTime
                    });
                }
            }

            var directories = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly)
                .OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
                .Where(IsAccessible);

            foreach (var dir in directories)
            {
                try
                {
                    items.Add(new FileItem
                    {
                        Name = dir.Name,
                        FullPath = dir.FullName,
                        IsDirectory = true,
                        Size = 0,
                        LastModified = dir.LastWriteTime
                    });
                }
                catch
                {
                    //Skip inaccessible directories
                }
            }

            var files = dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
                .Where(IsAccessible);

            foreach (var file in files)
            {
                try
                {
                    items.Add(new FileItem
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        IsDirectory = false,
                        Size = file.Length,
                        LastModified = file.LastWriteTime
                    });
                }
                catch
                {
                    //Skip inaccessible files
                }
            }
        }
        catch (Exception)
        {
            if (await IsRootOrDrive(path))
            {
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                {
                    items.Add(new FileItem
                    {
                        Name = drive.Name.Replace("\\", ""),
                        FullPath = drive.Name,
                        IsDirectory = true,
                        Size = drive.TotalSize,
                        LastModified = drive.RootDirectory.LastAccessTime
                    });
                }
            }
        }

        return items;
    }

    private static Task<bool> IsRootOrDrive(string path)
    {
        return Task.FromResult(path.Length <= 3 || (OperatingSystem.IsWindows() && path.EndsWith(":\\")));
    }

    private static bool IsAccessible(FileSystemInfo item)
    {
        try
        {
            if ((item.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                return false;

            var dangerousPaths = new[] { "windows", "system32", "syswow64", "winnt" };
            var lowerName = item.Name.ToLowerInvariant();
            return !dangerousPaths.Any(d => lowerName.Contains(d));
        }
        catch
        {
            return false;
        }
    }

    public static string SanitizePath(string path, bool isFilePath = false)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }

        try
        {
            if (path.StartsWith("/") && OperatingSystem.IsWindows())
            {
                if (path.Length > 1)
                {
                    var drive = path.Substring(1, 1).ToUpper();
                    if (drive.Length == 1 && char.IsLetter(drive[0]))
                    {
                        path = $"{drive}:\\{path.Substring(3).Replace("/", "\\")}";
                    }
                }
            }

            if (isFilePath)
            {
                var fileInfo = new FileInfo(path);
                if (fileInfo.Directory is { Exists: true })
                {
                    return Path.Combine(fileInfo.Directory.FullName, fileInfo.Name);
                }
            }
            else
            {
                path = Path.GetFullPath(path.TrimEnd('/', '\\'));
            }

            if (!isFilePath && !Directory.Exists(path))
            {
                return Path.GetDirectoryName(path) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            if (isFilePath && !File.Exists(path))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }

            var lowerPath = path.ToLowerInvariant();
            var dangerousPaths = new[] { "$recycle.bin", "system volume information" };
            if (dangerousPaths.Any(d => lowerPath.Contains(d)))
            {
                throw new UnauthorizedAccessException("Access denied to system path");
            }

            return path;
        }
        catch (Exception)
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
    }

    public static async Task<bool> IsTextFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var textExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "", ".txt", ".env", ".ini", ".cfg", ".conf", ".config",
            ".json", ".xml", ".html", ".htm", ".css", ".js", ".ts",
            ".md", ".log", ".csv", ".tsv", ".sh", ".bash", ".py", ".pl"
        };

        if (textExtensions.Contains(extension))
            return true;

        try
        {
            var content = await File.ReadAllTextAsync(path);
            return await IsTextContent(content);
        }
        catch
        {
            return false;
        }
    }

    private static Task<bool> IsTextContent(string content)
    {
        var nullBytes = 0;
        var controlChars = 0;
        var bytes = Encoding.UTF8.GetBytes(content);

        foreach (var b in bytes)
        {
            if (b == 0) nullBytes++;
            else if (b < 32 && b != 9 && b != 10 && b != 13) controlChars++;
        }

        var total = bytes.Length;
        return Task.FromResult(total > 0 &&
                               nullBytes == 0 &&
                               (double)controlChars / total < 0.05);
    }

    public static Task<string> GetUniqueFileName(string filePath)
    {
        if (!File.Exists(filePath)) return Task.FromResult(filePath);

        var dir = Path.GetDirectoryName(filePath);
        var name = Path.GetFileNameWithoutExtension(filePath);
        var ext = Path.GetExtension(filePath);

        int counter = 1;
        var newPath = filePath;

        while (File.Exists(newPath))
        {
            if (dir != null) newPath = Path.Combine(dir, $"{name}_{counter++}{ext}");
        }

        return Task.FromResult(newPath);
    }

    public static void CleanScript(string path)
    {
        var content = File.ReadAllText(path, new UTF8Encoding(false));

        if (HasBadLineEndings(content))
        {
            var cleanContent = content
                .Replace("\r\n", "\n") // Windows CRLF
                .Replace("\r", "\n"); // Mac CR

            File.WriteAllText(path, cleanContent, new UTF8Encoding(false));
        }
    }
    
    private static bool HasBadLineEndings(string content)
    {
        return content.Contains("\r\n") ||  // Windows CRLF
               content.Contains("\r");     // Mac CR
    }
}