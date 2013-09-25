using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sharp.MemoryManager
{
	class PageOffsets
	{
		public int PageHeaderOffset { get; private set; }
		public int PageDataOffset { get; private set; }

		public PageOffsets(int pageHeaderOffset, int pageDataOffset)
		{
			// TODO: Complete member initialization
			this.PageHeaderOffset = pageHeaderOffset;
			this.PageDataOffset = pageDataOffset;
		}
	}
}
