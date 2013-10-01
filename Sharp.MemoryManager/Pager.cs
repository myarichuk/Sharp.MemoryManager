using Sharp.MemoryManager.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sharp.MemoryManager
{
	public unsafe class Pager : IDisposable
	{
		#region Constants
		private const string PAGE_CHANGE_MUTEX_SUFFIX = "_CHANGES_SYNC_MUTEX";
		private const string TRANSACTION_MUTEX_SUFFIX = "_TRANSACTION_SYNC_MUTEX";
		private const int PAGE_DATA_SIZE = 8; //8 bytes in each page
		private const double FREE_SPACE_MARGIN_PERCENT = 1.1; //how much free space to reserve in hard drive												    
		private const long DEFAULT_DATA_CAPACITY = 8589934592; //default = 8GB of capacity
		private const long MAX_SEGMENT_SIZE = 104857600; //default = 100 MB max segment size
		#endregion

		#region Private Members

		private byte* m_BaseStoragePointer;

		private StorageHeader* m_StorageHeader;
		private MemoryMappedFile m_PagerStorage;
		private MemoryMappedViewAccessor m_PageStorageViewAccessor;
		private readonly SyncProvider m_PageChangeSyncProvider;
		private readonly SyncProvider m_TransactionSyncProvider;
		private volatile bool m_IsDisposed;
		private readonly string m_StorageName;
		private long m_TotalPagerSize;

		#endregion

		#region Constructor(s)

		public Pager(string storageName, long dataCapacity = DEFAULT_DATA_CAPACITY)
		{
			m_StorageName = storageName;			

			m_PageChangeSyncProvider = new SyncProvider(storageName + PAGE_CHANGE_MUTEX_SUFFIX);
			m_TransactionSyncProvider = new SyncProvider(storageName + TRANSACTION_MUTEX_SUFFIX);
			InitializeStorage(firstTimeInit: true,
							  pageDataSize : PAGE_DATA_SIZE,
							  capacity: dataCapacity);
			m_IsDisposed = false;
		}

		#endregion

		#region Public Properties

		public long TotalSize
		{
			get
			{
				return m_TotalPagerSize;
			}
		}

		public int PageDataSize
		{
			get
			{
				return m_StorageHeader->PageDataSize;
			}
		}

		#endregion

		#region Initialization Helpers

		private void InitializeStorage(long capacity,int pageDataSize, bool firstTimeInit = false)
		{
			using (var @lock = m_PageChangeSyncProvider.Lock())
			{
				var pageCount = capacity / pageDataSize;
				m_TotalPagerSize = Constants.Signature.Length +
								   Constants.StorageHeaderSize +
								   (Constants.SizeOfBool * pageCount) + //free page offset table
								   (Constants.SizeOfInt * pageCount) + //page offset table
								   (Constants.SizeOfInt * pageCount) + //page header offset table
								   (Constants.PageHeaderSize * pageCount * 2) + //reserve space for 2 copies of each page header
								   (pageDataSize * pageCount * 2); //reserve space for 2 copies of each page

				CheckDiskFreeSpaceAndThrowIfNeeded(m_TotalPagerSize);

				m_PagerStorage = MemoryMappedFile.CreateOrOpen(m_StorageName, m_TotalPagerSize);
				m_PageStorageViewAccessor = m_PagerStorage.CreateViewAccessor();
				m_PageStorageViewAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref m_BaseStoragePointer);

				m_StorageHeader = (StorageHeader*)(m_BaseStoragePointer + Constants.Signature.Length);

				if (firstTimeInit)
				{
					fixed (byte* signaturePtr = Constants.Signature)
						NativeMethods.memcpy(m_BaseStoragePointer, signaturePtr, Constants.Signature.Length);

					m_StorageHeader->Version = Constants.Version; //TODO : add version checking
					m_StorageHeader->TotalPageCount = pageCount;
					m_StorageHeader->PageDataSize = pageDataSize;
					
					m_StorageHeader->PageOffsets = (long*)(m_BaseStoragePointer + 														   
														  Constants.Signature.Length + 
														  Constants.StorageHeaderSize);

					m_StorageHeader->SegmentListHeaders = (SegmentListHeader*)(m_BaseStoragePointer +
																				Constants.Signature.Length +
																				Constants.StorageHeaderSize + 
																			    (Constants.SizeOfLong * pageCount));

					InitializeSegmentListHeaders();
				}

			}
		}

		#endregion

		#region Public Methods

		public Transaction NewTransaction(int transactionTimeout = Constants.NoLockTimeout)
		{
			ThrowIfDisposed();
			try
			{
				var syncReleaseObj = m_TransactionSyncProvider.Lock(transactionTimeout);

				return new Transaction(syncReleaseObj.Dispose, null);
			}
			catch (TimeoutException e)
			{
				throw new TimeoutException("Unable to start new transaction within defined timeout. ", e);
			}
		}

		public IEnumerable<byte> Get(DataHandle handle,bool shouldUseConcurrencyTag = false)
		{
			ThrowIfDisposed();
			throw new NotImplementedException();
		}

		public IEnumerable<byte> Get(Transaction tx,DataHandle handle, bool shouldUseConcurrencyTag = false)
		{
			ThrowIfDisposed();
			throw new NotImplementedException();
		}

		public void Set(Transaction tx, DataHandle handle, byte[] data, bool shouldUseConcurrencyTag = false)
		{
			ThrowIfDisposed();

			throw new NotImplementedException();
		}

		public void Delete(Transaction tx, DataHandle handle, bool shouldUseConcurrencyTag = false)
		{
			ThrowIfDisposed();

			throw new NotImplementedException();
		}

		public DataHandle Allocate(int requestedSize)
		{
			ThrowIfDisposed();
			using (var @lock = m_PageChangeSyncProvider.Lock())
			{
				DataHandle allocatedHandle = null;
				var roundedRequestedSize = MathUtil.NextPowerOf2(requestedSize);
				var requestedSizePower = MathUtil.GetPowerOf2FromResult(roundedRequestedSize / m_StorageHeader->PageDataSize);

				var relevantSegmentList = GetSegmentListHeader(requestedSizePower);
				if (relevantSegmentList->SegmentFreeCount > 0)
					allocatedHandle = AllocateExact(requestedSizePower,relevantSegmentList);

				if(allocatedHandle == null)
					allocatedHandle = AllocateWithSearch(requestedSizePower);

				if(allocatedHandle == null)
					throw new OutOfMemoryException("cannot allocate - insufficient free space");

				return allocatedHandle;
			}
		}
		

		public void Free(DataHandle handle)
		{
			ThrowIfDisposed();
			using (var @lock = m_PageChangeSyncProvider.Lock())
			{
				var requestedSizePower = MathUtil.GetPowerOf2FromResult(handle.SegmentSize / m_StorageHeader->PageDataSize);
				var relevantSegmentList = GetSegmentListHeader(requestedSizePower);

				ValidateHandle(handle, requestedSizePower, relevantSegmentList);

				//TODO : add handle validation here (SegmentIndex matches etc)
				var relevantSegment = GetSegment(relevantSegmentList, handle.SegmentIndex);
				
				relevantSegment->Status = AllocationStatus.NotAllocated;

				relevantSegmentList->SegmentFreeCount++;
				handle.IsValid = false;
			}
		}

		#endregion

		#region Helper Methods

		private void ValidateHandle(DataHandle handle, int requestedSizePower,SegmentListHeader* segmentListHeader)
		{
			if (!handle.IsValid)
				throw new ArgumentException("handle is invalid or not allocated", "handle");

			if (m_StorageHeader->SegmentListCount < requestedSizePower)
				throw new ArgumentException("Invalid SegmentSize in DataHandle");

			var relevantSegmentList = GetSegmentListHeader(requestedSizePower);
			if (handle.SegmentIndex > relevantSegmentList->SegmentCount)
				throw new ArgumentException("Invalid SegmentIndex in DataHandle");
		}

		private DataHandle AllocateWithSearch(int requestedSizePower)
		{
			//TODO : finish this
			return null;
		}

		private unsafe DataHandle AllocateExact(int segmentListIndex,SegmentListHeader* relevantSegmentList)
		{
			relevantSegmentList->SegmentFreeCount--;
			for (var segmentIndex = 0; segmentIndex < relevantSegmentList->SegmentCount; segmentIndex++)
			{
				var currentSegment = GetSegment(relevantSegmentList, segmentIndex);
				if (currentSegment->Status == AllocationStatus.NotAllocated)
				{
					currentSegment->Status = AllocationStatus.Full;
					var dataHandle = new DataHandle(relevantSegmentList->SegmentSize, segmentIndex);
					currentSegment->TagBytes = dataHandle.Tag;

					MarkAllocatedLowerLevels(segmentListIndex, currentSegment);
					return dataHandle;
				}
			}

			return null;
		}		

		private unsafe void MarkAllocatedLowerLevels(int segmentListInitialIndex, SegmentInfo* currentSegment)
		{
			if (segmentListInitialIndex == 0) return; //no need to mark on lowest possible level

			for (int segmentListIndex = segmentListInitialIndex - 1; segmentListIndex >= 0; segmentListIndex--)
			{
				var currentSegmentListHeader = GetSegmentListHeader(segmentListIndex);
				var segmentSizeInPages = currentSegmentListHeader->SegmentSize / m_StorageHeader->PageDataSize;

				//TODO : seems correct, test exhaustingly
				var relevantSegmentStartIndex = currentSegment->StartPage / segmentSizeInPages;
				var relevantSegmentEndIndex = currentSegment->EndPage / segmentSizeInPages;

				currentSegmentListHeader->SegmentFreeCount -= (relevantSegmentEndIndex - relevantSegmentStartIndex);
				
				for (long segmentIndex = relevantSegmentStartIndex; segmentIndex < relevantSegmentEndIndex; segmentIndex++)
				{
					var segment = GetSegment(currentSegmentListHeader, segmentIndex);
					segment->Status = AllocationStatus.Partial;
				}
			}
		}

		private void InitializeSegmentListHeaders()
		{
			long index = 0;
			for (long powerOf2 = 0; powerOf2 <= MathUtil.NextPowerOf2(MAX_SEGMENT_SIZE); powerOf2++)
			{
				var currentListHeader = GetSegmentListHeader(index);
				
				var powerOf2Result = (int)Math.Pow(2,powerOf2);

				currentListHeader->SegmentSize = m_StorageHeader->PageDataSize * powerOf2Result;
				currentListHeader->SegmentCount = (m_StorageHeader->TotalPageCount * m_StorageHeader->PageDataSize) / currentListHeader->SegmentSize;
				currentListHeader->SegmentFreeCount = currentListHeader->SegmentCount;
				if (currentListHeader->SegmentCount == 1)
				{
					m_StorageHeader->SegmentListCount = index;
					break;
				}
				index++;
			}
			
			var startingOffset = Constants.Signature.Length +
								 Constants.StorageHeaderSize +
								 (Constants.SizeOfLong * m_StorageHeader->TotalPageCount) +
								 (Constants.SegmentListHeaderSize * m_StorageHeader->SegmentListCount);

			for (long segmentListIndex = 0; segmentListIndex < m_StorageHeader->SegmentListCount; segmentListIndex++)
			{
				var currentListHeader = GetSegmentListHeader(segmentListIndex);
				InitializeSegments(currentListHeader,startingOffset);
				startingOffset += (Constants.SegmentInfoSize * currentListHeader->SegmentCount);
			}
		}

		private void InitializeSegments(SegmentListHeader* header, long segmentStartingOffset)
		{
			header->Segments = (SegmentInfo*)(m_BaseStoragePointer + segmentStartingOffset);
			var pageStepSize = header->SegmentSize / m_StorageHeader->PageDataSize;
			long currentStartPage = 0;
			long currentEndPage = pageStepSize - 1;

			for (int segmentIndex = 0; segmentIndex < header->SegmentCount; segmentIndex++)
			{
				var currentSegment = (SegmentInfo*)(m_BaseStoragePointer + segmentStartingOffset + (Constants.SegmentInfoSize * segmentIndex));
				currentSegment->Status = AllocationStatus.NotAllocated;
				currentSegment->StartPage = currentStartPage;
				currentSegment->EndPage = currentEndPage;
				NativeMethods.memset(currentSegment->Tag, 0, Constants.TagSize);
				
				currentStartPage += pageStepSize;
				currentEndPage += (pageStepSize - 1);
			}
		}

		// equivalent of m_StorageHeader->SegmentListHeaders[index] 
		private SegmentListHeader* GetSegmentListHeader(long index)
		{
			return &m_StorageHeader->SegmentListHeaders[index];
		}

		private SegmentInfo* GetSegment(SegmentListHeader* segmentListHeader, long index)
		{
			return &segmentListHeader->Segments[index];
		}

		private void ThrowIfDisposed()
		{
			if (m_IsDisposed)
				throw new ApplicationException("Pager is already disposed.");
		}


		private static void CheckDiskFreeSpaceAndThrowIfNeeded(long size)
		{
			var currentDriveLetter = Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory);
			var currentDriveInfo = new DriveInfo(currentDriveLetter);

			if ((size * FREE_SPACE_MARGIN_PERCENT) >= currentDriveInfo.AvailableFreeSpace)
			{
				throw new OutOfMemoryException("Not enough free space to store paging data");
			}
		}

		#endregion

		#region IDisposable Implementation

		~Pager()
		{
			DisposeThis();
		}

		public void Dispose()
		{
			DisposeThis();
			GC.SuppressFinalize(this);
		}

		private void DisposeThis()
		{
			if (!m_IsDisposed)
			{
				if (m_TransactionSyncProvider != null)
				{
					m_TransactionSyncProvider.Dispose();
				}

				if (m_PageStorageViewAccessor != null)
				{
					m_PageStorageViewAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
					m_PageStorageViewAccessor.Dispose();
				}

				if (m_PagerStorage != null)
				{
					m_PagerStorage.Dispose();
				}

				if (m_PageChangeSyncProvider != null)
				{
					m_PageChangeSyncProvider.Dispose();
				}
				
				m_IsDisposed = true;
			}
		}

		#endregion
	}
}
