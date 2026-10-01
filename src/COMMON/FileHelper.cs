using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace COMMON;

public class FileHelper
{
	public static void EnsureDir(string path, bool isDir = false)
	{
		string dir = (isDir ? path : Path.GetDirectoryName(path));
		if (!Directory.Exists(dir))
		{
			Directory.CreateDirectory(dir);
		}
	}

	public static void Delete(string path)
	{
		if (File.Exists(path))
		{
			File.Delete(path);
		}
	}

	public static async Task Download(string filPath, string url)
	{
		using HttpClient client = new HttpClient();
		using Stream stream = await client.GetStreamAsync(new Uri(url));
		using FileStream fs = new FileStream(filPath, FileMode.CreateNew);
		await stream.CopyToAsync(fs);
	}

	public static async Task DownloadFile(string savePath, string fileUrl)
	{
		using HttpClient client = new HttpClient();
		HttpResponseMessage obj = await client.GetAsync(fileUrl);
		obj.EnsureSuccessStatusCode();
		File.WriteAllBytes(savePath, await obj.Content.ReadAsByteArrayAsync());
	}

	public static void AppendLine(string filePath, string content)
	{
		File.AppendAllText(filePath, content + Environment.NewLine);
	}

	public static List<string> GetAllFilePath(string pDirectoryPath)
	{
		List<string> res = new List<string>();
		string[] files = Directory.GetFiles(pDirectoryPath);
		foreach (string filePath in files)
		{
			res.Add(filePath);
		}
		files = Directory.GetDirectories(pDirectoryPath);
		foreach (string directoryPath in files)
		{
			res.AddRange(GetAllFilePath(directoryPath));
		}
		return res;
	}

	public static void ConvertMp4ToMp3(string sourcePath, string destinationPath, out string output, out string error)
	{
		Process ffmpegProcess = new Process();
		ffmpegProcess.StartInfo.FileName = "ffmpeg";
		ffmpegProcess.StartInfo.Arguments = $"-i \"{sourcePath}\" -vn -acodec libmp3lame -q:a 2 \"{destinationPath}\"";
		ffmpegProcess.StartInfo.RedirectStandardOutput = true;
		ffmpegProcess.StartInfo.RedirectStandardError = true;
		ffmpegProcess.StartInfo.UseShellExecute = false;
		ffmpegProcess.StartInfo.CreateNoWindow = true;
		ffmpegProcess.Start();
		output = ffmpegProcess.StandardOutput.ReadToEnd();
		error = ffmpegProcess.StandardError.ReadToEnd();
		ffmpegProcess.WaitForExit();
		if (!ffmpegProcess.HasExited)
		{
			ffmpegProcess.Kill();
		}
	}
}
