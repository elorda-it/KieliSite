using System.Collections.Concurrent;

namespace COMMON;

public class QarSingleton
{
	private static QarSingleton _instance;

	private static readonly object Lock = new object();

	private readonly ConcurrentDictionary<string, bool> _checkRunDictionary = new ConcurrentDictionary<string, bool>();

	private readonly ConcurrentDictionary<int, int> _reLoginAdminDictionary = new ConcurrentDictionary<int, int>();

	private readonly ConcurrentDictionary<string, string> _stringDictionary = new ConcurrentDictionary<string, string>();

	private QarSingleton()
	{
	}

	public static QarSingleton GetInstance()
	{
		if (_instance != null)
		{
			return _instance;
		}
		lock (Lock)
		{
			if (_instance == null)
			{
				_instance = new QarSingleton();
			}
		}
		return _instance;
	}

	public string GetConnectionString()
	{
		if (_stringDictionary.TryGetValue("connectionString", out var value))
		{
			return value ?? string.Empty;
		}
		return GetMasterConnectionString();
	}

	public void SetConnectionString(string connectionString)
	{
		_stringDictionary.AddOrUpdate("connectionString", connectionString, (string _, string _) => connectionString);
	}

	public string GetMasterConnectionString()
	{
		if (_stringDictionary.TryGetValue("masterConnection", out var value))
		{
			return value ?? string.Empty;
		}
		return GetConnectionString();
	}

	public void SetMasterConnectionString(string connectionString)
	{
		_stringDictionary.AddOrUpdate("masterConnection", connectionString, (string _, string _) => connectionString);
	}

	public string GetReplicaConnectionString()
	{
		if (_stringDictionary.TryGetValue("replicaConnection", out var value))
		{
			return value ?? string.Empty;
		}
		return GetMasterConnectionString();
	}

	public void SetReplicaConnectionString(string connectionString)
	{
		_stringDictionary.AddOrUpdate("replicaConnection", connectionString, (string _, string _) => connectionString);
	}

	public string GetSiteTheme()
	{
		if (!_stringDictionary.TryGetValue("siteTheme", out var value))
		{
			return string.Empty;
		}
		return value ?? string.Empty;
	}

	public void SetSiteTheme(string siteTheme)
	{
		_stringDictionary.AddOrUpdate("siteTheme", siteTheme, (string _, string _) => siteTheme);
	}

	public string GetSiteUrl()
	{
		if (!_stringDictionary.TryGetValue("siteUrl", out var value))
		{
			return string.Empty;
		}
		return value ?? string.Empty;
	}

	public void SetSiteUrl(string siteUrl)
	{
		_stringDictionary.AddOrUpdate("siteUrl", siteUrl, (string _, string _) => siteUrl);
	}

	public void AddReLoginAdmin(int adminId, int updateTime)
	{
		if (_reLoginAdminDictionary.ContainsKey(adminId))
		{
			_reLoginAdminDictionary[adminId] = updateTime;
			return;
		}
		_reLoginAdminDictionary.AddOrUpdate(adminId, updateTime, (int _, int _) => updateTime);
	}

	public bool IsReLoginAdmin(int adminId, out int updateTime)
	{
		if (_reLoginAdminDictionary.TryGetValue(adminId, out var value))
		{
			updateTime = value;
			return true;
		}
		updateTime = 0;
		return false;
	}

	public void RemoveReLoginAdmin(int adminId)
	{
		_reLoginAdminDictionary.TryRemove(adminId, out var _);
	}

	public bool GetRunStatus(string key)
	{
		bool status;
		return _checkRunDictionary.TryGetValue(key, out status) && status;
	}

	public void SetRunStatus(string key, bool status)
	{
		if (status)
		{
			_checkRunDictionary.AddOrUpdate(key, status, (string _, bool _) => status);
		}
		else
		{
			_checkRunDictionary.TryRemove(key, out var _);
		}
	}
}
