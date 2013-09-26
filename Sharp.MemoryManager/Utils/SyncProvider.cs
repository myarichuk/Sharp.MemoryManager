using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sharp.MemoryManager
{
	public class SyncProvider : IDisposable
	{
		private Mutex m_ProviderMutex;
		private bool m_IsDisposed;		
		private string m_ProviderName;

		public SyncProvider(string providerName)
		{
			Contract.Requires(!String.IsNullOrWhiteSpace(providerName));
			
			m_ProviderName = providerName;
			m_ProviderMutex = new Mutex(false, providerName);
			m_IsDisposed = false;
		}

		public IDisposable Lock(int lockTimeout = Constants.DefaultLockTimeout)
		{
			if (lockTimeout <= 0)
				throw new ArgumentOutOfRangeException("lockTimeout should be larger than 0");

			if (lockTimeout != Constants.NoLockTimeout && !m_ProviderMutex.WaitOne(lockTimeout))
			{
				throw new TimeoutException("Timed out while tryign to acquire exclusive lock, for mutex (name = " + m_ProviderName + ")");
			}

			return new DisposableAction(() => m_ProviderMutex.ReleaseMutex());
		}

		#region IDisposable Implementation

		public void Dispose()
		{
			DisposeThis();
			GC.SuppressFinalize(this);
		}

		private void DisposeThis()
		{
			if (m_ProviderMutex != null && !m_IsDisposed)
			{
				m_ProviderMutex.Dispose();
				m_IsDisposed = true;
			}
			
		}

		~SyncProvider()
		{
			DisposeThis();
		}

		#endregion
	}
}
