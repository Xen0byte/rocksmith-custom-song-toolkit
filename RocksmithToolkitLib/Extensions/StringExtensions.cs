﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RocksmithToolkitLib.DLCPackage;


/*
Non-Sortable Artist, Title, Album Notes:
  Diacritics, Alpha, Numeric are allowed in any case combination
  Most special characters and puncuations are allowed with few exception
  
Sortable Artist, Title, Album Notes:
  ( ) are always stripped,
  / is replaced with a space,
  - usage is inconsistent (so removed it)
  , is stripped (in titles),
  ' is not stripped,
  . and ? usage are inconsistent (so leave these)
  Abbreviations/symbols like 'Mr.' and '&' are replaced with words
  Diacritics are replaced with their ASCII approximations if available

DLC Key, Tone Key Notes:
  Limited to a maximum length of 30
  ASCII Alpha and Numeric **ONLY** 
  No spaces, no special characters, no puncuation
  All alpha lower, upper, or mixed case are allowed
  All numeric is allowed
 */

// "return value;" is used throughout to aid with debugging

namespace RocksmithToolkitLib.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Capitalize the first character without changing the rest of the string
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string Capitalize(this string value)
        {
            if (String.IsNullOrEmpty(value))
                return String.Empty;

            value = string.Format("{0}{1}", value.Substring(0, 1).ToUpper(), value.Substring(1));
            return value;
        }

        public static string GetValidAcronym(this string value)
        {
            var v = Regex.Split(value, @"[\W\s]+").Where(r => !string.IsNullOrEmpty(r)).ToArray();
            if (v.Length > 1)
                return string.Join(string.Empty, v.Select(s => s[0])).ToUpper();

            value = value.ReplaceDiacritics();
            value = value.StripNonAlpaNumeric();
            return value;
        }

        public static string GetValidAppIdSixDigits(this string value)
        {
            // valid six digit AppID that begins with 2 , e.g. 248750
            Regex rgx = new Regex("^[2]\\d{5}$");  // "^[0-9]{6}$");
            value = rgx.Replace(value, "");
            return value;
        }

        /// <summary>
        /// Gets a valid Artist, Title, Album (ATA) name with spaces
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string GetValidAtaSpaceName(this string value)
        {
            // ODLC artist, title, album character use allows these
            // allow use of accents Über ñice \\p{L} diacritics
            // allow use of unicode punctuation \\p{P\\{S} not currently implimented
            // may need to be escaped \t\n\f\r#$()*+.?[\^{|  ... '-' needs to be escaped if not at the beginning or end of regex sequence
            // allow use of only these special characters \\-_ /&.:',!?()\"#
            // allow use of alphanumerics a-zA-Z0-9
            // tested and working ... Üuber!@#$%^&*()_+=-09{}][":';<>.,?/ñice

            Regex rgx = new Regex("[^a-zA-Z0-9\\-_/&',!.?()\"#\\p{L} ]*");
            value = rgx.Replace(value, "");
            // value = value.StripLeadingSpecialCharacters(); // some ODLC have

            return value;
        }

        public static string GetValidFileName(this string value)
        {
            value = String.Concat(value.Split(Path.GetInvalidFileNameChars()));

            return value;
        }

        public static string GetValidFilePath(this string value)
        {
            var fileName = Path.GetFileName(value);
            var pathName = Path.GetDirectoryName(value);
            fileName = fileName.GetValidFileName();
            pathName = pathName.GetValidPathName();
            value = Path.Combine(pathName, fileName);

            return value;
        }

        public static string GetValidInlayName(this string value, bool frets24 = false)
        {
            // remove all special characters, and leading numbers and replace spaces with underscore
            Regex rgx = new Regex("[^a-zA-Z0-9]_ ");
            value = rgx.Replace(value, "");
            value = value.StripLeadingNumbers();
            value = value.StripLeadingSpecialCharacters();

            // make sure (24) fret appears in the proper placement
            if (frets24)
            {
                if (value.Contains("24"))
                {
                    value = value.Replace("_24_", "_");
                    value = value.Replace("_24", "");
                    value = value.Replace("24_", "");
                    value = value.Replace(" 24 ", " ");
                    value = value.Replace("24 ", " ");
                    value = value.Replace(" 24", " ");
                    value = value.Replace("24", "");
                }
                value = value.Trim() + " 24";
            }

            value = value.ReplaceSpaceWith("_");

            return value;
        }

        /// <summary>
        /// Format string as valid DLCKey (aka SongKey), or ToneKey
        /// </summary>
        /// <param name="value">DLCKey or ToneKey for verification</param>
        /// <param name="songTitle">optional SongTitle varification comparison for DLCKey </param>
        /// <returns>contains no spaces, no accents, or special characters but can begin with or be all numbers or all lower case</returns>
        public static string GetValidKey(this string value, string songTitle = "")
        {
            string keyName = String.Empty;
            if (!String.IsNullOrEmpty(value))
            {
                keyName = value.StripNonAlpaNumeric();

                // check if they are the same, if so add 'Song' to end
                if (keyName == songTitle.Replace(" ", ""))
                    keyName = keyName + "Song";
            }

            // limit dlcKey length to 30
            keyName = keyName.Substring(0, Math.Min(30, keyName.Length));
            return keyName;
        }

        public static string GetValidPathName(this string value)
        {
            value = String.Concat(value.Split(Path.GetInvalidPathChars()));
            return value;
        }

        /// <summary>
        /// Standard short file name format for CDLC file names "{0}_{1}_{2}"
        /// </summary>
        /// <param name="stringFormat"></param>
        /// <param name="artist"></param>
        /// <param name="title"></param>
        /// <param name="version"></param>
        /// <param name="acronym">use artist acronym instead of full artist name</param>
        /// <returns></returns>
        public static string GetValidShortFileName(string artist, string title, string version, bool acronym)
        {
            string result;
            if (!acronym)
                result = String.Format("{0}_{1}_{2}", artist.GetValidAtaSpaceName(), title.GetValidAtaSpaceName(), version).Replace(" ", "-");
            else
                result = String.Format("{0}_{1}_{2}", artist.GetValidAcronym(), title.GetValidAtaSpaceName(), version).Replace(" ", "-");

            result = result.GetValidFileName().StripExcessWhiteSpace();
            return result;
        }

        public static string GetValidSortableName(this string value)
        {
            // processing order is important to achieve output like ODLC
            value = value.ReplaceAbbreviations();
            value = value.ReplaceDiacritics();
            value = value.StripSpecialCharacters();
            value = value.ShortWordMover(); // "The Beatles" becomes "Beatles, The"
            value = value.Capitalize(); // "blink-182" becomes "Blink 182"
            value = value.StripExcessWhiteSpace();

            return value;
        }

        public static string GetValidTempo(this string value)
        {
            float tempo = 0;
            float.TryParse(value.Trim(), out tempo);
            int bpm = (int)Math.Round(tempo);
            // check for valid tempo
            if (bpm > 0 && bpm < 300)
                return bpm.ToString();

            return "120"; // default tempo
        }

        public static string GetValidVersion(this string value)
        {
            if (!String.IsNullOrEmpty(value))
            {
                Regex rgx = new Regex(@"^[\d\.]*");
                var match = rgx.Match(value);
                if (match.Success)
                    return match.Value.Trim();
            }
            return "1";
        }

        public static string GetValidYear(this string value)
        {
            // check for valid four digit song year 
            if (!Regex.IsMatch(value, "^(19[0-9][0-9]|20[0-1][0-9])"))
                value = ""; // clear if not valid

            return value;
        }

        public static bool IsAppIdSixDigits(this string value)
        {
            // check for valid six digit AppID that begins with 2 , e.g. 248750
            return Regex.IsMatch(value, "^[2]\\d{5}$");  // "^[0-9]{6}$");
        }

        public static bool IsFilePathLengthValid(this string filePath)
        {
            if (Environment.OSVersion.Version.Major >= 6 && filePath.Length > 260)
                return false;

            if (Environment.OSVersion.Version.Major < 6 && filePath.Length > 215)
                return false;

            return true;
        }

        public static bool IsFilePathNameValid(this string filePath)
        {
            try
            {
                // check if filePath is null or empty
                if (String.IsNullOrEmpty(filePath))
                    return false;

                // check drive is valid
                var pathRoot = Path.GetPathRoot(filePath);
                if (!Directory.GetLogicalDrives().Contains(pathRoot))
                    return false;

                var fileName = Path.GetFileName(filePath);
                if (String.IsNullOrEmpty(fileName))
                    return false;

                var dirName = Path.GetDirectoryName(filePath);
                if (String.IsNullOrEmpty(dirName))
                    return false;

                if (dirName.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                    return false;

                if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    return false;
            }
            catch (ArgumentException)
            {
                return false;
            }

            return true;
        }

        public static bool IsFilePathValid(this string filePath)
        {
            if (filePath.IsFilePathLengthValid())
                if (filePath.IsFilePathNameValid())
                    return true;

            return false;
        }

        [Obsolete("Deprecated, please use appropriate StringExtension methods.", true)]
        public static string ObsoleteGetValidName(this string value, bool allowSpace = false, bool allowStartsWithNumber = false, bool underscoreSpace = false, bool frets24 = false)
        {
            // TODO: allow some additonal special charaters but not many

            // valid characters developed from actually reviewing ODLC artist, title, album names
            string name = String.Empty;

            if (!String.IsNullOrEmpty(value))
            {
                // ODLC artist, title, album character use allows these but not these
                // allow use of accents Über ñice \\p{L}
                // allow use of unicode punctuation \\p{P\\{S} not currently implimented
                // may need to be escaped \t\n\f\r#$()*+.?[\^{|  ... '-' needs to be escaped if not at the beginning or end of regex sequence
                // allow use of only these special characters \\-_ /&.:',!?()\"#
                // allow use of alphanumerics a-zA-Z0-9
                // tested and working ... Üuber!@#$%^&*()_+=-09{}][":';<>.,?/ñice

                Regex rgx = new Regex((allowSpace) ? "[^a-zA-Z0-9\\-_ /&.:',!?()\"#\\p{L}]" : "[^a-zA-Z0-9\\-_/&.:',!?()\"#\\p{L} ]");
                name = rgx.Replace(value, "");

                Regex rgx2 = new Regex(@"^[\d]*\s*");
                if (!allowStartsWithNumber)
                    name = rgx2.Replace(name, "");

                // prevent names from starting with special characters -_* etc
                Regex rgx3 = new Regex("^[^A-Za-z0-9]*");
                name = rgx3.Replace(name, "");

                if (frets24)
                {
                    if (name.Contains("24"))
                    {
                        name = name.Replace("_24_", "_");
                        name = name.Replace("_24", "");
                        name = name.Replace("24_", "");
                        name = name.Replace(" 24 ", " ");
                        name = name.Replace("24 ", " ");
                        name = name.Replace(" 24", " ");
                        name = name.Replace("24", "");
                    }
                    name = name.Trim() + " 24";
                }

                if (underscoreSpace)
                    name = name.Replace(" ", "_");
            }

            return name.Trim();
        }

        public static string ReplaceAbbreviations(this string value)
        {
            // this does a better job of replacing diacretics and special characters
            value = value.Replace(" & ", " and ");
            value = value.Replace("&", " and ");
            value = value.Replace("/", " ");
            value = value.Replace("-", " "); // inconsistent usage sometimes removed, sometimes replaced
            value = value.Replace(" + ", " plus ");
            value = value.Replace("+", " plus ");
            value = value.Replace(" @ ", " at ");
            value = value.Replace("@", " at ");
            value = value.Replace("Mr.", "Mister");
            value = value.Replace("Mrs.", "Misses");
            value = value.Replace("Ms.", "Miss");
            value = value.Replace("Jr.", "Junior");

            return value;
        }

        public static string ReplaceDiacritics(this string value)
        {
            value = Regex.Replace(value, "[ÀÁÂÃÅÄĀĂĄǍǺ]", "A");
            value = Regex.Replace(value, "[ǻǎàáâãäåąāă]", "a");
            value = Regex.Replace(value, "[ÇĆĈĊČ]", "C");
            value = Regex.Replace(value, "[çčćĉċ]", "c");
            value = Regex.Replace(value, "[ĎĐ]", "D");
            value = Regex.Replace(value, "[ďđ]", "d");
            value = Regex.Replace(value, "[ÈÉÊËĒĔĖĘĚ]", "E");
            value = Regex.Replace(value, "[ěèéêëēĕėę]", "e");
            value = Regex.Replace(value, "[ĜĞĠĢ]", "G");
            value = Regex.Replace(value, "[ģĝğġ]", "g");
            value = Regex.Replace(value, "[Ĥ]", "H");
            value = Regex.Replace(value, "[ĥ]", "h");
            value = Regex.Replace(value, "[ÌÍÎÏĨĪĬĮİǏ]", "I");
            value = Regex.Replace(value, "[ǐıįĭīĩìíîï]", "i");
            value = Regex.Replace(value, "[Ĵ]", "J");
            value = Regex.Replace(value, "[ĵ]", "j");
            value = Regex.Replace(value, "[Ķ]", "K");
            value = Regex.Replace(value, "[ķĸ]", "k");
            value = Regex.Replace(value, "[ĹĻĽĿŁ]", "L");
            value = Regex.Replace(value, "[ŀľļĺł]", "l");
            value = Regex.Replace(value, "[ÑŃŅŇŊ]", "N");
            value = Regex.Replace(value, "[ñńņňŉŋ]", "n");
            value = Regex.Replace(value, "[ÒÓÔÖÕŌŎŐƠǑǾ]", "O");
            value = Regex.Replace(value, "[ǿǒơòóôõöøōŏő]", "o");
            value = Regex.Replace(value, "[ŔŖŘ]", "R");
            value = Regex.Replace(value, "[ŗŕř]", "r");
            value = Regex.Replace(value, "[ŚŜŞŠ]", "S");
            value = Regex.Replace(value, "[şŝśš]", "s");
            value = Regex.Replace(value, "[ŢŤ]", "T");
            value = Regex.Replace(value, "[ťţ]", "t");
            value = Regex.Replace(value, "[ÙÚÛÜŨŪŬŮŰŲƯǓǕǗǙǛ]", "U");
            value = Regex.Replace(value, "[ǜǚǘǖǔưũùúûūŭůűų]", "u");
            value = Regex.Replace(value, "[Ŵ]", "W");
            value = Regex.Replace(value, "[ŵ]", "w");
            value = Regex.Replace(value, "[ÝŶŸ]", "Y");
            value = Regex.Replace(value, "[ýÿŷ]", "y");
            value = Regex.Replace(value, "[ŹŻŽ]", "Z");
            value = Regex.Replace(value, "[žźż]", "z");
            value = Regex.Replace(value, "[œ]", "oe");
            value = Regex.Replace(value, "[Œ]", "Oe");
            value = Regex.Replace(value, "[°]", "o");
            value = Regex.Replace(value, "[¡]", "!");
            value = Regex.Replace(value, "[¿]", "?");
            value = Regex.Replace(value, "[«»\u201C\u201D\u201E\u201F\u2033\u2036]", "\"");
            value = Regex.Replace(value, "[\u2026]", "...");

            return value;
        }

        public static string ReplaceDiacriticsFast(this string input)
        {
            // this does a good quick job of replacing diacretics
            // using "ISO-8859-8" gives better results than ""ISO-8859-1"
            byte[] byteOuput = Encoding.GetEncoding("ISO-8859-8").GetBytes(input);
            var result = Encoding.GetEncoding("ISO-8859-8").GetString(byteOuput);
            return result;
        }

        /// <summary>
        /// Replace white space with user choice of replacement or remove all together
        /// </summary>
        /// <param name="value"></param>
        /// <param name="replacementValue">Default is underscore</param>
        /// <returns></returns>
        public static string ReplaceSpaceWith(this string value, string replacementValue = "_")
        {
            var result = Regex.Replace(value.Trim(), @"[\s]", replacementValue);
            return result;
        }

        /// <summary>
        /// Moves short words like "The " from the begining of a string to the end ", The" 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="undoIt">Use to undo ShortWordMover strings</param>
        /// <returns></returns>
        public static string ShortWordMover(this string value, bool undoIt = false)
        {
            var shortWord = new string[] { "The ", "THE ", "the ", "A ", "a " };
            var newEnding = new string[] { ", The", ", THE", ", the", ", A", ", a" };

            for (int i = 0; i < shortWord.Length; i++)
            {
                if (undoIt)
                {
                    if (value.EndsWith(newEnding[i], StringComparison.InvariantCulture))
                        value = String.Format("{0}{1}", shortWord[0], value.Substring(0, value.Length - 5)).Trim();
                }
                else
                {
                    if (value.StartsWith(shortWord[i], StringComparison.InvariantCulture))
                        value = String.Format("{0}{1}", value.Substring(4, value.Length - 4), newEnding[i]).Trim();
                }
            }

            return value;
        }

        public static string StripDiacritics(this string value)
        {
            // test string = "áéíóúç";
            var result = Regex.Replace(value.Normalize(NormalizationForm.FormD), "[^A-Za-z| ]", String.Empty);

            return result;
        }

        public static string StripExcessWhiteSpace(this string value)
        {
            Regex rgx = new Regex("[ ]{2,}", RegexOptions.None);
            var result = rgx.Replace(value, " ");

            return result;
        }

        /// <summary>
        /// Strips non-printable ASCII characters
        /// </summary>
        /// <param name="filePath">Full path to the File</param>
        public static Stream StripIllegalXMLChars(this string filePath)
        {
            string tmpContents = File.ReadAllText(filePath);
            const string pattern = @"[\x01-\x08\x0B-\x0C\x0E-\x1F\x7F-\x84\x86-\x9F]"; // XML1.1

            tmpContents = Regex.Replace(tmpContents, pattern, "", RegexOptions.IgnoreCase);

            return new MemoryStream(new UTF8Encoding(false).GetBytes(tmpContents));
        }

        public static string StripLeadingNumbers(this string value)
        {
            Regex rgx = new Regex(@"^[\d]*\s*");
            var result = rgx.Replace(value, "");
            return result;
        }

        public static string StripLeadingSpecialCharacters(this string value)
        {
            Regex rgx = new Regex("^[^A-Za-z0-9(]*");
            var result = rgx.Replace(value, "");
            return result;
        }

        public static string StripNonAlpaNumeric(this string value)
        {
            // removes all non alphanumeric and all white space
            Regex rgx = new Regex("[^a-zA-Z0-9]+");
            var result = rgx.Replace(value, "");
            return result;
        }

        public static string StripPlatformEndName(this string value)
        {
            if (value.EndsWith(GamePlatform.Pc.GetPathName()[2]) ||
                value.EndsWith(GamePlatform.Mac.GetPathName()[2]) ||
                value.EndsWith(GamePlatform.XBox360.GetPathName()[2]) ||
                value.EndsWith(GamePlatform.PS3.GetPathName()[2]) ||
                value.EndsWith(GamePlatform.PS3.GetPathName()[2] + ".psarc"))
            {
                return value.Substring(0, value.LastIndexOf("_"));
            }

            return value;
        }

        public static string StripSpecialCharacters(this string value)
        {
            // value = Regex.Replace(value, "[`~#\\$€£*',.;:!?()[]\"{}/]", "");
            Regex rgx = new Regex("[^a-zA-Z0-9 _#'.]+$"); // these are acceptable
            var result = rgx.Replace(value, "");
            return result;
        }

        public static string ToNullTerminatedAscii(this Byte[] bytes)
        {
            var result = Encoding.ASCII.GetString(bytes).TrimEnd('\0');
            return result;
        }

        public static string ToNullTerminatedUTF8(this Byte[] bytes)
        {
            var result = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
            return result;
        }


    }
}
