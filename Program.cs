using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JsonAst
{
    public abstract class AstNode
    {
        public AstType Type { get; }
        public AstNode? Parent { get; set; }
        public List<AstNode> Children { get; set; }
        public int Length { get; set; }
        public int Offset { get; set; }

        public AstNode(int offset, AstNode? parent, AstType type)
        {
            Children = new List<AstNode>();
            parent?.Children.Add(this);
            Type = type;
            Offset = offset;
        }
    }

    public class NullAstNode : AstNode
    {
        public NullAstNode(int offset, AstNode? parent = null) : base(offset, parent, AstType.Null)
        {

        }
    }

    public class BooleanAstNode : AstNode
    {
        public bool Value { get; set; }

        public BooleanAstNode(int offset, bool value, AstNode? parent = null) : base(offset, parent, AstType.Null)
        {
            Value = value;
        }
    }

    public class ArrayAstNode : AstNode
    {
        public AstNode[]? Items { get; set; }

        public ArrayAstNode(int offset, AstNode? parent = null) : base(offset, parent, AstType.Array)
        {
        }

    }

    public class ObjectAstNode : AstNode
    {
        public PropertyAstNode[]? Properties { get; set; }

        public ObjectAstNode(int offset, AstNode? parent = null) : base(offset, parent, AstType.Object)
        {
        }
    }

    public class PropertyAstNode : AstNode
    {
        public StringAstNode? KeyNode { get; set; }
        public AstNode? ValueNode { get; set; }
        public AstNode[]? ChildNodes { get; set; }
        public int ColonOffset { get; set; } = 0;

        public PropertyAstNode(int offset, AstNode? parent = null) : base(offset, parent, AstType.Property)
        {
        }
    }

    public class StringAstNode : AstNode
    {
        public string Value { get; set; }

        public StringAstNode(int offset, AstNode? parent = null) : base(offset, parent, AstType.String)
        {
        }

        public StringAstNode(int offset, int length, AstNode? parent = null) : base(offset, parent, AstType.String)
        {
            Length = length;
        }
    }

    public class NumberAstNode : AstNode
    {
        public decimal Value { get; set; }

        public NumberAstNode(int offset, AstNode? parent = null) : base(offset, parent, AstType.Number)
        {
        }
    }

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var content = await File.ReadAllTextAsync("test.json");
            var parser = new JsonParser(content);
            var document = parser.Parse();

            var node = document.GetNodeFromOffset(7);
        }
    }

    public class JsonDocument
    {
        public AstNode? Root { get; }
        public JsonDiagnostic[] Diagnostics { get; }

        public JsonDocument(AstNode? root, JsonDiagnostic[] diagnostics)
        {
            Root = root;
            Diagnostics = diagnostics;
        }

        public AstNode? GetNodeFromOffset(int offset)
        {
            return GetNodeFromOffset(Root, offset);

        }

        private AstNode? GetNodeFromOffset(AstNode node, int offset)
        {
            if (!Contains(node, offset))
            {
                return null;
            }

            var children = node.Children;
            for (var i = 0; i < children.Count && children[i].Offset <= offset; i++)
            {
                var item = GetNodeFromOffset(children[i], offset);
                if (item != null)
                {
                    return item;
                }
            }

            return node;

        }

        private bool Contains(AstNode? node, int offset)
        {
            return offset >= node.Offset && offset < node.Offset + node.Length;
        }
    }

    public class JsonDiagnostic
    {
        public string Message { get; }
        public ErrorCodes Code { get; }
        public int StartPosition { get; }
        public int EndPosition { get; }

        public JsonDiagnostic(string message, ErrorCodes code, int startPosition, int endPosition)
        {
            Message = message;
            Code = code;
            StartPosition = startPosition;
            EndPosition = endPosition;
        }

    }

    public class JsonParser
    {
        private readonly string _content;
        private readonly Scanner _scanner;
        private readonly List<JsonDiagnostic> _diagnostics;

        private int _lastProblemPosition = 0;

        public JsonParser(string content)
        {
            _content = content;
            _scanner = new Scanner(content);
            _diagnostics = new List<JsonDiagnostic>();
        }

        public JsonDocument Parse()
        {
            AstNode? root = null;

            var token = ScanNext();
            if (token != SyntaxKind.Eof)
            {
                root = ParseValue(root);

                if (root == null)
                {
                    Error("Expected a JSON object, array or literal.", ErrorCodes.Undefined);
                }
                else
                {
                    Error("End of file expected.", ErrorCodes.Undefined);
                }
            }

            return new JsonDocument(root, _diagnostics.ToArray());
        }

        private void ErrorAtRange(string message, ErrorCodes code, int startPosition, int endPosition)
        {
            if (_diagnostics.Count > 0 && startPosition == _lastProblemPosition)
            {
                return;
            }

            _diagnostics.Add(new JsonDiagnostic(message, code, startPosition, endPosition));
            _lastProblemPosition = startPosition;
        }

        private void Error(string message, ErrorCodes code)
        {
            Error<AstNode>(message, code);
        }

        private T Error<T>(string message, ErrorCodes code, T? node = null) where T : AstNode
        {
            var start = _scanner.GetTokenOffset();
            var end = start + _scanner.GetTokenLength();

            if (start == end && start > 0)
            {
                start--;
                while (start > 0 && Regex.IsMatch(_content[start].ToString(), "\\s"))
                {
                    start--;
                }

                end = start + 1;
            }

            ErrorAtRange(message, code, start, end);

            if (node != null)
            {
                Finalize(node, false);
            }

            return node;
        }

        private AstNode? ParseValue(AstNode? parent)
        {
            return ParseArray(parent) ?? 
                ParseObject(parent) ?? 
                ParseString(parent) ??
                ParseNumber(parent) ??
                ParseLiteral(parent);
        }

        private AstNode? ParseLiteral(AstNode? parent)
        {
            switch (_scanner.GetToken())
            {
                case SyntaxKind.NullKeyword:
                    {
                        var node = new NullAstNode(_scanner.GetTokenOffset(), parent);
                        return Finalize(node);
                    }
                case SyntaxKind.TrueKeyword:
                case SyntaxKind.FalseKeyword:
                    {
                        var value = _scanner.GetToken() == SyntaxKind.TrueKeyword;
                        var node = new BooleanAstNode(_scanner.GetTokenOffset(), value, parent);
                        return Finalize( node);
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        private NumberAstNode? ParseNumber(AstNode? parent)
        {
            if (_scanner.GetToken() != SyntaxKind.NumericLiteral)
            {
                return null;
            }

            var node = new NumberAstNode(_scanner.GetTokenOffset(), parent);
            var value = _scanner.GetValue();

            if (!Decimal.TryParse(value, out var nodeValue))
            {
                // Error invalid number format
                return Error<NumberAstNode>("Invalid number format", ErrorCodes.Undefined);
            }

            node.Value = nodeValue;
            return Finalize(node);
        }

        private ArrayAstNode? ParseArray(AstNode? parent)
        {
            if (_scanner.GetToken() != SyntaxKind.OpenBracketToken)
            {
                return null;
            }

            var node = new ArrayAstNode(_scanner.GetTokenOffset(), parent);
            ScanNext(); // Consume open bracket

            var needsComma = false;
            var items = new List<AstNode>();

            while (_scanner.GetToken() != SyntaxKind.CloseBracketToken && _scanner.GetToken() != SyntaxKind.Eof)
            {
                if (_scanner.GetToken() == SyntaxKind.CommaToken)
                {
                    if (!needsComma)
                    {
                        // Error value expected
                    }

                    ScanNext(); // Consume comma
                    if (_scanner.GetToken() == SyntaxKind.CloseBraceToken)
                    {
                        if (needsComma)
                        {
                            // Error tailing comma
                        }

                        continue;
                    }
                }
                else if (needsComma)
                {
                    // Error
                }

                var item = ParseValue(node);
                if (item == null)
                {
                    // Error vaue exprected
                }
                else
                {
                    items.Add(item);
                }

                needsComma = true;
            }

            node.Items = items.ToArray();
            return Finalize(node);

        }

        private ObjectAstNode? ParseObject(AstNode? parent)
        {
            if (_scanner.GetToken() != SyntaxKind.OpenBraceToken)
            {
                return null;
            }

            var node = new ObjectAstNode(_scanner.GetTokenOffset(), parent);
            var properties = new List<PropertyAstNode>();

            ScanNext(); // Consume open brace token

            var needsComma = false;
            var seen = new Dictionary<string, AstNode>();

            while (_scanner.GetToken() != SyntaxKind.CloseBraceToken && _scanner.GetToken() != SyntaxKind.Eof)
            {
                if (_scanner.GetToken() == SyntaxKind.CommaToken)
                {
                    if (!needsComma)
                    {
                        // Error value expected
                    }

                    ScanNext(); // Consume comma
                    if (_scanner.GetToken() == SyntaxKind.CloseBraceToken)
                    {
                        if (needsComma)
                        {
                            // Error tailing comma
                        }

                        continue;
                    }
                }
                else if (needsComma)
                {
                    // Error
                }

                var property = ParseProperty(node, seen);

                if (property == null)
                {
                    // Error property expected
                }
                else
                {
                    properties.Add(property);
                }

                needsComma = true;
            }

            if (_scanner.GetToken() != SyntaxKind.CloseBraceToken)
            {
                return null;
            }

            node.Properties = properties.ToArray();
            return Finalize(node);
        }

        private PropertyAstNode? ParseProperty(AstNode? parent, IDictionary<string, AstNode> seen)
        {
            var node = new PropertyAstNode(_scanner.GetTokenOffset(), parent);
            var key = ParseString(node);
            if (key == null)
            {
                if (_scanner.GetToken() == SyntaxKind.Unknown)
                {
                    // Error property key must be double quoted
                    var keyNode = new StringAstNode(_scanner.GetTokenOffset(), _scanner.GetTokenLength(), node)
                    {
                        Value = _scanner.GetValue()
                    };
                    key = keyNode;
                    ScanNext(); // Consume unknown
                }
                else
                {
                    return null;
                }
            }

            node.KeyNode = key;

            var isSeen = seen.ContainsKey(key.Value);
            if (isSeen)
            {
                // Error duplicate object key
                var seenNode = seen[key.Value];
                if (seenNode is ObjectAstNode)
                {
                    // Error duplicate object key at position
                }
            }
            else
            {
                seen[key.Value] = node;
            }

            if (_scanner.GetToken() == SyntaxKind.ColonToken)
            {
                node.ColonOffset = _scanner.GetTokenOffset();
                ScanNext(); // Consume colon
            }
            else
            {
                // Error colon expected
            }

            var value = ParseValue(node);
            if (value == null)
            {
                // Error value expected
                return null;
            }

            node.ValueNode = value;
            node.Length = value.Offset + value.Length - node.Offset;
            return node;
        }

        private T Finalize<T>(T node, bool scanNext = true) where T : AstNode
        {
            node.Length = _scanner.GetTokenOffset() + _scanner.GetTokenLength() - node.Offset;
            if (scanNext)
            {
                ScanNext();
            }

            return node;
        }

        private StringAstNode ParseString(AstNode? parent)
        {
            if (_scanner.GetToken() != SyntaxKind.StringLiteral)
            {
                return null;
            }

            var node = new StringAstNode(_scanner.GetTokenOffset(), parent)
            {
                Value = _scanner.GetValue()
            };

            return Finalize(node);
        }

        private SyntaxKind ScanNext()
        {
            while (true)
            {
                var token = _scanner.Scan();

                switch (token)
                {
                    case SyntaxKind.LineCommentTrivia:
                    case SyntaxKind.BlockCommentTrivia:
                    case SyntaxKind.Trivia:
                    case SyntaxKind.LineBreakTrivia:
                        {
                            break;
                        }
                    default:
                        return token;
                }
            }
        }
    }
}
