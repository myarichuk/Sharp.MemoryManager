﻿using System;
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
		private const int DEFAULT_DATA_CAPACITY = Int32.MaxValue; //default around 2GB of capacity
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
		private int m_BaseOffsetOfSetA;
		private int m_BaseOffsetOfSetB;

		#endregion

		#region Constructor(s)

		public Pager(string storageName, int dataCapacity = DEFAULT_DATA_CAPACITY)
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

		private void InitializeStorage(int capacity,int pageDataSize, bool firstTimeInit = false)
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
					m_StorageHeader->FreePageCount = pageCount;
					
					m_StorageHeader->FreePageFlags = (bool*)(m_BaseStoragePointer + 
															  Constants.Signature.Length + 
															  Constants.StorageHeaderSize);
					
					m_StorageHeader->PageOffsets = (int*)(m_BaseStoragePointer + 														   
														  Constants.Signature.Length + 
														  Constants.StorageHeaderSize +
														  (Constants.SizeOfBool * pageCount));
					
					m_StorageHeader->PageHeaderOffsets = (int*)(m_BaseStoragePointer +
																Constants.Signature.Length +
																Constants.StorageHeaderSize +
																(Constants.SizeOfBool * pageCount) +
																(Constants.SizeOfInt * pageCount));

					m_BaseOffsetOfSetA = Constants.Signature.Length +
										 Constants.StorageHeaderSize +
										 (Constants.SizeOfBool * pageCount) +
										 (Constants.SizeOfInt * pageCount * 2);

					m_BaseOffsetOfSetB = m_BaseOffsetOfSetA +
										 (Constants.PageHeaderSize * pageCount) +
										 (pageDataSize * pageCount);

					FirstTimeInitialize_PageOffsetTable();
				}

			}
		}

		
		private void FirstTimeInitialize_PageOffsetTable()
		{
			var pageSetOffset = BaseOffsetByPageSet(PageSet.SetA);
			m_StorageHeader->LastFreePageNum = 0;
			for (int pageNum = 0; pageNum < m_StorageHeader->TotalPageCount; pageNum++)
			{
				m_StorageHeader->PageHeaderOffsets[pageNum] = pageSetOffset + PageHeaderRelativeOffset(pageNum);
				m_StorageHeader->PageOffsets[pageNum] = pageSetOffset + PageDataRelativeOffset(pageNum);
				m_StorageHeader->FreePageFlags[pageNum] = true;
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

				return new Transaction(syncReleaseObj.Dispose, CopyPageAndFetchOffset);
			}
			catch (TimeoutException e)
			{
				throw new TimeoutException("Unable to start new transaction within defined timeout. ", e);
			}
		}

		public IEnumerable<byte> Get(DataHandle handle,bool shouldUseConcurrencyTag = false)
		{
			Validate(handle);
			ThrowIfDisposed();
			if (!ArePagesAllocated(handle))
				return null;

			int actualDataSize;
			var data = GetInternal(handle,
								   pageNum => m_StorageHeader->PageHeaderOffsets[pageNum],
								   pageNum => m_StorageHeader->PageOffsets[pageNum],
								   shouldUseConcurrencyTag, out actualDataSize);

			return data.Take(actualDataSize);
		}

		public IEnumerable<byte> Get(Transaction tx,DataHandle handle, bool shouldUseConcurrencyTag = false)
		{
			Validate(handle);
			ThrowIfDisposed();
			if (!ArePagesAllocated(handle))
				return null;

			var pageWriteOffsets = tx.CopyPagesAndFetchOffsets(handle);
			int actualDataSize;
			var data = GetInternal(handle,
								   pageNum => pageWriteOffsets[pageNum].PageHeaderOffset,
								   pageNum => pageWriteOffsets[pageNum].PageDataOffset,
								   shouldUseConcurrencyTag, out actualDataSize);

			return data.Take(actualDataSize);
		}

		public void Set(Transaction tx, DataHandle handle, byte[] data, bool shouldUseConcurrencyTag = false)
		{
			Validate(handle, data);
			ThrowIfDisposed();

			var pageWriteOffsets = tx.CopyPagesAndFetchOffsets(handle);
			var pageIndex = 0;
			
			fixed(byte* dataPtr = data)
				foreach (var pageNum in handle.Pages)
				{
					var pageHeader = Header(pageWriteOffsets[pageNum].PageHeaderOffset);
					var pageData = Data(pageWriteOffsets[pageNum].PageDataOffset);
					
					var srcDataOffset = pageIndex * m_StorageHeader->PageDataSize;
					var dataLengthDifference = data.Length - srcDataOffset;
					var dataSize = dataLengthDifference < m_StorageHeader->PageDataSize ? dataLengthDifference : m_StorageHeader->PageDataSize;
					pageHeader->DataSize = dataSize;

					NativeMethods.memcpy(pageData, dataPtr + srcDataOffset, PageDataSize);

					pageIndex++;
				}
		}

		public void Delete(Transaction tx, DataHandle handle, bool shouldUseConcurrencyTag = false)
		{
			Validate(handle);
			ThrowIfDisposed();

			throw new NotImplementedException();
		}

		public DataHandle Allocate(int requestedSize)
		{
			ThrowIfDisposed();
			using (var @lock = m_PageChangeSyncProvider.Lock())
			{
				var alreadyAllocatedSize = 0;
				var pageDataSize = m_StorageHeader->PageDataSize;
				var allocatedPageNum = new List<int>((requestedSize / m_StorageHeader->PageDataSize) + 1);
				do
				{
					var pageNum = FindAndMarkFreePageNum();
					if (pageNum == -1)
						throw new OutOfMemoryException("unable to allocate more pages");

					allocatedPageNum.Add(pageNum);
					alreadyAllocatedSize += pageDataSize;
				} while (alreadyAllocatedSize < requestedSize);

				m_StorageHeader->LastFreePageNum = allocatedPageNum.Max();
				m_StorageHeader->FreePageCount -= allocatedPageNum.Count;
				return new DataHandle(allocatedPageNum);
			}
		}

		public void Free(DataHandle handle)
		{
			Validate(handle);
			ThrowIfDisposed();
			using (var @lock = m_PageChangeSyncProvider.Lock())
			{
				if (handle.Pages.Count() > m_StorageHeader->TotalPageCount - m_StorageHeader->FreePageCount)
					throw new ApplicationException("Attempt to free more pages than total amount of pages. Something is very wrong..");

				foreach (var pageNum in handle.Pages)
					m_StorageHeader->FreePageFlags[pageNum] = true;

				handle.IsValid = false;
				m_StorageHeader->FreePageCount += handle.Pages.Count();
				m_StorageHeader->LastFreePageNum = handle.Pages.Min();
			}
		}

		#endregion

		#region Transaction Related Methods

		private Tuple<int,int> CopyPageRangeAndFetchOffsets(int startPageNum, int endPageNum)
		{
			var sourceSet = PageSetByAbsoluteOffset(m_StorageHeader->PageOffsets[startPageNum]);
			if (sourceSet == PageSet.NotASet) throw new InvalidDataException("Invalid page set for page number = " + pageNum);

			var destinationSet = sourceSet == PageSet.SetA ? PageSet.SetB : PageSet.SetA;

			throw new NotImplementedException();
		}

		private PageOffsets CopyPageAndFetchOffset(int pageNum)
		{
			var sourceSet = PageSetByAbsoluteOffset(m_StorageHeader->PageOffsets[pageNum]);
			if (sourceSet == PageSet.NotASet) throw new InvalidDataException("Invalid page set for page number = " + pageNum);

			var destinationSet = sourceSet == PageSet.SetA ? PageSet.SetB : PageSet.SetA;
			var destPageHeaderOffset = BaseOffsetByPageSet(destinationSet) + PageHeaderRelativeOffset(pageNum);
			var destPageDataOffset = BaseOffsetByPageSet(destinationSet) + PageDataRelativeOffset(pageNum);

			//copy page header 
			NativeMethods.memcpy(m_BaseStoragePointer + m_StorageHeader->PageHeaderOffsets[pageNum],
								 m_BaseStoragePointer + destPageHeaderOffset, Constants.PageHeaderSize);

			//copy page data
			NativeMethods.memcpy(m_BaseStoragePointer + m_StorageHeader->PageOffsets[pageNum],
								 m_BaseStoragePointer + destPageDataOffset, m_StorageHeader->PageDataSize);

			return new PageOffsets(destPageHeaderOffset,destPageDataOffset);
		}

		#endregion

		#region Helper Methods

		private byte[] GetInternal(DataHandle handle, Func<int, int> headerOffsetRetriever, Func<int, int> dataOffsetRetriever, bool shouldUseConcurrencyTag, out int actualDataSize)
		{
			var data = new byte[handle.Pages.Count() * m_StorageHeader->PageDataSize];
			actualDataSize = 0;
			fixed (byte* dataPtr = data)
				foreach (var pageNum in handle.Pages)
				{
					var pageHeader = Header(headerOffsetRetriever(pageNum));
	
					//check the tag only once, assumption that all allocated pages have their tag changed together (when Set() method executes)
					if (shouldUseConcurrencyTag && new UidTag(pageHeader->TagBytes) != handle.Tag)
					{
						throw new ConcurrencyException("cannot fetch data, it has been changed - concurrency tags mismatch");
					}
	
					var pageData = Data(dataOffsetRetriever(pageNum));
	
					NativeMethods.memcpy(dataPtr + actualDataSize, pageData, pageHeader->DataSize);
	
					actualDataSize += pageHeader->DataSize;
				}

			return data;
		}

		private PageHeader* Header(int headerOffset)
		{
			return (PageHeader*)(m_BaseStoragePointer + headerOffset);
		}

		private byte* Data(int dataOffset)
		{
			return m_BaseStoragePointer + dataOffset;
		}

		private bool ArePagesAllocated(DataHandle handle)
		{
			foreach (var pageNum in handle.Pages)
				if (m_StorageHeader->FreePageFlags[pageNum])
					return false;

			return true;
		}

		private void Validate(DataHandle handle, byte[] data = null)
		{
			if (!handle.IsValid)
				throw new InvalidDataException("data handle is marked invalid (pages already freed?)");

			if (data != null && (handle.Pages.Count() * m_StorageHeader->PageDataSize) > data.Length)
				throw new ArgumentOutOfRangeException("size of data must be equal or less to total page size in the data handle");
		}

		private void ThrowIfDisposed()
		{
			if (m_IsDisposed)
				throw new ApplicationException("Pager is already disposed.");
		}

		//TODO : optimize this to make complexity less than O(n)
		private int FindAndMarkFreePageNum()
		{
			for (int pageNum = m_StorageHeader->LastFreePageNum; pageNum < m_StorageHeader->TotalPageCount; pageNum++)
				if (m_StorageHeader->FreePageFlags[pageNum])
				{
					m_StorageHeader->LastFreePageNum = pageNum;
					m_StorageHeader->FreePageFlags[pageNum] = false;
					return pageNum;
				}

			m_StorageHeader->LastFreePageNum = m_StorageHeader->TotalPageCount - 1;
			return -1;
		}

		private PageSet PageSetByAbsoluteOffset(int offset)
		{
			if (offset < BaseOffsetByPageSet(PageSet.SetA))
				return PageSet.NotASet;

			if (offset < BaseOffsetByPageSet(PageSet.SetB))
				return PageSet.SetA;
			else
				return PageSet.SetB;
		}

		private int BaseOffsetByPageSet(PageSet set)
		{
			if (set == PageSet.SetA)
				return m_BaseOffsetOfSetA; //free page offset table + page offset table					   
			else if (set == PageSet.SetB)
				return m_BaseOffsetOfSetB;
			else
				throw new ApplicationException("undefined page set");
		}

		private int PageHeaderRelativeOffset(int pageNum)
		{
			return Constants.PageHeaderSize * pageNum;
		}

		private int PageDataRelativeOffset(int pageNum)
		{
			return (Constants.PageHeaderSize * m_StorageHeader->TotalPageCount) + (m_StorageHeader->PageDataSize * pageNum);
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
