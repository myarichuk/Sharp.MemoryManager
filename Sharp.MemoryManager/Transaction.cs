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
		//page info : page number -> page offset
		private ConcurrentDictionary<int,int> m_ParticipatingPages;
		private Action m_TransactionRelease;

		internal Transaction(Action transactionRelease)
		{
			m_ParticipatingPages = new ConcurrentDictionary<int,int>();
			m_TransactionRelease = transactionRelease;
		}

		internal void CopyParticipatingPages(DataHandle handle)
		{

		}
		
		public void Dispose()
		{
			if(m_TransactionRelease != null)
				m_TransactionRelease();
		}
	}
}
