using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json.Converters;
using System;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace NantoNBai
{
    public class Formatter
    {
        public string Format(double from, double to, Nan format)
        {
            switch (format)
            {
                case Nan.Bai:
                    return $"{(from == 0 ? double.PositiveInfinity : Math.Floor(to / from))}倍";
                case Nan.Pasento:
                    return $"{(Pasento(from, to))}%";
                case Nan.Bunno:
                    return Bunsu(from, to);
                default:
                    return Format(from, to, Nan.Bai);
            }
        }

        private static string Pasento(double from, double to)
        {
            if (from == 0)
            {
                return "∞";
            }
            var n = to / from;
            if (n >= 0.01d)
            {
                return $"{Math.Floor(to / from * 100)}";
            }
            var pasento = (n * 100).ToString("0." + new string('0', 306));
            return Regex.Replace(pasento, @"^(0.0*[1-9])(.*)$", "$1");
        }

        public string Bunsu(double from, double to)
        {
            if (from == 0)
            {
                return "∞";
            }
            var waru = Gcd((long)from, (long)to);
            return $"{Math.Floor(to / waru)}/{Math.Floor(from / waru)}";
        }

        public static long Gcd(long a, long b)
        {
            if (a < b)
            {
                return Gcd(b, a);
            }
            while (b != 0)
            {
                var remainder = a % b;
                a = b;
                b = remainder;
            }
            return a;
        }
    }

    [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
    public enum Nan
    {
        [EnumMember(Value = "bai")]
        Bai,
        [EnumMember(Value = "pasento")]
        Pasento,
        [EnumMember(Value = "bunno")]
        Bunno
    }
}
