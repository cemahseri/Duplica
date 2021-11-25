using System.Collections.Generic;
using System.Linq;

namespace FastestDuplicateFileFinder.Models;

internal sealed class DuplicateFileGroup
{
    internal long Size { get; set; }

    internal IReadOnlyCollection<DuplicateFileInfo> Files { get; set; }

    internal DuplicateFileGroup(long size, IEnumerable<DuplicateFileInfo> files)
    {
        Size = size;

        Files = files.ToList().AsReadOnly();
    }
}