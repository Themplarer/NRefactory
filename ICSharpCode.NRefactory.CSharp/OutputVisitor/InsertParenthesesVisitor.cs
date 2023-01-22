// Copyright (c) 2010-2013 AlphaSierraPapa for the SharpDevelop Team
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;

namespace ICSharpCode.NRefactory.CSharp;

/// <summary>
/// Inserts the parentheses into the AST that are needed to ensure the AST can be printed correctly.
/// For example, if the AST contains
/// BinaryOperatorExpresson(2, Mul, BinaryOperatorExpression(1, Add, 1))); printing that AST
/// would incorrectly result in "2 * 1 + 1". By running InsertParenthesesVisitor, the necessary
/// parentheses are inserted: "2 * (1 + 1)".
/// </summary>
public class InsertParenthesesVisitor : DepthFirstAstVisitor
{
    /// <summary>
    /// Gets/Sets whether the visitor should insert parentheses to make the code better looking.
    /// If this property is false, it will insert parentheses only where strictly required by the language spec.
    /// </summary>
    public bool InsertParenthesesForReadability { get; set; }

    /// <summary>
    /// Gets the row number in the C# 4.0 spec operator precedence table.
    /// </summary>
    // Note: the operator precedence table on MSDN is incorrect
    // Not part of the table in the C# spec, but we need to ensure that queries within
    // primary expressions get parenthesized.
    private static PrecedenceLevel GetPrecedence(Expression expr) =>
        expr switch
        {
            QueryExpression or LambdaExpression => PrecedenceLevel.QueryOrLambda,
            UnaryOperatorExpression uoe =>
                uoe.Operator is UnaryOperatorType.PostDecrement or UnaryOperatorType.PostIncrement
                    ? PrecedenceLevel.Primary
                    : PrecedenceLevel.Unary,
            CastExpression => PrecedenceLevel.Unary,
            PrimitiveExpression primitive =>
                primitive.Value is < 0 or long and < 0 or float and < 0 or double and < 0 or decimal and < 0
                    ? PrecedenceLevel.Unary
                    : PrecedenceLevel.Primary,
            BinaryOperatorExpression boe => GetPrecedenceLevelForBinaryOperator(boe.Operator),
            IsExpression or AsExpression => PrecedenceLevel.RelationalAndTypeTesting,
            ConditionalExpression or DirectionExpression => PrecedenceLevel.Conditional,
            AssignmentExpression => PrecedenceLevel.Assignment,
            _ => PrecedenceLevel.Primary // anything else: primary expression
        };

    private static PrecedenceLevel GetPrecedenceLevelForBinaryOperator(BinaryOperatorType type) =>
        type switch
        {
            BinaryOperatorType.Multiply or BinaryOperatorType.Divide or BinaryOperatorType.Modulus => PrecedenceLevel.Multiplicative,
            BinaryOperatorType.Add or BinaryOperatorType.Subtract => PrecedenceLevel.Additive,
            BinaryOperatorType.ShiftLeft or BinaryOperatorType.ShiftRight => PrecedenceLevel.Shift,
            BinaryOperatorType.GreaterThan or BinaryOperatorType.GreaterThanOrEqual or BinaryOperatorType.LessThan or BinaryOperatorType.LessThanOrEqual
                => PrecedenceLevel.RelationalAndTypeTesting,
            BinaryOperatorType.Equality or BinaryOperatorType.InEquality => PrecedenceLevel.Equality,
            BinaryOperatorType.BitwiseAnd => PrecedenceLevel.BitwiseAnd,
            BinaryOperatorType.ExclusiveOr => PrecedenceLevel.ExclusiveOr,
            BinaryOperatorType.BitwiseOr => PrecedenceLevel.BitwiseOr,
            BinaryOperatorType.ConditionalAnd => PrecedenceLevel.ConditionalAnd,
            BinaryOperatorType.ConditionalOr => PrecedenceLevel.ConditionalOr,
            BinaryOperatorType.NullCoalescing => PrecedenceLevel.NullCoalescing,
            _ => throw new NotSupportedException("Invalid value for BinaryOperatorType")
        };

    /// <summary>
    /// Parenthesizes the expression if it does not have the minimum required precedence.
    /// </summary>
    private static void ParenthesizeIfRequired(Expression expr, PrecedenceLevel minimumPrecedence)
    {
        if (GetPrecedence(expr) < minimumPrecedence)
            Parenthesize(expr);
    }

    private static void Parenthesize(Expression expr) => expr.ReplaceWith(e => new ParenthesizedExpression { Expression = e });

    // Primary expressions
    public override void VisitMemberReferenceExpression(MemberReferenceExpression memberReferenceExpression)
    {
        ParenthesizeIfRequired(memberReferenceExpression.Target, PrecedenceLevel.Primary);
        base.VisitMemberReferenceExpression(memberReferenceExpression);
    }

    public override void VisitPointerReferenceExpression(PointerReferenceExpression pointerReferenceExpression)
    {
        ParenthesizeIfRequired(pointerReferenceExpression.Target, PrecedenceLevel.Primary);
        base.VisitPointerReferenceExpression(pointerReferenceExpression);
    }

    public override void VisitInvocationExpression(InvocationExpression invocationExpression)
    {
        ParenthesizeIfRequired(invocationExpression.Target, PrecedenceLevel.Primary);
        base.VisitInvocationExpression(invocationExpression);
    }

    public override void VisitIndexerExpression(IndexerExpression indexerExpression)
    {
        ParenthesizeIfRequired(indexerExpression.Target, PrecedenceLevel.Primary);

        // require parentheses for "(new int[1])[0]"
        if (indexerExpression.Target is ArrayCreateExpression ace && (InsertParenthesesForReadability || ace.Initializer.IsNull))
            Parenthesize(indexerExpression.Target);

        base.VisitIndexerExpression(indexerExpression);
    }

    // Unary expressions
    public override void VisitUnaryOperatorExpression(UnaryOperatorExpression unaryOperatorExpression)
    {
        ParenthesizeIfRequired(unaryOperatorExpression.Expression, GetPrecedence(unaryOperatorExpression));

        if (unaryOperatorExpression.Expression is UnaryOperatorExpression child && InsertParenthesesForReadability)
            Parenthesize(child);

        base.VisitUnaryOperatorExpression(unaryOperatorExpression);
    }

    public override void VisitCastExpression(CastExpression castExpression)
    {
        // Even in readability mode, don't parenthesize casts of casts.
        if (castExpression.Expression is not CastExpression)
            ParenthesizeIfRequired(castExpression.Expression, InsertParenthesesForReadability ? PrecedenceLevel.NullableRewrap : PrecedenceLevel.Unary);

        // There's a nasty issue in the C# grammar: cast expressions including certain operators are ambiguous in some cases
        // "(int)-1" is fine, but "(A)-b" is not a cast.
        if (castExpression.Expression is UnaryOperatorExpression { Operator: not (UnaryOperatorType.BitNot or UnaryOperatorType.Not) } &&
            TypeCanBeMisinterpretedAsExpression(castExpression.Type))
            Parenthesize(castExpression.Expression);

        // The above issue can also happen with PrimitiveExpressions representing negative values:
        if (castExpression.Expression is PrimitiveExpression { Value: { } } pe && TypeCanBeMisinterpretedAsExpression(castExpression.Type))
            Parenthesize(castExpression, pe);

        base.VisitCastExpression(castExpression);
    }

    private static void Parenthesize(CastExpression castExpression, PrimitiveExpression pe)
    {
        switch (Type.GetTypeCode(pe.Value.GetType()))
        {
            case TypeCode.SByte:
                if ((sbyte)pe.Value < 0)
                    Parenthesize(castExpression.Expression);

                break;
            case TypeCode.Int16:
                if ((short)pe.Value < 0)
                    Parenthesize(castExpression.Expression);

                break;
            case TypeCode.Int32:
                if ((int)pe.Value < 0)
                    Parenthesize(castExpression.Expression);

                break;
            case TypeCode.Int64:
                if ((long)pe.Value < 0)
                    Parenthesize(castExpression.Expression);

                break;
            case TypeCode.Single:
                if ((float)pe.Value < 0)
                    Parenthesize(castExpression.Expression);

                break;
            case TypeCode.Double:
                if ((double)pe.Value < 0)
                    Parenthesize(castExpression.Expression);

                break;
            case TypeCode.Decimal:
                if ((decimal)pe.Value < 0)
                    Parenthesize(castExpression.Expression);

                break;
        }
    }

    private static bool TypeCanBeMisinterpretedAsExpression(AstType type) =>
        // SimpleTypes can always be misinterpreted as IdentifierExpressions
        // MemberTypes can be misinterpreted as MemberReferenceExpressions if they don't use double colon
        // PrimitiveTypes or ComposedTypes can never be misinterpreted as expressions.
        type is MemberType mt
            ? !mt.IsDoubleColon
            : type is SimpleType;

    // Binary Operators
    public override void VisitBinaryOperatorExpression(BinaryOperatorExpression binaryOperatorExpression)
    {
        var precedence = GetPrecedence(binaryOperatorExpression);

        if (binaryOperatorExpression.Operator == BinaryOperatorType.NullCoalescing)
        {
            if (InsertParenthesesForReadability)
            {
                ParenthesizeIfRequired(binaryOperatorExpression.Left, PrecedenceLevel.NullableRewrap);
                ParenthesizeIfRequired(binaryOperatorExpression.Right,
                    GetBinaryOperatorType(binaryOperatorExpression.Right) == BinaryOperatorType.NullCoalescing
                        ? precedence
                        : PrecedenceLevel.NullableRewrap);
            }
            else
            {
                // ?? is right-associative
                ParenthesizeIfRequired(binaryOperatorExpression.Left, precedence + 1);
                ParenthesizeIfRequired(binaryOperatorExpression.Right, precedence);
            }
        }
        else
        {
            if (InsertParenthesesForReadability && precedence < PrecedenceLevel.Equality)
            {
                // In readable mode, boost the priority of the left-hand side if the operator
                // there isn't the same as the operator on this expression.
                var boostTo = IsBitwise(binaryOperatorExpression.Operator) ? PrecedenceLevel.Unary : PrecedenceLevel.Equality;

                ParenthesizeIfRequired(binaryOperatorExpression.Left,
                    GetBinaryOperatorType(binaryOperatorExpression.Left) == binaryOperatorExpression.Operator
                        ? precedence
                        : boostTo);
                ParenthesizeIfRequired(binaryOperatorExpression.Right, boostTo);
            }
            else
            {
                // all other binary operators are left-associative
                ParenthesizeIfRequired(binaryOperatorExpression.Left, precedence);
                ParenthesizeIfRequired(binaryOperatorExpression.Right, precedence + 1);
            }
        }

        base.VisitBinaryOperatorExpression(binaryOperatorExpression);
    }

    private static bool IsBitwise(BinaryOperatorType op) =>
        op is BinaryOperatorType.BitwiseAnd or BinaryOperatorType.BitwiseOr or BinaryOperatorType.ExclusiveOr;

    private static BinaryOperatorType? GetBinaryOperatorType(Expression expr) => (expr as BinaryOperatorExpression)?.Operator;

    public override void VisitIsExpression(IsExpression isExpression)
    {
        // few people know the precedence of 'is', so always put parentheses in nice-looking mode.
        ParenthesizeIfRequired(isExpression.Expression,
            InsertParenthesesForReadability ? PrecedenceLevel.NullableRewrap : PrecedenceLevel.RelationalAndTypeTesting);
        base.VisitIsExpression(isExpression);
    }

    public override void VisitAsExpression(AsExpression asExpression)
    {
        // few people know the precedence of 'as', so always put parentheses in nice-looking mode.
        ParenthesizeIfRequired(asExpression.Expression,
            InsertParenthesesForReadability ? PrecedenceLevel.NullableRewrap : PrecedenceLevel.RelationalAndTypeTesting);
        base.VisitAsExpression(asExpression);
    }

    // Conditional operator
    public override void VisitConditionalExpression(ConditionalExpression conditionalExpression)
    {
        // Associativity here is a bit tricky:
        // (a ? b : c ? d : e) == (a ? b : (c ? d : e))
        // (a ? b ? c : d : e) == (a ? (b ? c : d) : e)
        // Only ((a ? b : c) ? d : e) strictly needs the additional parentheses
        if (InsertParenthesesForReadability && !IsConditionalRefExpression(conditionalExpression))
        {
            // Precedence of ?: can be confusing; so always put parentheses in nice-looking mode.
            ParenthesizeIfRequired(conditionalExpression.Condition, PrecedenceLevel.NullableRewrap);
            ParenthesizeIfRequired(conditionalExpression.TrueExpression, PrecedenceLevel.NullableRewrap);
            ParenthesizeIfRequired(conditionalExpression.FalseExpression, PrecedenceLevel.NullableRewrap);
        }
        else
        {
            ParenthesizeIfRequired(conditionalExpression.Condition, PrecedenceLevel.Conditional + 1);
            ParenthesizeIfRequired(conditionalExpression.TrueExpression, PrecedenceLevel.Conditional);
            ParenthesizeIfRequired(conditionalExpression.FalseExpression, PrecedenceLevel.Conditional);
        }

        base.VisitConditionalExpression(conditionalExpression);
    }

    private static bool IsConditionalRefExpression(ConditionalExpression conditionalExpression) =>
        conditionalExpression.TrueExpression is DirectionExpression || conditionalExpression.FalseExpression is DirectionExpression;

    public override void VisitAssignmentExpression(AssignmentExpression assignmentExpression)
    {
        // assignment is right-associative
        ParenthesizeIfRequired(assignmentExpression.Left, PrecedenceLevel.Assignment + 1);
        HandleAssignmentRhs(assignmentExpression.Right);
        base.VisitAssignmentExpression(assignmentExpression);
    }

    private void HandleAssignmentRhs(Expression right) =>
        ParenthesizeIfRequired(right, InsertParenthesesForReadability && right is not DirectionExpression
            ? PrecedenceLevel.Conditional + 1
            : PrecedenceLevel.Assignment);

    public override void VisitVariableInitializer(VariableInitializer variableInitializer)
    {
        if (!variableInitializer.Initializer.IsNull)
            HandleAssignmentRhs(variableInitializer.Initializer);

        base.VisitVariableInitializer(variableInitializer);
    }

    // don't need to handle lambdas, they have lowest precedence and unambiguous associativity
    public override void VisitQueryExpression(QueryExpression queryExpression)
    {
        // Query expressions are strange beasts:
        // "var a = -from b in c select d;" is valid, so queries bind stricter than unary expressions.
        // However, the end of the query is greedy. So their start sort of has a high precedence,
        // while their end has a very low precedence. We handle this by checking whether a query is used
        // as left part of a binary operator, and parenthesize it if required.
        HandleLambdaOrQuery(queryExpression);
        base.VisitQueryExpression(queryExpression);
    }

    public override void VisitLambdaExpression(LambdaExpression lambdaExpression)
    {
        // Lambdas are greedy in the same way as query expressions.
        HandleLambdaOrQuery(lambdaExpression);
        base.VisitLambdaExpression(lambdaExpression);
    }

    void HandleLambdaOrQuery(Expression expr)
    {
        if (expr.Role == BinaryOperatorExpression.LeftRole)
            Parenthesize(expr);

        if (expr.Parent is IsExpression or AsExpression)
            Parenthesize(expr);

        // when readability is desired, always parenthesize query expressions within unary or binary operators
        if (InsertParenthesesForReadability && expr.Parent is UnaryOperatorExpression or BinaryOperatorExpression)
            Parenthesize(expr);
    }

    public override void VisitNamedExpression(NamedExpression namedExpression)
    {
        if (InsertParenthesesForReadability)
            ParenthesizeIfRequired(namedExpression.Expression, PrecedenceLevel.RelationalAndTypeTesting + 1);

        base.VisitNamedExpression(namedExpression);
    }

    private enum PrecedenceLevel
    {
        // Higher integer value = higher precedence.
        Assignment,
        Conditional, // ?:
        NullCoalescing, // ??
        ConditionalOr, // ||
        ConditionalAnd, // &&
        BitwiseOr, // |
        ExclusiveOr, // binary ^
        BitwiseAnd, // binary &
        Equality, // == !=
        RelationalAndTypeTesting, // < <= > >= is
        Shift, // << >>
        Additive, // binary + -
        Multiplicative, // * / %
        Switch, // C# 8 switch expression
        Range, // ..
        Unary,
        QueryOrLambda,
        NullableRewrap,
        Primary
    }
}