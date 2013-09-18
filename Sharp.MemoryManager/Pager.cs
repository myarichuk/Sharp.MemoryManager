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
		private const string MUTEX_SUFFIX = "_SYNC_MUTEX";
		private const int PAGE_DATA_SIZE = 8; //8 bytes in each page
		private const double FREE_SPACE_MARGIN_PERCENT = 1.1; //how much free space to reserve in hard drive
		#endregion

		#region Private Members

		private byte* m_BaseStoragePointer;
		private byte* m_BasePointerForPageSetA;
		private byte* m_BasePointerForPageSetB;

		private StorageHeader* m_StorageHeader;
		private MemoryMappedFile m_PagerStorage;
		private MemoryMappedViewAccessor m_PageStorageViewAccessor;
		private readonly SyncProvider m_SyncProvider;
		private bool m_IsDisposed;
		private readonly string m_StorageName;
		private long m_TotalPagerSize;

		#endregion

		#region Constructor(s)

		public Pager(string storageName, int dataCapacity)
		{
			m_StorageName = storageName;			

			m_SyncProvider = new SyncProvider(storageName + MUTEX_SUFFIX);
	
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
			using (var @lock = m_SyncProvider.Lock())
			{
				var pageCount = capacity / pageDataSize;
				m_TotalPagerSize = Constants.Signature.Length +
								   Constants.StorageHeaderSize +
								   (Constants.SizeOfInt * pageCount) + //free page offset table
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
					
					m_StorageHeader->FreePageOffsets = (int*)(m_BaseStoragePointer + 
															  Constants.Signature.Length + 
															  Constants.StorageHeaderSize);
					
					m_StorageHeader->PageOffsets = (int*)(m_BaseStoragePointer + 														   
														  Constants.Signature.Length + 
														  Constants.StorageHeaderSize +
														  (Constants.SizeOfInt * pageCount));
					
					m_StorageHeader->PageHeaderOffsets = (int*)(m_BaseStoragePointer +
																Constants.Signature.Length +
																Constants.StorageHeaderSize +
																(Constants.SizeOfInt * pageCount * 2));

					FirstTimeInitialize_PageOffsetTable();
				}

				m_BasePointerForPageSetA = m_BaseStoragePointer + OffsetByPageSet(PageSet.SetA);
				m_BasePointerForPageSetB = m_BaseStoragePointer + OffsetByPageSet(PageSet.SetB);
			}
		}

		
		private void FirstTimeInitialize_PageOffsetTable()
		{
			var pageSetOffset = OffsetByPageSet(PageSet.SetA);
			for (int pageNum = 0; pageNum < m_StorageHeader->TotalPageCount; pageNum++)
			{
				m_StorageHeader->PageHeaderOffsets[pageNum] = pageSetOffset + PageHeaderOffset(pageNum);
				m_StorageHeader->PageOffsets[pageNum] = pageSetOffset + PageDataOffset(pageNum);
				m_StorageHeader->FreePageOffsets[pageNum] = pageSetOffset + PageDataOffset(pageNum);
			}
		}

		#endregion

		#region Public Methods

		public Transaction NewTransaction()
		{
			throw new NotImplementedException();
		}

		public byte[] Get(DataHandle handle)
		{
			throw new NotImplementedException();
		}

		public void Set(Transaction tx, DataHandle handle, byte[] data, bool shouldUseConcurrencyTag = false)
		{
			//do not forget to check data size and allocated page size (that in handle)
			throw new NotImplementedException();
		}

		public DataHandle Allocate(int requestedSize)
		{
			using (var @lock = m_SyncProvider.Lock())
			{
				var alreadyAllocatedSize = 0;
				var pageDataSize = m_StorageHeader->PageDataSize;
				var allocatedPageNum = new List<int>((requestedSize / m_StorageHeader->PageDataSize) + 1);
				do
				{
					allocatedPageNum.Add(m_StorageHeader->FreePageOffsets[m_StorageHeader->FreePageCount--]);
					alreadyAllocatedSize += pageDataSize;
				} while (alreadyAllocatedSize < requestedSize);

				return new DataHandle(allocatedPageNum);
			}
		}

		public void Free(DataHandle handle)
		{
			using (var @lock = m_SyncProvider.Lock())
			{
				if (handle.PageNum.Count() > m_StorageHeader->TotalPageCount - m_StorageHeader->FreePageCount)
					throw new ApplicationException("Attempt to free more pages than total amount of pages. Something is very wrong..");

				foreach (var pageNum in handle.PageNum)
					m_StorageHeader->FreePageOffsets[m_StorageHeader->FreePageCount++] = m_StorageHeader->PageOffsets[pageNum];
			}
		}

		#endregion

		#region Helper Methods

		private PageSet PageSetByOffset(int offset)
		{
			if (offset < OffsetByPageSet(PageSet.SetA))
				return PageSet.NotASet;

			if (offset < OffsetByPageSet(PageSet.SetB))
				return PageSet.SetA;
			else
				return PageSet.SetB;
		}

		private int OffsetByPageSet(PageSet set)
		{
			if (set == PageSet.SetA)
			{
				return Constants.Signature.Length +
					   Constants.StorageHeaderSize +
					   (Constants.SizeOfInt * m_StorageHeader->TotalPageCount * 2); //free page offset table + page offset table					   
			}
			else if (set == PageSet.SetB)
			{
				return Constants.Signature.Length +
					   Constants.StorageHeaderSize +
					   (Constants.SizeOfInt * m_StorageHeader->TotalPageCount * 2) + //free page offset table and page offset table
					   (Constants.PageHeaderSize * m_StorageHeader->TotalPageCount) +
					   (m_StorageHeader->PageDataSize * m_StorageHeader->TotalPageCount);
			}
			else
			{
				throw new ApplicationException("undefined page set");
			}
		}

		private int PageHeaderOffset(int pageNum)
		{
			return Constants.PageHeaderSize * pageNum;
		}

		private int PageDataOffset(int pageNum)
		{
			return m_StorageHeader->PageDataSize * pageNum;
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
				if (m_PageStorageViewAccessor != null)
				{
					m_PageStorageViewAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
					m_PageStorageViewAccessor.Dispose();
				}

				if (m_PagerStorage != null)
				{
					m_PagerStorage.Dispose();
				}

				if (m_SyncProvider != null)
				{
					m_SyncProvider.Dispose();
				}
				
				m_IsDisposed = true;
			}
		}

		#endregion
	}
}
