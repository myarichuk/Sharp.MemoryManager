using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Sharp.MemoryManager
{
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct SegmentInfo
	{
		public long StartPage;
		public long EndPage;

		public AllocationStatus Status;

		private const int UidTagSize = Constants.TagSize;
		public fixed byte Tag[UidTagSize];

		public byte[] TagBytes
		{
			get
			{
				byte[] returnValue = new byte[UidTagSize];

				fixed (byte* returnValuePtr = returnValue)
				fixed (byte* tagPtr = Tag)
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
