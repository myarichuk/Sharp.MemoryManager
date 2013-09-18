using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sharp.MemoryManager
{
	public class DataHandle
	{
		public IEnumerable<int> PageNum { get; private set; }
		public UidTag Tag { get; private set; }

		internal DataHandle(IEnumerable<int> pageNum)
		{
			Tag = UidGenerator.New();
			PageNum = pageNum;
		}
	}
}
