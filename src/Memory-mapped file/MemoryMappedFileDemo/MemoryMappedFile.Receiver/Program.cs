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