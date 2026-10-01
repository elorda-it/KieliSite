using System;
using System.IO;
using SkiaSharp;

namespace COMMON;

public static class ImgHandler
{
	public static string CutImage(string webRoot, string fileName, MemoryStream avatarFile, int pointX = 0, int pointY = 0, int width = 0, int height = 0, int rotate = 0, int smallWidth = 100, int smallHeight = 100)
	{
		SKBitmap croppedBitmap = null;
		SKBitmap thumbImg = null;
		SKBitmap finalImgBig = null;
		SKBitmap finalImgMiddle = null;
		SKBitmap finalImgSmall = null;
		try
		{
			thumbImg = SKBitmap.Decode(avatarFile.ToArray());
			thumbImg = Rotate(thumbImg, rotate);
			SKRectI sourceRect = new SKRectI(pointX, pointY, pointX + width, pointY + height);
			if (sourceRect.Left < 0)
			{
				sourceRect.Left = 0;
			}
			if (sourceRect.Top < 0)
			{
				sourceRect.Top = 0;
			}
			if (sourceRect.Right > thumbImg.Width)
			{
				sourceRect.Right = thumbImg.Width;
			}
			if (sourceRect.Bottom > thumbImg.Height)
			{
				sourceRect.Bottom = thumbImg.Height;
			}
			int cropWidth = sourceRect.Width;
			int cropHeight = sourceRect.Height;
			if (cropWidth <= 0 || cropHeight <= 0)
			{
				return string.Empty;
			}
			croppedBitmap = new SKBitmap(cropWidth, cropHeight);
			using (SKCanvas canvas = new SKCanvas(croppedBitmap))
			{
				canvas.DrawBitmap(dest: new SKRect(0f, 0f, cropWidth, cropHeight), bitmap: thumbImg, source: sourceRect);
			}
			int bigTargetMaxW = smallWidth * 6;
			int bigTargetMaxH = smallHeight * 6;
			int middleTargetMaxW = smallWidth * 2;
			int middleTargetMaxH = smallHeight * 2;
			finalImgBig = ResizeIfLarger(croppedBitmap, bigTargetMaxW, bigTargetMaxH);
			finalImgMiddle = ResizeIfLarger(croppedBitmap, middleTargetMaxW, middleTargetMaxH);
			finalImgSmall = ResizeIfLarger(croppedBitmap, smallWidth, smallHeight);
			if (!Directory.Exists(webRoot))
			{
				Directory.CreateDirectory(webRoot);
			}
			string[] fArr = fileName.Split('.');
			string finalPathBig = Path.Combine(webRoot, fArr[0] + "_big.webp");
			string finalPathMiddle = Path.Combine(webRoot, fArr[0] + "_middle.webp");
			string finalPathSmall = Path.Combine(webRoot, fArr[0] + "_small.webp");
			SKEncodedImageFormat saveFormat = SKEncodedImageFormat.Webp;
			SaveBitmapAs(finalImgBig, finalPathBig, saveFormat);
			SaveBitmapAs(finalImgMiddle, finalPathMiddle, saveFormat);
			SaveBitmapAs(finalImgSmall, finalPathSmall, saveFormat);
			return finalPathBig;
		}
		catch (Exception)
		{
			return string.Empty;
		}
		finally
		{
			croppedBitmap?.Dispose();
			thumbImg?.Dispose();
			finalImgBig?.Dispose();
			finalImgMiddle?.Dispose();
			finalImgSmall?.Dispose();
		}
	}

	private static SKEncodedImageFormat GetSaveFormat(string filePath)
	{
		string fileFormat = Path.GetExtension(filePath).ToLower();
		SKEncodedImageFormat saveFormat = SKEncodedImageFormat.Png;
		switch (fileFormat)
		{
		case ".png":
			saveFormat = SKEncodedImageFormat.Png;
			break;
		case ".jpg":
		case ".jpeg":
			saveFormat = SKEncodedImageFormat.Jpeg;
			break;
		case ".gif":
			saveFormat = SKEncodedImageFormat.Gif;
			break;
		case ".webp":
			saveFormat = SKEncodedImageFormat.Webp;
			break;
		case ".avif":
			saveFormat = SKEncodedImageFormat.Avif;
			break;
		}
		return saveFormat;
	}

	public static void CompressImage(MemoryStream imageStream, string fullPathName, int compressLevel)
	{
		SKEncodedImageFormat saveFormat = GetSaveFormat(fullPathName);
		using SKImage image = SKImage.FromBitmap(SKBitmap.Decode(imageStream.ToArray()));
		using SKData data = image.Encode(saveFormat, compressLevel);
		using FileStream stream = File.OpenWrite(fullPathName);
		data.SaveTo(stream);
	}

	private static SKBitmap Rotate(SKBitmap bitmap, double angle)
	{
		double num = Math.PI * angle / 180.0;
		float sine = (float)Math.Abs(Math.Sin(num));
		float num2 = (float)Math.Abs(Math.Cos(num));
		int originalWidth = bitmap.Width;
		int originalHeight = bitmap.Height;
		int rotatedWidth = (int)(num2 * (float)originalWidth + sine * (float)originalHeight);
		int rotatedHeight = (int)(num2 * (float)originalHeight + sine * (float)originalWidth);
		SKBitmap rotatedBitmap = new SKBitmap(rotatedWidth, rotatedHeight);
		using SKCanvas surface = new SKCanvas(rotatedBitmap);
		surface.Translate(rotatedWidth / 2, rotatedHeight / 2);
		surface.RotateDegrees((float)angle);
		surface.Translate(-originalWidth / 2, -originalHeight / 2);
		surface.DrawBitmap(bitmap, default(SKPoint));
		return rotatedBitmap;
	}

	public static void ConvertImageColorsToWhite(MemoryStream imageFile, string outputImagePath)
	{
		using SKBitmap originalBitmap = SKBitmap.Decode(imageFile.ToArray());
		using SKBitmap resultBitmap = new SKBitmap(originalBitmap.Width, originalBitmap.Height);
		SKColor whiteOpaque = new SKColor(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
		SKColor whiteTransparent = new SKColor(byte.MaxValue, byte.MaxValue, byte.MaxValue, 0);
		for (int y = 0; y < originalBitmap.Height; y++)
		{
			for (int x = 0; x < originalBitmap.Width; x++)
			{
				SKColor pixel = originalBitmap.GetPixel(x, y);
				if (pixel == whiteOpaque)
				{
					resultBitmap.SetPixel(x, y, whiteTransparent);
				}
				else
				{
					resultBitmap.SetPixel(x, y, new SKColor(byte.MaxValue, byte.MaxValue, byte.MaxValue, pixel.Alpha));
				}
			}
		}
		using SKImage image = SKImage.FromBitmap(resultBitmap);
		using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
		using FileStream stream = File.OpenWrite(outputImagePath);
		data.SaveTo(stream);
	}

	public static (int x, int y, int width, int height) GetCropParameters(string imagePath)
	{
		using SKBitmap bitmap = SKBitmap.Decode(imagePath);
		float targetAspectRatio = 1.7777778f;
		int srcWidth = bitmap.Width;
		int srcHeight = bitmap.Height;
		int cropHeight;
		int cropWidth;
		if ((float)srcWidth / (float)srcHeight > targetAspectRatio)
		{
			cropHeight = srcHeight;
			cropWidth = (int)((float)srcHeight * targetAspectRatio);
		}
		else
		{
			cropWidth = srcWidth;
			cropHeight = (int)((float)srcWidth / targetAspectRatio);
		}
		int item = (srcWidth - cropWidth) / 2;
		int y = (srcHeight - cropHeight) / 2;
		return (x: item, y: y, width: cropWidth, height: cropHeight);
	}

	public static void DeleteImage(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return;
		}
		string size = string.Empty;
		string fileName = Path.GetFileNameWithoutExtension(path);
		if (fileName.EndsWith("_big"))
		{
			size = "_big.";
		}
		else if (fileName.EndsWith("_middle"))
		{
			size = "_middle.";
		}
		else if (fileName.EndsWith("_small"))
		{
			size = "_small.";
		}
		if (File.Exists(path))
		{
			File.Delete(path);
		}
		if (!string.IsNullOrEmpty(size))
		{
			if (File.Exists(path.Replace(size, "_big.")))
			{
				File.Delete(path.Replace(size, "_big."));
			}
			if (File.Exists(path.Replace(size, "_middle.")))
			{
				File.Delete(path.Replace(size, "_middle."));
			}
			if (File.Exists(path.Replace(size, "_small.")))
			{
				File.Delete(path.Replace(size, "_small."));
			}
		}
	}

	public static byte[] GenerateRatioImage(float width, float height, string picturePath, string savePath)
	{
		int imageWidth = (int)width;
		int imageHeight = (int)height;
		using SKSurface surface = SKSurface.Create(new SKImageInfo(imageWidth, imageHeight));
		SKCanvas canvas = surface.Canvas;
		canvas.Clear(new SKColor(238, 238, 238));
		using SKBitmap providedImage = SKBitmap.Decode(picturePath);
		float targetWidth = (float)imageWidth * 0.6f;
		float targetHeight = (float)imageHeight * 0.6f;
		float val = targetWidth / (float)(providedImage?.Width ?? 1);
		float heightRatio = (targetHeight / (float?)providedImage?.Height) ?? 1f;
		float minRatio = Math.Min(val, heightRatio);
		int newWidth;
		int newHeight;
		if ((float)providedImage.Width > targetWidth || (float)providedImage.Height > targetHeight)
		{
			newWidth = (int)((float)providedImage.Width * minRatio);
			newHeight = (int)((float)providedImage.Height * minRatio);
		}
		else
		{
			newWidth = providedImage.Width;
			newHeight = providedImage.Height;
		}
		using SKBitmap resizedImage = providedImage.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.High);
		int x = (imageWidth - newWidth) / 2;
		int y = (imageHeight - newHeight) / 2;
		canvas.DrawBitmap(resizedImage, x, y);
		canvas.Flush();
		using SKImage image = surface.Snapshot();
		using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
		using (FileStream fileStream = File.OpenWrite(savePath))
		{
			data.SaveTo(fileStream);
		}
		using MemoryStream stream = new MemoryStream();
		data.SaveTo(stream);
		stream.Seek(0L, SeekOrigin.Begin);
		return stream.ToArray();
	}

	public static void ConvertImageColorsToUlyWhite(MemoryStream image_file, string outputImagePath)
	{
		using SKBitmap originalBitmap = SKBitmap.Decode(image_file.ToArray());
		using SKBitmap resultBitmap = new SKBitmap(originalBitmap.Width, originalBitmap.Height);
		for (int y = 0; y < originalBitmap.Height; y++)
		{
			for (int x = 0; x < originalBitmap.Width; x++)
			{
				SKColor pixel = originalBitmap.GetPixel(x, y);
				resultBitmap.SetPixel(x, y, pixel);
			}
		}
		using SKImage image = SKImage.FromBitmap(resultBitmap);
		using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
		using FileStream stream = File.OpenWrite(outputImagePath);
		data.SaveTo(stream);
	}

	private static void SaveBitmapAs(SKBitmap bmp, string path, SKEncodedImageFormat format)
	{
		using SKImage image = SKImage.FromBitmap(bmp);
		using SKData data = image.Encode(format, 100);
		using FileStream stream = File.OpenWrite(path);
		data.SaveTo(stream);
	}

	private static SKBitmap ResizeIfLarger(SKBitmap source, int maxW, int maxH)
	{
		int srcW = source.Width;
		int srcH = source.Height;
		if (srcW <= maxW && srcH <= maxH)
		{
			return source.Copy();
		}
		float val = (float)maxW / (float)srcW;
		float ratioH = (float)maxH / (float)srcH;
		float ratio = Math.Min(val, ratioH);
		int newW = (int)((float)srcW * ratio);
		int newH = (int)((float)srcH * ratio);
		return source.Resize(new SKImageInfo(newW, newH), SKFilterQuality.High);
	}
}
