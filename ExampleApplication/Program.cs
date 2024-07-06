using Duplica;

long totalDuplicateSize = 0;

await foreach (var duplicateFileGroup in DuplicaAnalyzer.GetDuplicateFileGroupsAsync(@"E:\Uzantılar\!"))
{
    Console.WriteLine($"{duplicateFileGroup.OriginalFile.FullName}");

    var duplicateSize = duplicateFileGroup.Size * duplicateFileGroup.DuplicateFiles.Count;
    totalDuplicateSize += duplicateSize;

    Console.WriteLine($"{duplicateFileGroup.DuplicateFiles.Count} duplicate files found. Duplicate size: {HumanFriendlyFileSizeString(duplicateSize)}");
    foreach (var duplicateFile in duplicateFileGroup.DuplicateFiles)
    {
        Console.WriteLine($"  -{duplicateFile.FullName}");
    }

    Console.WriteLine();
}

Console.WriteLine($"Total Duplicate Size: {HumanFriendlyFileSizeString(totalDuplicateSize)}");

Console.ReadKey(true);
return;

// https://stackoverflow.com/a/281679
static string HumanFriendlyFileSizeString(long length)
{
    string[] sizes = [ "B", "KB", "MB", "GB", "TB" ];
    
    var order = 0;
    while (length >= 1024 && order < sizes.Length - 1)
    {
        order++;

        length /= 1024;
    }

    return $"{length} {sizes[order]}";
}