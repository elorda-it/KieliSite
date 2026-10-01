using System;
using Dapper;

namespace DBHelper;

public class QarTableNameResolver : SimpleCRUD.TableNameResolver
{
	public override string ResolveTableName(Type type)
	{
		return type.Name.ToLower();
	}
}
