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
            string[] chars = pattern.Split(' ');
            this._pattern = chars.Select((x) =>
            {
                if (x == "??" || x == "?")
                {
                    return (byte)0;
                }
                return byte.Parse(x, NumberStyles.HexNumber);
            }).ToArray();
            this._wildcard = chars.Select((x) =>
            {
                return x == "??" || x == "?";
            }).ToArray();
            this._offset = offset;
        }

        public int Find(byte[] data)
        {
            int[] skipTable = new int[256];
            int lastIndex = this._pattern.Length - 1;

            int safeSkip = Math.Max(lastIndex - Array.LastIndexOf(this._wildcard, true), 1);
            for (int i = 0; i < skipTable.Length; i++)
            {
                skipTable[i] = safeSkip;
            }

            for (int i = lastIndex - safeSkip; i < lastIndex; i++)
            {
                skipTable[this._pattern[i]] = lastIndex - i;
            }

            for (int i = 0; i <= data.Length - this._pattern.Length; )
            {
                for (int j = lastIndex; this._wildcard[j] || data[i + j] == this._pattern[j]; --j)
                {
                    if (j == 0)
                    {
                        return i + this._offset;
                    }
                }
                i += Math.Max(skipTable[data[i + lastIndex] & 0xFF], 1);
            }

            return -1;
        }
    }
}
