using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Sharp.MemoryManager
{
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct PageHeader
	{
		public UidTag Tag;
		public int DataSize;
	}
}
