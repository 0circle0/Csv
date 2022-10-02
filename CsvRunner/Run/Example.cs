using CsvImporter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace CsvRunner.Run
{
    public class Example
    {
        public Example()
        {
            
        }

        public async Task RunExample()
        {
            System.Diagnostics.Stopwatch sw = new();

            sw.Reset();
            sw.Start();

            int timesRun = 1;
            List<Person> person = new();
            Result result;
            Console.WriteLine("Parsing");
            for (int i = 0; i < timesRun; i++)
            {
                var path = "C:\\Users\\jense\\source\\repos\\CsvRunner\\bin\\Debug\\net6.0\\Example.csv";

                result = Csv.Import(path, out person);

                Console.WriteLine($"{result.Value} {(result.Value ? "Success" : result.Results)}");

                Console.WriteLine("Exporting");

                await Export(person);

                Console.WriteLine("Parsing Export");
                result = Csv.Import("C:\\Users\\jense\\source\\repos\\CsvRunner\\bin\\Debug\\net6.0\\Export Data.csv", out person);
                Console.WriteLine($"{result.Value} {(result ? "Success" : result.Value)}");
            }


            sw.Stop();
            Console.WriteLine($"Finished in {sw.ElapsedMilliseconds}ms\nAvg ticks: {sw.ElapsedTicks / timesRun}\nAvg ms: {sw.ElapsedMilliseconds / timesRun}");
        }

        private async Task Export(List<Person> person)
        {
            Result result = await Csv.Export(person);
            Console.WriteLine($"{result.Value} {(result.Value ? "Success" : result.Results)}");
        }

    }
}
