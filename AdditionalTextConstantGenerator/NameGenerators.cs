using System.Globalization;
using System.Text;

namespace Datacute.AdditionalTextConstantGenerator
{
    internal static class NameGenerators
    {
        public static string GetStringConstantName(this string additionalTextFilePath, string enclosingTypeName)
        {
            var stringConstantName = ConvertToStringConstantName(Path.GetFileNameWithoutExtension(additionalTextFilePath));
            if (stringConstantName.Length == 0 || enclosingTypeName.Equals(stringConstantName))
            {
                stringConstantName = ConvertToStringConstantName(Path.GetFileName(additionalTextFilePath));
            }
            return stringConstantName;
        }

        private static readonly Dictionary<char, string> CharacterNames = new Dictionary<char, string>
        {
            { '.', "dot" },
            { '-', "minus" },
            { '+', "plus" },
            { '*', "times" },
            { '/', "slash" },
            { '%', "pct" },
            { '<', "lt" },
            { '>', "gt" },
            { '=', "eq" },
            { '&', "amp" },
            { '|', "pipe" },
            { '^', "hat" },
            { '!', "excl" },
            { '?', "quest" },
            { ':', "colon" },
            { ',', "comma" },
            { ';', "semi" },
            { '~', "tilde" },
            { '`', "grave" },
            { '@', "at" },
            { '#', "hash" },
            { '$', "dollar" },
            { '\\', "backslash" },
            { '\'', "apos" },
            { '"', "quot" },
            { '[', "start" }, // lsqb lbrack
            { ']', "end" },
            { '{', "begin" }, // lcub lbrace
            { '}', "finish" },
            { '(', "open" },  // lpar lparen
            { ')', "close" },
            { ' ', "space" },
            { '\t', "tab" },
            { '\r', "CR" },
            { '\n', "LF" }
        };

        private static string ConvertToStringConstantName(string filename)
        {
            var validName = new StringBuilder();
            var previousCharacterEscaped = false;

            // Iterate through each character in the filename
            for (var i = 0; i < filename.Length; i++)
            {
                // Check if the character is a letter or a digit
                // Replace invalid characters with underscores
                var c = filename[i];
                var isValid = ValidCharacterForStringConstantName(c);
                if (isValid)
                {
                    validName.Append(c);
                    previousCharacterEscaped = false;
                }
                else if (CharacterNames.TryGetValue(c, out var name))
                {
                    if (!previousCharacterEscaped)
                    {
                        validName.Append('_');
                    }
                    validName.Append(name);
                    validName.Append('_');
                    previousCharacterEscaped = true;
                }
                else
                {
                    if (!previousCharacterEscaped)
                    {
                        validName.Append('_');
                    }
                    validName.Append($"u{char.ConvertToUtf32(filename, i):X}");
                    validName.Append('_');
                    previousCharacterEscaped = true;
                }

                if (char.IsHighSurrogate(c))
                {
                    i += 1;
                }
            }

            // Handle leading digits
            if (validName.Length > 0 && !ValidFirstCharacterForStringConstantName(validName))
            {
                validName.Insert(0, '_');
            }

            // Convert to CamelCase
            if (validName.Length > 0)
            {
                validName[0] = char.ToUpper(validName[0]);
            }

            return validName.ToString();
        }

        private static bool ValidCharacterForStringConstantName(char c)
        {
            switch (CharUnicodeInfo.GetUnicodeCategory(c))
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.LetterNumber:
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.ConnectorPunctuation:
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark:
                case UnicodeCategory.Format:
                    return true;
                default:
                    return false;
            }
        }

        private static bool ValidFirstCharacterForStringConstantName(StringBuilder validName)
        {
            var c = validName[0];
            return c == '_' || char.IsLetter(c);
        }

        public static string GetFileName(this string additionalTextFilePath) => Path.GetFileName(additionalTextFilePath);
    }
}