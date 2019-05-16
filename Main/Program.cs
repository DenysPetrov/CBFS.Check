using System;
using System.Threading;
using CallbackFS;

namespace Main
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("***App to check how CBFS doesn't work***");

            Run();
            Console.ReadLine();
        }

        private static void Run()
        {
            new Thread(() =>
            {
                CallbackFileSystem.Initialize("713CC6CE-B3E2-4fd9-838D-E28F558F6866");
                using (var cbfs = new CbfsHandler())
                {
                    cbfs.Initialize();

                    Console.ReadLine();
                }
            }).Start();
        }
    }
}
