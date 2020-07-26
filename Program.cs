using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Standart.Hash.xxHash;

namespace FastestDuplicateFileFinder
{
    internal static class Program
    {
        // Streams' default buffer size is 4 kilobyte, which is 4096 byte. That is not really useful for reading big files.
        // So, instead of 4 KB, using 4 MB as the buffer size will be better.
        private const int Buffer = 4 * 1024 * 1024;

        private static Dictionary<string, ulong> _uniqueHashesBeginning = new Dictionary<string, ulong>();
        private static readonly Dictionary<ulong, string> UniqueHashes = new Dictionary<ulong, string>();
        private static readonly Dictionary<string, string> DuplicateFiles = new Dictionary<string, string>();

        private static async Task Main(string[] args)
        {
            if (args.Length < 1)
            {
                throw new ArgumentException("No path specified.");
            }

            var directory = new DirectoryInfo(args[0]);
            var files = directory.GetFiles("*", SearchOption.AllDirectories);

            // If there is more than one file with the same size, there is a possibility that they are identical.
            // So, if there is any potential duplicate files, check further.
            var possibleDuplicateFiles = files.GroupBy(f => f.Length).Where(s => s.Count() > 1).SelectMany(f => f.ToList()).ToList();
            if (possibleDuplicateFiles.Any())
            {
                foreach (var file in possibleDuplicateFiles)
                {
                    var buffer = new byte[4096];

                    try
                    {
                        await using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            stream.Read(buffer, 0, buffer.Length);
                        }

                        _uniqueHashesBeginning.Add(file.FullName, xxHash64.ComputeHash(buffer));
                    }
                    catch
                    {
                        Console.WriteLine("An exception has been thrown. File: " + file.FullName);
                    }
                }

                _uniqueHashesBeginning = _uniqueHashesBeginning.GroupBy(f => f.Value).Where(s => s.Count() > 1).SelectMany(f => f).ToDictionary(p => p.Key, p => p.Value);
                if (_uniqueHashesBeginning.Any())
                {
                    foreach (var filePath in _uniqueHashesBeginning.Keys)
                    {
                        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, Buffer);

                        try
                        {
                            var hash = await xxHash64.ComputeHashAsync(stream, Buffer).ConfigureAwait(false);

                            if (UniqueHashes.TryGetValue(hash, out var duplicateFilePath))
                            {
                                DuplicateFiles.Add(duplicateFilePath, filePath);
                            }
                            else
                            {
                                UniqueHashes.Add(hash, filePath);
                            }
                        }
                        catch
                        {
                            Console.WriteLine("An exception has been thrown. File: " + filePath);
                        }
                    }
                }
            }

            if (DuplicateFiles.Values.Count == 0)
            {
                Console.WriteLine("No duplicate files found.");
            }
            else
            {
                foreach (var originalFile in DuplicateFiles.Values)
                {
                    Console.WriteLine("Duplicate for file: " + originalFile);

                    foreach (var duplicateFile in DuplicateFiles.Where(p => p.Value == originalFile).GroupBy(p => p.Key).Select(p => p.Key))
                    {
                        Console.WriteLine(duplicateFile);
                    }
                }
            }

            Console.ReadKey();
        }
    }
}