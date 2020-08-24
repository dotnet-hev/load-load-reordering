using System;
using System.Threading;

namespace load_load_reordering
{
	class Program
	{
		private static int sync;
		private static object[] lazyArray;

		private static object LazyInit(ref object _lazyObj)
		{
			if (_lazyObj == null)
				Interlocked.CompareExchange(ref _lazyObj, new object(), null);

			// RMO: Prevent reordering for two loads for the same address:
			// {
			//	 * Line 13: if (_lazyObj == null)
			//	 * Line 31: return _lazyObj;
			// }
			// 
			// When LazyInit returns null, observe in global time, the actual
			// execution order of &_lazyObj access is:
			//  1. Thread 1: Load &_lazyObj for return, the value is null.
			//  2. Thread 2: Load &_lazyObj for branch, the value is null,
			//			   Store an new object to &_lazyObj.
			//  3. Thread 1: Load &_lazyObj for branch, the value is not null,
			//			   Return null.
			//
			// Interlocked.MemoryBarrier();
			return _lazyObj;
		}

		private static void ThreadHandler()
		{
			while (true)
			{
				var s = sync;

				if (s <= 0)
					continue;
				if (Interlocked.CompareExchange(ref sync, s - 1, s) != s)
					continue;

				var array = lazyArray;
				for (int i = 0; i < array.Length; i++)
				{
					if (LazyInit(ref array[i]) == null)
						Console.WriteLine("LazyInit return null!");
				}

			}
		}

		static void Main(string[] args)
		{
			Thread[] threads = new Thread[Environment.ProcessorCount];
			Random rnd = new Random();

			for (int i = 0; i < threads.Length; i++)
			{
				threads[i] = new Thread(new ThreadStart(ThreadHandler));
				threads[i].Start();
			}

			while (true)
			{
				var len = rnd.Next(1, 16);
				lazyArray = new object[len];
				Interlocked.MemoryBarrier();
				sync = threads.Length;

				while (sync > 0)
					Interlocked.MemoryBarrier();
			}
		}
	}
}
