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
		private const int UidTagSize = 40;

		public fixed byte Tag[UidTagSize];
		public int DataSize;

		public byte[] TagBytes
		{
			get
			{
				byte[] returnValue = new byte[UidTagSize];

				fixed (byte* returnValuePtr = returnValue)
				fixed(byte* tagPtr = Tag)
					NativeMethods.memcpy(returnValuePtr, tagPtr, UidTagSize);

				return returnValue;
			}
			set
			{
				fixed (byte* tagPtr = Tag)
				fixed (byte* valuePtr = value)
					NativeMethods.memcpy(tagPtr, valuePtr, UidTagSize);
			}
		}

	}
}
