using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using MODEL.FormatModels;
using Serilog;

namespace COMMON;

public static class LanguagePackHelper
{
	private const string BaseAddress = "https://www.sozdikqor.org";

	public static string GetLanguagePackJsonString()
	{
		string result = string.Empty;
		string localFilePath = Path.Combine(AppContext.BaseDirectory, "language_pack.txt");
		try
		{
			using HttpClient client = new HttpClient();
			client.BaseAddress = new Uri("https://www.sozdikqor.org");
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/packs");
			HttpResponseMessage result2 = client.SendAsync(request).Result;
			result2.EnsureSuccessStatusCode();
			if (!IsValidResult(result2.Content.ReadAsStringAsync().Result, out var ajaxResult) || !Directory.Exists(AppContext.BaseDirectory))
			{
				throw new Exception("Not valid result from api");
			}
			result = ajaxResult.Data.ToString();
			if (File.Exists(localFilePath))
			{
				File.Delete(localFilePath);
			}
			File.WriteAllText(localFilePath, result);
		}
		catch (Exception exception)
		{
			if (File.Exists(localFilePath))
			{
				result = File.ReadAllText(localFilePath);
			}
			else
			{
				Log.Error(exception, "COMMON:GetLanguagePackJsonString");
			}
		}
		return result;
	}

	private static bool IsValidResult(string result, out AjaxMsgModel ajaxResult)
	{
		ajaxResult = JsonHelper.DeserializeObject<AjaxMsgModel>(result);
		AjaxMsgModel ajaxMsgModel = ajaxResult;
		if (ajaxMsgModel != null)
		{
			return ajaxMsgModel.Status == "success";
		}
		return false;
	}
}
