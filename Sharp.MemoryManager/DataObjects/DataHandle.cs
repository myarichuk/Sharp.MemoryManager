﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sharp.MemoryManager
{
	public class DataHandle
	{
		public int SegmentSize { get; private set; }

		public int SegmentIndex { get; private set; }
		
		public UidTag Tag { get; private set; }

		public bool IsValid { get; internal set; }

		internal DataHandle(int segmentSize, int segmentIndex)
		{
			IsValid = true;
			Tag = UidGenerator.New();
			SegmentIndex = segmentIndex;
			SegmentSize = segmentSize;
		}
	}
}
