namespace JsonAst
{
    public enum SyntaxKind
    {
        OpenBraceToken = 1,
        CloseBraceToken = 2,
        OpenBracketToken = 3,
        CloseBracketToken = 4,
        CommaToken = 5,
        ColonToken = 6,
        NullKeyword = 7,
        TrueKeyword = 8,
        FalseKeyword = 9,
        StringLiteral = 10,
        NumericLiteral = 11,
        LineCommentTrivia = 12,
        BlockCommentTrivia = 13,
        LineBreakTrivia = 14,
        Trivia = 15,
        Unknown = 16,
        Eof = 17
    }
}