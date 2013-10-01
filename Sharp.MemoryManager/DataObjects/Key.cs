using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sharp.MemoryManager
{
	public class Key : IEquatable<Key>, IComparable<Key>
	{
		private readonly byte[] m_KeyBytes;

		public Key(byte[] keyBytes)
		{
			m_KeyBytes = keyBytes;
		}

		public Key(string key)
		{
			m_KeyBytes = Encoding.Unicode.GetBytes(key);
		}

		public byte[] Bytes
		{
			get
			{
				return m_KeyBytes;
			}
		}

		#region Conversion Operators
		public static implicit operator Key(string key)
		{
			return new Key(key);
		}

		public static implicit operator string(Key key)
		{
			return Encoding.Unicode.GetString(key.m_KeyBytes);
		}
		#endregion

		#region Equality Comparers
		public bool Equals(Key other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return m_KeyBytes.Equals(other.m_KeyBytes);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((Key)obj);
		}

		public override int GetHashCode()
		{
			return m_KeyBytes.GetHashCode();
		}

		public static bool operator ==(Key left, Key right)
		{
			return Equals(left, right);
		}

		public static bool operator !=(Key left, Key right)
		{
			return !Equals(left, right);
		}
		#endregion
	}
}
