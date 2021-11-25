using System.IO;

namespace FastestDuplicateFileFinder.Models;

internal sealed class DuplicateFileInfo
{
    internal bool IsUnique { get; set; }

    internal FileInfo FileInfo { get; set; }

    internal DuplicateFileInfo(FileInfo fileInfo)
    {
        FileInfo = fileInfo;
    }
}