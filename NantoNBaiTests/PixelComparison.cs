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
        /// <summary>1 チャンネルあたりの許容差。</summary>
        /// <remarks>
        /// グリフのアウトラインは環境に依存しないが、塗りのアンチエイリアスは
        /// ラスタライザのビルドでごく僅かに変わりうる。境界の 1 段差は許容する。
        /// </remarks>
        private const int ChannelTolerance = 24;

        /// <summary>許容する差分ピクセルの割合。透かしや書式の崩れはこれを大きく超える。</summary>
        private const double PixelTolerance = 0.002d;

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
            var worstChannelDifference = 0;
            var firstX = -1;
            var firstY = -1;

            for (var y = 0; y < expectedBitmap.Height; y++)
            {
                for (var x = 0; x < expectedBitmap.Width; x++)
                {
                    var expectedPixel = expectedBitmap.GetPixel(x, y);
                    var actualPixel = actualBitmap.GetPixel(x, y);

                    var channelDifference = Math.Max(
                        Math.Max(
                            Math.Abs(expectedPixel.Red - actualPixel.Red),
                            Math.Abs(expectedPixel.Green - actualPixel.Green)),
                        Math.Abs(expectedPixel.Blue - actualPixel.Blue));

                    if (channelDifference <= ChannelTolerance)
                    {
                        continue;
                    }

                    if (differences == 0)
                    {
                        (firstX, firstY) = (x, y);
                    }

                    differences++;
                    worstChannelDifference = Math.Max(worstChannelDifference, channelDifference);
                }
            }

            var total = expectedBitmap.Width * expectedBitmap.Height;
            var ratio = (double)differences / total;

            Assert.IsTrue(
                ratio <= PixelTolerance,
                $"描画結果が期待画像と違う。差分 {differences} / {total} ピクセル ({ratio:P3}、"
                + $"許容 {PixelTolerance:P1})、最大の色差 {worstChannelDifference}。"
                + $"最初の差分は ({firstX}, {firstY})。{actualPath} と {expectedPath} を見比べる");
        }
    }
}
