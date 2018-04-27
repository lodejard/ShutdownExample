using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WebAppShutdown
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Before Run");
            BuildWebHost(args).Run();
            Console.WriteLine("After Run");

            Console.WriteLine("Press [enter] to exit:");
            Console.ReadLine();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .Build();
    }
}
