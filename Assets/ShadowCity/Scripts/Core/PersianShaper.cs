// ============================================================================
// SHADOW CITY — Core/PersianShaper.cs
// Arabic/Persian contextual shaping + RTL reordering for UGUI Text.
// UGUI renders glyphs LTR without joining; this converts logical Persian
// strings into presentation-form glyphs in visual order.
// Ported/condensed from standard arabic-shaping tables; supports the full
// Persian alphabet incl. پ چ ژ گ ی ک, lam-alef ligatures and Persian digits.
// ============================================================================
using System.Collections.Generic;
using System.Text;

namespace ShadowCity
{
    public static class PersianShaper
    {
        // isolated, final, initial, medial
        static readonly Dictionary<char, char[]> Forms = new()
        {
            {'ا', new[]{'\uFE8D','\uFE8E','\uFE8D','\uFE8E'}},
            {'آ', new[]{'\uFE81','\uFE82','\uFE81','\uFE82'}},
            {'ب', new[]{'\uFE8F','\uFE90','\uFE91','\uFE92'}},
            {'پ', new[]{'\uFB56','\uFB57','\uFB58','\uFB59'}},
            {'ت', new[]{'\uFE95','\uFE96','\uFE97','\uFE98'}},
            {'ث', new[]{'\uFE99','\uFE9A','\uFE9B','\uFE9C'}},
            {'ج', new[]{'\uFE9D','\uFE9E','\uFE9F','\uFEA0'}},
            {'چ', new[]{'\uFB7A','\uFB7B','\uFB7C','\uFB7D'}},
            {'ح', new[]{'\uFEA1','\uFEA2','\uFEA3','\uFEA4'}},
            {'خ', new[]{'\uFEA5','\uFEA6','\uFEA7','\uFEA8'}},
            {'د', new[]{'\uFEA9','\uFEAA','\uFEA9','\uFEAA'}},
            {'ذ', new[]{'\uFEAB','\uFEAC','\uFEAB','\uFEAC'}},
            {'ر', new[]{'\uFEAD','\uFEAE','\uFEAD','\uFEAE'}},
            {'ز', new[]{'\uFEAF','\uFEB0','\uFEAF','\uFEB0'}},
            {'ژ', new[]{'\uFB8A','\uFB8B','\uFB8A','\uFB8B'}},
            {'س', new[]{'\uFEB1','\uFEB2','\uFEB3','\uFEB4'}},
            {'ش', new[]{'\uFEB5','\uFEB6','\uFEB7','\uFEB8'}},
            {'ص', new[]{'\uFEB9','\uFEBA','\uFEBB','\uFEBC'}},
            {'ض', new[]{'\uFEBD','\uFEBE','\uFEBF','\uFEC0'}},
            {'ط', new[]{'\uFEC1','\uFEC2','\uFEC3','\uFEC4'}},
            {'ظ', new[]{'\uFEC5','\uFEC6','\uFEC7','\uFEC8'}},
            {'ع', new[]{'\uFEC9','\uFECA','\uFECB','\uFECC'}},
            {'غ', new[]{'\uFECD','\uFECE','\uFECF','\uFED0'}},
            {'ف', new[]{'\uFED1','\uFED2','\uFED3','\uFED4'}},
            {'ق', new[]{'\uFED5','\uFED6','\uFED7','\uFED8'}},
            {'ک', new[]{'\uFB8E','\uFB8F','\uFB90','\uFB91'}},
            {'ك', new[]{'\uFB8E','\uFB8F','\uFB90','\uFB91'}},
            {'گ', new[]{'\uFB92','\uFB93','\uFB94','\uFB95'}},
            {'ل', new[]{'\uFEDD','\uFEDE','\uFEDF','\uFEE0'}},
            {'م', new[]{'\uFEE1','\uFEE2','\uFEE3','\uFEE4'}},
            {'ن', new[]{'\uFEE5','\uFEE6','\uFEE7','\uFEE8'}},
            {'و', new[]{'\uFEED','\uFEEE','\uFEED','\uFEEE'}},
            {'ه', new[]{'\uFEE9','\uFEEA','\uFEEB','\uFEEC'}},
            {'ی', new[]{'\uFBFC','\uFBFD','\uFBFE','\uFBFF'}},
            {'ي', new[]{'\uFBFC','\uFBFD','\uFBFE','\uFBFF'}},
            {'ئ', new[]{'\uFE89','\uFE8A','\uFE8B','\uFE8C'}},
            {'ء', new[]{'\uFE80','\uFE80','\uFE80','\uFE80'}},
            {'ة', new[]{'\uFE93','\uFE94','\uFE93','\uFE94'}},
            {'أ', new[]{'\uFE83','\uFE84','\uFE83','\uFE84'}},
            {'إ', new[]{'\uFE87','\uFE88','\uFE87','\uFE88'}},
            {'ؤ', new[]{'\uFE85','\uFE86','\uFE85','\uFE86'}},
        };

        // Letters that never join to the following letter
        static readonly HashSet<char> RightOnly = new()
            { 'ا','آ','د','ذ','ر','ز','ژ','و','أ','إ','ؤ','ء','ة' };

        static bool IsArabic(char c) =>
            (c >= 0x0600 && c <= 0x06FF) || (c >= 0xFB50 && c <= 0xFEFF);

        static bool Joins(char c) => Forms.ContainsKey(c) && !RightOnly.Contains(c);

        /// <summary>Shape + reorder a logical string for UGUI display.</summary>
        public static string Shape(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            bool hasArabic = false;
            foreach (var c in input) if (IsArabic(c)) { hasArabic = true; break; }
            if (!hasArabic) return input;

            // 1) Contextual joining
            var chars = input.ToCharArray();
            var shaped = new char[chars.Length];
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!Forms.TryGetValue(c, out var f)) { shaped[i] = c; continue; }
                bool prevJoins = i > 0 && Joins(chars[i - 1]);
                bool nextIs = i < chars.Length - 1 && Forms.ContainsKey(chars[i + 1]);
                int form = prevJoins ? (nextIs && !RightOnly.Contains(c) ? 3 : 1)
                                     : (nextIs && !RightOnly.Contains(c) ? 2 : 0);
                shaped[i] = f[form];
            }

            // 1b) Lam-alef ligatures
            var lig = new StringBuilder(shaped.Length);
            for (int i = 0; i < shaped.Length; i++)
            {
                if (i < shaped.Length - 1 &&
                    (shaped[i] == '\uFEDF' || shaped[i] == '\uFEE0'))
                {
                    char nxt = shaped[i + 1];
                    char l = '\0';
                    if (nxt == '\uFE8D' || nxt == '\uFE8E')
                        l = shaped[i] == '\uFEDF' ? '\uFEFB' : '\uFEFC';
                    else if (nxt == '\uFE81' || nxt == '\uFE82')
                        l = shaped[i] == '\uFEDF' ? '\uFEF5' : '\uFEF6';
                    if (l != '\0') { lig.Append(l); i++; continue; }
                }
                lig.Append(shaped[i]);
            }

            // 2) Simple bidi: split into runs, reverse RTL text, keep LTR
            //    (digits/latin) runs intact, then emit runs in reverse order.
            var runs = new List<(string text, bool rtl)>();
            var cur = new StringBuilder();
            bool curRtl = true;
            foreach (var c in lig.ToString())
            {
                bool rtl = IsArabic(c) || c == ' ' && curRtl;
                bool neutral = " .,:;!؟?()-+×٪،٬».«"[..0].Length == 0 && "،؛.:!؟ ".IndexOf(c) >= 0;
                if (char.IsDigit(c) || (c >= 'A' && c <= 'z')) rtl = false;
                else if (neutral) rtl = curRtl;
                else rtl = IsArabic(c) || rtl;

                if (cur.Length > 0 && rtl != curRtl)
                { runs.Add((cur.ToString(), curRtl)); cur.Clear(); }
                cur.Append(c); curRtl = rtl;
            }
            if (cur.Length > 0) runs.Add((cur.ToString(), curRtl));

            var sb = new StringBuilder(lig.Length);
            for (int r = runs.Count - 1; r >= 0; r--)
            {
                if (runs[r].rtl)
                {
                    var arr = runs[r].text.ToCharArray();
                    System.Array.Reverse(arr);
                    sb.Append(arr);
                }
                else sb.Append(runs[r].text);
            }
            return sb.ToString();
        }

        static readonly char[] FaDigits = "۰۱۲۳۴۵۶۷۸۹".ToCharArray();
        public static string ToPersianDigits(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
                sb.Append(c >= '0' && c <= '9' ? FaDigits[c - '0'] : c);
            return sb.ToString();
        }
    }
}
