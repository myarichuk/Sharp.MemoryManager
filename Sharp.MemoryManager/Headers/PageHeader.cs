using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Sharp.MemoryManager
{
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct PageHeader
	{
		public fixed byte UidTag[40];
		public int DataSize;
	}
}
