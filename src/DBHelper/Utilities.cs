using System.Data;
using COMMON;
using MySqlConnector;

namespace DBHelper;

public static class Utilities
{
	/// <summary>
	/// The framework's admin code opens a transaction and then runs commands without passing it
	/// (fine for MySql.Data, refused by MySqlConnector). IgnoreCommandTransaction keeps that working:
	/// MySQL has one transaction per connection, so those commands still run inside it.
	/// </summary>
	private static string Prepare(string connectionString)
	{
		MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder(connectionString) { IgnoreCommandTransaction = true };
		return builder.ConnectionString;
	}

	public static IDbConnection GetMasterConnection()
	{
		string connectionString = QarSingleton.GetInstance().GetMasterConnectionString();
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			connectionString = QarSingleton.GetInstance().GetConnectionString();
		}
		MySqlConnection mySqlConnection = new MySqlConnection(Prepare(connectionString));
		((IDbConnection)mySqlConnection).Open();
		return mySqlConnection;
	}

	public static IDbConnection GetReplicaConnection()
	{
		string connectionString = QarSingleton.GetInstance().GetReplicaConnectionString();
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			connectionString = QarSingleton.GetInstance().GetMasterConnectionString();
		}
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			connectionString = QarSingleton.GetInstance().GetConnectionString();
		}
		MySqlConnection mySqlConnection = new MySqlConnection(Prepare(connectionString));
		((IDbConnection)mySqlConnection).Open();
		return mySqlConnection;
	}

	public static IDbConnection GetOpenConnection()
	{
		return GetMasterConnection();
	}
}
