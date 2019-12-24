using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace LoLCustomSharp
{
    public class SigScanner
    {
        private readonly byte[] _pattern;
        private readonly bool[] _wildcard;
        private readonly int _offset;

        public SigScanner(string pattern, int offset = 0)
        {
            var chars = pattern.Split(' ');
            _pattern = chars.Select((x) =>
            {
                if (x == "??" || x == "?")
                {
                    return (byte)0;
                }
                return byte.Parse(x, NumberStyles.HexNumber);
            }).ToArray();
            _wildcard = chars.Select((x) =>
            {
                return x == "??" || x == "?";
            }).ToArray();
            _offset = offset;
        }

        public int Find(byte[] data)
        {
            var skipTable = new int[256];
            var lastIndex = _pattern.Length - 1;

            var safeSkip = Math.Max(lastIndex - Array.LastIndexOf(_wildcard, true), 1);
            for (var i = 0; i < skipTable.Length; i++)
            {
                skipTable[i] = safeSkip;
            }

            for (var i = lastIndex - safeSkip; i < lastIndex; i++)
            {
                skipTable[_pattern[i]] = lastIndex - i;
            }

            for (var i = 0; i <= data.Length - _pattern.Length; )
            {
                for (var j = lastIndex; _wildcard[j] || data[i + j] == _pattern[j]; --j)
                {
                    if (j == 0)
                    {
                        return i + _offset;
                    }
                }
                i += Math.Max(skipTable[data[i + lastIndex] & 0xFF], 1);
            }

            return -1;
        }
    }
}
