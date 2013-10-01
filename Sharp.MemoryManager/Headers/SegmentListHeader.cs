using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Sharp.MemoryManager
{
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct SegmentListHeader
	{
		public int SegmentSize;
		public long SegmentCount;
		public long SegmentFreeCount;
		public SegmentInfo* Segments;
	}
}
