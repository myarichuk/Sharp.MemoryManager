using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sharp.MemoryManager
{
	public static class UidGenerator
	{
		private static int m_LastId;

		static UidGenerator()
		{
			m_LastId = 0;
		}

		public static UidTag New()
		{
			var ticks = DateTime.UtcNow.Ticks;
			var atomicId = Interlocked.Increment(ref m_LastId);
			return new UidTag(ticks, atomicId);
		}
	}
}
