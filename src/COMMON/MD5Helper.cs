using System.Security.Cryptography;
using System.Text;

namespace COMMON;

public class MD5Helper
{
	public static string CreateHashMD5(string s)
	{
		byte[] data = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(s));
		string md5 = "";
		for (int i = 0; i < data.Length; i++)
		{
			md5 += data[i].ToString("x2").ToUpperInvariant();
		}
		return md5;
	}

	public static string PasswordMd5Encrypt(string password)
	{
		password = CreateHashMD5(password);
		password = CreateHashMD5(password + "QarSolutions2024");
		return password;
	}
}
