using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Sharp.MemoryManager
{
	[StructLayout(LayoutKind.Sequential,Pack = 1)]
	public unsafe struct StorageHeader
	{
		public int Version;
		public int PageDataSize;
		public long TotalPageCount;
		public long SegmentListCount;
		public long* PageOffsets;
		public SegmentListHeader* SegmentListHeaders;
	}
}
