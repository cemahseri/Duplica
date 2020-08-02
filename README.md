# FastestDuplicateFileFinder
Yet another duplicate file finder that's developed by KISS principle. But it's faster. With fast, I mean real fast.

# Usage
Just drag and drop the folder you want to scan, to the executable file.
Or alternatively, you can specify a path from the command line.
```
FastestDuplicateFileFinder.exe C:\path\to\your\mom
```

# Extremely Fast
It can scan ~2,000 files, which is ~10 GB, in less than one second on my poor Pentium Dual-Core E6800 @ 3.33 GHz. xxHash, the fastest non-cryptographic hashing algorithm that is out there, is supporting this little piece of code.

# To-Do
- If the beginnings of the files are the same, the algorithm will calculate the hash by reading the whole file. Instead of this, calculating hash with every next 4 MBs _(or so)_ of the file would be much better. Like, check all of those files' next 4 MBs. If they are different, there is no duplicate files. If they are same, check the next 4 MBs, and so. Opening, reading and closing streams are so cheap. So even in the worst scenario _(which is those files are identical and you will have to read the whole file)_, it'll not be that much worse. Like, few bloody seconds? IDK.
