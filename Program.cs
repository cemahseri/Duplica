using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Standart.Hash.xxHash;
using FastestDuplicateFileFinder.Models;

namespace FastestDuplicateFileFinder;

internal static class Program
{
    // Streams' default buffer size is 4096 bytes. That is not really useful for reading big files.
    // So, instead of 4 KBs, using 1 MB as the buffer size will be better.
    // I was using 4 MBs before but thanks to Dai, I've changed it to 1 MB: https://stackoverflow.com/questions/1862982/c-sharp-filestream-optimal-buffer-size-for-writing-large-files#comment123344066_1863003
    private const int BufferSize = 1 * 1024 * 1024;

    private static async Task Main(string[] paths)
    {
        if (paths.Length < 1)
        {
            Console.WriteLine("No path(s) specified. Press any key to exit...");

            Console.ReadKey(true);
            return;
        }

        var files = new List<FileInfo>();

        foreach (var path in paths)
        {
            if (!Directory.Exists(path))
            {
                Console.Write("Path does not exist. Skipping: " + path);
                continue;
            }

            var directoryInfo = new DirectoryInfo(path);

            // "*.*" will only get files with extensions. But "*" will get every file.
            // And if SearchOption.AllDirectories wasn't used, it wouldn't be recursive scan and it'd only get the files in the target path, with not including files in the child directories.
            files.AddRange(directoryInfo.GetFiles("*", SearchOption.AllDirectories));
        }

        // If there are no files to compare, abort.
        if (!files.Any())
        {
            Console.WriteLine("There are no files. All of those paths you've provided are empty. Press any key to exit...");

            Console.ReadKey(true);
            return;
        }

        Console.WriteLine("Files to check: " + files.Count);
        Console.WriteLine();

        IReadOnlyCollection<DuplicateFileGroup> possibleDuplicateFileGroups = files.GroupBy(f => f.Length)
            .Where(s => s.Count() > 1)
            .Select(g => new DuplicateFileGroup(g.Key, g.Select(f => new DuplicateFileInfo(f))))
            .ToList()
            .AsReadOnly();

        // If there are no possible duplicate file groups, abort. It means there are no same sized files, which means every file is unique.
        if (!possibleDuplicateFileGroups.Any())
        {
            Console.WriteLine("There are no duplicate files. Press any key to exit...");

            Console.ReadKey(true);
            return;
        }

        Console.WriteLine("Possible duplicate files: " + (possibleDuplicateFileGroups.SelectMany(g => g.Files).Count() - possibleDuplicateFileGroups.Count));
        Console.WriteLine();

        foreach (var duplicateFileGroup in possibleDuplicateFileGroups.OrderBy(g => g.Size)) // We are sorting duplicate file groups from low size to high size.
        {
            // If there are no possible duplicate files in the current group, skip.
            if (!duplicateFileGroup.Files.Any())
            {
                continue;
            }

            var numberOfChunks = duplicateFileGroup.Size / BufferSize;

            for (var chunk = 0; chunk <= numberOfChunks; chunk++)
            {
                var hashes = new Dictionary<ulong, List<DuplicateFileInfo>>();

                foreach (var file in duplicateFileGroup.Files)
                {
                    // If the file is unique, skip. We already figured out that it's not same with the other files in the group.
                    // So continuing to check it will be waste of resources.
                    if (file.IsUnique)
                    {
                        continue;
                    }
                    
                    await using var stream = new FileStream(file.FileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan);

                    // If we can't read the stream, skip the file.
                    if (!stream.CanRead)
                    {
                        continue;
                    }

                    // We need a stream that supports seeking. If we can't seek, skip the file.
                    if (!stream.CanSeek)
                    {
                        continue;
                    }

                    stream.Seek(chunk * BufferSize, SeekOrigin.Begin);

                    var buffer = new byte[BufferSize];

                    await stream.ReadAsync(buffer, 0, BufferSize);

                    var hash = xxHash64.ComputeHash(buffer, buffer.Length);

                    if (hashes.TryGetValue(hash, out var possibleDuplicateFiles))
                    {
                        possibleDuplicateFiles.Add(file);
                    }
                    else
                    {
                        hashes.Add(hash, new List<DuplicateFileInfo> { file });
                    }
                }

                if (hashes.Count > 1)
                {
                    foreach (var info in hashes.Values.Where(f => f.Count == 1))
                    {
                        info.First().IsUnique = true;
                    }
                }
            }

            var duplicateFiles = duplicateFileGroup.Files.Where(f => !f.IsUnique)
                .OrderBy(f => f.FileInfo.CreationTime)
                .ToList();

            if (duplicateFiles.Any())
            {
                var originalFile = duplicateFiles.First();

                Console.WriteLine("Duplicate files for: " + originalFile.FileInfo.FullName);

                foreach (var file in duplicateFiles.Skip(1))
                {
                    Console.WriteLine("  " + file.FileInfo.FullName);
                }

                Console.WriteLine();
            }
        }

        Console.WriteLine("Checking duplicate files has been finished. Press any key to exit...");
        Console.ReadKey(true);
    }
}