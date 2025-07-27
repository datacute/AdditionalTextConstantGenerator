using Microsoft.CodeAnalysis;

namespace Datacute.AdditionalTextConstantGenerator;

public readonly record struct AttributeData
{
    public readonly string ExtensionArg;
    public readonly string PathArg;
    public readonly bool OutputDiagnosticTraceLog = false;
    public readonly string FilePath;

    public AttributeData(in GeneratorAttributeSyntaxContext generatorAttributeSyntaxContext)
    {
        var attributeTargetSymbol = (ITypeSymbol)generatorAttributeSyntaxContext.TargetSymbol;
        var attributeData = generatorAttributeSyntaxContext.Attributes[0];
        var args = attributeData.ConstructorArguments;
        ExtensionArg = (args.Length == 0 ? null : args[0].Value as string) ?? ".txt";
        PathArg = (args.Length < 2 ? null : args[1].Value as string) ?? attributeTargetSymbol.Name;
        if (!attributeData.NamedArguments.IsEmpty)
        {
            foreach (KeyValuePair<string, TypedConstant> namedArgument in attributeData.NamedArguments)
            {
                var value = namedArgument.Value.Value;
                if (value != null)
                {
                    switch (value)
                    {
                        case bool boolValue:
                            switch (namedArgument.Key)
                            {
                                case "DiagnosticTraceLog":
                                    OutputDiagnosticTraceLog = boolValue;
                                    break;
                            }

                            break;
                        case string stringValue:
                            switch (namedArgument.Key)
                            {
                                case "Extension":
                                    ExtensionArg = stringValue;
                                    break;
                                case "Path":
                                    PathArg = stringValue;
                                    break;
                            }

                            break;
                    }
                }
            }
        }

        FilePath = generatorAttributeSyntaxContext.TargetNode.SyntaxTree.FilePath;
    }
}