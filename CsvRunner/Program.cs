using CsvRunner.Run;

public class Program
{
    public static async Task Main(string[] args)
    {
        var example = new Example();
        await example.RunExample();
    }
}