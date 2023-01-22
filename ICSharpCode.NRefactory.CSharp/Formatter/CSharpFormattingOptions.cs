//
// CSharpFormattingOptions.cs
//
// Author:
//       Mike Krüger <mkrueger@novell.com>
//
// Copyright (c) 2009 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.


using System;

namespace ICSharpCode.NRefactory.CSharp;

public class CSharpFormattingOptions : IEquatable<CSharpFormattingOptions>
{
    internal CSharpFormattingOptions()
    {
    }

    public string Name { get; set; }

    public bool IsBuiltIn { get; set; }

    public CSharpFormattingOptions Clone() =>
        //return (CSharpFormattingOptions)MemberwiseClone ();
        // DON'T use MemberwiseClone() since we want to return a CSharpFormattingOptions, not any
        // derived class.
        CopyTo(new CSharpFormattingOptions());

    #region Indentation

    public bool IndentNamespaceBody { get; set; }

    public bool IndentClassBody { get; set; }

    public bool IndentInterfaceBody { get; set; }

    public bool IndentStructBody { get; set; }

    public bool IndentEnumBody { get; set; }

    public bool IndentMethodBody { get; set; }

    public bool IndentPropertyBody { get; set; }

    public bool IndentEventBody { get; set; }

    public bool IndentBlocks { get; set; }

    public bool IndentSwitchBody { get; set; }

    public bool IndentCaseBody { get; set; }

    public bool IndentBreakStatements { get; set; }

    public bool AlignEmbeddedStatements { get; set; }

    public bool AlignElseInIfStatements { get; set; }

    public PropertyFormatting AutoPropertyFormatting { get; set; }

    public PropertyFormatting SimplePropertyFormatting { get; set; }

    public EmptyLineFormatting EmptyLineFormatting { get; set; }

    public bool IndentPreprocessorDirectives { get; set; }

    public bool AlignToMemberReferenceDot { get; set; }

    public bool IndentBlocksInsideExpressions { get; set; }

    #endregion

    #region Braces

    public BraceStyle NamespaceBraceStyle { get; set; }

    public BraceStyle ClassBraceStyle { get; set; }

    public BraceStyle InterfaceBraceStyle { get; set; }

    public BraceStyle StructBraceStyle { get; set; }

    public BraceStyle EnumBraceStyle { get; set; }

    public BraceStyle MethodBraceStyle { get; set; }

    public BraceStyle AnonymousMethodBraceStyle { get; set; }

    public BraceStyle ConstructorBraceStyle { get; set; }

    public BraceStyle DestructorBraceStyle { get; set; }

    public BraceStyle PropertyBraceStyle { get; set; }

    public BraceStyle PropertyGetBraceStyle { get; set; }

    public BraceStyle PropertySetBraceStyle { get; set; }

    public PropertyFormatting SimpleGetBlockFormatting { get; set; }

    public PropertyFormatting SimpleSetBlockFormatting { get; set; }

    public BraceStyle EventBraceStyle { get; set; }

    public BraceStyle EventAddBraceStyle { get; set; }

    public BraceStyle EventRemoveBraceStyle { get; set; }

    public bool AllowEventAddBlockInline { get; set; }

    public bool AllowEventRemoveBlockInline { get; set; }

    public BraceStyle StatementBraceStyle { get; set; }

    public bool AllowIfBlockInline { get; set; }

    public bool AllowOneLinedArrayInitialziers { get; set; } = true;

    #endregion

    #region NewLines

    public NewLinePlacement ElseNewLinePlacement { get; set; }

    public NewLinePlacement ElseIfNewLinePlacement { get; set; }

    public NewLinePlacement CatchNewLinePlacement { get; set; }

    public NewLinePlacement FinallyNewLinePlacement { get; set; }

    public NewLinePlacement WhileNewLinePlacement { get; set; }

    public NewLinePlacement EmbeddedStatementPlacement { get; set; } = NewLinePlacement.NewLine;

    #endregion

    #region Spaces

    public bool SpaceBeforeMethodDeclarationParentheses { get; set; }

    public bool SpaceBetweenEmptyMethodDeclarationParentheses { get; set; }

    public bool SpaceBeforeMethodDeclarationParameterComma { get; set; }

    public bool SpaceAfterMethodDeclarationParameterComma { get; set; }

    public bool SpaceWithinMethodDeclarationParentheses { get; set; }

    public bool SpaceBeforeMethodCallParentheses { get; set; }

    public bool SpaceBetweenEmptyMethodCallParentheses { get; set; }

    public bool SpaceBeforeMethodCallParameterComma { get; set; }

    public bool SpaceAfterMethodCallParameterComma { get; set; }

    public bool SpaceWithinMethodCallParentheses { get; set; }

    public bool SpaceBeforeFieldDeclarationComma { get; set; }

    public bool SpaceAfterFieldDeclarationComma { get; set; }

    public bool SpaceBeforeLocalVariableDeclarationComma { get; set; }

    public bool SpaceAfterLocalVariableDeclarationComma { get; set; }

    public bool SpaceBeforeConstructorDeclarationParentheses { get; set; }

    public bool SpaceBetweenEmptyConstructorDeclarationParentheses { get; set; }

    public bool SpaceBeforeConstructorDeclarationParameterComma { get; set; }

    public bool SpaceAfterConstructorDeclarationParameterComma { get; set; }

    public bool SpaceWithinConstructorDeclarationParentheses { get; set; }

    public NewLinePlacement NewLineBeforeConstructorInitializerColon { get; set; }

    public NewLinePlacement NewLineAfterConstructorInitializerColon { get; set; }

    public bool SpaceBeforeIndexerDeclarationBracket { get; set; }

    public bool SpaceWithinIndexerDeclarationBracket { get; set; }

    public bool SpaceBeforeIndexerDeclarationParameterComma { get; set; }

    public bool SpaceAfterIndexerDeclarationParameterComma { get; set; }

    public bool SpaceBeforeDelegateDeclarationParentheses { get; set; }

    public bool SpaceBetweenEmptyDelegateDeclarationParentheses { get; set; }

    public bool SpaceBeforeDelegateDeclarationParameterComma { get; set; }

    public bool SpaceAfterDelegateDeclarationParameterComma { get; set; }

    public bool SpaceWithinDelegateDeclarationParentheses { get; set; }

    public bool SpaceBeforeNewParentheses { get; set; }

    public bool SpaceBeforeIfParentheses { get; set; }

    public bool SpaceBeforeWhileParentheses { get; set; }

    public bool SpaceBeforeForParentheses { get; set; }

    public bool SpaceBeforeForeachParentheses { get; set; }

    public bool SpaceBeforeCatchParentheses { get; set; }

    public bool SpaceBeforeSwitchParentheses { get; set; }

    public bool SpaceBeforeLockParentheses { get; set; }

    public bool SpaceBeforeUsingParentheses { get; set; }

    public bool SpaceAroundAssignment { get; set; }

    public bool SpaceAroundLogicalOperator { get; set; }

    public bool SpaceAroundEqualityOperator { get; set; }

    public bool SpaceAroundRelationalOperator { get; set; }

    public bool SpaceAroundBitwiseOperator { get; set; }

    public bool SpaceAroundAdditiveOperator { get; set; }

    public bool SpaceAroundMultiplicativeOperator { get; set; }

    public bool SpaceAroundShiftOperator { get; set; }

    public bool SpaceAroundNullCoalescingOperator { get; set; }

    public bool SpaceAfterUnsafeAddressOfOperator { get; set; }

    public bool SpaceAfterUnsafeAsteriskOfOperator { get; set; }

    public bool SpaceAroundUnsafeArrowOperator { get; set; }

    public bool SpacesWithinParentheses { get; set; }

    public bool SpacesWithinIfParentheses { get; set; }

    public bool SpacesWithinWhileParentheses { get; set; }

    public bool SpacesWithinForParentheses { get; set; }

    public bool SpacesWithinForeachParentheses { get; set; }

    public bool SpacesWithinCatchParentheses { get; set; }

    public bool SpacesWithinSwitchParentheses { get; set; }

    public bool SpacesWithinLockParentheses { get; set; }

    public bool SpacesWithinUsingParentheses { get; set; }

    public bool SpacesWithinCastParentheses { get; set; }

    public bool SpacesWithinSizeOfParentheses { get; set; }

    public bool SpaceBeforeSizeOfParentheses { get; set; }

    public bool SpacesWithinTypeOfParentheses { get; set; }

    public bool SpacesWithinNewParentheses { get; set; }

    public bool SpacesBetweenEmptyNewParentheses { get; set; }

    public bool SpaceBeforeNewParameterComma { get; set; }

    public bool SpaceAfterNewParameterComma { get; set; }

    public bool SpaceBeforeTypeOfParentheses { get; set; }

    public bool SpacesWithinCheckedExpressionParantheses { get; set; }

    public bool SpaceBeforeConditionalOperatorCondition { get; set; }

    public bool SpaceAfterConditionalOperatorCondition { get; set; }

    public bool SpaceBeforeConditionalOperatorSeparator { get; set; }

    public bool SpaceAfterConditionalOperatorSeparator { get; set; }

    public bool SpacesWithinBrackets { get; set; }

    public bool SpacesBeforeBrackets { get; set; }

    public bool SpaceBeforeBracketComma { get; set; }

    public bool SpaceAfterBracketComma { get; set; }

    public bool SpaceBeforeForSemicolon { get; set; }

    public bool SpaceAfterForSemicolon { get; set; }

    public bool SpaceAfterTypecast { get; set; }

    public bool SpaceBeforeArrayDeclarationBrackets { get; set; }

    public bool SpaceInNamedArgumentAfterDoubleColon { get; set; }

    public bool RemoveEndOfLineWhiteSpace { get; set; }

    public bool SpaceBeforeSemicolon { get; set; }

    #endregion

    #region Blank Lines

    public int MinimumBlankLinesBeforeUsings { get; set; }

    public int MinimumBlankLinesAfterUsings { get; set; }

    public int MinimumBlankLinesBeforeFirstDeclaration { get; set; }

    public int MinimumBlankLinesBetweenTypes { get; set; }

    public int MinimumBlankLinesBetweenFields { get; set; }

    public int MinimumBlankLinesBetweenEventFields { get; set; }

    public int MinimumBlankLinesBetweenMembers { get; set; }

    public int MinimumBlankLinesAroundRegion { get; set; }

    public int MinimumBlankLinesInsideRegion { get; set; }

    #endregion

    #region Keep formatting

    public bool KeepCommentsAtFirstColumn { get; set; }

    #endregion

    #region Wrapping

    public Wrapping ArrayInitializerWrapping { get; set; }

    public BraceStyle ArrayInitializerBraceStyle { get; set; }

    public Wrapping ChainedMethodCallWrapping { get; set; }

    public Wrapping MethodCallArgumentWrapping { get; set; }

    public NewLinePlacement NewLineAferMethodCallOpenParentheses { get; set; }

    public NewLinePlacement MethodCallClosingParenthesesOnNewLine { get; set; }

    public Wrapping IndexerArgumentWrapping { get; set; }

    public NewLinePlacement NewLineAferIndexerOpenBracket { get; set; }

    public NewLinePlacement IndexerClosingBracketOnNewLine { get; set; }

    public Wrapping MethodDeclarationParameterWrapping { get; set; }

    public NewLinePlacement NewLineAferMethodDeclarationOpenParentheses { get; set; }

    public NewLinePlacement MethodDeclarationClosingParenthesesOnNewLine { get; set; }

    public Wrapping IndexerDeclarationParameterWrapping { get; set; }

    public NewLinePlacement NewLineAferIndexerDeclarationOpenBracket { get; set; }

    public NewLinePlacement IndexerDeclarationClosingBracketOnNewLine { get; set; }

    public bool AlignToFirstIndexerArgument { get; set; }

    public bool AlignToFirstIndexerDeclarationParameter { get; set; }

    public bool AlignToFirstMethodCallArgument { get; set; }

    public bool AlignToFirstMethodDeclarationParameter { get; set; }

    public NewLinePlacement NewLineBeforeNewQueryClause { get; set; }

    #endregion

    #region Using Declarations

    public UsingPlacement UsingPlacement { get; set; }

    #endregion

    /*public static CSharpFormattingOptions Load (FilePath selectedFile)
    {
        using (var stream = System.IO.File.OpenRead (selectedFile)) {
            return Load (stream);
        }
    }

    public static CSharpFormattingOptions Load (System.IO.Stream input)
    {
        CSharpFormattingOptions result = FormattingOptionsFactory.CreateMonoOptions ();
        result.Name = "noname";
        using (XmlTextReader reader = new XmlTextReader (input)) {
            while (reader.Read ()) {
                if (reader.NodeType == XmlNodeType.Element) {
                    if (reader.LocalName == "Property") {
                        var info = typeof(CSharpFormattingOptions).GetProperty (reader.GetAttribute ("name"));
                        string valString = reader.GetAttribute ("value");
                        object value;
                        if (info.PropertyType == typeof(bool)) {
                            value = Boolean.Parse (valString);
                        } else if (info.PropertyType == typeof(int)) {
                            value = Int32.Parse (valString);
                        } else {
                            value = Enum.Parse (info.PropertyType, valString);
                        }
                        info.SetValue (result, value, null);
                    } else if (reader.LocalName == "FormattingProfile") {
                        result.Name = reader.GetAttribute ("name");
                    }
                } else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "FormattingProfile") {
                    //Console.WriteLine ("result:" + result.Name);
                    return result;
                }
            }
        }
        return result;
    }

    public void Save (string fileName)
    {
        using (var writer = new XmlTextWriter (fileName, Encoding.Default)) {
            writer.Formatting = System.Xml.Formatting.Indented;
            writer.Indentation = 1;
            writer.IndentChar = '\t';
            writer.WriteStartElement ("FormattingProfile");
            writer.WriteAttributeString ("name", Name);
            foreach (PropertyInfo info in typeof (CSharpFormattingOptions).GetProperties ()) {
                if (info.GetCustomAttributes (false).Any (o => o.GetType () == typeof(ItemPropertyAttribute))) {
                    writer.WriteStartElement ("Property");
                    writer.WriteAttributeString ("name", info.Name);
                    writer.WriteAttributeString ("value", info.GetValue (this, null).ToString ());
                    writer.WriteEndElement ();
                }
            }
            writer.WriteEndElement ();
        }
    }

    public bool Equals (CSharpFormattingOptions other)
    {
        foreach (PropertyInfo info in typeof (CSharpFormattingOptions).GetProperties ()) {
            if (info.GetCustomAttributes (false).Any (o => o.GetType () == typeof(ItemPropertyAttribute))) {
                object val = info.GetValue (this, null);
                object otherVal = info.GetValue (other, null);
                if (val == null) {
                    if (otherVal == null)
                        continue;
                    return false;
                }
                if (!val.Equals (otherVal)) {
                    //Console.WriteLine ("!equal");
                    return false;
                }
            }
        }
        //Console.WriteLine ("== equal");
        return true;
    }*/

    public CSharpFormattingOptions CopyTo(CSharpFormattingOptions other)
    {
        other.IndentNamespaceBody = IndentNamespaceBody;
        other.IndentClassBody = IndentClassBody;
        other.IndentInterfaceBody = IndentInterfaceBody;
        other.IndentStructBody = IndentStructBody;
        other.IndentEnumBody = IndentEnumBody;
        other.IndentMethodBody = IndentMethodBody;
        other.IndentPropertyBody = IndentPropertyBody;
        other.IndentEventBody = IndentEventBody;
        other.IndentBlocks = IndentBlocks;
        other.IndentSwitchBody = IndentSwitchBody;
        other.IndentCaseBody = IndentCaseBody;
        other.IndentBreakStatements = IndentBreakStatements;
        other.AlignEmbeddedStatements = AlignEmbeddedStatements;
        other.AlignElseInIfStatements = AlignElseInIfStatements;
        other.AutoPropertyFormatting = AutoPropertyFormatting;
        other.SimplePropertyFormatting = SimplePropertyFormatting;
        other.EmptyLineFormatting = EmptyLineFormatting;
        other.IndentPreprocessorDirectives = IndentPreprocessorDirectives;
        other.AlignToMemberReferenceDot = AlignToMemberReferenceDot;
        other.IndentBlocksInsideExpressions = IndentBlocksInsideExpressions;
        other.NamespaceBraceStyle = NamespaceBraceStyle;
        other.ClassBraceStyle = ClassBraceStyle;
        other.InterfaceBraceStyle = InterfaceBraceStyle;
        other.StructBraceStyle = StructBraceStyle;
        other.EnumBraceStyle = EnumBraceStyle;
        other.MethodBraceStyle = MethodBraceStyle;
        other.AnonymousMethodBraceStyle = AnonymousMethodBraceStyle;
        other.ConstructorBraceStyle = ConstructorBraceStyle;
        other.DestructorBraceStyle = DestructorBraceStyle;
        other.PropertyBraceStyle = PropertyBraceStyle;
        other.PropertyGetBraceStyle = PropertyGetBraceStyle;
        other.PropertySetBraceStyle = PropertySetBraceStyle;
        other.SimpleGetBlockFormatting = SimpleGetBlockFormatting;
        other.SimpleSetBlockFormatting = SimpleSetBlockFormatting;
        other.EventBraceStyle = EventBraceStyle;
        other.EventAddBraceStyle = EventAddBraceStyle;
        other.EventRemoveBraceStyle = EventRemoveBraceStyle;
        other.AllowEventAddBlockInline = AllowEventAddBlockInline;
        other.AllowEventRemoveBlockInline = AllowEventRemoveBlockInline;
        other.StatementBraceStyle = StatementBraceStyle;
        other.AllowIfBlockInline = AllowIfBlockInline;
        other.AllowOneLinedArrayInitialziers = AllowOneLinedArrayInitialziers;
        other.ElseNewLinePlacement = ElseNewLinePlacement;
        other.ElseIfNewLinePlacement = ElseIfNewLinePlacement;
        other.CatchNewLinePlacement = CatchNewLinePlacement;
        other.FinallyNewLinePlacement = FinallyNewLinePlacement;
        other.WhileNewLinePlacement = WhileNewLinePlacement;
        other.EmbeddedStatementPlacement = EmbeddedStatementPlacement;
        other.SpaceBeforeMethodDeclarationParentheses = SpaceBeforeMethodDeclarationParentheses;
        other.SpaceBetweenEmptyMethodDeclarationParentheses = SpaceBetweenEmptyMethodDeclarationParentheses;
        other.SpaceBeforeMethodDeclarationParameterComma = SpaceBeforeMethodDeclarationParameterComma;
        other.SpaceAfterMethodDeclarationParameterComma = SpaceAfterMethodDeclarationParameterComma;
        other.SpaceWithinMethodDeclarationParentheses = SpaceWithinMethodDeclarationParentheses;
        other.SpaceBeforeMethodCallParentheses = SpaceBeforeMethodCallParentheses;
        other.SpaceBetweenEmptyMethodCallParentheses = SpaceBetweenEmptyMethodCallParentheses;
        other.SpaceBeforeMethodCallParameterComma = SpaceBeforeMethodCallParameterComma;
        other.SpaceAfterMethodCallParameterComma = SpaceAfterMethodCallParameterComma;
        other.SpaceWithinMethodCallParentheses = SpaceWithinMethodCallParentheses;
        other.SpaceBeforeFieldDeclarationComma = SpaceBeforeFieldDeclarationComma;
        other.SpaceAfterFieldDeclarationComma = SpaceAfterFieldDeclarationComma;
        other.SpaceBeforeLocalVariableDeclarationComma = SpaceBeforeLocalVariableDeclarationComma;
        other.SpaceAfterLocalVariableDeclarationComma = SpaceAfterLocalVariableDeclarationComma;
        other.SpaceBeforeConstructorDeclarationParentheses = SpaceBeforeConstructorDeclarationParentheses;
        other.SpaceBetweenEmptyConstructorDeclarationParentheses = SpaceBetweenEmptyConstructorDeclarationParentheses;
        other.SpaceBeforeConstructorDeclarationParameterComma = SpaceBeforeConstructorDeclarationParameterComma;
        other.SpaceAfterConstructorDeclarationParameterComma = SpaceAfterConstructorDeclarationParameterComma;
        other.SpaceWithinConstructorDeclarationParentheses = SpaceWithinConstructorDeclarationParentheses;
        other.NewLineBeforeConstructorInitializerColon = NewLineBeforeConstructorInitializerColon;
        other.NewLineAfterConstructorInitializerColon = NewLineAfterConstructorInitializerColon;
        other.SpaceBeforeIndexerDeclarationBracket = SpaceBeforeIndexerDeclarationBracket;
        other.SpaceWithinIndexerDeclarationBracket = SpaceWithinIndexerDeclarationBracket;
        other.SpaceBeforeIndexerDeclarationParameterComma = SpaceBeforeIndexerDeclarationParameterComma;
        other.SpaceAfterIndexerDeclarationParameterComma = SpaceAfterIndexerDeclarationParameterComma;
        other.SpaceBeforeDelegateDeclarationParentheses = SpaceBeforeDelegateDeclarationParentheses;
        other.SpaceBetweenEmptyDelegateDeclarationParentheses = SpaceBetweenEmptyDelegateDeclarationParentheses;
        other.SpaceBeforeDelegateDeclarationParameterComma = SpaceBeforeDelegateDeclarationParameterComma;
        other.SpaceAfterDelegateDeclarationParameterComma = SpaceAfterDelegateDeclarationParameterComma;
        other.SpaceWithinDelegateDeclarationParentheses = SpaceWithinDelegateDeclarationParentheses;
        other.SpaceBeforeNewParentheses = SpaceBeforeNewParentheses;
        other.SpaceBeforeIfParentheses = SpaceBeforeIfParentheses;
        other.SpaceBeforeWhileParentheses = SpaceBeforeWhileParentheses;
        other.SpaceBeforeForParentheses = SpaceBeforeForParentheses;
        other.SpaceBeforeForeachParentheses = SpaceBeforeForeachParentheses;
        other.SpaceBeforeCatchParentheses = SpaceBeforeCatchParentheses;
        other.SpaceBeforeSwitchParentheses = SpaceBeforeSwitchParentheses;
        other.SpaceBeforeLockParentheses = SpaceBeforeLockParentheses;
        other.SpaceBeforeUsingParentheses = SpaceBeforeUsingParentheses;
        other.SpaceAroundAssignment = SpaceAroundAssignment;
        other.SpaceAroundLogicalOperator = SpaceAroundLogicalOperator;
        other.SpaceAroundEqualityOperator = SpaceAroundEqualityOperator;
        other.SpaceAroundRelationalOperator = SpaceAroundRelationalOperator;
        other.SpaceAroundBitwiseOperator = SpaceAroundBitwiseOperator;
        other.SpaceAroundAdditiveOperator = SpaceAroundAdditiveOperator;
        other.SpaceAroundMultiplicativeOperator = SpaceAroundMultiplicativeOperator;
        other.SpaceAroundShiftOperator = SpaceAroundShiftOperator;
        other.SpaceAroundNullCoalescingOperator = SpaceAroundNullCoalescingOperator;
        other.SpaceAfterUnsafeAddressOfOperator = SpaceAfterUnsafeAddressOfOperator;
        other.SpaceAfterUnsafeAsteriskOfOperator = SpaceAfterUnsafeAsteriskOfOperator;
        other.SpaceAroundUnsafeArrowOperator = SpaceAroundUnsafeArrowOperator;
        other.SpacesWithinParentheses = SpacesWithinParentheses;
        other.SpacesWithinIfParentheses = SpacesWithinIfParentheses;
        other.SpacesWithinWhileParentheses = SpacesWithinWhileParentheses;
        other.SpacesWithinForParentheses = SpacesWithinForParentheses;
        other.SpacesWithinForeachParentheses = SpacesWithinForeachParentheses;
        other.SpacesWithinCatchParentheses = SpacesWithinCatchParentheses;
        other.SpacesWithinSwitchParentheses = SpacesWithinSwitchParentheses;
        other.SpacesWithinLockParentheses = SpacesWithinLockParentheses;
        other.SpacesWithinUsingParentheses = SpacesWithinUsingParentheses;
        other.SpacesWithinCastParentheses = SpacesWithinCastParentheses;
        other.SpacesWithinSizeOfParentheses = SpacesWithinSizeOfParentheses;
        other.SpaceBeforeSizeOfParentheses = SpaceBeforeSizeOfParentheses;
        other.SpacesWithinTypeOfParentheses = SpacesWithinTypeOfParentheses;
        other.SpacesWithinNewParentheses = SpacesWithinNewParentheses;
        other.SpacesBetweenEmptyNewParentheses = SpacesBetweenEmptyNewParentheses;
        other.SpaceBeforeNewParameterComma = SpaceBeforeNewParameterComma;
        other.SpaceAfterNewParameterComma = SpaceAfterNewParameterComma;
        other.SpaceBeforeTypeOfParentheses = SpaceBeforeTypeOfParentheses;
        other.SpacesWithinCheckedExpressionParantheses = SpacesWithinCheckedExpressionParantheses;
        other.SpaceBeforeConditionalOperatorCondition = SpaceBeforeConditionalOperatorCondition;
        other.SpaceAfterConditionalOperatorCondition = SpaceAfterConditionalOperatorCondition;
        other.SpaceBeforeConditionalOperatorSeparator = SpaceBeforeConditionalOperatorSeparator;
        other.SpaceAfterConditionalOperatorSeparator = SpaceAfterConditionalOperatorSeparator;
        other.SpacesWithinBrackets = SpacesWithinBrackets;
        other.SpacesBeforeBrackets = SpacesBeforeBrackets;
        other.SpaceBeforeBracketComma = SpaceBeforeBracketComma;
        other.SpaceAfterBracketComma = SpaceAfterBracketComma;
        other.SpaceBeforeForSemicolon = SpaceBeforeForSemicolon;
        other.SpaceAfterForSemicolon = SpaceAfterForSemicolon;
        other.SpaceAfterTypecast = SpaceAfterTypecast;
        other.SpaceBeforeArrayDeclarationBrackets = SpaceBeforeArrayDeclarationBrackets;
        other.SpaceInNamedArgumentAfterDoubleColon = SpaceInNamedArgumentAfterDoubleColon;
        other.RemoveEndOfLineWhiteSpace = RemoveEndOfLineWhiteSpace;
        other.SpaceBeforeSemicolon = SpaceBeforeSemicolon;
        other.MinimumBlankLinesBeforeUsings = MinimumBlankLinesBeforeUsings;
        other.MinimumBlankLinesAfterUsings = MinimumBlankLinesAfterUsings;
        other.MinimumBlankLinesBeforeFirstDeclaration = MinimumBlankLinesBeforeFirstDeclaration;
        other.MinimumBlankLinesBetweenTypes = MinimumBlankLinesBetweenTypes;
        other.MinimumBlankLinesBetweenFields = MinimumBlankLinesBetweenFields;
        other.MinimumBlankLinesBetweenEventFields = MinimumBlankLinesBetweenEventFields;
        other.MinimumBlankLinesBetweenMembers = MinimumBlankLinesBetweenMembers;
        other.MinimumBlankLinesAroundRegion = MinimumBlankLinesAroundRegion;
        other.MinimumBlankLinesInsideRegion = MinimumBlankLinesInsideRegion;
        other.KeepCommentsAtFirstColumn = KeepCommentsAtFirstColumn;
        other.ArrayInitializerWrapping = ArrayInitializerWrapping;
        other.ArrayInitializerBraceStyle = ArrayInitializerBraceStyle;
        other.ChainedMethodCallWrapping = ChainedMethodCallWrapping;
        other.MethodCallArgumentWrapping = MethodCallArgumentWrapping;
        other.NewLineAferMethodCallOpenParentheses = NewLineAferMethodCallOpenParentheses;
        other.MethodCallClosingParenthesesOnNewLine = MethodCallClosingParenthesesOnNewLine;
        other.IndexerArgumentWrapping = IndexerArgumentWrapping;
        other.NewLineAferIndexerOpenBracket = NewLineAferIndexerOpenBracket;
        other.IndexerClosingBracketOnNewLine = IndexerClosingBracketOnNewLine;
        other.MethodDeclarationParameterWrapping = MethodDeclarationParameterWrapping;
        other.NewLineAferMethodDeclarationOpenParentheses = NewLineAferMethodDeclarationOpenParentheses;
        other.MethodDeclarationClosingParenthesesOnNewLine = MethodDeclarationClosingParenthesesOnNewLine;
        other.IndexerDeclarationParameterWrapping = IndexerDeclarationParameterWrapping;
        other.NewLineAferIndexerDeclarationOpenBracket = NewLineAferIndexerDeclarationOpenBracket;
        other.IndexerDeclarationClosingBracketOnNewLine = IndexerDeclarationClosingBracketOnNewLine;
        other.AlignToFirstIndexerArgument = AlignToFirstIndexerArgument;
        other.AlignToFirstIndexerDeclarationParameter = AlignToFirstIndexerDeclarationParameter;
        other.AlignToFirstMethodCallArgument = AlignToFirstMethodCallArgument;
        other.AlignToFirstMethodDeclarationParameter = AlignToFirstMethodDeclarationParameter;
        other.NewLineBeforeNewQueryClause = NewLineBeforeNewQueryClause;
        other.UsingPlacement = UsingPlacement;
        return other;
    }

    public bool Equals(CSharpFormattingOptions other)
    {
        if (other == null)
            return false;

        if (IndentNamespaceBody != other.IndentNamespaceBody) return false;
        if (IndentClassBody != other.IndentClassBody) return false;
        if (IndentInterfaceBody != other.IndentInterfaceBody) return false;
        if (IndentStructBody != other.IndentStructBody) return false;
        if (IndentEnumBody != other.IndentEnumBody) return false;
        if (IndentMethodBody != other.IndentMethodBody) return false;
        if (IndentPropertyBody != other.IndentPropertyBody) return false;
        if (IndentEventBody != other.IndentEventBody) return false;
        if (IndentBlocks != other.IndentBlocks) return false;
        if (IndentSwitchBody != other.IndentSwitchBody) return false;
        if (IndentCaseBody != other.IndentCaseBody) return false;
        if (IndentBreakStatements != other.IndentBreakStatements) return false;
        if (AlignEmbeddedStatements != other.AlignEmbeddedStatements) return false;
        if (AlignElseInIfStatements != other.AlignElseInIfStatements) return false;
        if (AutoPropertyFormatting != other.AutoPropertyFormatting) return false;
        if (SimplePropertyFormatting != other.SimplePropertyFormatting) return false;
        if (EmptyLineFormatting != other.EmptyLineFormatting) return false;
        if (IndentPreprocessorDirectives != other.IndentPreprocessorDirectives) return false;
        if (AlignToMemberReferenceDot != other.AlignToMemberReferenceDot) return false;
        if (IndentBlocksInsideExpressions != other.IndentBlocksInsideExpressions) return false;
        if (NamespaceBraceStyle != other.NamespaceBraceStyle) return false;
        if (ClassBraceStyle != other.ClassBraceStyle) return false;
        if (InterfaceBraceStyle != other.InterfaceBraceStyle) return false;
        if (StructBraceStyle != other.StructBraceStyle) return false;
        if (EnumBraceStyle != other.EnumBraceStyle) return false;
        if (MethodBraceStyle != other.MethodBraceStyle) return false;
        if (AnonymousMethodBraceStyle != other.AnonymousMethodBraceStyle) return false;
        if (ConstructorBraceStyle != other.ConstructorBraceStyle) return false;
        if (DestructorBraceStyle != other.DestructorBraceStyle) return false;
        if (PropertyBraceStyle != other.PropertyBraceStyle) return false;
        if (PropertyGetBraceStyle != other.PropertyGetBraceStyle) return false;
        if (PropertySetBraceStyle != other.PropertySetBraceStyle) return false;
        if (SimpleGetBlockFormatting != other.SimpleGetBlockFormatting) return false;
        if (SimpleSetBlockFormatting != other.SimpleSetBlockFormatting) return false;
        if (EventBraceStyle != other.EventBraceStyle) return false;
        if (EventAddBraceStyle != other.EventAddBraceStyle) return false;
        if (EventRemoveBraceStyle != other.EventRemoveBraceStyle) return false;
        if (AllowEventAddBlockInline != other.AllowEventAddBlockInline) return false;
        if (AllowEventRemoveBlockInline != other.AllowEventRemoveBlockInline) return false;
        if (StatementBraceStyle != other.StatementBraceStyle) return false;
        if (AllowIfBlockInline != other.AllowIfBlockInline) return false;
        if (AllowOneLinedArrayInitialziers != other.AllowOneLinedArrayInitialziers) return false;
        if (ElseNewLinePlacement != other.ElseNewLinePlacement) return false;
        if (ElseIfNewLinePlacement != other.ElseIfNewLinePlacement) return false;
        if (CatchNewLinePlacement != other.CatchNewLinePlacement) return false;
        if (FinallyNewLinePlacement != other.FinallyNewLinePlacement) return false;
        if (WhileNewLinePlacement != other.WhileNewLinePlacement) return false;
        if (EmbeddedStatementPlacement != other.EmbeddedStatementPlacement) return false;
        if (SpaceBeforeMethodDeclarationParentheses != other.SpaceBeforeMethodDeclarationParentheses) return false;
        if (SpaceBetweenEmptyMethodDeclarationParentheses != other.SpaceBetweenEmptyMethodDeclarationParentheses) return false;
        if (SpaceBeforeMethodDeclarationParameterComma != other.SpaceBeforeMethodDeclarationParameterComma) return false;
        if (SpaceAfterMethodDeclarationParameterComma != other.SpaceAfterMethodDeclarationParameterComma) return false;
        if (SpaceWithinMethodDeclarationParentheses != other.SpaceWithinMethodDeclarationParentheses) return false;
        if (SpaceBeforeMethodCallParentheses != other.SpaceBeforeMethodCallParentheses) return false;
        if (SpaceBetweenEmptyMethodCallParentheses != other.SpaceBetweenEmptyMethodCallParentheses) return false;
        if (SpaceBeforeMethodCallParameterComma != other.SpaceBeforeMethodCallParameterComma) return false;
        if (SpaceAfterMethodCallParameterComma != other.SpaceAfterMethodCallParameterComma) return false;
        if (SpaceWithinMethodCallParentheses != other.SpaceWithinMethodCallParentheses) return false;
        if (SpaceBeforeFieldDeclarationComma != other.SpaceBeforeFieldDeclarationComma) return false;
        if (SpaceAfterFieldDeclarationComma != other.SpaceAfterFieldDeclarationComma) return false;
        if (SpaceBeforeLocalVariableDeclarationComma != other.SpaceBeforeLocalVariableDeclarationComma) return false;
        if (SpaceAfterLocalVariableDeclarationComma != other.SpaceAfterLocalVariableDeclarationComma) return false;
        if (SpaceBeforeConstructorDeclarationParentheses != other.SpaceBeforeConstructorDeclarationParentheses) return false;
        if (SpaceBetweenEmptyConstructorDeclarationParentheses != other.SpaceBetweenEmptyConstructorDeclarationParentheses) return false;
        if (SpaceBeforeConstructorDeclarationParameterComma != other.SpaceBeforeConstructorDeclarationParameterComma) return false;
        if (SpaceAfterConstructorDeclarationParameterComma != other.SpaceAfterConstructorDeclarationParameterComma) return false;
        if (SpaceWithinConstructorDeclarationParentheses != other.SpaceWithinConstructorDeclarationParentheses) return false;
        if (NewLineBeforeConstructorInitializerColon != other.NewLineBeforeConstructorInitializerColon) return false;
        if (NewLineAfterConstructorInitializerColon != other.NewLineAfterConstructorInitializerColon) return false;
        if (SpaceBeforeIndexerDeclarationBracket != other.SpaceBeforeIndexerDeclarationBracket) return false;
        if (SpaceWithinIndexerDeclarationBracket != other.SpaceWithinIndexerDeclarationBracket) return false;
        if (SpaceBeforeIndexerDeclarationParameterComma != other.SpaceBeforeIndexerDeclarationParameterComma) return false;
        if (SpaceAfterIndexerDeclarationParameterComma != other.SpaceAfterIndexerDeclarationParameterComma) return false;
        if (SpaceBeforeDelegateDeclarationParentheses != other.SpaceBeforeDelegateDeclarationParentheses) return false;
        if (SpaceBetweenEmptyDelegateDeclarationParentheses != other.SpaceBetweenEmptyDelegateDeclarationParentheses) return false;
        if (SpaceBeforeDelegateDeclarationParameterComma != other.SpaceBeforeDelegateDeclarationParameterComma) return false;
        if (SpaceAfterDelegateDeclarationParameterComma != other.SpaceAfterDelegateDeclarationParameterComma) return false;
        if (SpaceWithinDelegateDeclarationParentheses != other.SpaceWithinDelegateDeclarationParentheses) return false;
        if (SpaceBeforeNewParentheses != other.SpaceBeforeNewParentheses) return false;
        if (SpaceBeforeIfParentheses != other.SpaceBeforeIfParentheses) return false;
        if (SpaceBeforeWhileParentheses != other.SpaceBeforeWhileParentheses) return false;
        if (SpaceBeforeForParentheses != other.SpaceBeforeForParentheses) return false;
        if (SpaceBeforeForeachParentheses != other.SpaceBeforeForeachParentheses) return false;
        if (SpaceBeforeCatchParentheses != other.SpaceBeforeCatchParentheses) return false;
        if (SpaceBeforeSwitchParentheses != other.SpaceBeforeSwitchParentheses) return false;
        if (SpaceBeforeLockParentheses != other.SpaceBeforeLockParentheses) return false;
        if (SpaceBeforeUsingParentheses != other.SpaceBeforeUsingParentheses) return false;
        if (SpaceAroundAssignment != other.SpaceAroundAssignment) return false;
        if (SpaceAroundLogicalOperator != other.SpaceAroundLogicalOperator) return false;
        if (SpaceAroundEqualityOperator != other.SpaceAroundEqualityOperator) return false;
        if (SpaceAroundRelationalOperator != other.SpaceAroundRelationalOperator) return false;
        if (SpaceAroundBitwiseOperator != other.SpaceAroundBitwiseOperator) return false;
        if (SpaceAroundAdditiveOperator != other.SpaceAroundAdditiveOperator) return false;
        if (SpaceAroundMultiplicativeOperator != other.SpaceAroundMultiplicativeOperator) return false;
        if (SpaceAroundShiftOperator != other.SpaceAroundShiftOperator) return false;
        if (SpaceAroundNullCoalescingOperator != other.SpaceAroundNullCoalescingOperator) return false;
        if (SpaceAfterUnsafeAddressOfOperator != other.SpaceAfterUnsafeAddressOfOperator) return false;
        if (SpaceAfterUnsafeAsteriskOfOperator != other.SpaceAfterUnsafeAsteriskOfOperator) return false;
        if (SpaceAroundUnsafeArrowOperator != other.SpaceAroundUnsafeArrowOperator) return false;
        if (SpacesWithinParentheses != other.SpacesWithinParentheses) return false;
        if (SpacesWithinIfParentheses != other.SpacesWithinIfParentheses) return false;
        if (SpacesWithinWhileParentheses != other.SpacesWithinWhileParentheses) return false;
        if (SpacesWithinForParentheses != other.SpacesWithinForParentheses) return false;
        if (SpacesWithinForeachParentheses != other.SpacesWithinForeachParentheses) return false;
        if (SpacesWithinCatchParentheses != other.SpacesWithinCatchParentheses) return false;
        if (SpacesWithinSwitchParentheses != other.SpacesWithinSwitchParentheses) return false;
        if (SpacesWithinLockParentheses != other.SpacesWithinLockParentheses) return false;
        if (SpacesWithinUsingParentheses != other.SpacesWithinUsingParentheses) return false;
        if (SpacesWithinCastParentheses != other.SpacesWithinCastParentheses) return false;
        if (SpacesWithinSizeOfParentheses != other.SpacesWithinSizeOfParentheses) return false;
        if (SpaceBeforeSizeOfParentheses != other.SpaceBeforeSizeOfParentheses) return false;
        if (SpacesWithinTypeOfParentheses != other.SpacesWithinTypeOfParentheses) return false;
        if (SpacesWithinNewParentheses != other.SpacesWithinNewParentheses) return false;
        if (SpacesBetweenEmptyNewParentheses != other.SpacesBetweenEmptyNewParentheses) return false;
        if (SpaceBeforeNewParameterComma != other.SpaceBeforeNewParameterComma) return false;
        if (SpaceAfterNewParameterComma != other.SpaceAfterNewParameterComma) return false;
        if (SpaceBeforeTypeOfParentheses != other.SpaceBeforeTypeOfParentheses) return false;
        if (SpacesWithinCheckedExpressionParantheses != other.SpacesWithinCheckedExpressionParantheses) return false;
        if (SpaceBeforeConditionalOperatorCondition != other.SpaceBeforeConditionalOperatorCondition) return false;
        if (SpaceAfterConditionalOperatorCondition != other.SpaceAfterConditionalOperatorCondition) return false;
        if (SpaceBeforeConditionalOperatorSeparator != other.SpaceBeforeConditionalOperatorSeparator) return false;
        if (SpaceAfterConditionalOperatorSeparator != other.SpaceAfterConditionalOperatorSeparator) return false;
        if (SpacesWithinBrackets != other.SpacesWithinBrackets) return false;
        if (SpacesBeforeBrackets != other.SpacesBeforeBrackets) return false;
        if (SpaceBeforeBracketComma != other.SpaceBeforeBracketComma) return false;
        if (SpaceAfterBracketComma != other.SpaceAfterBracketComma) return false;
        if (SpaceBeforeForSemicolon != other.SpaceBeforeForSemicolon) return false;
        if (SpaceAfterForSemicolon != other.SpaceAfterForSemicolon) return false;
        if (SpaceAfterTypecast != other.SpaceAfterTypecast) return false;
        if (SpaceBeforeArrayDeclarationBrackets != other.SpaceBeforeArrayDeclarationBrackets) return false;
        if (SpaceInNamedArgumentAfterDoubleColon != other.SpaceInNamedArgumentAfterDoubleColon) return false;
        if (RemoveEndOfLineWhiteSpace != other.RemoveEndOfLineWhiteSpace) return false;
        if (SpaceBeforeSemicolon != other.SpaceBeforeSemicolon) return false;
        if (MinimumBlankLinesBeforeUsings != other.MinimumBlankLinesBeforeUsings) return false;
        if (MinimumBlankLinesAfterUsings != other.MinimumBlankLinesAfterUsings) return false;
        if (MinimumBlankLinesBeforeFirstDeclaration != other.MinimumBlankLinesBeforeFirstDeclaration) return false;
        if (MinimumBlankLinesBetweenTypes != other.MinimumBlankLinesBetweenTypes) return false;
        if (MinimumBlankLinesBetweenFields != other.MinimumBlankLinesBetweenFields) return false;
        if (MinimumBlankLinesBetweenEventFields != other.MinimumBlankLinesBetweenEventFields) return false;
        if (MinimumBlankLinesBetweenMembers != other.MinimumBlankLinesBetweenMembers) return false;
        if (MinimumBlankLinesAroundRegion != other.MinimumBlankLinesAroundRegion) return false;
        if (MinimumBlankLinesInsideRegion != other.MinimumBlankLinesInsideRegion) return false;
        if (KeepCommentsAtFirstColumn != other.KeepCommentsAtFirstColumn) return false;
        if (ArrayInitializerWrapping != other.ArrayInitializerWrapping) return false;
        if (ArrayInitializerBraceStyle != other.ArrayInitializerBraceStyle) return false;
        if (ChainedMethodCallWrapping != other.ChainedMethodCallWrapping) return false;
        if (MethodCallArgumentWrapping != other.MethodCallArgumentWrapping) return false;
        if (NewLineAferMethodCallOpenParentheses != other.NewLineAferMethodCallOpenParentheses) return false;
        if (MethodCallClosingParenthesesOnNewLine != other.MethodCallClosingParenthesesOnNewLine) return false;
        if (IndexerArgumentWrapping != other.IndexerArgumentWrapping) return false;
        if (NewLineAferIndexerOpenBracket != other.NewLineAferIndexerOpenBracket) return false;
        if (IndexerClosingBracketOnNewLine != other.IndexerClosingBracketOnNewLine) return false;
        if (MethodDeclarationParameterWrapping != other.MethodDeclarationParameterWrapping) return false;
        if (NewLineAferMethodDeclarationOpenParentheses != other.NewLineAferMethodDeclarationOpenParentheses) return false;
        if (MethodDeclarationClosingParenthesesOnNewLine != other.MethodDeclarationClosingParenthesesOnNewLine) return false;
        if (IndexerDeclarationParameterWrapping != other.IndexerDeclarationParameterWrapping) return false;
        if (NewLineAferIndexerDeclarationOpenBracket != other.NewLineAferIndexerDeclarationOpenBracket) return false;
        if (IndexerDeclarationClosingBracketOnNewLine != other.IndexerDeclarationClosingBracketOnNewLine) return false;
        if (AlignToFirstIndexerArgument != other.AlignToFirstIndexerArgument) return false;
        if (AlignToFirstIndexerDeclarationParameter != other.AlignToFirstIndexerDeclarationParameter) return false;
        if (AlignToFirstMethodCallArgument != other.AlignToFirstMethodCallArgument) return false;
        if (AlignToFirstMethodDeclarationParameter != other.AlignToFirstMethodDeclarationParameter) return false;
        if (NewLineBeforeNewQueryClause != other.NewLineBeforeNewQueryClause) return false;
        if (UsingPlacement != other.UsingPlacement) return false;

        return true;
    }
}