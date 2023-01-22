//
// method.cs: Method based declarations
//
// Authors: Miguel de Icaza (miguel@gnu.org)
//          Martin Baulig (martin@ximian.com)
//          Marek Safar (marek.safar@gmail.com)
//
// Dual licensed under the terms of the MIT X11 or GNU GPL
//
// Copyright 2001, 2002, 2003 Ximian, Inc (http://www.ximian.com)
// Copyright 2004-2008 Novell, Inc
// Copyright 2011 Xamarin Inc.
//

using System;
using System.Linq;

namespace ICSharpCode.NRefactory.MonoCSharp;

static class Operator
{
    private static readonly string[][] Names;

    static Operator()
    {
        Names = new string[(int)OpType.TOP][];
        Names[(int)OpType.LogicalNot] = new[] { "!", "op_LogicalNot" };
        Names[(int)OpType.OnesComplement] = new[] { "~", "op_OnesComplement" };
        Names[(int)OpType.Increment] = new[] { "++", "op_Increment" };
        Names[(int)OpType.Decrement] = new[] { "--", "op_Decrement" };
        Names[(int)OpType.True] = new[] { "true", "op_True" };
        Names[(int)OpType.False] = new[] { "false", "op_False" };
        Names[(int)OpType.Addition] = new[] { "+", "op_Addition" };
        Names[(int)OpType.Subtraction] = new[] { "-", "op_Subtraction" };
        Names[(int)OpType.UnaryPlus] = new[] { "+", "op_UnaryPlus" };
        Names[(int)OpType.UnaryNegation] = new[] { "-", "op_UnaryNegation" };
        Names[(int)OpType.Multiply] = new[] { "*", "op_Multiply" };
        Names[(int)OpType.Division] = new[] { "/", "op_Division" };
        Names[(int)OpType.Modulus] = new[] { "%", "op_Modulus" };
        Names[(int)OpType.BitwiseAnd] = new[] { "&", "op_BitwiseAnd" };
        Names[(int)OpType.BitwiseOr] = new[] { "|", "op_BitwiseOr" };
        Names[(int)OpType.ExclusiveOr] = new[] { "^", "op_ExclusiveOr" };
        Names[(int)OpType.LeftShift] = new[] { "<<", "op_LeftShift" };
        Names[(int)OpType.RightShift] = new[] { ">>", "op_RightShift" };
        Names[(int)OpType.Equality] = new[] { "==", "op_Equality" };
        Names[(int)OpType.Inequality] = new[] { "!=", "op_Inequality" };
        Names[(int)OpType.GreaterThan] = new[] { ">", "op_GreaterThan" };
        Names[(int)OpType.LessThan] = new[] { "<", "op_LessThan" };
        Names[(int)OpType.GreaterThanOrEqual] = new[] { ">=", "op_GreaterThanOrEqual" };
        Names[(int)OpType.LessThanOrEqual] = new[] { "<=", "op_LessThanOrEqual" };
        Names[(int)OpType.Implicit] = new[] { "implicit", "op_Implicit" };
        Names[(int)OpType.Explicit] = new[] { "explicit", "op_Explicit" };
        Names[(int)OpType.Is] = new[] { "is", "op_Is" };
    }

    public static string GetName(OpType ot) => Names[(int)ot][0];

    public static string GetMetadataName(OpType ot) => Names[(int)ot][1];

    public static string GetName(string metadataName) => Names.FirstOrDefault(array => array[1] == metadataName)?[0];

    public static string GetMetadataName(string name) => Names.FirstOrDefault(array => array[0] == name)?[1];

    public static OpType? GetType(string metadataName) =>
        (OpType?)Names.Select((array, index) => (Array: array, Index: index))
            .Cast<(string[] Array, int Index)?>()
            .FirstOrDefault(t => t?.Array[1] == metadataName)?
            .Index;

    public enum OpType : byte
    {
        // Unary operators
        LogicalNot,
        OnesComplement,
        Increment,
        Decrement,
        True,
        False,

        // Unary and Binary operators
        Addition,
        Subtraction,

        UnaryPlus,
        UnaryNegation,

        // Binary operators
        Multiply,
        Division,
        Modulus,
        BitwiseAnd,
        BitwiseOr,
        ExclusiveOr,
        LeftShift,
        RightShift,
        Equality,
        Inequality,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,

        // Implicit and Explicit
        Implicit,
        Explicit,

        // Pattern matching
        Is,

        // Just because of enum
        TOP
    };
}