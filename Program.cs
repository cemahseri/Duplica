using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Standart.Hash.xxHash;

namespace FastestDuplicateFileFinder
{
    internal static class Program
    {
        // First process is checking the size of files. Then, if there are files with the same size, then we will be calculating file's hash.
        // Simply, reading the whole file will be enough, but it will be so expensive. Instead of that, we will read the first 4 KBs of the file.
        private const int BeginningReadingBuffer = 4 * 1024;

        // Streams' default buffer size is 4096 bytes. That is not really useful for reading big files.
        // So, instead of 4 KBs, using 4 MBs as the buffer size will be better.
        private const int RepeatedReadingBuffer = 4 * 1024 * 1024;

        private static readonly Dictionary<ulong, List<FileInfo>> UniqueHashes = new Dictionary<ulong, List<FileInfo>>();

        private static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                throw new ArgumentException("No path specified.");
            }

            var directory = new DirectoryInfo(args[0]);

            // "*.*" will only get files with extensions. But "*" will get every file.
            // And if SearchOption.AllDirectories wasn't used, it wouldn't be recursive scan and it'd only get the files in the target path, with not including files in the child directories.
            Console.WriteLine("Getting files...");
            var files = directory.GetFiles("*", SearchOption.AllDirectories);

            if (files.Length == 0)
            {
                Console.WriteLine("Uhhh... Dude, this directory is empty.");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Files to check: " + files.Length + Environment.NewLine);

            // If there is more than one file with the same size, there is a possibility that they are identical.
            // So, we are only interested in same sized files. Because of that, we are ignoring different sized files.
            Console.WriteLine("Checking possible duplicate files...");
            var possibleDuplicateFiles = files.GroupBy(f => f.Length).Where(s => s.Count() > 1).SelectMany(f => f).ToList();

            Console.WriteLine("Possible duplicate files: " + possibleDuplicateFiles.Count + Environment.NewLine);

            // Now we are checking if possible duplicate files are really identical.
            // And again, we ignoring the unique hashes. We do not need them, since they mean that they are not duplicate file.
            Console.WriteLine("Checking duplicate files...");
            foreach (var (file, _) in files.Select(f => Tuple.Create(f, CalculateHash(f.FullName))).GroupBy(t => t.Item2).Where(h => h.Count() > 1).SelectMany(t => t))
            {
                var hash = CalculateHash(file.FullName, RepeatedReadingBuffer);

                if (!UniqueHashes.ContainsKey(hash))
                {
                    UniqueHashes.Add(hash, new List<FileInfo> { file });
                }
                else
                {
                    UniqueHashes[hash].Add(file);
                }
            }

            if (UniqueHashes.Any())
            {
                // Let's sort files based on their creation time. Because, oldest file will be our original file and others will be marked as duplicate.
                foreach (var duplicateFiles in UniqueHashes.Values.Select(d => d.OrderBy(f => f.CreationTime).ToList()))
                {
                    Console.WriteLine(Environment.NewLine + "Duplicate files for: " + duplicateFiles.First());

                    foreach (var duplicateFile in duplicateFiles.Skip(1))
                    {
                        Console.WriteLine("  " + duplicateFile.FullName);
                    }
                }
            }
            else
            {
                Console.WriteLine("No duplicate files found.");
            }

            Console.ReadKey();
        }

        // Using BeginningReadingBuffer won't change anything, since they are both 4096 bytes.
        // But if I want to change buffer size in the future, it will be easier to do so.
        private static ulong CalculateHash(string path, int bufferSize = BeginningReadingBuffer, int startingIndex = 0)
        {
            var buffer = new byte[bufferSize];

            // The main reason I'm wrapping this code with try-catch block is reading the file might throw an exception.
            // For example, there might be a problem with the HDD - which I have faced recently while developing this program.
            // Instead of aborting the whole process, skipping only a file would be better.
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize);
                stream.Read(buffer, startingIndex, bufferSize);
            }
            catch
            {
                Console.WriteLine("An exception has been thrown. Possibly corrupted file: " + path);

                // LU (or UL, Ul, uL, ul, Lu, lU, lu) postfix tells the compiler that we are using unsigned long (ulong) type instead of the default integer type, int. Nothing that much happens if you don't use it.
                return 0LU;
            }

            return xxHash64.ComputeHash(buffer);
        }
    }
}