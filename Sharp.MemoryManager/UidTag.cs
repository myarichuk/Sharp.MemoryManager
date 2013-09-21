using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp.MemoryManager
{
	//unique id tag --> provides unique "timestamp"
	public class UidTag : IEquatable<UidTag>,IComparable<UidTag>,IComparable
	{
		private const string Delimiter = "-";
		private string m_UidTag;
		private long m_Ticks;
		private int m_AtomicId;

		internal UidTag(byte[] tagBytes)
		{
			m_UidTag = Encoding.Unicode.GetString(tagBytes);
			var elements = m_UidTag.Split(Delimiter.First()).ToArray();
			if (elements.Length != 2 || 
				!long.TryParse(elements[0], out m_Ticks) ||
				!int.TryParse(elements[1],out m_AtomicId))
					throw new ArgumentException("invalid tag was parsed from bytes");
		}

		internal UidTag(long ticks, int atomicId)
		{
			m_Ticks = ticks;
			m_AtomicId = atomicId;
			m_UidTag = ticks + Delimiter + atomicId;
		}

		#region Object Overrides & Interface implementations

		public static implicit operator string(UidTag tag)
		{
			return tag.m_UidTag;
		}

		public static implicit operator byte[](UidTag tag)
		{
			return Encoding.Unicode.GetBytes(tag.m_UidTag);
		}

		public static bool operator >(UidTag one, UidTag another)
		{
			return one.CompareTo(another) > 0;
		}

		public static bool operator <(UidTag one, UidTag another)
		{
			return one.CompareTo(another) < 0;
		}

		public static bool operator ==(UidTag one, UidTag another)
		{
			return one.Equals(another);
		}

		public static bool operator !=(UidTag one, UidTag another)
		{
			return !one.Equals(another);
		}

		public override string ToString()
		{
			return m_UidTag;
		}

		public override int GetHashCode()
		{
			return m_UidTag.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			
			var otherTag = obj as UidTag;
			if (otherTag == null) return false;

			return Equals(otherTag);
		}

		public bool Equals(UidTag other)
		{
			return m_Ticks == other.m_Ticks && m_AtomicId == other.m_AtomicId;
		}

		public int CompareTo(UidTag other)
		{
			if (m_Ticks > other.m_Ticks)
				return 1;
			else if (m_Ticks < other.m_Ticks)
				return -1;
			else
			{
				if (m_AtomicId > other.m_AtomicId)
					return 1;
				else if (m_AtomicId == other.m_AtomicId)
					return 0;
				else
					return -1;
			}
		}

		public int CompareTo(object obj)
		{
			if (ReferenceEquals(null, obj)) return 1; //null values always come before non-null
			if (ReferenceEquals(this, obj)) return 0;

			var otherTag = obj as UidTag;
			if (otherTag == null)
			{
				throw new ArgumentException("obj must be of type UidTag");
			}

			return CompareTo(otherTag);
		}

		#endregion
	}
}
