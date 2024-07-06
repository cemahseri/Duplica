namespace Duplica.Models;

public sealed class DuplicateFileGroup
{
    public long Size { get; }

    public FileInfo OriginalFile { get; internal set; }

    public IList<FileInfo> DuplicateFiles { get; }

    internal DuplicateFileGroup(long size, IList<FileInfo> files)
    {
        Size = size;

        DuplicateFiles = files;
    }
}