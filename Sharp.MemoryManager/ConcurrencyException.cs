using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sharp.MemoryManager
{
	public class ConcurrencyException : Exception
	{
		public ConcurrencyException(string message)
			: base(message)
		{
		}

		public ConcurrencyException(string message,Exception innerException)
			: base(message,innerException)
		{
		}
	}
}
