using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp.MemoryManager.Utils
{
	public static class MathUtil
	{
		public static int GetPowerOf2FromResult(int powerOf2Result)
		{
			var powerOf2 = Math.Log(powerOf2Result, 2);
			return (int)powerOf2;
		}

		public static int NextPowerOf2(int x)
		{
			var result = Math.Pow(2, Math.Ceiling(Math.Log(x) / Math.Log(2)));
			return (int)result;
		}
		
		public static long NextPowerOf2(long x)
		{
			var result = Math.Pow(2, Math.Ceiling(Math.Log(x) / Math.Log(2)));
			return (long)result;
		}
	}
}
