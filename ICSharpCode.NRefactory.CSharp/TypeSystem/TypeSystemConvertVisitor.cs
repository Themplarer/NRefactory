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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ICSharpCode.NRefactory.CSharp.TypeSystem.ConstantValues;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;

namespace ICSharpCode.NRefactory.CSharp.TypeSystem;

/// <summary>
/// Produces type and member definitions from the DOM.
/// </summary>
public class TypeSystemConvertVisitor : DepthFirstAstVisitor<IUnresolvedEntity>
{
    /// <summary>
    /// Version of the C# type system loader.
    /// Should be incremented when fixing bugs so that project contents cached on disk
    /// (which might be incorrect due to the bug) are re-created.
    /// </summary>
    public const int Version = 2;

    private readonly CSharpUnresolvedFile _unresolvedFile;
    private UsingScope _usingScope;
    private CSharpUnresolvedTypeDefinition _currentTypeDefinition;
    private DefaultUnresolvedMethod _currentMethod;

    private InterningProvider _interningProvider = new SimpleInterningProvider();

    private static readonly IUnresolvedParameter DelegateObjectParameter = MakeParameter(KnownTypeReference.Object, "object");
    private static readonly IUnresolvedParameter DelegateIntPtrMethodParameter = MakeParameter(KnownTypeReference.IntPtr, "method");
    private static readonly IUnresolvedParameter DelegateAsyncCallbackParameter = MakeParameter(typeof(AsyncCallback).ToTypeReference(), "callback");
    private static readonly IUnresolvedParameter DelegateResultParameter = MakeParameter(typeof(IAsyncResult).ToTypeReference(), "result");

    /// <summary>
    /// Creates a new TypeSystemConvertVisitor.
    /// </summary>
    /// <param name="fileName">The file name (used for DomRegions).</param>
    public TypeSystemConvertVisitor(string fileName)
    {
        if (fileName == null)
            throw new ArgumentNullException(nameof(fileName));

        _unresolvedFile = new CSharpUnresolvedFile
        {
            FileName = fileName
        };
        _usingScope = _unresolvedFile.RootUsingScope;
    }

    /// <summary>
    /// Creates a new TypeSystemConvertVisitor and initializes it with a given context.
    /// </summary>
    /// <param name="unresolvedFile">The parsed file to which members should be added.</param>
    /// <param name="currentUsingScope">The current using scope.</param>
    /// <param name="currentTypeDefinition">The current type definition.</param>
    public TypeSystemConvertVisitor(CSharpUnresolvedFile unresolvedFile, UsingScope currentUsingScope = null,
        CSharpUnresolvedTypeDefinition currentTypeDefinition = null)
    {
        _unresolvedFile = unresolvedFile ?? throw new ArgumentNullException(nameof(unresolvedFile));
        _usingScope = currentUsingScope ?? unresolvedFile.RootUsingScope;
        _currentTypeDefinition = currentTypeDefinition;
    }

    /// <summary>
    /// Gets/Sets the interning provider to use.
    /// The default value is a new <see cref="SimpleInterningProvider"/> instance.
    /// </summary>
    public InterningProvider InterningProvider
    {
        get => _interningProvider;
        set
        {
            if (_interningProvider == null)
                throw new ArgumentNullException();

            _interningProvider = value;
        }
    }

    /// <summary>
    /// Gets/Sets whether to ignore XML documentation.
    /// The default value is false.
    /// </summary>
    public bool SkipXmlDocumentation { get; set; }

    public CSharpUnresolvedFile UnresolvedFile => _unresolvedFile;

    private DomRegion MakeRegion(TextLocation start, TextLocation end) => new(_unresolvedFile.FileName, start.Line, start.Column, end.Line, end.Column);

    private DomRegion MakeRegion(AstNode node) =>
        node == null || node.IsNull ? DomRegion.Empty : MakeRegion(GetStartLocationAfterAttributes(node), node.EndLocation);

    internal static TextLocation GetStartLocationAfterAttributes(AstNode node)
    {
        var child = node.FirstChild;
        // Skip attributes and comments between attributes for the purpose of
        // getting a declaration's region.

        while (child != null && (child is AttributeSection || child.NodeType == NodeType.Whitespace))
            child = child.NextSibling;

        return (child ?? node).StartLocation;
    }

    private DomRegion MakeBraceRegion(AstNode node) =>
        node == null || node.IsNull
            ? DomRegion.Empty
            : MakeRegion(node.GetChildByRole(Roles.LBrace).StartLocation, node.GetChildByRole(Roles.RBrace).EndLocation);

    #region Compilation Unit

    public override IUnresolvedEntity VisitSyntaxTree(SyntaxTree unit)
    {
        _unresolvedFile.Errors = unit.Errors;
        return base.VisitSyntaxTree(unit);
    }

    #endregion

    #region Using Declarations

    public override IUnresolvedEntity VisitExternAliasDeclaration(ExternAliasDeclaration externAliasDeclaration)
    {
        _usingScope.ExternAliases.Add(externAliasDeclaration.Name);
        return null;
    }

    public override IUnresolvedEntity VisitUsingDeclaration(UsingDeclaration usingDeclaration)
    {
        if (ConvertTypeReference(usingDeclaration.Import, NameLookupMode.TypeInUsingDeclaration) is TypeOrNamespaceReference typeOrNamespaceReference)
            _usingScope.Usings.Add(typeOrNamespaceReference);

        return null;
    }

    public override IUnresolvedEntity VisitUsingAliasDeclaration(UsingAliasDeclaration usingDeclaration)
    {
        if (ConvertTypeReference(usingDeclaration.Import, NameLookupMode.TypeInUsingDeclaration) is TypeOrNamespaceReference typeOrNamespaceReference)
            _usingScope.UsingAliases.Add(new KeyValuePair<string, TypeOrNamespaceReference>(usingDeclaration.Alias, typeOrNamespaceReference));

        return null;
    }

    #endregion

    #region Namespace Declaration

    public override IUnresolvedEntity VisitNamespaceDeclaration(NamespaceDeclaration namespaceDeclaration)
    {
        var region = MakeRegion(namespaceDeclaration);
        var previousUsingScope = _usingScope;

        foreach (var ident in namespaceDeclaration.Identifiers)
            _usingScope = new UsingScope(_usingScope, ident)
            {
                Region = region
            };

        base.VisitNamespaceDeclaration(namespaceDeclaration);
        _unresolvedFile.UsingScopes.Add(_usingScope); // add after visiting children so that nested scopes come first
        _usingScope = previousUsingScope;
        return null;
    }

    #endregion

    #region Type Definitions

    private CSharpUnresolvedTypeDefinition CreateTypeDefinition(string name)
    {
        CSharpUnresolvedTypeDefinition newType;

        if (_currentTypeDefinition != null)
        {
            newType = new CSharpUnresolvedTypeDefinition(_currentTypeDefinition, name);

            foreach (var typeParameter in _currentTypeDefinition.TypeParameters)
                newType.TypeParameters.Add(typeParameter);

            _currentTypeDefinition.NestedTypes.Add(newType);
        }
        else
        {
            newType = new CSharpUnresolvedTypeDefinition(_usingScope, name);
            _unresolvedFile.TopLevelTypeDefinitions.Add(newType);
        }

        newType.UnresolvedFile = _unresolvedFile;
        newType.HasExtensionMethods = false; // gets set to true when an extension method is added
        return newType;
    }

    public override IUnresolvedEntity VisitTypeDeclaration(TypeDeclaration typeDeclaration)
    {
        var typeDefinition = _currentTypeDefinition = CreateTypeDefinition(typeDeclaration.Name);
        typeDefinition.Region = MakeRegion(typeDeclaration);
        typeDefinition.BodyRegion = MakeBraceRegion(typeDeclaration);
        AddXmlDocumentation(typeDefinition, typeDeclaration);
        ApplyModifiers(typeDefinition, typeDeclaration.Modifiers);

        switch (typeDeclaration.ClassType)
        {
            case ClassType.Enum:
                typeDefinition.Kind = TypeKind.Enum;
                break;
            case ClassType.Interface:
                typeDefinition.Kind = TypeKind.Interface;
                typeDefinition.IsAbstract = true; // interfaces are implicitly abstract
                break;
            case ClassType.Struct:
                typeDefinition.Kind = TypeKind.Struct;
                typeDefinition.IsSealed = true; // enums/structs are implicitly sealed
                break;
        }

        ConvertAttributes(typeDefinition.Attributes, typeDeclaration.Attributes);

        ConvertTypeParameters(typeDefinition.TypeParameters, typeDeclaration.TypeParameters, typeDeclaration.Constraints, SymbolKind.TypeDefinition);

        foreach (var baseType in typeDeclaration.BaseTypes)
            typeDefinition.BaseTypes.Add(ConvertTypeReference(baseType, NameLookupMode.BaseTypeReference));

        foreach (var member in typeDeclaration.Members)
            member.AcceptVisitor(this);

        _currentTypeDefinition = (CSharpUnresolvedTypeDefinition)_currentTypeDefinition.DeclaringTypeDefinition;
        typeDefinition.ApplyInterningProvider(_interningProvider);
        return typeDefinition;
    }

    public override IUnresolvedEntity VisitDelegateDeclaration(DelegateDeclaration delegateDeclaration)
    {
        var td = _currentTypeDefinition = CreateTypeDefinition(delegateDeclaration.Name);
        td.Kind = TypeKind.Delegate;
        td.Region = MakeRegion(delegateDeclaration);
        td.BaseTypes.Add(KnownTypeReference.MulticastDelegate);
        AddXmlDocumentation(td, delegateDeclaration);

        ApplyModifiers(td, delegateDeclaration.Modifiers);
        td.IsSealed = true; // delegates are implicitly sealed

        ConvertTypeParameters(td.TypeParameters, delegateDeclaration.TypeParameters, delegateDeclaration.Constraints, SymbolKind.TypeDefinition);

        var returnType = ConvertTypeReference(delegateDeclaration.ReturnType);
        var parameters = new List<IUnresolvedParameter>();
        ConvertParameters(parameters, delegateDeclaration.Parameters);
        AddDefaultMethodsToDelegate(td, returnType, parameters);

        foreach (var section in delegateDeclaration.Attributes)
            if (section.AttributeTarget == "return")
            {
                var returnTypeAttributes = new List<IUnresolvedAttribute>();
                ConvertAttributes(returnTypeAttributes, section);
                var invokeMethod = (IUnresolvedMethod)td.Members.Single(m => m.Name == "Invoke");
                var endInvokeMethod = (IUnresolvedMethod)td.Members.Single(m => m.Name == "EndInvoke");

                foreach (var attr in returnTypeAttributes)
                {
                    invokeMethod.ReturnTypeAttributes.Add(attr);
                    endInvokeMethod.ReturnTypeAttributes.Add(attr);
                }
            }
            else
                ConvertAttributes(td.Attributes, section);

        _currentTypeDefinition = (CSharpUnresolvedTypeDefinition)_currentTypeDefinition.DeclaringTypeDefinition;
        td.ApplyInterningProvider(_interningProvider);
        return td;
    }

    private static IUnresolvedParameter MakeParameter(ITypeReference type, string name)
    {
        var p = new DefaultUnresolvedParameter(type, name);
        p.Freeze();
        return p;
    }

    /// <summary>
    /// Adds the 'Invoke', 'BeginInvoke', 'EndInvoke' methods, and a constructor, to the <paramref name="delegateType"/>.
    /// </summary>
    public static void AddDefaultMethodsToDelegate(DefaultUnresolvedTypeDefinition delegateType, ITypeReference returnType,
        IReadOnlyCollection<IUnresolvedParameter> parameters)
    {
        if (delegateType == null)
            throw new ArgumentNullException(nameof(delegateType));

        if (returnType == null)
            throw new ArgumentNullException(nameof(returnType));

        if (parameters == null)
            throw new ArgumentNullException(nameof(parameters));

        var region = delegateType.Region;
        region = new DomRegion(region.FileName, region.BeginLine, region.BeginColumn); // remove end position

        var invoke = new DefaultUnresolvedMethod(delegateType, "Invoke")
        {
            Accessibility = Accessibility.Public,
            IsSynthetic = true
        };

        foreach (var p in parameters)
            invoke.Parameters.Add(p);

        invoke.ReturnType = returnType;
        invoke.Region = region;
        delegateType.Members.Add(invoke);

        var beginInvoke = new DefaultUnresolvedMethod(delegateType, "BeginInvoke")
        {
            Accessibility = Accessibility.Public,
            IsSynthetic = true
        };

        foreach (var p in parameters)
            beginInvoke.Parameters.Add(p);

        beginInvoke.Parameters.Add(DelegateAsyncCallbackParameter);
        beginInvoke.Parameters.Add(DelegateObjectParameter);
        beginInvoke.ReturnType = DelegateResultParameter.Type;
        beginInvoke.Region = region;
        delegateType.Members.Add(beginInvoke);

        var endInvoke = new DefaultUnresolvedMethod(delegateType, "EndInvoke")
        {
            Accessibility = Accessibility.Public,
            IsSynthetic = true
        };
        endInvoke.Parameters.Add(DelegateResultParameter);
        endInvoke.ReturnType = invoke.ReturnType;
        endInvoke.Region = region;
        delegateType.Members.Add(endInvoke);

        var ctor = new DefaultUnresolvedMethod(delegateType, ".ctor")
        {
            SymbolKind = SymbolKind.Constructor,
            Accessibility = Accessibility.Public,
            IsSynthetic = true
        };
        ctor.Parameters.Add(DelegateObjectParameter);
        ctor.Parameters.Add(DelegateIntPtrMethodParameter);
        ctor.ReturnType = delegateType;
        ctor.Region = region;
        delegateType.Members.Add(ctor);
    }

    #endregion

    #region Fields

    public override IUnresolvedEntity VisitFieldDeclaration(FieldDeclaration fieldDeclaration)
    {
        var isSingleField = fieldDeclaration.Variables.Count == 1;
        var modifiers = fieldDeclaration.Modifiers;
        DefaultUnresolvedField field = null;

        foreach (var vi in fieldDeclaration.Variables)
        {
            field = new DefaultUnresolvedField(_currentTypeDefinition, vi.Name)
            {
                Region = isSingleField ? MakeRegion(fieldDeclaration) : MakeRegion(vi),
                BodyRegion = MakeRegion(vi)
            };

            ConvertAttributes(field.Attributes, fieldDeclaration.Attributes);
            AddXmlDocumentation(field, fieldDeclaration);

            ApplyModifiers(field, modifiers);
            field.IsVolatile = (modifiers & Modifiers.Volatile) != 0;
            field.IsReadOnly = (modifiers & Modifiers.Readonly) != 0;

            field.ReturnType = ConvertTypeReference(fieldDeclaration.ReturnType);

            if ((modifiers & Modifiers.Const) != 0)
            {
                field.ConstantValue = ConvertConstantValue(field.ReturnType, vi.Initializer);
                field.IsStatic = true;
            }

            _currentTypeDefinition.Members.Add(field);
            field.ApplyInterningProvider(_interningProvider);
        }

        return isSingleField ? field : null;
    }

    public override IUnresolvedEntity VisitFixedFieldDeclaration(FixedFieldDeclaration fixedFieldDeclaration)
    {
        var isSingleField = fixedFieldDeclaration.Variables.Count == 1;
        var modifiers = fixedFieldDeclaration.Modifiers;
        DefaultUnresolvedField field = null;

        foreach (var vi in fixedFieldDeclaration.Variables)
        {
            field = new DefaultUnresolvedField(_currentTypeDefinition, vi.Name)
            {
                Region = isSingleField ? MakeRegion(fixedFieldDeclaration) : MakeRegion(vi),
                BodyRegion = MakeRegion(vi)
            };

            ConvertAttributes(field.Attributes, fixedFieldDeclaration.Attributes);
            AddXmlDocumentation(field, fixedFieldDeclaration);

            ApplyModifiers(field, modifiers);

            field.ReturnType = ConvertTypeReference(fixedFieldDeclaration.ReturnType);
            field.IsFixed = true;
            field.ConstantValue = ConvertConstantValue(field.ReturnType, vi.CountExpression);

            _currentTypeDefinition.Members.Add(field);
            field.ApplyInterningProvider(_interningProvider);
        }

        return isSingleField ? field : null;
    }

    public override IUnresolvedEntity VisitEnumMemberDeclaration(EnumMemberDeclaration enumMemberDeclaration)
    {
        var field = new DefaultUnresolvedField(_currentTypeDefinition, enumMemberDeclaration.Name);
        field.Region = field.BodyRegion = MakeRegion(enumMemberDeclaration);
        ConvertAttributes(field.Attributes, enumMemberDeclaration.Attributes);
        AddXmlDocumentation(field, enumMemberDeclaration);

        if (_currentTypeDefinition.TypeParameters.Count == 0)
            field.ReturnType = _currentTypeDefinition;
        else
        {
            var typeArgs = new ITypeReference[_currentTypeDefinition.TypeParameters.Count];

            for (var i = 0; i < typeArgs.Length; i++)
                typeArgs[i] = TypeParameterReference.Create(SymbolKind.TypeDefinition, i);

            field.ReturnType = _interningProvider.Intern(new ParameterizedTypeReference(_currentTypeDefinition, typeArgs));
        }

        field.Accessibility = Accessibility.Public;
        field.IsStatic = true;

        if (!enumMemberDeclaration.Initializer.IsNull)
            field.ConstantValue = ConvertConstantValue(field.ReturnType, enumMemberDeclaration.Initializer);
        else
            field.ConstantValue = _currentTypeDefinition.Members.LastOrDefault() is not DefaultUnresolvedField prevField ||
                                  prevField.ConstantValue == null
                ? ConvertConstantValue(field.ReturnType, new PrimitiveExpression(0))
                : _interningProvider.Intern(new IncrementConstantValue(prevField.ConstantValue));

        _currentTypeDefinition.Members.Add(field);
        field.ApplyInterningProvider(_interningProvider);
        return field;
    }

    #endregion

    #region Methods

    public override IUnresolvedEntity VisitMethodDeclaration(MethodDeclaration methodDeclaration)
    {
        var m = new DefaultUnresolvedMethod(_currentTypeDefinition, methodDeclaration.Name);
        _currentMethod = m; // required for resolving type parameters
        m.Region = MakeRegion(methodDeclaration);
        m.BodyRegion = MakeRegion(methodDeclaration.Body);
        AddXmlDocumentation(m, methodDeclaration);

        if (InheritsConstraints(methodDeclaration) && methodDeclaration.Constraints.Count == 0)
        {
            var index = 0;

            foreach (var tpDecl in methodDeclaration.TypeParameters)
            {
                var tp = new MethodTypeParameterWithInheritedConstraints(index++, tpDecl.Name)
                {
                    Region = MakeRegion(tpDecl)
                };
                ConvertAttributes(tp.Attributes, tpDecl.Attributes);
                tp.Variance = tpDecl.Variance;
                tp.ApplyInterningProvider(_interningProvider);
                m.TypeParameters.Add(tp);
            }
        }
        else
            ConvertTypeParameters(m.TypeParameters, methodDeclaration.TypeParameters, methodDeclaration.Constraints, SymbolKind.Method);

        m.ReturnType = ConvertTypeReference(methodDeclaration.ReturnType);
        ConvertAttributes(m.Attributes, methodDeclaration.Attributes.Where(s => s.AttributeTarget != "return"));
        ConvertAttributes(m.ReturnTypeAttributes, methodDeclaration.Attributes.Where(s => s.AttributeTarget == "return"));

        ApplyModifiers(m, methodDeclaration.Modifiers);

        if (methodDeclaration.IsExtensionMethod)
        {
            m.IsExtensionMethod = true;
            _currentTypeDefinition.HasExtensionMethods = true;
        }

        m.IsPartial = methodDeclaration.HasModifier(Modifiers.Partial);
        m.IsAsync = methodDeclaration.HasModifier(Modifiers.Async);

        m.HasBody = !methodDeclaration.Body.IsNull;

        ConvertParameters(m.Parameters, methodDeclaration.Parameters);

        if (!methodDeclaration.PrivateImplementationType.IsNull)
        {
            m.Accessibility = Accessibility.None;
            m.IsExplicitInterfaceImplementation = true;
            m.ExplicitInterfaceImplementations.Add(
                _interningProvider.Intern(new DefaultMemberReference(
                    m.SymbolKind,
                    ConvertTypeReference(methodDeclaration.PrivateImplementationType),
                    m.Name, m.TypeParameters.Count, GetParameterTypes(m.Parameters))));
        }

        _currentTypeDefinition.Members.Add(m);
        _currentMethod = null;
        m.ApplyInterningProvider(_interningProvider);
        return m;
    }

    private IList<ITypeReference> GetParameterTypes(IList<IUnresolvedParameter> parameters)
    {
        if (parameters.Count == 0)
            return EmptyList<ITypeReference>.Instance;

        var types = parameters
            .Select(p => p.Type)
            .ToArray();
        return _interningProvider.InternList(types);
    }

    private static bool InheritsConstraints(MethodDeclaration methodDeclaration) =>
        // overrides and explicit interface implementations inherit constraints
        (methodDeclaration.Modifiers & Modifiers.Override) == Modifiers.Override ||
        !methodDeclaration.PrivateImplementationType.IsNull;

    private void ConvertTypeParameters(IList<IUnresolvedTypeParameter> output, AstNodeCollection<TypeParameterDeclaration> typeParameters,
        AstNodeCollection<Constraint> constraints, SymbolKind ownerType)
    {
        // output might be non-empty when type parameters were copied from an outer class
        var index = output.Count;
        var list = new List<DefaultUnresolvedTypeParameter>();

        foreach (var tpDecl in typeParameters)
        {
            var tp = new DefaultUnresolvedTypeParameter(ownerType, index++, tpDecl.Name)
            {
                Region = MakeRegion(tpDecl)
            };
            ConvertAttributes(tp.Attributes, tpDecl.Attributes);
            tp.Variance = tpDecl.Variance;
            list.Add(tp);
            output.Add(tp); // tp must be added to list here so that it can be referenced by constraints
        }

        foreach (var c in constraints)
        foreach (var tp in list)
            if (tp.Name == c.TypeParameter.Identifier)
            {
                foreach (var type in c.BaseTypes)
                {
                    if (type is PrimitiveType primType)
                        switch (primType.Keyword)
                        {
                            case "new":
                                tp.HasDefaultConstructorConstraint = true;
                                continue;
                            case "class":
                                tp.HasReferenceTypeConstraint = true;
                                continue;
                            case "struct":
                                tp.HasValueTypeConstraint = true;
                                continue;
                        }

                    var lookupMode = ownerType == SymbolKind.TypeDefinition ? NameLookupMode.BaseTypeReference : NameLookupMode.Type;
                    tp.Constraints.Add(ConvertTypeReference(type, lookupMode));
                }

                break;
            }

        foreach (var tp in list) tp.ApplyInterningProvider(_interningProvider);
    }

    #endregion

    #region Operators

    public override IUnresolvedEntity VisitOperatorDeclaration(OperatorDeclaration operatorDeclaration)
    {
        var m = new DefaultUnresolvedMethod(_currentTypeDefinition, operatorDeclaration.Name)
        {
            SymbolKind = SymbolKind.Operator,
            Region = MakeRegion(operatorDeclaration),
            BodyRegion = MakeRegion(operatorDeclaration.Body)
        };
        AddXmlDocumentation(m, operatorDeclaration);

        m.ReturnType = ConvertTypeReference(operatorDeclaration.ReturnType);
        ConvertAttributes(m.Attributes, operatorDeclaration.Attributes.Where(s => s.AttributeTarget != "return"));
        ConvertAttributes(m.ReturnTypeAttributes, operatorDeclaration.Attributes.Where(s => s.AttributeTarget == "return"));

        ApplyModifiers(m, operatorDeclaration.Modifiers);
        m.HasBody = !operatorDeclaration.Body.IsNull;

        ConvertParameters(m.Parameters, operatorDeclaration.Parameters);

        _currentTypeDefinition.Members.Add(m);
        m.ApplyInterningProvider(_interningProvider);
        return m;
    }

    #endregion

    #region Constructors

    public override IUnresolvedEntity VisitConstructorDeclaration(ConstructorDeclaration constructorDeclaration)
    {
        var modifiers = constructorDeclaration.Modifiers;
        var isStatic = (modifiers & Modifiers.Static) != 0;
        var ctor = new DefaultUnresolvedMethod(_currentTypeDefinition, isStatic ? ".cctor" : ".ctor")
        {
            SymbolKind = SymbolKind.Constructor,
            Region = MakeRegion(constructorDeclaration)
        };

        ctor.BodyRegion = !constructorDeclaration.Initializer.IsNull
            ? MakeRegion(constructorDeclaration.Initializer.StartLocation, constructorDeclaration.EndLocation)
            : MakeRegion(constructorDeclaration.Body);

        ctor.ReturnType = KnownTypeReference.Void;

        ConvertAttributes(ctor.Attributes, constructorDeclaration.Attributes);
        ConvertParameters(ctor.Parameters, constructorDeclaration.Parameters);
        AddXmlDocumentation(ctor, constructorDeclaration);
        ctor.HasBody = !constructorDeclaration.Body.IsNull;

        if (isStatic)
            ctor.IsStatic = true;
        else
            ApplyModifiers(ctor, modifiers);

        _currentTypeDefinition.Members.Add(ctor);
        ctor.ApplyInterningProvider(_interningProvider);
        return ctor;
    }

    #endregion

    #region Destructors

    public override IUnresolvedEntity VisitDestructorDeclaration(DestructorDeclaration destructorDeclaration)
    {
        var dtor = new DefaultUnresolvedMethod(_currentTypeDefinition, "Finalize")
        {
            SymbolKind = SymbolKind.Destructor,
            Region = MakeRegion(destructorDeclaration),
            BodyRegion = MakeRegion(destructorDeclaration.Body),
            Accessibility = Accessibility.Protected,
            IsOverride = true,
            ReturnType = KnownTypeReference.Void,
            HasBody = !destructorDeclaration.Body.IsNull
        };

        ConvertAttributes(dtor.Attributes, destructorDeclaration.Attributes);
        AddXmlDocumentation(dtor, destructorDeclaration);

        _currentTypeDefinition.Members.Add(dtor);
        dtor.ApplyInterningProvider(_interningProvider);
        return dtor;
    }

    #endregion

    #region Properties / Indexers

    public override IUnresolvedEntity VisitPropertyDeclaration(PropertyDeclaration propertyDeclaration)
    {
        var p = new DefaultUnresolvedProperty(_currentTypeDefinition, propertyDeclaration.Name)
        {
            Region = MakeRegion(propertyDeclaration),
            BodyRegion = MakeBraceRegion(propertyDeclaration)
        };
        ApplyModifiers(p, propertyDeclaration.Modifiers);
        p.ReturnType = ConvertTypeReference(propertyDeclaration.ReturnType);
        ConvertAttributes(p.Attributes, propertyDeclaration.Attributes);
        AddXmlDocumentation(p, propertyDeclaration);

        if (!propertyDeclaration.PrivateImplementationType.IsNull)
        {
            p.Accessibility = Accessibility.None;
            p.IsExplicitInterfaceImplementation = true;
            p.ExplicitInterfaceImplementations.Add(_interningProvider.Intern(new DefaultMemberReference(
                p.SymbolKind, ConvertTypeReference(propertyDeclaration.PrivateImplementationType), p.Name)));
        }

        var isExtern = propertyDeclaration.HasModifier(Modifiers.Extern);
        p.Getter = ConvertAccessor(propertyDeclaration.Getter, p, "get_", isExtern);
        p.Setter = ConvertAccessor(propertyDeclaration.Setter, p, "set_", isExtern);
        _currentTypeDefinition.Members.Add(p);
        p.ApplyInterningProvider(_interningProvider);
        return p;
    }

    public override IUnresolvedEntity VisitIndexerDeclaration(IndexerDeclaration indexerDeclaration)
    {
        var p = new DefaultUnresolvedProperty(_currentTypeDefinition, "Item")
        {
            SymbolKind = SymbolKind.Indexer,
            Region = MakeRegion(indexerDeclaration),
            BodyRegion = MakeBraceRegion(indexerDeclaration)
        };
        ApplyModifiers(p, indexerDeclaration.Modifiers);
        p.ReturnType = ConvertTypeReference(indexerDeclaration.ReturnType);
        ConvertAttributes(p.Attributes, indexerDeclaration.Attributes);
        AddXmlDocumentation(p, indexerDeclaration);

        ConvertParameters(p.Parameters, indexerDeclaration.Parameters);

        if (!indexerDeclaration.PrivateImplementationType.IsNull)
        {
            p.Accessibility = Accessibility.None;
            p.IsExplicitInterfaceImplementation = true;
            p.ExplicitInterfaceImplementations.Add(_interningProvider.Intern(new DefaultMemberReference(
                p.SymbolKind, indexerDeclaration.PrivateImplementationType.ToTypeReference(), p.Name, 0, GetParameterTypes(p.Parameters))));
        }

        var isExtern = indexerDeclaration.HasModifier(Modifiers.Extern);
        p.Getter = ConvertAccessor(indexerDeclaration.Getter, p, "get_", isExtern);
        p.Setter = ConvertAccessor(indexerDeclaration.Setter, p, "set_", isExtern);

        _currentTypeDefinition.Members.Add(p);
        p.ApplyInterningProvider(_interningProvider);
        return p;
    }

    private DefaultUnresolvedMethod ConvertAccessor(Accessor accessor, IUnresolvedMember p, string prefix, bool memberIsExtern)
    {
        if (accessor.IsNull)
            return null;

        var a = new DefaultUnresolvedMethod(_currentTypeDefinition, prefix + p.Name)
        {
            SymbolKind = SymbolKind.Accessor,
            AccessorOwner = p,
            Accessibility = GetAccessibility(accessor.Modifiers) ?? p.Accessibility,
            IsAbstract = p.IsAbstract,
            IsOverride = p.IsOverride,
            IsSealed = p.IsSealed,
            IsStatic = p.IsStatic,
            IsSynthetic = p.IsSynthetic,
            IsVirtual = p.IsVirtual,
            Region = MakeRegion(accessor),
            BodyRegion = MakeRegion(accessor.Body),
            // An accessor has no body if both are true:
            //  a) there's no body in the code
            //  b) the member is either abstract or extern
            HasBody = !(accessor.Body.IsNull && (p.IsAbstract || memberIsExtern))
        };

        if (p.SymbolKind == SymbolKind.Indexer)
            foreach (var indexerParam in ((IUnresolvedProperty)p).Parameters)
                a.Parameters.Add(indexerParam);

        DefaultUnresolvedParameter param = null;

        if (accessor.Role == PropertyDeclaration.GetterRole)
            a.ReturnType = p.ReturnType;
        else
        {
            param = new DefaultUnresolvedParameter(p.ReturnType, "value");
            a.Parameters.Add(param);
            a.ReturnType = KnownTypeReference.Void;
        }

        foreach (var section in accessor.Attributes)
            if (section.AttributeTarget == "return")
                ConvertAttributes(a.ReturnTypeAttributes, section);
            else if (param != null && section.AttributeTarget == "param")
                ConvertAttributes(param.Attributes, section);
            else
                ConvertAttributes(a.Attributes, section);

        if (p.IsExplicitInterfaceImplementation)
        {
            a.IsExplicitInterfaceImplementation = true;
            Debug.Assert(p.ExplicitInterfaceImplementations.Count == 1);
            a.ExplicitInterfaceImplementations.Add(_interningProvider.Intern(new DefaultMemberReference(
                SymbolKind.Accessor,
                p.ExplicitInterfaceImplementations[0].DeclaringTypeReference,
                a.Name, 0, GetParameterTypes(a.Parameters)
            )));
        }

        a.ApplyInterningProvider(_interningProvider);
        return a;
    }

    #endregion

    #region Events

    public override IUnresolvedEntity VisitEventDeclaration(EventDeclaration eventDeclaration)
    {
        var isSingleEvent = eventDeclaration.Variables.Count == 1;
        var modifiers = eventDeclaration.Modifiers;
        DefaultUnresolvedEvent ev = null;

        foreach (var vi in eventDeclaration.Variables)
        {
            ev = new DefaultUnresolvedEvent(_currentTypeDefinition, vi.Name)
            {
                Region = isSingleEvent ? MakeRegion(eventDeclaration) : MakeRegion(vi),
                BodyRegion = MakeRegion(vi)
            };

            ApplyModifiers(ev, modifiers);
            AddXmlDocumentation(ev, eventDeclaration);

            ev.ReturnType = ConvertTypeReference(eventDeclaration.ReturnType);

            var valueParameter = new DefaultUnresolvedParameter(ev.ReturnType, "value");
            ev.AddAccessor = CreateDefaultEventAccessor(ev, "add_" + ev.Name, valueParameter);
            ev.RemoveAccessor = CreateDefaultEventAccessor(ev, "remove_" + ev.Name, valueParameter);

            foreach (var section in eventDeclaration.Attributes)
                if (section.AttributeTarget == "method")
                    foreach (var attrNode in section.Attributes)
                    {
                        IUnresolvedAttribute attr = ConvertAttribute(attrNode);
                        ev.AddAccessor.Attributes.Add(attr);
                        ev.RemoveAccessor.Attributes.Add(attr);
                    }
                else if (section.AttributeTarget != "field")
                    ConvertAttributes(ev.Attributes, section);

            _currentTypeDefinition.Members.Add(ev);
            ev.ApplyInterningProvider(_interningProvider);
        }

        return isSingleEvent ? ev : null;
    }

    private DefaultUnresolvedMethod CreateDefaultEventAccessor(IUnresolvedEvent ev, string name, IUnresolvedParameter valueParameter)
    {
        var a = new DefaultUnresolvedMethod(_currentTypeDefinition, name)
        {
            SymbolKind = SymbolKind.Accessor,
            AccessorOwner = ev,
            Region = ev.BodyRegion,
            BodyRegion = DomRegion.Empty,
            Accessibility = ev.Accessibility,
            IsAbstract = ev.IsAbstract,
            IsOverride = ev.IsOverride,
            IsSealed = ev.IsSealed,
            IsStatic = ev.IsStatic,
            IsSynthetic = ev.IsSynthetic,
            IsVirtual = ev.IsVirtual,
            HasBody = true, // even if it's compiler-generated; the body still exists
            ReturnType = KnownTypeReference.Void
        };
        a.Parameters.Add(valueParameter);
        return a;
    }

    public override IUnresolvedEntity VisitCustomEventDeclaration(CustomEventDeclaration eventDeclaration)
    {
        var e = new DefaultUnresolvedEvent(_currentTypeDefinition, eventDeclaration.Name)
        {
            Region = MakeRegion(eventDeclaration),
            BodyRegion = MakeBraceRegion(eventDeclaration)
        };
        ApplyModifiers(e, eventDeclaration.Modifiers);
        e.ReturnType = ConvertTypeReference(eventDeclaration.ReturnType);
        ConvertAttributes(e.Attributes, eventDeclaration.Attributes);
        AddXmlDocumentation(e, eventDeclaration);

        if (!eventDeclaration.PrivateImplementationType.IsNull)
        {
            e.Accessibility = Accessibility.None;
            e.IsExplicitInterfaceImplementation = true;
            e.ExplicitInterfaceImplementations.Add(_interningProvider.Intern(new DefaultMemberReference(
                e.SymbolKind, eventDeclaration.PrivateImplementationType.ToTypeReference(), e.Name)));
        }

        // custom events can't be extern; the non-custom event syntax must be used for extern events
        e.AddAccessor = ConvertAccessor(eventDeclaration.AddAccessor, e, "add_", false);
        e.RemoveAccessor = ConvertAccessor(eventDeclaration.RemoveAccessor, e, "remove_", false);

        _currentTypeDefinition.Members.Add(e);
        e.ApplyInterningProvider(_interningProvider);
        return e;
    }

    #endregion

    #region Modifiers

    private static void ApplyModifiers(DefaultUnresolvedTypeDefinition td, Modifiers modifiers)
    {
        td.Accessibility = GetAccessibility(modifiers) ?? (td.DeclaringTypeDefinition != null ? Accessibility.Private : Accessibility.Internal);
        td.IsAbstract = (modifiers & (Modifiers.Abstract | Modifiers.Static)) != 0;
        td.IsSealed = (modifiers & (Modifiers.Sealed | Modifiers.Static)) != 0;
        td.IsShadowing = (modifiers & Modifiers.New) != 0;
        td.IsPartial = (modifiers & Modifiers.Partial) != 0;
    }

    private static void ApplyModifiers(AbstractUnresolvedMember m, Modifiers modifiers)
    {
        // members from interfaces are always Public+Abstract. (NOTE: 'new' modifier is valid in interfaces as well.)
        if (m.DeclaringTypeDefinition.Kind == TypeKind.Interface)
        {
            m.Accessibility = Accessibility.Public;
            m.IsAbstract = true;
            m.IsShadowing = (modifiers & Modifiers.New) != 0;
            return;
        }

        m.Accessibility = GetAccessibility(modifiers) ?? Accessibility.Private;
        m.IsAbstract = (modifiers & Modifiers.Abstract) != 0;
        m.IsOverride = (modifiers & Modifiers.Override) != 0;
        m.IsSealed = (modifiers & Modifiers.Sealed) != 0;
        m.IsShadowing = (modifiers & Modifiers.New) != 0;
        m.IsStatic = (modifiers & Modifiers.Static) != 0;
        m.IsVirtual = (modifiers & Modifiers.Virtual) != 0;
    }

    private static Accessibility? GetAccessibility(Modifiers modifiers) =>
        (modifiers & Modifiers.VisibilityMask) switch
        {
            Modifiers.Private => Accessibility.Private,
            Modifiers.Internal => Accessibility.Internal,
            Modifiers.Protected | Modifiers.Internal => Accessibility.ProtectedOrInternal,
            Modifiers.Protected => Accessibility.Protected,
            Modifiers.Public => Accessibility.Public,
            _ => null
        };

    #endregion

    #region Attributes

    public override IUnresolvedEntity VisitAttributeSection(AttributeSection attributeSection)
    {
        // non-assembly attributes are handled by their parent entity
        if (attributeSection.AttributeTarget == "assembly")
            ConvertAttributes(_unresolvedFile.AssemblyAttributes, attributeSection);
        else if (attributeSection.AttributeTarget == "module")
            ConvertAttributes(_unresolvedFile.ModuleAttributes, attributeSection);

        return null;
    }

    private void ConvertAttributes(IList<IUnresolvedAttribute> outputList, IEnumerable<AttributeSection> attributes)
    {
        foreach (var section in attributes) ConvertAttributes(outputList, section);
    }

    private void ConvertAttributes(ICollection<IUnresolvedAttribute> outputList, AttributeSection attributeSection)
    {
        foreach (var attr in attributeSection.Attributes) outputList.Add(ConvertAttribute(attr));
    }

    internal static ITypeReference ConvertAttributeType(AstType type, InterningProvider interningProvider)
    {
        var tr = type.ToTypeReference(NameLookupMode.Type, interningProvider);

        if (!type.GetChildByRole(Roles.Identifier).IsVerbatim)
        {
            // Try to add "Attribute" suffix, but only if the identifier
            // (=last identifier in fully qualified name) isn't a verbatim identifier.
            if (tr is SimpleTypeOrNamespaceReference st)
                return interningProvider.Intern(new AttributeTypeReference(st, interningProvider.Intern(st.AddSuffix("Attribute"))));

            if (tr is MemberTypeOrNamespaceReference mt)
                return interningProvider.Intern(new AttributeTypeReference(mt, interningProvider.Intern(mt.AddSuffix("Attribute"))));
        }

        return tr;
    }

    private CSharpAttribute ConvertAttribute(Attribute attr)
    {
        var region = MakeRegion(attr);
        var type = ConvertAttributeType(attr.Type, _interningProvider);
        List<IConstantValue> positionalArguments = null;
        List<KeyValuePair<string, IConstantValue>> namedCtorArguments = null;
        List<KeyValuePair<string, IConstantValue>> namedArguments = null;

        foreach (var expr in attr.Arguments)
            if (expr is NamedArgumentExpression namedArgumentExpression)
            {
                namedCtorArguments ??= new List<KeyValuePair<string, IConstantValue>>();
                namedCtorArguments.Add(KeyValuePair.Create(_interningProvider.Intern(namedArgumentExpression.Name),
                    ConvertAttributeArgument(namedArgumentExpression.Expression)));
            }
            else if (expr is NamedExpression namedExpression)
            {
                namedArguments ??= new List<KeyValuePair<string, IConstantValue>>();
                namedArguments.Add(KeyValuePair.Create(_interningProvider.Intern(namedExpression.Name),
                    ConvertAttributeArgument(namedExpression.Expression)));
            }
            else
            {
                positionalArguments ??= new List<IConstantValue>();
                positionalArguments.Add(ConvertAttributeArgument(expr));
            }

        return new CSharpAttribute(type, region, _interningProvider.InternList(positionalArguments), namedCtorArguments, namedArguments);
    }

    #endregion

    #region Types

    private ITypeReference ConvertTypeReference(AstType type, NameLookupMode lookupMode = NameLookupMode.Type) =>
        type.ToTypeReference(lookupMode, _interningProvider);

    #endregion

    #region Constant Values

    private IConstantValue ConvertConstantValue(ITypeReference targetType, AstNode expression) =>
        ConvertConstantValue(targetType, expression, _currentTypeDefinition, _currentMethod, _usingScope, _interningProvider);

    internal static IConstantValue ConvertConstantValue(
        ITypeReference targetType, AstNode expression,
        IUnresolvedTypeDefinition parentTypeDefinition, IUnresolvedMethod parentMethodDefinition, UsingScope parentUsingScope,
        InterningProvider interningProvider)
    {
        var b = new ConstantValueBuilder(false, interningProvider);
        var c = expression.AcceptVisitor(b);

        if (c == null)
            return new ErrorConstantValue(targetType);

        return c is PrimitiveConstantExpression pc && pc.Type == targetType
            // Save memory by directly using a SimpleConstantValue.
            ? interningProvider.Intern(new SimpleConstantValue(targetType, pc.Value))
            // cast to the desired type
            : interningProvider.Intern(new ConstantCast(targetType, c, true));
    }

    private IConstantValue ConvertAttributeArgument(Expression expression) =>
        expression.AcceptVisitor(new ConstantValueBuilder(true, _interningProvider));

    sealed class ConstantValueBuilder : DepthFirstAstVisitor<ConstantExpression>
    {
        private readonly InterningProvider _localInterningProvider;
        private readonly bool _isAttributeArgument;

        public ConstantValueBuilder(bool isAttributeArgument, InterningProvider interningProvider)
        {
            _localInterningProvider = interningProvider;
            _isAttributeArgument = isAttributeArgument;
        }

        protected override ConstantExpression VisitChildren(AstNode node) => null;

        public override ConstantExpression VisitNullReferenceExpression(NullReferenceExpression nullReferenceExpression) =>
            _localInterningProvider.Intern(new PrimitiveConstantExpression(KnownTypeReference.Object, null));

        public override ConstantExpression VisitSizeOfExpression(SizeOfExpression sizeOfExpression) =>
            new SizeOfConstantValue(sizeOfExpression.Type.ToTypeReference(NameLookupMode.Type, _localInterningProvider));

        public override ConstantExpression VisitPrimitiveExpression(PrimitiveExpression primitiveExpression)
        {
            var val = _localInterningProvider.InternValue(primitiveExpression.Value);
            var typeCode = val == null ? TypeCode.Object : Type.GetTypeCode(val.GetType());
            return _localInterningProvider.Intern(new PrimitiveConstantExpression(typeCode.ToTypeReference(), val));
        }

        private ITypeReference ConvertTypeReference(AstType type) => type.ToTypeReference(NameLookupMode.Type, _localInterningProvider);

        private IList<ITypeReference> ConvertTypeArguments(AstNodeCollection<AstType> types)
        {
            var count = types.Count;

            if (count == 0)
                return null;

            var result = new ITypeReference[count];
            var pos = 0;

            foreach (var type in types) result[pos++] = ConvertTypeReference(type);

            return _localInterningProvider.InternList(result);
        }

        public override ConstantExpression VisitIdentifierExpression(IdentifierExpression identifierExpression)
        {
            var identifier = _localInterningProvider.Intern(identifierExpression.Identifier);
            return new ConstantIdentifierReference(identifier, ConvertTypeArguments(identifierExpression.TypeArguments));
        }

        public override ConstantExpression VisitMemberReferenceExpression(MemberReferenceExpression memberReferenceExpression)
        {
            var memberName = _localInterningProvider.Intern(memberReferenceExpression.MemberName);

            if (memberReferenceExpression.Target is TypeReferenceExpression tre)
                // handle "int.MaxValue"
                return new ConstantMemberReference(
                    ConvertTypeReference(tre.Type),
                    memberName,
                    ConvertTypeArguments(memberReferenceExpression.TypeArguments));

            return memberReferenceExpression.Target.AcceptVisitor(this) is { } v
                ? new ConstantMemberReference(v, memberName, ConvertTypeArguments(memberReferenceExpression.TypeArguments))
                : null;
        }

        public override ConstantExpression VisitParenthesizedExpression(ParenthesizedExpression parenthesizedExpression) =>
            parenthesizedExpression.Expression.AcceptVisitor(this);

        public override ConstantExpression VisitCastExpression(CastExpression castExpression)
        {
            var v = castExpression.Expression.AcceptVisitor(this);

            if (v == null)
                return null;

            var typeReference = ConvertTypeReference(castExpression.Type);
            return _localInterningProvider.Intern(new ConstantCast(typeReference, v, false));
        }

        public override ConstantExpression VisitCheckedExpression(CheckedExpression checkedExpression) =>
            checkedExpression.Expression.AcceptVisitor(this) is { } v
                ? new ConstantCheckedExpression(true, v)
                : null;

        public override ConstantExpression VisitUncheckedExpression(UncheckedExpression uncheckedExpression) =>
            uncheckedExpression.Expression.AcceptVisitor(this) is { } v
                ? new ConstantCheckedExpression(false, v)
                : null;

        public override ConstantExpression VisitDefaultValueExpression(DefaultValueExpression defaultValueExpression) =>
            _localInterningProvider.Intern(new ConstantDefaultValue(ConvertTypeReference(defaultValueExpression.Type)));

        public override ConstantExpression VisitUnaryOperatorExpression(UnaryOperatorExpression unaryOperatorExpression) =>
            unaryOperatorExpression.Expression.AcceptVisitor(this) is { } v
                ? unaryOperatorExpression.Operator switch
                {
                    UnaryOperatorType.Not or UnaryOperatorType.BitNot or UnaryOperatorType.Minus or UnaryOperatorType.Plus =>
                        new ConstantUnaryOperator(unaryOperatorExpression.Operator, v),
                    _ => null
                }
                : null;

        public override ConstantExpression VisitBinaryOperatorExpression(BinaryOperatorExpression binaryOperatorExpression)
        {
            var left = binaryOperatorExpression.Left.AcceptVisitor(this);
            var right = binaryOperatorExpression.Right.AcceptVisitor(this);

            return left == null || right == null
                ? null
                : new ConstantBinaryOperator(left, binaryOperatorExpression.Operator, right);
        }

        public override ConstantExpression VisitTypeOfExpression(TypeOfExpression typeOfExpression) =>
            _isAttributeArgument
                ? new TypeOfConstantExpression(ConvertTypeReference(typeOfExpression.Type))
                : null;

        public override ConstantExpression VisitObjectCreateExpression(ObjectCreateExpression objectCreateExpression) =>
            !objectCreateExpression.Arguments.Any()
                ? CreateConstant(objectCreateExpression)
                : null;

        // built in primitive type constants can be created with new
        // Todo: correctly resolve the type instead of doing the string approach
        private static ConstantExpression CreateConstant(ObjectCreateExpression objectCreateExpression) =>
            objectCreateExpression.Type.ToString() switch
            {
                "System.Boolean" or "bool" => new PrimitiveConstantExpression(KnownTypeReference.Boolean, new bool()),
                "System.Char" or "char" => new PrimitiveConstantExpression(KnownTypeReference.Char, new char()),
                "System.SByte" or "sbyte" => new PrimitiveConstantExpression(KnownTypeReference.SByte, new sbyte()),
                "System.Byte" or "byte" => new PrimitiveConstantExpression(KnownTypeReference.Byte, new byte()),
                "System.Int16" or "short" => new PrimitiveConstantExpression(KnownTypeReference.Int16, new short()),
                "System.UInt16" or "ushort" => new PrimitiveConstantExpression(KnownTypeReference.UInt16, new ushort()),
                "System.Int32" or "int" => new PrimitiveConstantExpression(KnownTypeReference.Int32, new int()),
                "System.UInt32" or "uint" => new PrimitiveConstantExpression(KnownTypeReference.UInt32, new uint()),
                "System.Int64" or "long" => new PrimitiveConstantExpression(KnownTypeReference.Int64, new long()),
                "System.UInt64" or "ulong" => new PrimitiveConstantExpression(KnownTypeReference.UInt64, new ulong()),
                "System.Single" or "float" => new PrimitiveConstantExpression(KnownTypeReference.Single, new float()),
                "System.Double" or "double" => new PrimitiveConstantExpression(KnownTypeReference.Double, new double()),
                "System.Decimal" or "decimal" => new PrimitiveConstantExpression(KnownTypeReference.Decimal, new decimal()),
                _ => null
            };

        public override ConstantExpression VisitArrayCreateExpression(ArrayCreateExpression arrayCreateExpression)
        {
            var initializer = arrayCreateExpression.Initializer;

            // Attributes only allow one-dimensional arrays
            if (!_isAttributeArgument || initializer.IsNull || arrayCreateExpression.Arguments.Count >= 2)
                return null;

            var type = GetTypeFromArray(arrayCreateExpression);
            var elements = new ConstantExpression[initializer.Elements.Count];
            var pos = 0;

            foreach (var expr in initializer.Elements)
            {
                var c = expr.AcceptVisitor(this);

                if (c == null)
                    return null;

                elements[pos++] = c;
            }

            return new ConstantArrayCreation(type, elements);
        }

        private ITypeReference GetTypeFromArray(ArrayCreateExpression arrayCreateExpression)
        {
            if (arrayCreateExpression.Type.IsNull)
                return null;

            var type = ConvertTypeReference(arrayCreateExpression.Type);
            return arrayCreateExpression.AdditionalArraySpecifiers
                .Reverse()
                .Aggregate(type, (current, spec) => _localInterningProvider.Intern(new ArrayTypeReference(current, spec.Dimensions)));
        }
    }

    #endregion

    #region Parameters

    private void ConvertParameters(ICollection<IUnresolvedParameter> outputList, IEnumerable<ParameterDeclaration> parameters)
    {
        foreach (var parameterDeclaration in parameters)
        {
            var parameter = new DefaultUnresolvedParameter(ConvertTypeReference(parameterDeclaration.Type),
                _interningProvider.Intern(parameterDeclaration.Name))
            {
                Region = MakeRegion(parameterDeclaration)
            };
            ConvertAttributes(parameter.Attributes, parameterDeclaration.Attributes);

            switch (parameterDeclaration.ParameterModifier)
            {
                case ParameterModifier.In:
                    parameter.IsIn = true;
                    parameter.Type = _interningProvider.Intern(new ByReferenceTypeReference(parameter.Type));
                    break;
                case ParameterModifier.Ref:
                    parameter.IsRef = true;
                    parameter.Type = _interningProvider.Intern(new ByReferenceTypeReference(parameter.Type));
                    break;
                case ParameterModifier.Out:
                    parameter.IsOut = true;
                    parameter.Type = _interningProvider.Intern(new ByReferenceTypeReference(parameter.Type));
                    break;
                case ParameterModifier.Params:
                    parameter.IsParams = true;
                    break;
            }

            if (!parameterDeclaration.DefaultExpression.IsNull)
                parameter.DefaultValue = ConvertConstantValue(parameter.Type, parameterDeclaration.DefaultExpression);

            outputList.Add(_interningProvider.Intern(parameter));
        }
    }

    internal static IList<ITypeReference> GetParameterTypes(IEnumerable<ParameterDeclaration> parameters, InterningProvider interningProvider) =>
        parameters
            .Select(p => p.Type.ToTypeReference(NameLookupMode.Type, interningProvider) is var type &&
                         p.ParameterModifier is ParameterModifier.In or ParameterModifier.Ref or ParameterModifier.Out
                ? interningProvider.Intern(new ByReferenceTypeReference(type))
                : type)
            .ToArray();

    #endregion

    #region XML Documentation

    private void AddXmlDocumentation(IUnresolvedEntity entity, AstNode entityDeclaration)
    {
        if (SkipXmlDocumentation)
            return;

        StringBuilder documentation = null;

        // traverse children until the first non-whitespace node
        for (var node = entityDeclaration.FirstChild; node is { NodeType: NodeType.Whitespace }; node = node.NextSibling)
            if (node is Comment { IsDocumentation: true } c)
            {
                documentation ??= new StringBuilder();

                if (c.CommentType == CommentType.MultiLineDocumentation)
                    PrepareMultilineDocumentation(c.Content, documentation);
                else
                {
                    if (documentation.Length > 0)
                        documentation.AppendLine();

                    documentation.Append(c.Content.Length > 0 && c.Content[0] == ' ' ? c.Content[1..] : c.Content);
                }
            }

        if (documentation != null) _unresolvedFile.AddDocumentation(entity, documentation.ToString());
    }

    private static void PrepareMultilineDocumentation(string content, StringBuilder b)
    {
        using var reader = new StringReader(content);

        ReadFirstLine(b, reader);
        var lines = ReadLines(reader);

        if (lines.Count > 0)
        {
            var secondLine = lines[0];
            var patternLength = ReducePatternLength(lines, GetPatternLength(secondLine), secondLine);

            // Append the lines to the string builder:
            for (var i = 0; i < lines.Count; i++)
            {
                if (i == 0 && b.Length > 0)
                    b.Append(Environment.NewLine);

                b.Append(lines[i], patternLength, lines[i].Length - patternLength);
            }
        }
    }

    private static int ReducePatternLength(IEnumerable<string> lines, int patternLength, string secondLine) =>
        lines
            .Skip(1)
            .Aggregate(patternLength, (current, line) => secondLine
                .Take(current)
                .Zip(line)
                .Select((t, index) => (Index: index, IsSameChar: t.First == t.Second))
                .First(t => t.IsSameChar)
                .Index);

    private static int GetPatternLength(string secondLine) =>
        GetNotEmptyCharIndex(secondLine) is var patternLength && patternLength < secondLine.Length && secondLine[patternLength] == '*'
            ? GetNotEmptyCharIndex(secondLine, patternLength + 1)
            // no asterisk
            : 0;

    private static int GetNotEmptyCharIndex(string secondLine, int index = 0)
    {
        while (index < secondLine.Length && char.IsWhiteSpace(secondLine[index]))
            index++;

        return index;
    }

    private static void ReadFirstLine(StringBuilder b, TextReader reader)
    {
        var firstLine = reader.ReadLine();

        if (string.IsNullOrWhiteSpace(firstLine)) return;

        if (firstLine[0] == ' ')
            b.Append(firstLine, 1, firstLine.Length - 1);
        else
            b.Append(firstLine);
    }

    private static List<string> ReadLines(TextReader reader)
    {
        var lines = new List<string>();

        while (reader.ReadLine() is { } line)
            lines.Add(line);

        // If the last line (the line with '*/' delimiter) is white space only, ignore it.

        if (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            lines.RemoveAt(lines.Count - 1);

        return lines;
    }

    #endregion
}