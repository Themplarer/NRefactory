using System.Collections.Generic;
using System.Text;

namespace ICSharpCode.NRefactory.CSharp;

public sealed class CommentReferencesCreator
{
    private readonly List<CommentReference> _refs;
    private readonly StringBuilder _sb;

    public CommentReference[] CommentReferences => _refs.ToArray();

    public string Text => _sb.ToString();

    public CommentReferencesCreator(StringBuilder sb)
    {
        _refs = new List<CommentReference>();
        _sb = sb;
        _sb.Clear();
    }

    public void AddText(string text) => Add(text, null, false);

    public void AddReference(string text, object reference, bool isLocal = false) => Add(text, reference, isLocal);

    private void Add(string s, object reference, bool isLocal)
    {
        _refs.Add(new CommentReference(s.Length, reference, isLocal));
        _sb.Append(s);
    }
}