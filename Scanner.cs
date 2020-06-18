using System;

namespace JsonAst
{
    public class Scanner
    {
        private readonly string _text;
        private readonly int _length;

        private SyntaxKind _token = SyntaxKind.Unknown;

        private string _value = "";
        private int _position = 0;
        private int _tokenOffset = 0;
        private int _lineNumber = 0;
        private int _lineStartOffset = 0;
        private int _previousLineStartOffset = 0;
        private int _tokenLineStartOffset = 0;

        public Scanner(string text)
        {
            _text = text;
            _length = text.Length;
        }

        public string GetValue()
        {
            return _value;
        }

        public SyntaxKind GetToken()
        {
            return _token;
        }

        private bool IsLineBreak(char ch)
        {
            return ch == (int)CharacterCodes.lineFeed ||
                ch == (int)CharacterCodes.carriageReturn ||
                ch == (int)CharacterCodes.lineSeparator ||
                ch == (int)CharacterCodes.paragraphSeparator;
        }

        private bool IsWhiteSpace(char ch)
        {
            return ch == (int)CharacterCodes.space ||
                ch == (int)CharacterCodes.tab ||
                ch == (int)CharacterCodes.verticalTab ||
                ch == (int)CharacterCodes.formFeed ||
                ch == (int)CharacterCodes.nonBreakingSpace ||
                ch == (int)CharacterCodes.ogham ||
                ch >= (int)CharacterCodes.enQuad &&
                ch <= (int)CharacterCodes.zeroWidthSpace ||
                ch == (int)CharacterCodes.narrowNoBreakSpace ||
                ch == (int)CharacterCodes.mathematicalSpace ||
                ch == (int)CharacterCodes.ideographicSpace ||
                ch == (int)CharacterCodes.byteOrderMark;

        }

        public SyntaxKind Scan()
        {
            _value = "";
            _tokenOffset = _position;
            _lineStartOffset = _lineNumber;
            _previousLineStartOffset = _tokenLineStartOffset;

            if (_position >= _length)
            {
                _tokenOffset = _length;
                return _token = SyntaxKind.Eof;
            }

            var code = _text[_position];

            if (IsWhiteSpace(code))
            {
                do
                {
                    _position++;
                    _value += code;
                    code = _text[_position];
                } while (Char.IsWhiteSpace(code));

                return _token = SyntaxKind.Trivia;
            }

            if (IsLineBreak(code))
            {
                _position++;
                _value += code;
                if (code == '\r' && _text[_position] == '\n')
                {
                    _value += _text[_position];
                    _position++;
                }

                _lineNumber++;
                _tokenLineStartOffset = _position;

                return _token = SyntaxKind.LineBreakTrivia;
            }

            switch (code)
            {
                case '{':
                    {
                        _position++;
                        return _token = SyntaxKind.OpenBraceToken;
                    }
                case '}':
                    {
                        _position++;
                        return _token = SyntaxKind.CloseBraceToken;
                    }
                case '[':
                    {
                        _position++;
                        return _token = SyntaxKind.OpenBracketToken;
                    }
                case ']':
                    {
                        _position++;
                        return _token = SyntaxKind.CloseBracketToken;
                    }
                case ':':
                    {
                        _position++;
                        return _token = SyntaxKind.ColonToken;
                    }
                case ',':
                    {
                        _position++;
                        return _token = SyntaxKind.CommaToken;
                    }
                case '"':
                    {
                        _position++;
                        _value += ScanString();
                        return _token = SyntaxKind.StringLiteral;
                    }
                case '-':
                    {
                        _position++;
                        _value += code;

                        // Doesn't follow with a number so the minus is invalid
                        if (_position == _length || Char.IsDigit(_value[_position]))
                        {
                            return _token = SyntaxKind.Unknown;
                        }

                        _value += ScanNumber();
                        return _token = SyntaxKind.NumericLiteral;
                    }
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    {
                        _value += ScanNumber();
                        return _token = SyntaxKind.NumericLiteral;
                    }
                default:
                    {
                        while (_position < _length && IsUnknownCharacterCode(code))
                        {
                            _position++;
                            code = _text[_position];
                        }

                        if (_tokenOffset != _position)
                        {
                            _value = _text.Substring(_tokenOffset, _position - _tokenOffset);

                            switch (_value)
                            {
                                case "null":
                                    {
                                        return _token = SyntaxKind.NullKeyword;
                                    }
                                case "true":
                                    {
                                        return _token = SyntaxKind.TrueKeyword;
                                    }
                                case "false":
                                    {
                                        return _token = SyntaxKind.FalseKeyword;
                                    }
                                default:
                                    {
                                        return _token = SyntaxKind.Unknown;
                                    }
                            }
                        }

                        _value += code;
                        _position++;
                        return _token = SyntaxKind.Unknown;
                    }
            }
        }

        private bool IsUnknownCharacterCode(char ch)
        {
            if (IsWhiteSpace(ch) || IsLineBreak(ch))
            {
                return false;
            }

            switch ((int)ch)
            {
                case (int) CharacterCodes.closeBrace:
                case (int) CharacterCodes.closeBracket:
                case (int) CharacterCodes.openBrace:
                case (int) CharacterCodes.openBracket:
                case (int) CharacterCodes.doubleQuote:
                case (int) CharacterCodes.colon:
                case (int) CharacterCodes.comma:
                case (int) CharacterCodes.slash:
                    {
                        return false;
                    }
            }
            return true;

        }

        private string ScanString()
        {
            var result = "";
            var start = _position;

            while (true)
            {
                // Out of index
                if (_position >= _length)
                {
                    result += _text[start.._position];
                    break;
                }

                var ch = _text[_position];

                // Empty string
                if (ch == '"')
                {
                    result += _text[start.._position];
                    _position++;
                    break;
                }

                if (ch == '\\')
                {
                    result += _text[start.._position];
                    _position++;

                    if (_position >= _length)
                    {
                        // Unexpected string end
                        break;
                    }

                    var ch2 = _text[_position++];
                    switch (ch2)
                    {
                        case '"':
                        case '\\':
                        case '/':
                        case 'b':
                        case 'f':
                        case 'n':
                        case 'r':
                        case 't':
                        case 'u':
                            {
                                // TODO
                                break;
                            }
                        default:
                            {
                                // Invalid escape character
                                break;
                            }
                    }

                    start = _position;
                    continue;
                }

                if (ch >= 0 && ch <= 0x1f)
                {
                    if (IsLineBreak(ch))
                    {
                        result += _text[start.._position];
                        // Unexpected line break
                        break;
                    }

                    // Invalid character
                }

                _position++;
            }

            return result;
        }

        private string ScanNumber()
        {
            var start = _position;

            if (_text[_position] == '0')
            {
                _position++;
            }
            else
            {
                _position++;
                while (_position < _text.Length && Char.IsDigit(_text[_position]))
                {
                    _position++;
                }
            }

            if (_position < _text.Length && _text[_position] == '.')
            {
                _position++;
                if (_position < _text.Length && Char.IsDigit(_text[_position]))
                {
                    _position++;
                    while (_position < _text.Length && Char.IsDigit(_text[_position]))
                    {
                        _position++;
                    }
                }
                else
                {
                    // Error unexpected end of number
                    return _text[start.._position];
                }
            }

            var end = _position;
            if (_position < _text.Length && (_text[_position] == 'E' || _text[_position] == 'e'))
            {
                _position++;
                if (_position < _text.Length && _text[_position] == '+' || _text[_position] == '-')
                {
                    _position++;
                }

                if (_position < _text.Length && Char.IsDigit(_text[_position]))
                {
                    _position++;
                    while (_position < _text.Length && Char.IsDigit(_text[_position]))
                    {
                        _position++;
                    }

                    end = _position;
                } 
                else
                {
                    // scanError = ScanError.UnexpectedEndOfNumber;
                }
            }

            return _text.Substring(start, end - start);
        }

        public int GetTokenOffset()
        {
            return _tokenOffset;
        }

        public int GetTokenLength()
        {
            return _position - _tokenOffset;
        }
    }
}
