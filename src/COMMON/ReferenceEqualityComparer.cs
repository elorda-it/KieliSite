using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace COMMON;

public sealed class ReferenceEqualityComparer : IEqualityComparer<object>
{
	public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

	public new bool Equals(object x, object y)
	{
		return x == y;
	}

	public int GetHashCode(object obj)
	{
		return RuntimeHelpers.GetHashCode(obj);
	}
}
