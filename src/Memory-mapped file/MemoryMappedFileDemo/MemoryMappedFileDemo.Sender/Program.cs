// https://learn.microsoft.com/en-us/dotnet/standard/io/memory-mapped-files

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