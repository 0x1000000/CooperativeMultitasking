using System;
using System.Threading;
using System.Threading.Tasks;

namespace CooperativeMultitasking
{
    class Program
    {
        static void Main()
        {
            CooperativeContext.Run(
                b => DoWork(b, "A", 4),
                b => DoWork(b, "B", 3, extraWork: true),
                b => DoWork(b, "C", 2),
                b => DoWork(b, "D", 1)
            );
        }

        static async ValueTask DoWork(ICooperativeBroker broker, string name, int delay, bool extraWork = false)
        {
            for (int i = 1; i <= delay; i++)
            {
                Console.WriteLine($"Work {name}: {i}, Thread: {Thread.CurrentThread.ManagedThreadId}");
                await broker;
                if (extraWork)
                {
                    Console.WriteLine($"Work {name}: {i} (Extra), Thread: {Thread.CurrentThread.ManagedThreadId}");
                    await broker;
                }
            }

            Console.WriteLine($"Work {name} is completed, Thread: {Thread.CurrentThread.ManagedThreadId}");
        }
    }
}
