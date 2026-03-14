using System.Collections.Generic;
using UnityEngine;

namespace RTLTMPro
{
    internal sealed class RTLInputFieldVisualText
    {
        public static RTLInputFieldVisualText Identity(string text)
        {
            int length = text?.Length ?? 0;
            int[] boundaries = new int[length + 1];
            for (int i = 0; i <= length; i++)
            {
                boundaries[i] = i;
            }

            return new RTLInputFieldVisualText(text ?? string.Empty, false, boundaries, boundaries);
        }

        private RTLInputFieldVisualText(string text, bool isRightToLeft, int[] logicalToVisual, int[] visualToLogical)
        {
            Text = text;
            IsRightToLeft = isRightToLeft;
            LogicalToVisual = logicalToVisual;
            VisualToLogical = visualToLogical;
        }

        public string Text { get; }
        public bool IsRightToLeft { get; }
        public int[] LogicalToVisual { get; }
        public int[] VisualToLogical { get; }

        public int ToVisualIndex(int logicalIndex)
        {
            logicalIndex = Mathf.Clamp(logicalIndex, 0, LogicalToVisual.Length - 1);
            if (IsRightToLeft && logicalIndex == LogicalToVisual.Length - 1)
                return 0;
            return LogicalToVisual[logicalIndex];
        }

        public int ToLogicalIndex(int visualIndex)
        {
            visualIndex = Mathf.Clamp(visualIndex, 0, VisualToLogical.Length - 1);
            return VisualToLogical[visualIndex];
        }

        public static RTLInputFieldVisualText Create(List<RTLToken> visualTokens, int logicalLength)
        {
            char[] chars = new char[visualTokens.Count];
            for (int i = 0; i < visualTokens.Count; i++)
            {
                chars[i] = (char)visualTokens[i].CharCode;
            }

            int[] visualToLogical = new int[visualTokens.Count + 1];
            int[] logicalToVisual = new int[logicalLength + 1];
            for (int i = 0; i < logicalToVisual.Length; i++)
            {
                logicalToVisual[i] = -1;
            }

            for (int i = 0; i < visualTokens.Count; i++)
            {
                RTLToken token = visualTokens[i];
                visualToLogical[i] = token.LogicalEnd;
                visualToLogical[i + 1] = token.LogicalStart;

                logicalToVisual[token.LogicalStart] = i + 1;
                logicalToVisual[token.LogicalEnd] = i;
                for (int logicalIndex = token.LogicalStart + 1; logicalIndex < token.LogicalEnd; logicalIndex++)
                {
                    logicalToVisual[logicalIndex] = i;
                }
            }

            if (logicalToVisual.Length > 0)
            {
                if (logicalToVisual[0] < 0)
                {
                    logicalToVisual[0] = visualTokens.Count;
                }

                for (int i = 1; i < logicalToVisual.Length; i++)
                {
                    if (logicalToVisual[i] < 0)
                    {
                        logicalToVisual[i] = logicalToVisual[i - 1];
                    }
                }
            }

            return new RTLInputFieldVisualText(new string(chars), true, logicalToVisual, visualToLogical);
        }
    }

    internal struct RTLToken
    {
        public RTLToken(int charCode, int logicalStart, int logicalEnd)
        {
            CharCode = charCode;
            LogicalStart = logicalStart;
            LogicalEnd = logicalEnd;
        }

        public int CharCode;
        public int LogicalStart;
        public int LogicalEnd;
    }

    internal static class RTLInputFieldTextProcessor
    {
        private static readonly List<RTLToken> LtrTokenBuffer = new List<RTLToken>(512);
        private static readonly List<RTLToken> TagTokenBuffer = new List<RTLToken>(512);
        private static readonly Dictionary<int, int> MirroredCharsMap = new Dictionary<int, int>
        {
            ['('] = ')',
            [')'] = '(',
            ['['] = ']',
            [']'] = '[',
            ['{'] = '}',
            ['}'] = '{',
            ['<'] = '>',
            ['>'] = '<',
        };
        private static readonly HashSet<int> TashkeelCharactersSet = new HashSet<int>
        {
            (int)TashkeelCharacters.Fathan,
            (int)TashkeelCharacters.Dammatan,
            (int)TashkeelCharacters.Kasratan,
            (int)TashkeelCharacters.Fatha,
            (int)TashkeelCharacters.Damma,
            (int)TashkeelCharacters.Kasra,
            (int)TashkeelCharacters.Shadda,
            (int)TashkeelCharacters.Sukun,
            (int)TashkeelCharacters.MaddahAbove,
            (int)TashkeelCharacters.SuperscriptAlef,
            (int)TashkeelCharacters.ShaddaWithDammatanIsolatedForm,
            (int)TashkeelCharacters.ShaddaWithKasratanIsolatedForm,
            (int)TashkeelCharacters.ShaddaWithFathaIsolatedForm,
            (int)TashkeelCharacters.ShaddaWithDammaIsolatedForm,
            (int)TashkeelCharacters.ShaddaWithKasraIsolatedForm,
            (int)TashkeelCharacters.ShaddaWithSuperscriptAlefIsolatedForm,
        };

        private static readonly Dictionary<int, int> ShaddaCombinationMap = new Dictionary<int, int>
        {
            [(int)TashkeelCharacters.Dammatan] = (int)TashkeelCharacters.ShaddaWithDammatanIsolatedForm,
            [(int)TashkeelCharacters.Kasratan] = (int)TashkeelCharacters.ShaddaWithKasratanIsolatedForm,
            [(int)TashkeelCharacters.Fatha] = (int)TashkeelCharacters.ShaddaWithFathaIsolatedForm,
            [(int)TashkeelCharacters.Damma] = (int)TashkeelCharacters.ShaddaWithDammaIsolatedForm,
            [(int)TashkeelCharacters.Kasra] = (int)TashkeelCharacters.ShaddaWithKasraIsolatedForm,
            [(int)TashkeelCharacters.SuperscriptAlef] = (int)TashkeelCharacters.ShaddaWithSuperscriptAlefIsolatedForm,
        };

        public static RTLInputFieldVisualText Build(string input, bool farsi, bool preserveNumbers, bool forceFix)
        {
            if (string.IsNullOrEmpty(input))
            {
                return RTLInputFieldVisualText.Identity(input);
            }

            if (!forceFix && !TextUtils.IsRTLInput(input))
            {
                return RTLInputFieldVisualText.Identity(input);
            }

            var tokens = new List<RTLToken>(input.Length);
            for (int i = 0; i < input.Length; i++)
            {
                tokens.Add(new RTLToken(input[i], i, i + 1));
            }

            var tashkeelTokens = new List<(RTLToken Token, int Position)>();
            RemoveTashkeel(tokens, tashkeelTokens);
            FixYah(tokens, farsi);
            ShapeArabic(tokens, preserveNumbers, farsi);
            RestoreTashkeel(tokens, tashkeelTokens);
            FixShaddaCombinations(tokens);

            return RTLInputFieldVisualText.Create(ReorderVisualTokens(tokens, farsi, preserveNumbers), input.Length);
        }

        private static List<RTLToken> ReorderVisualTokens(List<RTLToken> tokens, bool farsi, bool preserveNumbers)
        {
            var output = new List<RTLToken>(tokens.Count);
            LtrTokenBuffer.Clear();
            TagTokenBuffer.Clear();

            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                bool isInMiddle = i > 0 && i < tokens.Count - 1;
                bool isAtBeginning = i == 0;
                bool isAtEnd = i == tokens.Count - 1;

                RTLToken token = tokens[i];
                int nextCharacter = isAtEnd ? 0 : tokens[i + 1].CharCode;
                int previousCharacter = isAtBeginning ? 0 : tokens[i - 1].CharCode;

                if (token.CharCode == '>')
                {
                    bool isValidTag = false;
                    int nextI = i;
                    TagTokenBuffer.Add(token);

                    for (int j = i - 1; j >= 0; j--)
                    {
                        RTLToken tagToken = tokens[j];
                        TagTokenBuffer.Add(tagToken);

                        if (tagToken.CharCode == '<')
                        {
                            if (j + 1 < tokens.Count && tokens[j + 1].CharCode == ' ')
                            {
                                break;
                            }

                            isValidTag = true;
                            nextI = j;
                            break;
                        }
                    }

                    if (isValidTag)
                    {
                        FlushTokens(LtrTokenBuffer, output);
                        FlushTokens(TagTokenBuffer, output);
                        i = nextI;
                        continue;
                    }

                    TagTokenBuffer.Clear();
                }

                if (Char32Utils.IsPunctuation(token.CharCode) || Char32Utils.IsSymbol(token.CharCode))
                {
                    token = MirrorTokenIfNeeded(token, previousCharacter, nextCharacter);

                    if (isInMiddle)
                    {
                        bool isAfterRTLCharacter = Char32Utils.IsRTLCharacter(previousCharacter);
                        bool isBeforeRTLCharacter = Char32Utils.IsRTLCharacter(nextCharacter);
                        bool isBeforeWhiteSpace = Char32Utils.IsWhiteSpace(nextCharacter);
                        bool isAfterWhiteSpace = Char32Utils.IsWhiteSpace(previousCharacter);
                        bool isUnderline = token.CharCode == '_';
                        bool isSpecialPunctuation = token.CharCode == '.' || token.CharCode == 0x061B || token.CharCode == 0x061F;

                        if ((isBeforeRTLCharacter && isAfterRTLCharacter) ||
                            (isAfterWhiteSpace && isSpecialPunctuation) ||
                            (isBeforeWhiteSpace && isAfterRTLCharacter) ||
                            (isBeforeRTLCharacter && isAfterWhiteSpace) ||
                            ((isBeforeRTLCharacter || isAfterRTLCharacter) && isUnderline))
                        {
                            FlushTokens(LtrTokenBuffer, output);
                            output.Add(token);
                        }
                        else
                        {
                            LtrTokenBuffer.Add(token);
                        }
                    }
                    else if (isAtEnd)
                    {
                        LtrTokenBuffer.Add(token);
                    }
                    else if (isAtBeginning)
                    {
                        output.Add(token);
                    }

                    continue;
                }

                if (isInMiddle)
                {
                    bool isAfterEnglishChar = Char32Utils.IsEnglishLetter(previousCharacter);
                    bool isBeforeEnglishChar = Char32Utils.IsEnglishLetter(nextCharacter);
                    bool isAfterNumber = Char32Utils.IsNumber(previousCharacter, preserveNumbers, farsi);
                    bool isBeforeNumber = Char32Utils.IsNumber(nextCharacter, preserveNumbers, farsi);
                    bool isAfterSymbol = Char32Utils.IsSymbol(previousCharacter);
                    bool isBeforeSymbol = Char32Utils.IsSymbol(nextCharacter);

                    if (token.CharCode == ' ' &&
                        (isBeforeEnglishChar || isBeforeNumber || isBeforeSymbol) &&
                        (isAfterEnglishChar || isAfterNumber || isAfterSymbol))
                    {
                        LtrTokenBuffer.Add(token);
                        continue;
                    }
                }

                if (Char32Utils.IsEnglishLetter(token.CharCode) || Char32Utils.IsNumber(token.CharCode, preserveNumbers, farsi) || IsSurrogate(token.CharCode))
                {
                    LtrTokenBuffer.Add(token);
                    continue;
                }

                FlushTokens(LtrTokenBuffer, output);

                if (token.CharCode != 0xFFFF && token.CharCode != (int)SpecialCharacters.ZeroWidthNoJoiner)
                {
                    output.Add(token);
                }
            }

            FlushTokens(LtrTokenBuffer, output);
            return output;
        }

        private static RTLToken MirrorTokenIfNeeded(RTLToken token, int previousCharacter, int nextCharacter)
        {
            if (MirroredCharsMap.TryGetValue(token.CharCode, out int mirrored))
            {
                bool isAfterRTLCharacter = Char32Utils.IsRTLCharacter(previousCharacter);
                bool isBeforeRTLCharacter = Char32Utils.IsRTLCharacter(nextCharacter);
                if (isAfterRTLCharacter || isBeforeRTLCharacter)
                {
                    token.CharCode = mirrored;
                }
            }

            return token;
        }

        private static bool IsSurrogate(int charCode)
        {
            return (charCode >= 0xD800 && charCode <= 0xDBFF) || (charCode >= 0xDC00 && charCode <= 0xDFFF);
        }

        private static void FlushTokens(List<RTLToken> buffer, List<RTLToken> output)
        {
            for (int i = buffer.Count - 1; i >= 0; i--)
            {
                output.Add(buffer[i]);
            }

            buffer.Clear();
        }

        private static void RemoveTashkeel(List<RTLToken> tokens, List<(RTLToken Token, int Position)> tashkeelTokens)
        {
            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                if (TashkeelCharactersSet.Contains(tokens[i].CharCode))
                {
                    tashkeelTokens.Add((tokens[i], i));
                    tokens.RemoveAt(i);
                }
            }

            tashkeelTokens.Reverse();
        }

        private static void RestoreTashkeel(List<RTLToken> tokens, List<(RTLToken Token, int Position)> tashkeelTokens)
        {
            for (int i = 0; i < tashkeelTokens.Count; i++)
            {
                (RTLToken token, int position) = tashkeelTokens[i];
                position = Mathf.Clamp(position, 0, tokens.Count);
                tokens.Insert(position, token);
            }
        }

        private static void FixShaddaCombinations(List<RTLToken> tokens)
        {
            int i = 0;
            while (i < tokens.Count - 1)
            {
                if (tokens[i].CharCode == (int)TashkeelCharacters.Shadda &&
                    ShaddaCombinationMap.TryGetValue(tokens[i + 1].CharCode, out int combined))
                {
                    RTLToken token = tokens[i];
                    token.CharCode = combined;
                    token.LogicalEnd = tokens[i + 1].LogicalEnd;
                    tokens[i] = token;
                    tokens.RemoveAt(i + 1);
                    continue;
                }

                i++;
            }
        }

        private static void FixYah(List<RTLToken> tokens, bool farsi)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                RTLToken token = tokens[i];
                if (farsi && token.CharCode == (int)ArabicGeneralLetters.Yeh)
                {
                    token.CharCode = (int)ArabicGeneralLetters.FarsiYeh;
                }
                else if (!farsi && token.CharCode == (int)ArabicGeneralLetters.FarsiYeh)
                {
                    token.CharCode = (int)ArabicGeneralLetters.Yeh;
                }

                tokens[i] = token;
            }
        }

        private static void ShapeArabic(List<RTLToken> tokens, bool preserveNumbers, bool farsi)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                RTLToken token = tokens[i];
                int charCode = token.CharCode;

                if (charCode == (int)ArabicGeneralLetters.Lam && i < tokens.Count - 1 && HandleSpecialLam(tokens, i))
                {
                    token = tokens[i];
                    charCode = token.CharCode;
                }

                if (charCode == (int)ArabicGeneralLetters.Tatweel ||
                    charCode == (int)SpecialCharacters.ZeroWidthNoJoiner)
                {
                    continue;
                }

                if (charCode < 0xFFFF && TextUtils.IsGlyphFixedArabicCharacter((char)charCode))
                {
                    char converted = GlyphTable.Convert((char)charCode);
                    token.CharCode = IsMiddleLetter(tokens, i) ? converted + 3 :
                        IsFinishingLetter(tokens, i) ? converted + 1 :
                        IsLeadingLetter(tokens, i) ? converted + 2 :
                        converted;
                    tokens[i] = token;
                }
            }

            if (!preserveNumbers)
            {
                for (int i = 0; i < tokens.Count; i++)
                {
                    RTLToken token = tokens[i];
                    if (token.CharCode >= (int)EnglishNumbers.Zero && token.CharCode <= (int)EnglishNumbers.Nine)
                    {
                        int offset = token.CharCode - (int)EnglishNumbers.Zero;
                        token.CharCode = farsi ? (int)FarsiNumbers.Zero + offset : (int)HinduNumbers.Zero + offset;
                        tokens[i] = token;
                    }
                }
            }
        }

        private static bool HandleSpecialLam(List<RTLToken> tokens, int index)
        {
            int nextChar = tokens[index + 1].CharCode;
            int replacement;
            switch (nextChar)
            {
                case (int)ArabicGeneralLetters.AlefHamzaBelow:
                    replacement = 0xFEF7;
                    break;
                case (int)ArabicGeneralLetters.Alef:
                    replacement = 0xFEF9;
                    break;
                case (int)ArabicGeneralLetters.AlefHamzaAbove:
                    replacement = 0xFEF5;
                    break;
                case (int)ArabicGeneralLetters.AlefMaddaAbove:
                    replacement = 0xFEF3;
                    break;
                default:
                    return false;
            }

            RTLToken lam = tokens[index];
            lam.CharCode = replacement;
            lam.LogicalEnd = tokens[index + 1].LogicalEnd;
            tokens[index] = lam;
            tokens.RemoveAt(index + 1);
            return true;
        }

        private static bool IsLeadingLetter(List<RTLToken> letters, int index)
        {
            int currentIndexLetter = letters[index].CharCode;
            int previousIndexLetter = index == 0 ? 0 : letters[index - 1].CharCode;
            int nextIndexLetter = index < letters.Count - 1 ? letters[index + 1].CharCode : 0;

            bool isPreviousLetterNonConnectable = index == 0 ||
                                                  !IsGlyphFixedArabicCharacter(previousIndexLetter) ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.Hamza ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.AlefMaddaAbove ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.AlefHamzaAbove ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.AlefHamzaBelow ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.WawHamzaAbove ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.Alef ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.Dal ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.Thal ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.Reh ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.Zain ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.Jeh ||
                                                  previousIndexLetter == (int)ArabicGeneralLetters.Waw ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.AlefMaddaAbove ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.AlefHamzaAbove ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.AlefHamzaBelow ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.WawHamzaAbove ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.Alef ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.Hamza ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.Dal ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.Thal ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.Reh ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.Zain ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.Jeh ||
                                                  previousIndexLetter == (int)ArabicIsolatedLetters.Waw ||
                                                  previousIndexLetter == (int)SpecialCharacters.ZeroWidthNoJoiner;

            bool canThisLetterBeLeading = currentIndexLetter != ' ' &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.Hamza &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.AlefHamzaAbove &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.AlefHamzaBelow &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.AlefMaddaAbove &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.WawHamzaAbove &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.Alef &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.Dal &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.Thal &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.Reh &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.Zain &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.Jeh &&
                                          currentIndexLetter != (int)ArabicGeneralLetters.Waw &&
                                          currentIndexLetter != (int)SpecialCharacters.ZeroWidthNoJoiner;

            bool isNextLetterConnectable = index < letters.Count - 1 &&
                                           IsGlyphFixedArabicCharacter(nextIndexLetter) &&
                                           nextIndexLetter != (int)ArabicGeneralLetters.Hamza &&
                                           nextIndexLetter != (int)SpecialCharacters.ZeroWidthNoJoiner;

            return isPreviousLetterNonConnectable && canThisLetterBeLeading && isNextLetterConnectable;
        }

        private static bool IsFinishingLetter(List<RTLToken> letters, int index)
        {
            int currentIndexLetter = letters[index].CharCode;
            int previousIndexLetter = index == 0 ? 0 : letters[index - 1].CharCode;

            bool isPreviousLetterConnectable = index != 0 &&
                                               previousIndexLetter != ' ' &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.Hamza &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.AlefMaddaAbove &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.AlefHamzaAbove &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.AlefHamzaBelow &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.WawHamzaAbove &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.Alef &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.Dal &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.Thal &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.Reh &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.Zain &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.Jeh &&
                                               previousIndexLetter != (int)ArabicGeneralLetters.Waw &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.Hamza &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.AlefMaddaAbove &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.AlefHamzaAbove &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.AlefHamzaBelow &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.WawHamzaAbove &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.Alef &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.Dal &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.Thal &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.Reh &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.Zain &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.Jeh &&
                                               previousIndexLetter != (int)ArabicIsolatedLetters.Waw &&
                                               previousIndexLetter != (int)SpecialCharacters.ZeroWidthNoJoiner &&
                                               IsGlyphFixedArabicCharacter(previousIndexLetter);

            bool canThisLetterBeFinishing = currentIndexLetter != ' ' &&
                                            currentIndexLetter != (int)SpecialCharacters.ZeroWidthNoJoiner &&
                                            currentIndexLetter != (int)ArabicGeneralLetters.Hamza;

            return isPreviousLetterConnectable && canThisLetterBeFinishing;
        }

        private static bool IsMiddleLetter(List<RTLToken> letters, int index)
        {
            int currentIndexLetter = letters[index].CharCode;
            int previousIndexLetter = index == 0 ? 0 : letters[index - 1].CharCode;
            int nextIndexLetter = index < letters.Count - 1 ? letters[index + 1].CharCode : 0;

            bool middleLetterCheck = index != 0 &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.Hamza &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.AlefMaddaAbove &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.AlefHamzaAbove &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.AlefHamzaBelow &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.WawHamzaAbove &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.Alef &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.Dal &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.Thal &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.Reh &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.Zain &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.Jeh &&
                                     currentIndexLetter != (int)ArabicGeneralLetters.Waw &&
                                     currentIndexLetter != (int)SpecialCharacters.ZeroWidthNoJoiner;

            bool previousLetterCheck = index != 0 &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.Hamza &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.AlefMaddaAbove &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.AlefHamzaAbove &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.AlefHamzaBelow &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.WawHamzaAbove &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.Alef &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.Dal &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.Thal &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.Reh &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.Zain &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.Jeh &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.Waw &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.AlefMaddaAbove &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.AlefHamzaAbove &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.AlefHamzaBelow &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.WawHamzaAbove &&
                                       previousIndexLetter != (int)ArabicGeneralLetters.Hamza &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.Alef &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.Dal &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.Thal &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.Reh &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.Zain &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.Jeh &&
                                       previousIndexLetter != (int)ArabicIsolatedLetters.Waw &&
                                       previousIndexLetter != (int)SpecialCharacters.ZeroWidthNoJoiner &&
                                       IsGlyphFixedArabicCharacter(previousIndexLetter);

            bool nextLetterCheck = index < letters.Count - 1 &&
                                   IsGlyphFixedArabicCharacter(nextIndexLetter) &&
                                   nextIndexLetter != (int)SpecialCharacters.ZeroWidthNoJoiner &&
                                   nextIndexLetter != (int)ArabicGeneralLetters.Hamza &&
                                   nextIndexLetter != (int)ArabicIsolatedLetters.Hamza;

            return nextLetterCheck && previousLetterCheck && middleLetterCheck;
        }

        private static bool IsGlyphFixedArabicCharacter(int charCode)
        {
            return charCode < 0xFFFF && TextUtils.IsGlyphFixedArabicCharacter((char)charCode);
        }
    }
}

