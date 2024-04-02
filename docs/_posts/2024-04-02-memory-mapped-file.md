---
layout: post
title: Memory-mapped file
date: 2024-04-02 12:57 +0200
description: Also, Mutex
github-link:
---

I recently learned of the concept of [Memory-mapped File](https://en.wikipedia.org/wiki/Memory-mapped_file). Wikipedia explains it better than I ever could, but it's the act of, as the name suggests, mapping a file onto a section of memory, so that a program interacts with it in memory, which is typically easier and faster than reading from and writing to disc. When the program finishes intracting with the file, all the changes are applied to the file in mass storage.

It was new to me, so I naturally asked the Internet:  
**Can I do it in C#?**

[Yes, I can.](https://learn.microsoft.com/en-us/dotnet/standard/io/memory-mapped-files)

It turns out, there are two distinct types of MMFs:

- Persisted memory-mapped files
- Non-persisted memory-mapped files

**Persosted MMFs** are basically what I imagined it would look like - you open a file, work with it in memory and close it when you're done, changes are saved to file.

I was actually more interested in **Non-persisted MMFs**. They work the same, but there is actually no file, just some space in memory.

**Why would you want a MMF without an F?**

Mainly, because this space in memory can be shared between processes. You can create a non-presisted MMF, you give it size in bytes and the operating system gives you a part of memory with your declared size that your process and any other process that asks for it by name can interact with as if it was a file.

## My example

I looked at examples provided by Microsoft and prepared my own, heavily influenced by what they did.

I created 2 console applications - `Sender` and `Receiver`.

`Sender` creates the MMF, writes to it whatever text it reads from console.  
Then, the `Receiver` has a chance to open the file, read what the sender wrote and overwrites it with a message read from console.  
Lastly, `Sender` reads the response from the `Receiver` and closes.

Here's my code for `Sender`:

```
using System.IO.MemoryMappedFiles;

string mapName = "mmf-demo";
long capacity = 0x400;
string mutexName = "mmf-demo-mutex";

using var mmf = MemoryMappedFile.CreateNew(mapName, capacity);

Console.WriteLine($"Memory mapped file created. Name: {mapName}");

Mutex mutex = new Mutex(true, mutexName, out bool mutexCreated);

Console.WriteLine($"Mutex created. Name: {mutexName}");

using (var stream = mmf.CreateViewStream())
{
    BinaryWriter writer = new(stream);

    Console.WriteLine("Write a message:");
    string message = Console.ReadLine()!;

    writer.Write(message);
}
mutex.ReleaseMutex();

Console.WriteLine("Message sent.");
Console.WriteLine("Press any key to read file contents...");

Console.ReadKey(true);

mutex.WaitOne();

using (var stream = mmf.CreateViewStream())
{
    BinaryReader reader = new(stream);
    Console.WriteLine(reader.ReadString());
}

mutex.ReleaseMutex();

Console.ReadLine();
```

And here's code for `Receiver`:

```
using System.IO.MemoryMappedFiles;

string mapName = "mmf-demo";
string mutexName = "mmf-demo-mutex";

Console.WriteLine("Press any key to try reading memory mapped file...");
Console.ReadKey(true);

try
{
	using var mmf = MemoryMappedFile.OpenExisting(mapName);

    Console.WriteLine($"Memory mapped file opened. Name: {mapName}");

	Mutex mutex = Mutex.OpenExisting(mutexName);
	mutex.WaitOne();

    using (var stream = mmf.CreateViewStream())
	{
		BinaryReader reader = new(stream);
		string message = reader.ReadString();

        Console.WriteLine("Message:");
        Console.WriteLine(message);
    }
	mutex.ReleaseMutex();

    Console.WriteLine("Write a response:");
    string response = Console.ReadLine()!;

	mutex.WaitOne();

	using (var stream = mmf.CreateViewStream())
	{
		BinaryWriter writer = new(stream);
		writer.Write(response);
	}
	mutex.ReleaseMutex();

	Console.WriteLine("Response sent.");
}
catch (FileNotFoundException)
{
    Console.WriteLine("Memory mapped file not found.");
}

Console.ReadLine();
```

It's actually not very complicated, but you could note some things.

- `MemoryMappedFile` needs to be disposed, hence the `using` keyword.
- I don't check for the size of input. If any message written to the file is bigger than the capacity 1KB I allocated, you will see `System.NotSupportedException: „Unable to expand length of this stream beyond its capacity."`. I decided to leave it as is for conciseness, but in real application, some safeguard should be put in place.
- This code uses `Mutex`. **What is a `Mutex`?**

## What is a `Mutex`?

According to [Microsoft itself](https://learn.microsoft.com/en-us/dotnet/api/system.threading.mutex), `Mutex` is:

> A synchronization primitive that can also be used for interprocess synchronization.

In essence, it's a similar mechanism as `Lock`, but not only within a process, but on system-level.

Basically, when you don't want multiple processes accessing a resource, an MMF in our case, at the same time, you make each process ask the `Mutex` (the same `Mutex` in each - it's given a name to identify it) for access. If the `Mutex` is free, a process takes ownership of it, if it's not, the process waits until it's released somewhere else.

I had some doubts about the sample code I recreated. `MemoryMappedFile` is being disposed by the end of the program, but what about `Mutex`? If it's created not in-process but in the system, shouldn't the OS be asked to release it after we're done with it?

`Mutex` class is `IDisposable`, so it would make sense to use `using` keyword. **Why isn't Microsoft doing it in their examples?**
I managed to find this piece of information in [documentation for `CreateMutex()` method](https://learn.microsoft.com/pl-pl/windows/win32/api/synchapi/nf-synchapi-createmutexa):

> Use the CloseHandle function to close the handle. The system closes the handle automatically when the process terminates. The mutex object is destroyed when its last handle has been closed.

If I understand it properly, you just need to close all processes that use the mutex for the OS to know it needs to dispose of the named mutex.

## Summary

`MemoryMappedFile` is a very specific tool. It might not be the most usefull, but there might come a time when it's the right one for the job at hand, so at leas knowing that it exitst, could come in handy.

I also learned what exactly a mutex is - a word I'd heard thrown around from time to time. It's definetely not a C#-specific term, making it worth knowing no matter what language one ends up using.
