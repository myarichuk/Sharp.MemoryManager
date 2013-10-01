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
			throw new NotImplementedException();
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
