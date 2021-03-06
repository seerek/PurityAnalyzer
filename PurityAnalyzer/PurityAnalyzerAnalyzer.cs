using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace PurityAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PurityAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        public const string PurityDiagnosticId = "PurityAnalyzer";
        public const string ReturnsNewObjectDiagnosticId = "ReturnsNewObjectAnalyzer";
        public const string GenericParameterNotUsedAsObjectDiagnosticId = "GenericParameterNotUsedAsObjectDiagnostic";


        private const string Category = "Purity";


        private static DiagnosticDescriptor ImpurityRule =
            new DiagnosticDescriptor(
                PurityDiagnosticId,
                "Impurity error",
                "{0}",
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "Impurity error");

        private static DiagnosticDescriptor NotALambdaRule =
            new DiagnosticDescriptor(
                PurityDiagnosticId,
                "Not a Lambda error",
                "{0}",
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "Not a Lambda error");

        private static DiagnosticDescriptor ReturnsNewObjectRule =
            new DiagnosticDescriptor(
                ReturnsNewObjectDiagnosticId,
                "Returns new object error",
                "{0}",
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "Returns new object error");

        private static DiagnosticDescriptor GenericParameterNotUsedAsObjectRule =
            new DiagnosticDescriptor(
                GenericParameterNotUsedAsObjectDiagnosticId,
                "Not used as object error",
                "{0}",
                Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "Not used as object error");

        public static Maybe<string> CustomPureTypesFilename { get; set; }
        public static Maybe<string> CustomPureMethodsFilename { get; set; }

        public static Maybe<string> CustomPureExceptLocallyMethodsFilename { get; set; }
        public static Maybe<string> CustomPureExceptReadLocallyMethodsFilename { get; set; }
        public static Maybe<string> CustomReturnsNewObjectMethodsFilename { get; set; }

        public static Maybe<(string fullClassName, string methodName)> PureLambdaMethod { get; set; }

        public static Func<SyntaxTree, Task<SemanticModel>> GetSemanticModelForSyntaxTreeAsync { get; set; }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ImpurityRule, ReturnsNewObjectRule, NotALambdaRule, GenericParameterNotUsedAsObjectRule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeMethodSyntaxNode, SyntaxKind.ConstructorDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeMethodSyntaxNode, SyntaxKind.MethodDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeMethodSyntaxNode, SyntaxKind.OperatorDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeClassSyntaxNode, SyntaxKind.ClassDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzePropertySyntaxNode, SyntaxKind.PropertyDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzePureLambdaMethod, SyntaxKind.InvocationExpression);
        }

        private void AnalyzePureLambdaMethod(
            SyntaxNodeAnalysisContext context)
        {
            if (PureLambdaMethod.HasNoValue)
                return;

            var knownSymbols =
                new KnownSymbols(
                    Utils.GetKnownPureMethods(),
                    Utils.GetKnownPureExceptLocallyMethods(),
                    Utils.GetKnownPureExceptReadLocallyMethods(),
                    Utils.GetKnownReturnsNewObjectMethods(context.SemanticModel),
                    Utils.GetKnownPureTypes(context.SemanticModel),
                    Utils.GetKnownNotUsedAsObjectTypeParameters(),
                    new Dictionary<string, string[]>()); //TODO: get from file

            InvocationExpressionSyntax expression = (InvocationExpressionSyntax) context.Node;

            if (!Utils.IsPureLambdaMethodInvocation(
                context.SemanticModel,
                PureLambdaMethod.GetValue().fullClassName,
                PureLambdaMethod.GetValue().methodName,
                expression))
                return;

            var arguments = expression.ArgumentList.Arguments.Select(x => x.Expression).ToList();

            foreach (var argument in arguments)
            {
                if (!(argument is LambdaExpressionSyntax lambda))
                {
                    var diagnostic = Diagnostic.Create(
                        NotALambdaRule,
                        argument.GetLocation(),
                        "Only lambda arguments can be used with Pure Lambda methods");

                    context.ReportDiagnostic(diagnostic);

                    continue;
                }

                ProcessImpuritiesForMethod(
                    context,
                    lambda,
                    knownSymbols,
                    pureLambdaConfig: new PureLambdaConfig(
                        lambda,
                        PureLambdaMethod.GetValue().fullClassName,
                        PureLambdaMethod.GetValue().methodName));
            }
        }

        private void AnalyzeMethodSyntaxNode(SyntaxNodeAnalysisContext context)
        {
            var knownSymbols =
                new KnownSymbols(
                    Utils.GetKnownPureMethods(),
                    Utils.GetKnownPureExceptLocallyMethods(),
                    Utils.GetKnownPureExceptReadLocallyMethods(),
                    Utils.GetKnownReturnsNewObjectMethods(context.SemanticModel),
                    Utils.GetKnownPureTypes(context.SemanticModel),
                    Utils.GetKnownNotUsedAsObjectTypeParameters(),
                    new Dictionary<string, string[]>()); //TODO: get from file

            var methodDeclaration = (BaseMethodDeclarationSyntax) context.Node;

            if (methodDeclaration is MethodDeclarationSyntax method && (method.TypeParameterList?.Parameters.Any() ?? false))
            {
                ProcessNotUsedAsObjectAttribute(context, method, context.SemanticModel, knownSymbols, method.TypeParameterList);
            }

            var attributes = GetAttributes(methodDeclaration.AttributeLists);

            if (attributes.Any(Utils.IsIsPureAttribute))
            {
                ProcessImpuritiesForMethod(context, methodDeclaration, knownSymbols);
                return;
            }

            attributes.FirstOrNoValue(Utils.IsIsPureExceptLocallyAttribute).ExecuteIfHasValue(attribute =>
            {
                if (methodDeclaration.IsStatic())
                {
                    var diagnostic = Diagnostic.Create(
                        ImpurityRule,
                        attribute.GetLocation(),
                        "IsPureExceptLocallyAttribute cannot be applied on static methods");

                    context.ReportDiagnostic(diagnostic);
                    return;
                }

                ProcessImpuritiesForMethod(
                    context,
                    methodDeclaration,
                    knownSymbols,
                    PurityType.PureExceptLocally);
            });

            attributes.FirstOrNoValue(Utils.IsIsPureExceptReadLocallyAttribute).ExecuteIfHasValue(attribute =>
            {
                if (methodDeclaration.IsStatic())
                {
                    var diagnostic = Diagnostic.Create(
                        ImpurityRule,
                        attribute.GetLocation(),
                        "IsPureExceptReadLocallyAttribute cannot be applied on static methods");

                    context.ReportDiagnostic(diagnostic);
                    return;
                }

                ProcessImpuritiesForMethod(
                    context,
                    methodDeclaration,
                    knownSymbols,
                    PurityType.PureExceptReadLocally);
            });

            attributes.FirstOrNoValue(Utils.IsReturnsNewObjectAttribute).ExecuteIfHasValue(attribute =>
            {
                var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);

                if (methodSymbol != null)
                {
                    ProcessNonNewObjectReturnsForMethod(
                        context,
                        methodDeclaration,
                        knownSymbols,
                        methodSymbol);
                }
            });

            foreach (var attribute in attributes)
            {
                if(!Utils.IsDoesNotUseClassTypeParameterAsObjectAttribute(attribute, out var typeParameterIdentifier))
                    continue;

                var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);

                if (methodSymbol != null)
                {
                    ProcessDoesNotUseClassTypeParameterAsObjectAttributeForMethod(
                        context,
                        methodDeclaration,
                        knownSymbols,
                        typeParameterIdentifier);
                }
            }

        }

        private void ProcessDoesNotUseClassTypeParameterAsObjectAttributeForMethod(
            SyntaxNodeAnalysisContext context,
            BaseMethodDeclarationSyntax methodDeclaration,
            KnownSymbols knownSymbols,
            IdentifierNameSyntax typeParameterIdentifier)
        {
            var typeParameterSymbol =
                context.SemanticModel.GetSymbolInfo(typeParameterIdentifier).Symbol as ITypeParameterSymbol;


            if (typeParameterSymbol == null)
            {
                //TODO: handle this case
                return;
            }

            var relevantObjectMethods =
                TypeParametersUsedAsObjectsModule.GetObjectMethodsRelevantToCastingFromGenericTypeParameters(context.SemanticModel);

            var nodes = TypeParametersUsedAsObjectsModule.GetNodesWhereTIsUsedAsObject(
                methodDeclaration,
                context.SemanticModel,
                relevantObjectMethods,
                typeParameterSymbol,
                knownSymbols, RecursiveStateForNotUsedAsObject.Empty);

            foreach (var node in nodes)
            {
                var diagnostic = Diagnostic.Create(
                    GenericParameterNotUsedAsObjectRule,
                    node.GetLocation(),
                    "Type parameter is used as an object");

                context.ReportDiagnostic(diagnostic);
            }

        }


        private static void ProcessNotUsedAsObjectAttribute(
            SyntaxNodeAnalysisContext context,
            SyntaxNode scope,
            SemanticModel semanticModel,
            KnownSymbols knownSymbols,
            TypeParameterListSyntax typeParameterList)
        {
            var relevantObjectMethods =
                TypeParametersUsedAsObjectsModule.GetObjectMethodsRelevantToCastingFromGenericTypeParameters(semanticModel);

            var typeParameters = typeParameterList.Parameters.ToList();

            foreach (var typeParameter in typeParameters)
            {
                if (!typeParameter.AttributeLists.SelectMany(x => x.Attributes)
                    .Any(Utils.IsNotUsedAsObjectAttribute)) continue;

                var nodes = TypeParametersUsedAsObjectsModule.GetNodesWhereTIsUsedAsObject(
                    scope,
                    semanticModel,
                    relevantObjectMethods,
                    semanticModel.GetDeclaredSymbol(typeParameter),
                    knownSymbols, RecursiveStateForNotUsedAsObject.Empty);

                foreach (var node in nodes)
                {
                    var diagnostic = Diagnostic.Create(
                        GenericParameterNotUsedAsObjectRule,
                        node.GetLocation(),
                        "Type parameter is used as an object");

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private void ProcessNonNewObjectReturnsForMethod(
            SyntaxNodeAnalysisContext context,
            BaseMethodDeclarationSyntax methodDeclaration,
            KnownSymbols knownSymbols,
            IMethodSymbol methodSymbol)
        {
            if (methodSymbol.IsAbstract)
                return;

            foreach (var expression in Utils.GetNonNewObjectReturnsForMethod(methodDeclaration, context.SemanticModel, knownSymbols, RecursiveState.Empty))
            {
                var diagnostic = Diagnostic.Create(
                    ReturnsNewObjectRule,
                    expression.Parent.GetLocation(),
                    "non-new object return");

                context.ReportDiagnostic(diagnostic);
            }
        }

        private void ProcessNonNewObjectReturnsForProperty(
            SyntaxNodeAnalysisContext context,
            PropertyDeclarationSyntax propertyDeclaration,
            KnownSymbols knownSymbols,
            IPropertySymbol propertySymbol)
        {
            if (propertySymbol.IsAbstract)
                return;

            bool isAutoPropertyWithGetAccessor =
                propertyDeclaration.AccessorList != null 
                && propertyDeclaration.AccessorList.Accessors.Any(x =>
                    x.Keyword.Kind() == SyntaxKind.GetKeyword)
                && propertyDeclaration.AccessorList.Accessors.All(x =>
                    x.Body == null && x.ExpressionBody == null);

            if (isAutoPropertyWithGetAccessor)
            {
                var diagnostic = Diagnostic.Create(
                    ReturnsNewObjectRule,
                    propertyDeclaration.GetLocation(),
                    "Auto properties do not return new objects");

                context.ReportDiagnostic(diagnostic);

                return;
            }

            foreach (var expression in Utils.GetNonNewObjectReturnsForPropertyGet(propertyDeclaration, context.SemanticModel, knownSymbols, RecursiveState.Empty))
            {
                var diagnostic = Diagnostic.Create(
                    ReturnsNewObjectRule,
                    expression.Parent.GetLocation(),
                    "non-new object return");

                context.ReportDiagnostic(diagnostic);
            }
        }


        private static AttributeSyntax[] GetAttributes(SyntaxList<AttributeListSyntax> methodDeclarationAttributeLists)
        {
            return methodDeclarationAttributeLists.SelectMany(x => x.Attributes).ToArray();
        }


        private void AnalyzeClassSyntaxNode(SyntaxNodeAnalysisContext context)
        {
            var knownSymbols =
                new KnownSymbols(
                    Utils.GetKnownPureMethods(),
                    Utils.GetKnownPureExceptLocallyMethods(),
                    Utils.GetKnownPureExceptReadLocallyMethods(),
                    Utils.GetKnownReturnsNewObjectMethods(context.SemanticModel),
                    Utils.GetKnownPureTypes(context.SemanticModel),
                    Utils.GetKnownNotUsedAsObjectTypeParameters(),
                    new Dictionary<string, string[]>()); //TODO: get from file

            var classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;

            if (classDeclarationSyntax.AttributeLists.SelectMany(x => x.Attributes).Select(x => x.Name)
                .OfType<IdentifierNameSyntax>().Any(x => Utils.IsIsPureAttribute(x.Identifier.Text)))
            {
                foreach (var methodDeclaration in
                    classDeclarationSyntax.Members
                        .OfType<MethodDeclarationSyntax>()
                        .Cast<MemberDeclarationSyntax>()
                        .Concat(classDeclarationSyntax.Members.OfType<ConstructorDeclarationSyntax>()))
                {
                    ProcessImpuritiesForMethod(context, methodDeclaration, knownSymbols);
                }

                foreach (var propertyDeclaration in classDeclarationSyntax.Members.OfType<PropertyDeclarationSyntax>())
                {
                    ProcessImpuritiesForProperty(context, propertyDeclaration, knownSymbols);
                }

                foreach (var fieldDeclaration in classDeclarationSyntax.Members.OfType<FieldDeclarationSyntax>())
                {
                    foreach (var fieldVar in fieldDeclaration.Declaration.Variables)
                    {
                        if (fieldVar.Initializer != null)
                        {
                            var initializedTo = fieldVar.Initializer.Value;

                            ProcessImpuritiesForMethod(context, initializedTo, knownSymbols);
                        }
                    }
                }
            }

            if (classDeclarationSyntax.TypeParameterList?.Parameters.Any() ?? false)
            {
                ProcessNotUsedAsObjectAttribute(context, classDeclarationSyntax, context.SemanticModel, knownSymbols, classDeclarationSyntax.TypeParameterList);
            }
        }

        private void AnalyzePropertySyntaxNode(
            SyntaxNodeAnalysisContext context)
        {
            var knownSymbols =
                new KnownSymbols(
                    Utils.GetKnownPureMethods(),
                    Utils.GetKnownPureExceptLocallyMethods(),
                    Utils.GetKnownPureExceptReadLocallyMethods(),
                    Utils.GetKnownReturnsNewObjectMethods(context.SemanticModel),
                    Utils.GetKnownPureTypes(context.SemanticModel),
                    Utils.GetKnownNotUsedAsObjectTypeParameters(),
                    new Dictionary<string, string[]>()); //TODO: get from file

            var propertyDeclarationSyntax = (PropertyDeclarationSyntax)context.Node;

            var attributes = propertyDeclarationSyntax.AttributeLists.SelectMany(x => x.Attributes).ToArray();

            if (attributes.Any(Utils.IsIsPureAttribute))
            {
                ProcessImpuritiesForProperty(context, propertyDeclarationSyntax, knownSymbols);
            }

            attributes.FirstOrNoValue(Utils.IsIsPureExceptLocallyAttribute).ExecuteIfHasValue(attribute =>
            {
                if (propertyDeclarationSyntax.IsStatic())
                {
                    var diagnostic = Diagnostic.Create(
                        ImpurityRule,
                        attribute.GetLocation(),
                        "IsPureExceptLocallyAttribute cannot be applied on static properties");

                    context.ReportDiagnostic(diagnostic);
                    return;
                }

                ProcessImpuritiesForProperty(
                    context,
                    propertyDeclarationSyntax,
                    knownSymbols,
                    purityType: PurityType.PureExceptLocally);
            });

            attributes.FirstOrNoValue(Utils.IsIsPureExceptReadLocallyAttribute).ExecuteIfHasValue(attribute =>
            {
                if (propertyDeclarationSyntax.IsStatic())
                {
                    var diagnostic = Diagnostic.Create(
                        ImpurityRule,
                        attribute.GetLocation(),
                        "IsPureExceptReadLocallyAttribute cannot be applied on static properties");

                    context.ReportDiagnostic(diagnostic);
                    return;
                }

                ProcessImpuritiesForProperty(
                    context,
                    propertyDeclarationSyntax,
                    knownSymbols,
                    purityType: PurityType.PureExceptReadLocally);
            });

            attributes.FirstOrNoValue(Utils.IsReturnsNewObjectAttribute).ExecuteIfHasValue(attribute =>
            {
                var propertySymbol = context.SemanticModel.GetDeclaredSymbol(propertyDeclarationSyntax);

                if (propertySymbol != null)
                {
                    ProcessNonNewObjectReturnsForProperty(
                        context,
                        propertyDeclarationSyntax,
                        knownSymbols,
                        propertySymbol);
                }
            });

        }

        private static void ProcessImpuritiesForProperty(
            SyntaxNodeAnalysisContext context,
            PropertyDeclarationSyntax propertyDeclarationSyntax,
            KnownSymbols knownSymbols,
            PurityType purityType = PurityType.Pure)
        {
            if (propertyDeclarationSyntax.AccessorList != null)
            {
                bool isAutoReadWriteProperty =
                    propertyDeclarationSyntax.AccessorList.Accessors.Count == 2
                    && propertyDeclarationSyntax.AccessorList.Accessors.Any(x =>
                        x.Keyword.Kind() == SyntaxKind.GetKeyword)
                    && propertyDeclarationSyntax.AccessorList.Accessors.Any(x =>
                        x.Keyword.Kind() == SyntaxKind.SetKeyword)
                    && propertyDeclarationSyntax.AccessorList.Accessors.All(x =>
                        x.Body == null && x.ExpressionBody == null);

                if (isAutoReadWriteProperty)
                {
                    if (purityType == PurityType.Pure || purityType == PurityType.PureExceptReadLocally)
                    {
                        var diagnostic = Diagnostic.Create(
                            ImpurityRule,
                            propertyDeclarationSyntax.AccessorList.GetLocation(),
                            "Impure auto property");

                        context.ReportDiagnostic(diagnostic);
                    }
                }
                else
                {
                    foreach (var accessor in propertyDeclarationSyntax.AccessorList.Accessors)
                    {
                        ProcessImpuritiesForMethod(
                            context, accessor, knownSymbols, purityType);
                    }
                }
            }
            else if (propertyDeclarationSyntax.ExpressionBody != null)
            {
                ProcessImpuritiesForMethod(
                    context,
                    propertyDeclarationSyntax.ExpressionBody,
                    knownSymbols,
                    purityType);
            }

            if (propertyDeclarationSyntax.Initializer != null)
            {
                var initializedTo = propertyDeclarationSyntax.Initializer.Value;

                ProcessImpuritiesForMethod(
                    context, initializedTo, knownSymbols, purityType);
            }

        }

        private static void ProcessImpuritiesForMethod(
            SyntaxNodeAnalysisContext context,
            SyntaxNode methodLikeNode,
            KnownSymbols knownSymbols,
            PurityType purityType = PurityType.Pure,
            Maybe<PureLambdaConfig> pureLambdaConfig = default)
        {
            var impurities =
                Utils.GetImpurities(
                    GetBodyOfDeclaration(methodLikeNode),
                    context.SemanticModel,
                    knownSymbols,
                    RecursiveState.Empty,
                    purityType,
                    pureLambdaConfig)
                    .ToList();

            if (methodLikeNode is ConstructorDeclarationSyntax constructor)
            {
                var containingType = constructor.FirstAncestorOrSelf<TypeDeclarationSyntax>();

                if (Utils.AnyImpureFieldInitializer(
                    containingType,
                    context.SemanticModel,
                    knownSymbols,
                    RecursiveState.Empty,
                    constructor.IsStatic() ? (InstanceStaticCombination) new InstanceStaticCombination.Static() : new InstanceStaticCombination.InstanceAndStatic()))
                {
                    impurities.Add(new Impurity(methodLikeNode, "There are impure field initializers"));
                }

                if (Utils.AnyImpurePropertyInitializer(
                    containingType,
                    context.SemanticModel,
                    knownSymbols,
                    RecursiveState.Empty,
                    constructor.IsStatic() ? (InstanceStaticCombination)new InstanceStaticCombination.Static() : new InstanceStaticCombination.InstanceAndStatic()))
                {
                    impurities.Add(new Impurity(methodLikeNode, "There are impure property initializers"));
                }
            }

            foreach (var impurity in impurities)
            {
                var diagnostic = Diagnostic.Create(
                    ImpurityRule,
                    impurity.Node.GetLocation(),
                    impurity.Message);

                context.ReportDiagnostic(diagnostic);
            }
        }

        public static SyntaxNode GetBodyOfDeclaration(SyntaxNode method)
        {
            if (method is BaseMethodDeclarationSyntax declarationSyntax)
                return GetMethodBody(declarationSyntax);

            return method;
        }

        public static SyntaxNode GetMethodBody(BaseMethodDeclarationSyntax method)
        {
            return method.Body ?? (SyntaxNode)method.ExpressionBody;
        }
    }
}
