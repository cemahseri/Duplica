using System.IO.Hashing;
using System.Threading.Tasks.Dataflow;
using Duplica.Models;

namespace Duplica;

public static class DuplicaAnalyzer
{
    // Streams' default buffer size is 4096 bytes. That is not really useful for reading big files.
    // So, instead of 4 KBs, using 512 KB-1 MB as the buffer size will be better.
    private const int BufferSize = 1 * 1024 * 1024;

    public static IEnumerable<DuplicateFileGroup> GetDuplicateFileGroups(string path)
        => GetDuplicateFileGroupsAsync(path).ToBlockingEnumerable();

    public static IEnumerable<DuplicateFileGroup> GetDuplicateFileGroups(IEnumerable<FileInfo> files)
        => GetDuplicateFileGroupsAsync(files).ToBlockingEnumerable();

    public static async IAsyncEnumerable<DuplicateFileGroup> GetDuplicateFileGroupsAsync(string path)
    {
        var directoryInfo = new DirectoryInfo(path);
        if (!directoryInfo.Exists)
        {
            throw new DirectoryNotFoundException(path);
        }

        var files = directoryInfo.GetFiles("*", new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true
        });

        if (!files.Any())
        {
            yield break;
        }

        await foreach (var duplicateFileGroup in GetDuplicateFileGroupsAsync(files))
        {
            yield return duplicateFileGroup;
        }
    }

    public static async IAsyncEnumerable<DuplicateFileGroup> GetDuplicateFileGroupsAsync(IEnumerable<FileInfo> files)
    {
        var transformBlock = new TransformBlock<DuplicateFileGroup, DuplicateFileGroup>(ProcessDuplicateFileGroupAsync, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 10,
            EnsureOrdered = false
        });

        var duplicateFileGroups = files.GroupBy(f => f.Length)
            .Where(g => g.Key > 0 && g.Count() > 1)
            .Select(g => new DuplicateFileGroup(g.Key, [ ..g ]));

        foreach (var duplicateFileGroup in duplicateFileGroups)
        {
            transformBlock.Post(duplicateFileGroup);
        }

        transformBlock.Complete();

        await foreach (var duplicateFileGroup in transformBlock.ReceiveAllAsync())
        {
            if (duplicateFileGroup != null)
            {
                yield return duplicateFileGroup;
            }
        }

        await transformBlock.Completion;
    }

    private static async Task<DuplicateFileGroup> ProcessDuplicateFileGroupAsync(DuplicateFileGroup duplicateFileGroup)
    {
        var numberOfChunks = duplicateFileGroup.Size / BufferSize;

        var buffer = new byte[BufferSize];

        for (long chunk = 0; chunk <= numberOfChunks; chunk++)
        {
            var hashes = new Dictionary<UInt128, IList<FileInfo>>();

            foreach (var fileInfo in duplicateFileGroup.DuplicateFiles)
            {
                try
                {
                    await using var stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan);

                    if (!stream.CanRead)
                    {
                        continue;
                    }

                    if (!stream.CanSeek)
                    {
                        continue;
                    }

                    stream.Seek(chunk * BufferSize, SeekOrigin.Begin);

                    Array.Clear(buffer);

                    await stream.ReadAsync(buffer.AsMemory());

                    var hash = XxHash128.HashToUInt128(buffer);

                    if (!hashes.ContainsKey(hash))
                    {
                        hashes[hash] = [];
                    }

                    hashes[hash].Add(fileInfo);
                }
                catch
                {
                    // :--DDD
                }
            }

            foreach (var fileInfo in hashes.Values.Where(f => f.Count == 1).SelectMany(a => a))
            {
                duplicateFileGroup.DuplicateFiles.Remove(fileInfo);
            }
        }

        if (duplicateFileGroup.DuplicateFiles.Count <= 1)
        {
            return null;
        }

        duplicateFileGroup.OriginalFile = duplicateFileGroup.DuplicateFiles.MinBy(f => f.LastAccessTime);
        duplicateFileGroup.DuplicateFiles.Remove(duplicateFileGroup.OriginalFile);

        return duplicateFileGroup;
    }
}