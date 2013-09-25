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
		private Action m_TransactionRelease;
		private Func<int, PageOffsets> HandleParticipatingPages;

		internal Transaction(Action transactionRelease, Func<int,PageOffsets> handleParticipatingPages)
		{
			m_ParticipatingPages = new ConcurrentDictionary<int,PageOffsets>();
			m_TransactionRelease = transactionRelease;
			HandleParticipatingPages = handleParticipatingPages;
		}

		internal void CopyParticipatingPages(DataHandle handle)
		{
			foreach (var pageNum in handle.Pages)
				m_ParticipatingPages.GetOrAdd(pageNum,HandleParticipatingPages(pageNum));
		}
		
		public void Dispose()
		{
			if(m_TransactionRelease != null)
				m_TransactionRelease();
		}
	}
}
