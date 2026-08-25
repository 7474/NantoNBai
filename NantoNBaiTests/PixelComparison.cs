using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp;
using System;
using System.IO;

namespace NantoNBai.Tests
{
    /// <summary>
    /// 画像を1ピクセルずつ比べる。描画に使っているのと同じ SkiaSharp (MIT) だけで済ませている。
    /// </summary>
    internal static class PixelComparison
    {
        public static void AssertSame(string expectedPath, byte[] actual, string actualPath)
        {
            File.WriteAllBytes(actualPath, actual);

            using var expectedBitmap = SKBitmap.Decode(expectedPath)
                ?? throw new InvalidOperationException($"期待画像を読み込めない: {expectedPath}");
            using var actualBitmap = SKBitmap.Decode(actual)
                ?? throw new InvalidOperationException("生成した画像を読み込めない");

            Assert.AreEqual(
                (expectedBitmap.Width, expectedBitmap.Height),
                (actualBitmap.Width, actualBitmap.Height),
                $"画像の大きさが違う ({actualPath} を確認)");

            var differences = 0;
            var firstX = -1;
            var firstY = -1;

            for (var y = 0; y < expectedBitmap.Height; y++)
            {
                for (var x = 0; x < expectedBitmap.Width; x++)
                {
                    if (expectedBitmap.GetPixel(x, y) == actualBitmap.GetPixel(x, y))
                    {
                        continue;
                    }

                    if (differences == 0)
                    {
                        (firstX, firstY) = (x, y);
                    }

                    differences++;
                }
            }

            Assert.AreEqual(
                0,
                differences,
                $"描画結果が期待画像と違う。最初の差分は ({firstX}, {firstY})。{actualPath} と {expectedPath} を見比べる");
        }
    }
}
