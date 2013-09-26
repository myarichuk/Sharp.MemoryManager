using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

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

		[TestMethod]
		public void Pager_Single_Set_Get_Inside_Transaction()
		{
			const int ALLOCATION_SIZE = 128;
			byte[] testData = new byte[ALLOCATION_SIZE];
			var rand = new Random();
			rand.NextBytes(testData);

			using (var pager = new Pager("TestStorage", 1024))
			{
				var handle = pager.Allocate(ALLOCATION_SIZE);
				using (var tx = pager.NewTransaction())
				{
					pager.Set(tx, handle, testData);
					var fetchedData = pager.Get(tx, handle);
					CollectionAssert.AreEqual(testData.ToList(), fetchedData.ToList());
				}

				pager.Free(handle);
			}
		}

		[TestMethod]
		public void Pager_Multiple_Set_Get_Inside_Transaction_SingleThreaded()
		{
			const int ALLOCATION_SIZE = 128;
			const int TEST_COLLECTION_SIZE = 25;
			var rand = new Random();

			using (var pager = new Pager("TestStorage", ALLOCATION_SIZE * TEST_COLLECTION_SIZE))
			{
				var handleCollection = new List<DataHandle>();
				for (int i = 0; i < TEST_COLLECTION_SIZE; i++)
					handleCollection.Add(pager.Allocate(ALLOCATION_SIZE));
				using (var tx = pager.NewTransaction())
				{
					foreach (var handle in handleCollection)
					{
						byte[] testData = new byte[ALLOCATION_SIZE];
						rand.NextBytes(testData);

						pager.Set(tx, handle, testData);
						var fetchedData = pager.Get(tx, handle);
						CollectionAssert.AreEqual(testData.ToList(), fetchedData.ToList());
					}
				}

				foreach(var handle in handleCollection)
					pager.Free(handle);
			}
		}

		//the same as #1 test, just in the middle of transaction, pages get allocated/freed
		[TestMethod]
		public void Pager_Multiple_Set_Get_Inside_Transaction_2_SingleThreaded()
		{
			const int ALLOCATION_SIZE = 128;
			const int DUMMY_ALLOCATION_SIZE = 32;
			const int TEST_COLLECTION_SIZE = 50;
			var rand = new Random();

			using (var pager = new Pager("TestStorage", ALLOCATION_SIZE * TEST_COLLECTION_SIZE * 2))
			{
				var handleCollection = new List<DataHandle>();
				for (int i = 0; i < TEST_COLLECTION_SIZE; i++)
					handleCollection.Add(pager.Allocate(ALLOCATION_SIZE));

				bool testIsRunning = true;
				var wasAllocatedAtLeastOnceEvent = new ManualResetEventSlim();
				var allocationAndFreeingTask = Task.Factory.StartNew(() =>
					{
						while (testIsRunning)
						{
							var handle1 = pager.Allocate(DUMMY_ALLOCATION_SIZE);
							var handle2 = pager.Allocate(DUMMY_ALLOCATION_SIZE);
							Thread.Sleep(1);
							if (!wasAllocatedAtLeastOnceEvent.IsSet)
								wasAllocatedAtLeastOnceEvent.Set();
							pager.Free(handle1);
							pager.Free(handle2);
						}
					});

				wasAllocatedAtLeastOnceEvent.Wait();

				using (var tx = pager.NewTransaction())
				{
					foreach (var handle in handleCollection)
					{
						byte[] testData = new byte[ALLOCATION_SIZE];
						rand.NextBytes(testData);

						pager.Set(tx, handle, testData);
						var fetchedData = pager.Get(tx, handle);
						CollectionAssert.AreEqual(testData.ToList(), fetchedData.ToList());
					}
				}
				testIsRunning = false;
				Task.WaitAny(allocationAndFreeingTask);

				foreach (var handle in handleCollection)
					pager.Free(handle);				
			}
		}

		[TestMethod]
		public void Pager_Multiple_Set_Get_Inside_Transaction_MultiThreaded()
		{
			const int ALLOCATION_SIZE = 128;
			const int TEST_COLLECTION_SIZE = 25;
			var rand = new Random();
			using (var pager = new Pager("TestStorage", ALLOCATION_SIZE * TEST_COLLECTION_SIZE))
			{
				var handleCollection = new List<DataHandle>();
				for (int i = 0; i < TEST_COLLECTION_SIZE; i++)
					handleCollection.Add(pager.Allocate(ALLOCATION_SIZE));
				using (var tx = pager.NewTransaction())
				{
					Parallel.ForEach(handleCollection, handle =>
					{
						byte[] testData = new byte[ALLOCATION_SIZE];
						rand.NextBytes(testData);

						pager.Set(tx, handle, testData);
						var fetchedData = pager.Get(tx, handle);
						CollectionAssert.AreEqual(testData.ToList(), fetchedData.ToList());
					});
				}

				foreach (var handle in handleCollection)
					pager.Free(handle);
			}
		}
	}
}
