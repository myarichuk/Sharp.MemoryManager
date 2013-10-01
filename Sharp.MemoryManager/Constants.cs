using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Sharp.MemoryManager
{
	public static class Constants
	{
		public static readonly int Version = 1;
		public static readonly byte[] Signature = Guid.Parse("7BC6F5FD-FC3F-4139-85B6-E8C88CA13F90").ToByteArray();
		public static readonly int StorageHeaderSize = Marshal.SizeOf(typeof(StorageHeader));
		public static readonly int PageHeaderSize = Marshal.SizeOf(typeof(PageHeader));
		public static readonly int SegmentListHeaderSize = Marshal.SizeOf(typeof(SegmentListHeader));
		public static readonly int SegmentInfoSize = Marshal.SizeOf(typeof(SegmentInfo));		
		public static readonly int SizeOfInt = Marshal.SizeOf(typeof(int));
		public static readonly int SizeOfLong = Marshal.SizeOf(typeof(long));
		public static readonly int SizeOfBool = Marshal.SizeOf(typeof(bool));
		public const int DefaultLockTimeout = 60000;
		public const int NoLockTimeout = Int32.MinValue;
		public const int TagSize = 40;
	}
}
