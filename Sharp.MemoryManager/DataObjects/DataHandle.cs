using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sharp.MemoryManager
{
	public class DataHandle
	{
		public IEnumerable<int> Pages { get; private set; }
		
		public UidTag Tag { get; private set; }

		public bool IsValid { get; internal set; }

		internal DataHandle(IEnumerable<int> pageNum)
		{
			IsValid = true;
			Tag = UidGenerator.New();
			Pages = new List<int>(pageNum);
		}
	}
}
