using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiskAggregator
{
    class Program
    {
        static void Main(string[] args)
        {
            MainProgram prog = new MainProgram();
            prog.Start();

            while (true)
            {
                prog.Input(Console.ReadLine());
            }
        }
    }
}
