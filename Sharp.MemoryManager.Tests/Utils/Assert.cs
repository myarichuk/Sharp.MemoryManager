using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnitTesting = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sharp.MemoryManager
{
	public static class Assert
	{
		public static void Throws(Action action)
		{
			Throws<Exception>(action);
		}

		public static void NotThrows(Action action)
		{
			NotThrows<Exception>(action);
		}

		public static void Throws<T>(Action action) where T : Exception
		{
			bool exceptionWasThrown = false;
			Type exceptionType = null;
			try
			{
				action();
			}
			catch(Exception e)
			{
				exceptionType = e.GetType();
				exceptionWasThrown = true;
			}

			if (exceptionWasThrown && exceptionType != null)
			{
				if (!exceptionType.IsAssignableFrom(typeof(T)))
				{
					var errorMsg = String.Format("Expected exception of type {0} to throw, but catched exception of type {1}", typeof(T), exceptionType);
					UnitTesting.Assert.Fail(errorMsg);
				}
			}
			else
			{
				var errorMsg = String.Format("Expected to throw exception of type {0}, but no exception was thrown", typeof(T));
				UnitTesting.Assert.Fail(errorMsg);				
			}
		}

		public static void NotThrows<T>(Action action) where T : Exception
		{
			bool exceptionWasThrown = false;
			Type exceptionType = null;
			try
			{
				action();
			}
			catch (Exception e)
			{
				exceptionType = e.GetType();
				exceptionWasThrown = true;
			}

			if (exceptionWasThrown)
			{
				var errorMsg = String.Format("Expected not to throw exception of type {0}, but exception was thrown", typeof(T));
				UnitTesting.Assert.Fail(errorMsg);

			}
		}

	}
}
