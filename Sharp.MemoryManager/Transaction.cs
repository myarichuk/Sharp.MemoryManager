using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Sharp.MemoryManager
{
	public class Transaction : IDisposable
	{
		//page info : page number -> page offsets
		private ConcurrentDictionary<int, PageOffsets> m_ParticipatingPages;
		private Action TransactionRelease;
		private Func<int, PageOffsets> HandleParticipatingPages;

		internal Transaction(Action transactionRelease, Func<int,PageOffsets> handleParticipatingPages)
		{
			m_ParticipatingPages = new ConcurrentDictionary<int,PageOffsets>();
			TransactionRelease = transactionRelease;
			HandleParticipatingPages = handleParticipatingPages;
		}

		//if pages are already copied - do nothing
		internal IDictionary<int,PageOffsets> CopyPagesAndFetchOffsets(DataHandle handle)
		{
			var handlePageOffsets = new Dictionary<int, PageOffsets>();
			if (!m_ParticipatingPages.Any(pageNumWithOffsetsPair => handle.Pages.Contains(pageNumWithOffsetsPair.Key)))
				foreach (var pageNum in handle.Pages)
					handlePageOffsets.Add(pageNum, m_ParticipatingPages.GetOrAdd(pageNum, HandleParticipatingPages(pageNum)));
			else
				handlePageOffsets = m_ParticipatingPages.Where(pageNumWithOffsetsPair => handle.Pages.Contains(pageNumWithOffsetsPair.Key))
														.ToDictionary(pageNumWithOffsetsPair => pageNumWithOffsetsPair.Key,
																	  pageNumWithOffsetsPair => pageNumWithOffsetsPair.Value);

			return handlePageOffsets;
		}		

		public void Commit()
		{			
			throw new NotImplementedException();
			TransactionRelease();
		}

		public void Dispose()
		{
			if(TransactionRelease != null)
				TransactionRelease();
		}
	}
}
