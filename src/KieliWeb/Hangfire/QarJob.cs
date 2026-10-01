using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using COMMON;
using DBHelper;
using Dapper;
using MODEL;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace KieliWeb.Hangfire;

public class QarJob
{
	private readonly IMemoryCache _memoryCache;

	private readonly IWebHostEnvironment _environment;

	public QarJob(IMemoryCache memoryCache, IWebHostEnvironment environment)
	{
		_memoryCache = memoryCache;
		_environment = environment;
	}

	/// <summary>Every hour: the new items of «Жаңалық көздері» become news cards (Setup/NewsCollector.cs).</summary>
	public Task JobCollectNews() => KieliWeb.Setup.NewsCollector.RunAsync(_memoryCache);

	public void JobDeleteOldLogFiles()
	{
		string key = MethodBase.GetCurrentMethod()?.Name;
		if (QarSingleton.GetInstance().GetRunStatus(key))
		{
			return;
		}
		QarSingleton.GetInstance().SetRunStatus(key, status: true);
		try
		{
			string logDirectoryPath = _environment.ContentRootPath + (_environment.ContentRootPath.EndsWith("/") ? "" : "/") + "logs";
			DirectoryInfo directory = new DirectoryInfo(logDirectoryPath);
			if (!directory.Exists)
			{
				return;
			}
			FileInfo[] txtFiles = directory.GetFiles("*.txt");
			FileInfo[] array = txtFiles;
			foreach (FileInfo file in array)
			{
				if ((DateTime.Now - file.CreationTime).Days > 7)
				{
					file.Delete();
				}
			}
		}
		catch (Exception exception)
		{
			Log.Error(exception, "JobDeleteOldLogFiles");
		}
		finally
		{
			QarSingleton.GetInstance().SetRunStatus(key, status: false);
		}
	}

	public static void JobSaveReloginAdminIds()
	{
		string key = MethodBase.GetCurrentMethod()?.Name;
		if (QarSingleton.GetInstance().GetRunStatus(key))
		{
			return;
		}
		QarSingleton.GetInstance().SetRunStatus(key, status: true);
		try
		{
			using IDbConnection connection = Utilities.GetOpenConnection();
			List<Admin> reloginAdminList = connection.GetList<Admin>("where reLogin = 1").ToList();
			foreach (Admin reloginAdmin in reloginAdminList)
			{
				QarSingleton.GetInstance().AddReLoginAdmin(reloginAdmin.Id, reloginAdmin.UpdateTime);
			}
		}
		catch (Exception exception)
		{
			if (key != null)
			{
				Log.Error(exception, key);
			}
		}
		finally
		{
			QarSingleton.GetInstance().SetRunStatus(key, status: false);
		}
	}
}
