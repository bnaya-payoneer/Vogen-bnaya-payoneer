﻿using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Vogen.Diagnostics;

namespace Vogen.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DoNotCompareWithPrimitivesInEfCoreAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> _knownNames =
        new[] { "Where", "Single", "SkipWhile", "TakeWhile", "Select" }.ToImmutableHashSet();

    // ReSharper disable once ArrangeObjectCreationWhenTypeEvident - current bug in Roslyn analyzer means it
    // won't find this and will report:
    // "error RS2002: Rule 'XYZ123' is part of the next unshipped analyzer release, but is not a supported diagnostic for any analyzer"
    private static readonly DiagnosticDescriptor _rule = new DiagnosticDescriptor(
        RuleIdentifiers.DoNotCompareWithPrimitivesInEfCore,
        "Comparing primitives with value objects in EFCore expressions can cause casting issues",
        "Value object '{0}' is being compared to an int. Compare it with the value object instead.",
        RuleCategories.Usage,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
        "The value object is being compared with a primitive in an EFCore expression. This can lead to EFCore trying and failing to cast between the two.");


    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(_rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeQueryExpression, SyntaxKind.QueryExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax) context.Node;
        if (invocationExpr.Expression is not MemberAccessExpressionSyntax memberAccessExpr)
        {
            return;
        }

        if (!_knownNames.Contains(memberAccessExpr.Name.Identifier.Text))
        {
            return;
        }

        ExpressionSyntax expressionSyntax = memberAccessExpr.Expression;

        if (!IsAMemberOfDbSet(context, expressionSyntax))
        {
            return;
        }

        foreach (ArgumentSyntax eachArgument in
                 invocationExpr.ArgumentList.Arguments.Where(e => e.Expression is LambdaExpressionSyntax))
        {
            foreach (BinaryExpressionSyntax eachBinaryExpression in eachArgument.DescendantNodes().OfType<BinaryExpressionSyntax>())
            {
                ITypeSymbol? left = context.SemanticModel.GetTypeInfo(eachBinaryExpression.Left).Type;
                ITypeSymbol? right = context.SemanticModel.GetTypeInfo(eachBinaryExpression.Right).Type;

                if (left is null || right is null)
                {
                    continue;
                }

                // Check if left is ValueObject and right is integer
                if (IsValueObject(left) && right.SpecialType == SpecialType.System_Int32)
                {
                    context.ReportDiagnostic(
                        DiagnosticsCatalogue.BuildDiagnostic(_rule, left.Name, eachBinaryExpression.GetLocation()));
                }
            }
        }
    }

    private static void AnalyzeQueryExpression(SyntaxNodeAnalysisContext context)
    {
        var queryExpr = (QueryExpressionSyntax) context.Node;
        var whereClauses = queryExpr.Body.DescendantNodes().OfType<WhereClauseSyntax>();
        var fromClause = queryExpr.FromClause;

        if (!IsAMemberOfDbSet(context, fromClause.Expression))
        {
            return;
        }

        foreach (var eachArgument in whereClauses)
        {
            foreach (BinaryExpressionSyntax eachBinaryExpression in eachArgument.DescendantNodes().OfType<BinaryExpressionSyntax>())
            {
                ITypeSymbol? left = context.SemanticModel.GetTypeInfo(eachBinaryExpression.Left).Type;
                ITypeSymbol? right = context.SemanticModel.GetTypeInfo(eachBinaryExpression.Right).Type;

                if (left is null || right is null)
                {
                    continue;
                }

                // Check if left is ValueObject and right is integer
                if (IsValueObject(left) && right.SpecialType == SpecialType.System_Int32)
                {
                    context.ReportDiagnostic(
                        DiagnosticsCatalogue.BuildDiagnostic(_rule, left.Name, eachBinaryExpression.GetLocation()));
                }
            }
        }
    }

    private static bool IsAMemberOfDbSet(SyntaxNodeAnalysisContext context, ExpressionSyntax expressionSyntax)
    {
        var symbolInfo = context.SemanticModel.GetSymbolInfo(expressionSyntax);

        ITypeSymbol? typeSymbol = null;

        if (symbolInfo.Symbol is IPropertySymbol ps)
        {
            typeSymbol = ps.Type;
        }

        if (symbolInfo.Symbol is ILocalSymbol ls)
        {
            typeSymbol = ls.Type;
        }

        if (typeSymbol is null)
        {
            return false;
        }

        var dbSetType = context.SemanticModel.Compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbSet`1");

        if (dbSetType is null)
        {
            return false;
        }

        return InheritsFrom(typeSymbol, dbSetType);
    }


    private static bool IsValueObject(ITypeSymbol type) =>
        type is INamedTypeSymbol symbol && VoFilter.IsTarget(symbol);

    private static bool InheritsFrom(ITypeSymbol? type, INamedTypeSymbol? baseType)
    {
        if (baseType is null)
        {
            return false;
        }

        while (type != null)
        {
            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, baseType))
            {
                return true;
            }

            type = type.BaseType!;
        }

        return false;
    }
}