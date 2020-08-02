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
        // First process is checking size of files. Then, if there is files with the same size, then we will be calculating file's hash.
        // Simply, reading the whole file will be enough, but it will be so expensive. Instead of that, we will read the first 4 KBs of the file.
        private const int BeginningReadingBuffer = 4 * 1024;

        // Streams' default buffer size is 4096 bytes. That is not really useful for reading big files.
        // So, instead of 4 KBs, using 4 MBs as the buffer size will be better.
        private const int RepeatedReadingBuffer = 4 * 1024 * 1024;

        private static readonly Dictionary<ulong, FileInfo> UniqueHashes = new Dictionary<ulong, FileInfo>();
        private static readonly Dictionary<FileInfo, List<FileInfo>> DuplicateFiles = new Dictionary<FileInfo, List<FileInfo>>();

        private static async Task Main(string[] args)
        {
            if (args.Length < 1)
            {
                throw new ArgumentException("No path specified.");
            }

            var directory = new DirectoryInfo(args[0]);
            // "*.*" will only get files with extensions. But "*" will get every file.
            // And if SearchOption.AllDirectories wasn't used, it wouldn't be recursive scan and it'd only get the files in the target path, with not including files in the child directories.
            var files = directory.GetFiles("*", SearchOption.AllDirectories);

            // If there is more than one file with the same size, there is a possibility that they are identical.
            // So, we are only interested in same sized files. Because of that, we are ignoring different sized files.
            var possibleDuplicateFiles = files.GroupBy(f => f.Length).Where(s => s.Count() > 1).SelectMany(f => f.ToList());

            var hashes = await GetHashes(possibleDuplicateFiles).ConfigureAwait(false);

            // Now we are checking if possible duplicate files are really identical.
            // And again, we ignoring the unique hashes. We do not need them, since they mean that they are not duplicate file.
            foreach (var (originalFile, _) in hashes.GroupBy(t => t.Item2).Where(h => h.Count() > 1).SelectMany(t => t.ToList()))
            {
                DuplicateFiles.Add(originalFile, new List<FileInfo>());

                // Finally, checking the whole file.
                await using var stream = new FileStream(originalFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, RepeatedReadingBuffer);

                try
                {
                    var hash = await xxHash64.ComputeHashAsync(stream, RepeatedReadingBuffer).ConfigureAwait(false);

                    // If the hash is already on the dictionary, it means that it's a duplicate file. Send it to GULAG.
                    if (UniqueHashes.TryGetValue(hash, out var duplicateFile))
                    {
                        DuplicateFiles[originalFile].Add(duplicateFile);
                    }
                    else
                    {
                        UniqueHashes.Add(hash, originalFile);
                    }
                }
                catch
                {
                    Console.WriteLine("An exception has been thrown. Possibly corrupted file: " + originalFile.FullName);
                }
            }

            if (DuplicateFiles.Values.Count == 0)
            {
                Console.WriteLine("No duplicate files found.");
            }

            Console.ReadKey();
        }

        // I'd like to use IAsyncEnumerable<FileInfo> instead of IEnumerable<FileInfo> in the parameter's type, but arrays doesn't implement IAsyncEnumerable interface.
        // I could have been used ToList() LINQ method while calling the method, but it'll not worth it, I guess.
        private static async Task<IList<Tuple<FileInfo, ulong>>> GetHashes(IEnumerable<FileInfo> files) // Lmao, what a mess... Task<IList<Tuple<FileInfo, ulong>>> This is reaching out to the infinity xD
        {
            IList<Tuple<FileInfo, ulong>> hashes = new List<Tuple<FileInfo, ulong>>();

            foreach (var file in files)
            {
                var buffer = new byte[BeginningReadingBuffer];

                await using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // The main reason I'm wrapping this code with try-catch block is reading the file might throw an exception.
                    // For example, there might be a problem with the HDD - which I faced recently while developing this program.
                    // Instead of aborting the whole process, skipping only a file would be better.
                    try
                    {
                        // Just a small note. Using buffer.Length is also okay, but since BeginningReadingBuffer is a constant, it'll be literally baked into here.
                        // So, it'll kinda help the performance but you'll not feel it. Yeah baby, micro-optimisation!
                        stream.Read(buffer, 0, BeginningReadingBuffer);
                    }
                    catch
                    {
                        Console.WriteLine("An exception has been thrown. Possibly corrupted file: " + file.FullName);
                        continue;
                    }
                }

                hashes.Add(Tuple.Create(file, xxHash64.ComputeHash(buffer)));
            }

            return hashes;
        }
    }
}