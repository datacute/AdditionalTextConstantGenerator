using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using Datacute.IncrementalGeneratorExtensions;

namespace Datacute.AdditionalTextConstantGenerator
{
    /// <summary>
    /// Generate partial classes containing string constants derived from additional texts.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class Generator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            LightweightTrace.Add(GeneratorStage.Initialize);

            context.RegisterPostInitializationOutput(c =>
            {
                c.AddEmbeddedAttributeDefinition();
                c.AddSource($"Datacute.Attributes.AdditionalTextConstantsAttribute.g.cs", Templates.AttributeClass);
            });
            
            // 1. Base attribute data -> AttributeContext
            var attributeContexts =
                context.SelectAttributeContexts(
                    Templates.AttributeFullyQualified,
                    generatorAttributeSyntaxContext => new AttributeData(generatorAttributeSyntaxContext));

            // 2. Options -> GeneratorOptions
            var options =
                context.AnalyzerConfigOptionsProvider
                    .Select(GeneratorOptions.Select)
                    .WithTrackingName(GeneratorStage.AnalyzerConfigOptionsProviderSelect);

            // 3. Combine base attributes and options -> (AttributeContext, GeneratorOptions)
            var attributesAndOptions =
                attributeContexts
                    .Combine(options)
                    .Select(SelectAttributeAndOptions)
                    .WithTrackingName(TrackingNames.AttributesAndOptionsCombined);

            // --- Prepare AdditionalTextContent Matching Data Separately ---

            // Selects (AttributeContext, Path, Extension) from (AttributeContext, Options)
            var attributesAndGlobs =
                attributesAndOptions
                    .Select(SelectAttributesAndGlobs)
                    .WithTrackingName(TrackingNames.AttributeGlobInfoSelected);

            // Selects (Path, Extension) from (AttributeContext, Path, Extension)
            var attributeGlobs =
                attributesAndGlobs
                    .Select(SelectJustGlobs)
                    .WithTrackingName(TrackingNames.AttributeGlobsSelected);

            // 4. Find all (AttributeContext, AdditionalText) matches
            var matchedAdditionalTextAndAttribute =
                context.AdditionalTextsProvider
                    .Select(SelectFileInfo)
                    .WithTrackingName(GeneratorStage.AdditionalTextsProviderSelect)
                    .CombineEquatable(attributeGlobs)
                    .WithTrackingName(TrackingNames.FileInfoAndGlobsCombined)
                    .Select(SelectAdditionalTextAndGlobWithAttributeGlobs)
                    .Where(DoesAdditionalTextGlobMatchAnyAttributeGlobs)
                    .WithTrackingName(TrackingNames.MatchingFilesFiltered)
                    .Select(ExtractAdditionalTextWithFileInfo)
                    .WithTrackingName(TrackingNames.AdditionalTextExtracted)
                    .CombineEquatable(attributesAndGlobs)
                    .WithTrackingName(TrackingNames.AdditionalTextAndAllAttributeGlobsCombined)
                    .SelectMany(SelectMatchingTextAndAttribute)
                    .WithTrackingName(TrackingNames.MatchingTextAndAttributeSelected);

            // 5. Group texts by AttributeContext into a lookup
            var additionalTextsByAttributeContextLookup =
                matchedAdditionalTextAndAttribute
                    .CollectEquatable()
                    .Select(GroupAdditionalTextsByAttributeContext)
                    .WithTrackingName(TrackingNames.AdditionalTextsGroupedByAttributeContext);

            // --- Combine Base Attributes with Grouped AdditionalTexts (Left Join) ---

            // 6. Combine the main attributes stream with the single lookup dictionary
            var generationInput =
                attributesAndOptions
                    .Combine(additionalTextsByAttributeContextLookup)
                    .Select(PerformAdditionalTextLookup)
                    .WithTrackingName(TrackingNames.GenerationInputPrepared);

            // 7. Register Source Output
            context.RegisterSourceOutput(generationInput,
                (sourceProductionContext, inputData) =>
                {
                    LightweightTrace.Add(GeneratorStage.RegisterSourceOutput);
                    var (attributeContext, generatorOptions, additionalTexts) = inputData;
                    GenerateFolderEmbed(sourceProductionContext, attributeContext, additionalTexts, generatorOptions);
                });
        }

        // --- Helper Methods ---

        // Selects (AdditionalText, Directory, Extension) from AdditionalText
        private static AttributeAndOptions SelectAttributeAndOptions((AttributeContextAndData<AttributeData> AttributeContext, GeneratorOptions Options) attributeAndOptions, CancellationToken _) =>
            new(attributeAndOptions.AttributeContext, attributeAndOptions.Options);

        // Selects (AttributeContext, Path, Extension) from (AttributeContext, Options)
        private static AttributeAndGlob SelectAttributesAndGlobs(AttributeAndOptions attributeAndOptions, CancellationToken ct)
        {
            var additionalTextSearchPath = GetAdditionalTextSearchPath(attributeAndOptions.AttributeContext.AttributeData, attributeAndOptions.Options);
            var glob = new Glob(additionalTextSearchPath, attributeAndOptions.AttributeContext.AttributeData.ExtensionArg);
            return new AttributeAndGlob(attributeAndOptions.AttributeContext, glob);
        }

        // Selects (Path, Extension) from (AttributeContext, Path, Extension)
        private static Glob SelectJustGlobs(AttributeAndGlob attributeAndGlob, CancellationToken _) => attributeAndGlob.Glob;

        // Selects (AdditionalText, Directory, Extension) from AdditionalText
        private static AdditionalTextAndGlob SelectFileInfo(AdditionalText additionalText, CancellationToken _) =>
            new(additionalText, new Glob(Path.GetDirectoryName(additionalText.Path), Path.GetExtension(additionalText.Path)));

        private static AdditionalTextAndGlobWithAttributeGlobs SelectAdditionalTextAndGlobWithAttributeGlobs(
            (AdditionalTextAndGlob AdditionalTextAndGlob, EquatableImmutableArray<Glob> AttributeGlobs) source,
            CancellationToken _) =>
            new(source.AdditionalTextAndGlob, source.AttributeGlobs);

        // Checks if the file's (Directory, Extension) matches any glob in the list
        private static bool DoesAdditionalTextGlobMatchAnyAttributeGlobs(
            AdditionalTextAndGlobWithAttributeGlobs additionalTextAndGlobWithAttributeGlobs)
        {
            var attributeGlobsArray = additionalTextAndGlobWithAttributeGlobs.AttributeGlobs;
            var additionalTextAndGlob = additionalTextAndGlobWithAttributeGlobs.AdditionalTextAndGlob;
            var glob = additionalTextAndGlob.Glob;
            var directory = glob.Directory;
            var extension = glob.Extension;

            return attributeGlobsArray.Any(attributeGlob => 
                directory == attributeGlob.Directory && 
                (attributeGlob.Extension == ".*" || extension == attributeGlob.Extension));
        }

        // Extracts AdditionalText content, keeping the original file+glob info
        private static AdditionalTextGlobsAndAdditionalTextContents ExtractAdditionalTextWithFileInfo(
            AdditionalTextAndGlobWithAttributeGlobs additionalTextAndGlobWithAttributeGlobs, 
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(8);

            var additionalText = additionalTextAndGlobWithAttributeGlobs.AdditionalTextAndGlob.AdditionalText;
            LightweightTrace.Add(TrackingNames.GeneratingDocComment, additionalText.Path.Length);

            var additionalTextContent = AdditionalTextContentCreator.GenerateAdditionalTextContent(additionalText, ct);

            return new AdditionalTextGlobsAndAdditionalTextContents(additionalTextAndGlobWithAttributeGlobs, additionalTextContent);
        }

        // Creates (AttributeContext, AdditionalTextContent) for each matching attribute
        private static IEnumerable<AttributeAndAdditionalText> SelectMatchingTextAndAttribute(
                (AdditionalTextGlobsAndAdditionalTextContents TextAndFileData, EquatableImmutableArray<AttributeAndGlob> AttributesAndGlobs) 
                    textAndAllAttributeGlobs,
                CancellationToken ct)
        {
            var textAndFileData = textAndAllAttributeGlobs.TextAndFileData;
            var allAttributesAndGlobsArray = textAndAllAttributeGlobs.AttributesAndGlobs;
            var glob = textAndFileData.FileAndGlobs.AdditionalTextAndGlob.Glob;
            var directory = glob.Directory;
            var extension = glob.Extension;
            var additionalTextContent = textAndFileData.AdditionalTextContent;

            return allAttributesAndGlobsArray
                .Where(attrAndGlob => 
                    directory == attrAndGlob.Glob.Directory && 
                    (attrAndGlob.Glob.Extension == ".*" || extension == attrAndGlob.Glob.Extension))
                .Select(matchingAttrAndGlob => new AttributeAndAdditionalText(matchingAttrAndGlob.AttributeContext, additionalTextContent));
        }

        // Groups the collected (AttributeContext, AdditionalTextContent) into a dictionary lookup
        private static Dictionary<AttributeContextAndData<AttributeData>, EquatableImmutableArray<AdditionalTextContent>> GroupAdditionalTextsByAttributeContext(
            EquatableImmutableArray<AttributeAndAdditionalText> attributeAndAdditionalTexts, CancellationToken ct)
        {
            var grouped = new Dictionary<AttributeContextAndData<AttributeData>, ImmutableArray<AdditionalTextContent>.Builder>();
            foreach (var (context, additionalTextContent) in attributeAndAdditionalTexts)
            {
                ct.ThrowIfCancellationRequested();
                if (!grouped.TryGetValue(context, out var builder))
                {
                    builder = ImmutableArray.CreateBuilder<AdditionalTextContent>();
                    grouped[context] = builder;
                }
                builder.Add(additionalTextContent);
            }

            return grouped.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToImmutable().ToEquatableImmutableArray(ct));
        }

        // Performs the left join lookup for additional texts based on the AttributeContext
        private static AttributeOptionsAndAdditionalTexts PerformAdditionalTextLookup(
            (AttributeAndOptions AttributeAndOptions, Dictionary<AttributeContextAndData<AttributeData>, EquatableImmutableArray<AdditionalTextContent>> AdditionalTextsByAttributeContextLookup) attributeOptionsAndLookup,
            CancellationToken _)
        {
            var attributeContext = attributeOptionsAndLookup.AttributeAndOptions.AttributeContext;
            var options = attributeOptionsAndLookup.AttributeAndOptions.Options;
            var lookupDictionary = attributeOptionsAndLookup.AdditionalTextsByAttributeContextLookup;

            var additionalTextContentsArray = lookupDictionary.TryGetValue(attributeContext, out var foundAdditionalTextContentsArray)
                ? foundAdditionalTextContentsArray
                : EquatableImmutableArray<AdditionalTextContent>.Empty;

            return new AttributeOptionsAndAdditionalTexts(attributeContext, options, additionalTextContentsArray);
        }

        private static void GenerateFolderEmbed(
            in SourceProductionContext context,
            in AttributeContextAndData<AttributeData> attributeContext,
            EquatableImmutableArray<AdditionalTextContent> additionalTextContents,
            in GeneratorOptions options)
        {
            var cancellationToken = context.CancellationToken;
            cancellationToken.ThrowIfCancellationRequested(1);

            var additionalTextSearchPath = GetAdditionalTextSearchPath(attributeContext.AttributeData, options);

            var codeGenerator = new CodeGenerator(
                attributeContext,
                additionalTextSearchPath,
                additionalTextContents,
                options,
                cancellationToken);

            var hintName = attributeContext.CreateHintName("AdditionalTextConstants");
            var source = codeGenerator.GetSourceText();
            context.AddSource(hintName, source);
        }

        private static string GetAdditionalTextSearchPath(in AttributeData attributeData, in GeneratorOptions options)
        {
            string baseDir;
            string additionalTextSearchPath;

            var pathArg = attributeData.PathArg;

            if (pathArg.StartsWith("/") || pathArg.StartsWith("\\"))
            {
                baseDir = options.ProjectDir;
                additionalTextSearchPath = pathArg.Length > 0 ? pathArg.Substring(1) : string.Empty;
            }
            else
            {
                baseDir = Path.GetDirectoryName(attributeData.FilePath) ?? string.Empty;
                additionalTextSearchPath = pathArg;
            }

            try
            {
                return Path.GetFullPath(Path.Combine(baseDir, additionalTextSearchPath));
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
        }
    }

    public record struct AttributeAndOptions(AttributeContextAndData<AttributeData> AttributeContext, GeneratorOptions Options);
    public record struct Glob(string? Directory, string Extension);
    public record struct AttributeAndGlob(AttributeContextAndData<AttributeData> AttributeContext, Glob Glob);
    public record struct AdditionalTextAndGlob(AdditionalText AdditionalText, Glob Glob);
    public record struct AdditionalTextGlobsAndAdditionalTextContents(AdditionalTextAndGlobWithAttributeGlobs FileAndGlobs, AdditionalTextContent AdditionalTextContent);
    public record struct AdditionalTextAndGlobWithAttributeGlobs(AdditionalTextAndGlob AdditionalTextAndGlob, EquatableImmutableArray<Glob> AttributeGlobs);
    public record struct AttributeAndAdditionalText(AttributeContextAndData<AttributeData> AttributeContext, AdditionalTextContent TextContent);
    public record struct AttributeOptionsAndAdditionalTexts(AttributeContextAndData<AttributeData> Context, GeneratorOptions Options, EquatableImmutableArray<AdditionalTextContent> TextContents);
}
