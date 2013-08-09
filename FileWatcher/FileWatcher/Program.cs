using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileWatcher
{
    class Program
    {
        static void Main(string[] args)
        {
            MainProgram prog = new MainProgram();
            prog.Start();

            Console.ReadLine();
        }
    }
}
