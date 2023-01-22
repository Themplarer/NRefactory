using System;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.Utils;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem.ConstantValues;

/// <summary>
/// Increments an integer <see cref="IConstantValue"/> by a fixed amount without changing the type.
/// </summary>
[Serializable]
public sealed class IncrementConstantValue : IConstantValue, ISupportsInterning
{
    private readonly IConstantValue _baseValue;
    private readonly int _incrementAmount;

    public IncrementConstantValue(IConstantValue baseValue, int incrementAmount = 1)
    {
        if (baseValue == null)
            throw new ArgumentNullException(nameof(baseValue));

        if (baseValue is IncrementConstantValue icv)
        {
            _baseValue = icv._baseValue;
            _incrementAmount = icv._incrementAmount + incrementAmount;
        }
        else
        {
            _baseValue = baseValue;
            _incrementAmount = incrementAmount;
        }
    }

    public ResolveResult Resolve(ITypeResolveContext context)
    {
        var rr = _baseValue.Resolve(context);

        if (rr.IsCompileTimeConstant && rr.ConstantValue != null)
        {
            var val = rr.ConstantValue;
            var typeCode = val == null ? TypeCode.Empty : Type.GetTypeCode(val.GetType());

            if (typeCode is >= TypeCode.SByte and <= TypeCode.UInt64)
            {
                var intVal = (long)CSharpPrimitiveCast.Cast(TypeCode.Int64, val, false);
                var newVal = CSharpPrimitiveCast.Cast(typeCode, unchecked(intVal + _incrementAmount), false);
                return new ConstantResolveResult(rr.Type, newVal);
            }
        }

        return new ErrorResolveResult(rr.Type);
    }

    int ISupportsInterning.GetHashCodeForInterning() => unchecked(_baseValue.GetHashCode() * 33 ^ _incrementAmount);

    bool ISupportsInterning.EqualsForInterning(ISupportsInterning other) =>
        other is IncrementConstantValue o && _baseValue == o._baseValue && _incrementAmount == o._incrementAmount;
}