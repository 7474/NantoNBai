using DocumentFormat.OpenXml.Packaging;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using A = DocumentFormat.OpenXml.Drawing;

namespace NantoNBai.Rendering
{
    /// <summary>
    /// テーマの配色と線スタイルを解決する。schemeClr の色変換 (lumMod / lumOff / shade / tint) を含む。
    /// </summary>
    public sealed class SlideTheme
    {
        private readonly Dictionary<string, SKColor> _scheme = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _colorMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<A.Outline> _lineStyles = new();

        public SlideTheme(SlideMasterPart? masterPart)
        {
            var theme = masterPart?.ThemePart?.Theme;
            var scheme = theme?.ThemeElements?.ColorScheme;
            if (scheme is not null)
            {
                foreach (var color in scheme.ChildElements.OfType<A.Color2Type>())
                {
                    var value = ReadColor2Type(color);
                    if (value is not null)
                    {
                        _scheme[color.LocalName] = value.Value;
                    }
                }
            }

            var map = masterPart?.SlideMaster.ColorMap;
            if (map is not null)
            {
                _colorMap["bg1"] = map.Background1?.InnerText ?? "lt1";
                _colorMap["tx1"] = map.Text1?.InnerText ?? "dk1";
                _colorMap["bg2"] = map.Background2?.InnerText ?? "lt2";
                _colorMap["tx2"] = map.Text2?.InnerText ?? "dk2";
            }

            var lineStyles = theme?.ThemeElements?.FormatScheme?.LineStyleList;
            if (lineStyles is not null)
            {
                _lineStyles.AddRange(lineStyles.Elements<A.Outline>());
            }
        }

        /// <summary>スライドの背景色。テンプレートは白 (bg1) のみを使う。</summary>
        public SKColor Background => Lookup("bg1") ?? SKColors.White;

        public SKColor? ResolveFill(A.SolidFill? fill)
        {
            if (fill is null)
            {
                return null;
            }

            if (fill.GetFirstChild<A.RgbColorModelHex>() is { Val.Value: { } hex })
            {
                return ParseHex(hex);
            }

            var scheme = fill.GetFirstChild<A.SchemeColor>();
            if (scheme is not null)
            {
                return ApplyTransforms(Lookup(scheme.Val?.InnerText), scheme);
            }

            return null;
        }

        /// <summary>a:lnRef / a:fillRef のようなスタイル参照が指す色。</summary>
        public SKColor? ResolveStyleColor(A.StyleMatrixReferenceType reference)
        {
            var scheme = reference.GetFirstChild<A.SchemeColor>();
            if (scheme is not null)
            {
                return ApplyTransforms(Lookup(scheme.Val?.InnerText), scheme);
            }

            if (reference.GetFirstChild<A.RgbColorModelHex>() is { Val.Value: { } hex })
            {
                return ParseHex(hex);
            }

            return null;
        }

        /// <summary>テーマの線スタイル (a:lnStyleLst) の太さ。EMU。</summary>
        public int ResolveLineWidth(uint index)
        {
            // lnRef の idx は 1 始まり
            var position = (int)index - 1;
            if (position < 0 || position >= _lineStyles.Count)
            {
                return 9525;
            }

            return _lineStyles[position].Width?.Value ?? 9525;
        }

        private SKColor? Lookup(string? name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (_colorMap.TryGetValue(name, out var mapped))
            {
                name = mapped;
            }

            return _scheme.TryGetValue(name, out var color) ? color : null;
        }

        private static SKColor? ReadColor2Type(A.Color2Type color)
        {
            if (color.GetFirstChild<A.RgbColorModelHex>() is { Val.Value: { } hex })
            {
                return ParseHex(hex);
            }

            if (color.GetFirstChild<A.SystemColor>() is { } system)
            {
                var last = system.LastColor?.Value;
                if (last is not null)
                {
                    return ParseHex(last);
                }

                return system.Val?.Value == A.SystemColorValues.Window ? SKColors.White : SKColors.Black;
            }

            return null;
        }

        private static SKColor? ApplyTransforms(SKColor? color, A.SchemeColor scheme)
        {
            if (color is null)
            {
                return null;
            }

            var value = color.Value;

            foreach (var child in scheme.ChildElements)
            {
                switch (child)
                {
                    case A.LuminanceModulation modulation:
                        value = ScaleLuminance(value, Percent(modulation.Val?.Value), 0f);
                        break;
                    case A.LuminanceOffset offset:
                        value = ScaleLuminance(value, 1f, Percent(offset.Val?.Value));
                        break;
                    case A.Shade shade:
                        value = Multiply(value, Percent(shade.Val?.Value));
                        break;
                    case A.Tint tint:
                        value = Lighten(value, Percent(tint.Val?.Value));
                        break;
                    case A.Alpha:
                        // テンプレートは透明度を使っていない
                        break;
                }
            }

            return value;
        }

        private static float Percent(int? value) => (value ?? 100000) / 100000f;

        private static SKColor ScaleLuminance(SKColor color, float modulation, float offset)
        {
            color.ToHsl(out var hue, out var saturation, out var lightness);
            var scaled = Math.Clamp(lightness / 100f * modulation + offset, 0f, 1f);
            return SKColor.FromHsl(hue, saturation, scaled * 100f);
        }

        // DrawingML の shade / tint は線形色空間で行うのが正しい。
        // sRGB のまま掛けると PowerPoint の出す色とずれるため、変換して掛け戻す。
        private static SKColor Multiply(SKColor color, float factor) => FromLinear(
            ToLinear(color.Red) * factor,
            ToLinear(color.Green) * factor,
            ToLinear(color.Blue) * factor);

        private static SKColor Lighten(SKColor color, float factor) => FromLinear(
            ToLinear(color.Red) * factor + (1f - factor),
            ToLinear(color.Green) * factor + (1f - factor),
            ToLinear(color.Blue) * factor + (1f - factor));

        private static float ToLinear(byte channel)
        {
            var value = channel / 255f;
            return value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
        }

        private static SKColor FromLinear(float red, float green, float blue) => new(
            ToSrgb(red), ToSrgb(green), ToSrgb(blue));

        private static byte ToSrgb(float value)
        {
            value = Math.Clamp(value, 0f, 1f);
            var srgb = value <= 0.0031308f ? value * 12.92f : 1.055f * MathF.Pow(value, 1f / 2.4f) - 0.055f;
            return (byte)Math.Clamp(MathF.Round(srgb * 255f), 0f, 255f);
        }

        private static SKColor ParseHex(string hex) => new(
            byte.Parse(hex.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }
}
