using System;

namespace COMMON;

public class RandomHelper
{
	private static readonly Random getrandom = new Random();

	private static readonly object syncLock = new object();

	public static string GetNumberRandom(int length)
	{
		string randomCode = string.Empty;
		Random random = new Random();
		for (int i = 0; i < length; i++)
		{
			int number = random.Next();
			randomCode += number % 10;
		}
		return randomCode;
	}

	public static int GetRandomNumber(int min, int max)
	{
		lock (syncLock)
		{
			return getrandom.Next(min, max);
		}
	}
}
