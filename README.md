[![NuGet Version (Duplica)](https://img.shields.io/nuget/v/Duplica?style=for-the-badge&color=D800FF)](https://www.nuget.org/packages/Duplica)
[![NuGet Downloads (Duplica)](https://img.shields.io/nuget/dt/Duplica?style=for-the-badge&color=D800FF)](https://www.nuget.org/packages/Duplica)

# Duplica
A very fast duplicate file finder.

# Usage Example
You can check the ExampleApplication project!
```csharp
await foreach (var duplicateFileGroup in DuplicaAnalyzer.GetDuplicateFileGroupsAsync(@"E:\path\to\your\mom"))
{
    Console.WriteLine($"{duplicateFileGroup.OriginalFile.FullName}");

    Console.WriteLine($"{duplicateFileGroup.DuplicateFiles.Count} duplicate files found.");
    foreach (var duplicateFile in duplicateFileGroup.DuplicateFiles)
    {
        Console.WriteLine($"  -{duplicateFile.FullName}");
    }

    Console.WriteLine();
}
```

# To-Do
- Use different buffer sizes based on file size. *(1 MB buffer seems to be optimal but 512 KB buffer seems to be working better with smaller files.)*