using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sharp.MemoryManager
{
	public enum AllocationStatus : byte
	{
		Full = 2,
		Partial = 1,
		NotAllocated = 0
	}
}
