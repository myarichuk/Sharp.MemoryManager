using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp.MemoryManager
{
	internal class DisposableAction : IDisposable
	{
		private Action m_OnDisposeDelegate;

		public DisposableAction(Action onDisposeDelegate)
		{
			Contract.Requires(onDisposeDelegate != null,"onDisposeDelegate is null");
			m_OnDisposeDelegate = onDisposeDelegate;
		}

		public void Dispose()
		{
			m_OnDisposeDelegate();
		}
	}
}
