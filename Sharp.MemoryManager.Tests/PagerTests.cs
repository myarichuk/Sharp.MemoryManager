using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using System.IO;

namespace Sharp.MemoryManager.Tests
{
	[TestClass]
	public class PagerTests
	{
		[TestMethod]
		public void Pager_Initialized_ExceptionNotThrown()
		{
			Assert.NotThrows(() => 
			{ 
				using (var pager = new Pager("TestStorage", 1024)) { } 
			});
		}

		[TestMethod]
		public void Pager_SingleAllocation_ProperPagesAllocated()
		{
			const int ALLOCATION_SIZE = 128;
			using (var pager = new Pager("TestStorage", 1024)) 
			{
				var handle = pager.Allocate(ALLOCATION_SIZE);
				var totalAllocatedSize = handle.Pages.Count() * pager.PageDataSize;

				handle.Pages.Should().OnlyHaveUniqueItems();
				totalAllocatedSize.Should().BeGreaterOrEqualTo(ALLOCATION_SIZE);	
			} 
		}

		[TestMethod]
		public void Pager_MultipleAllocations_ProperPagesAllocated()
		{
			const int ALLOCATION_SIZE = 128;
			var allocatedHandles = new List<DataHandle>();
			using (var pager = new Pager("TestStorage", 1024 * 4))
			{

				for(int allocationIndex = 0; allocationIndex < 4; allocationIndex++)
					allocatedHandles.Add(pager.Allocate(ALLOCATION_SIZE));

				allocatedHandles.SelectMany(handle => handle.Pages).Should().OnlyHaveUniqueItems();
				allocatedHandles.ForEach(handle => handle.Pages.Should().OnlyHaveUniqueItems());
				allocatedHandles.ForEach(handle => ALLOCATION_SIZE.Should().BeLessOrEqualTo(handle.Pages.Count() * pager.PageDataSize));
			}
		}


		[TestMethod]
		public void Pager_Allocation_Freeing_Cannot_Free_Twice()
		{
			const int ALLOCATION_SIZE = 128;
			using (var pager = new Pager("TestStorage", 1024))
			{
				var handle = pager.Allocate(ALLOCATION_SIZE);
				pager.Free(handle);

				Assert.Throws<InvalidDataException>(() => pager.Free(handle));
			}
		}

		[TestMethod]
		public void Pager_Allocation_Freeing_And_AgainAllocation_TheSamePagesAllocated()
		{
			const int ALLOCATION_SIZE = 128;
			using (var pager = new Pager("TestStorage", 1024))
			{
				var handle1 = pager.Allocate(ALLOCATION_SIZE);				
				pager.Free(handle1);

				var handle2 = pager.Allocate(ALLOCATION_SIZE);
				pager.Free(handle2);

				handle1.Pages.ShouldBeEquivalentTo(handle2.Pages);

				handle1.IsValid.Should().BeFalse();
				handle2.IsValid.Should().BeFalse();

			}
		}
	
	}
}
