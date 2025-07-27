using Datacute.IncrementalGeneratorExtensions;

namespace Datacute.AdditionalTextConstantGenerator
{
    public enum TrackingNames
    {
        // Pipeline Stages
        AttributesAndOptionsCombined = 130,      // Step 3: Attributes and Options combined
        AttributeGlobInfoSelected = 141,         // Helper: Path/Extension selected for attributes
        AttributeGlobsSelected = 142,             // Helper: Just Path/Extension for resource matching
        FileInfoAndGlobsCombined = 143,          // Step 4b: FileInfo combined with ResourceGlobs
        MatchingFilesFiltered = 144,             // Step 4c: Files filtered by glob match
        AdditionalTextExtracted = 146,         // Step 4d: EmbeddedResource created (with FileAndGlobs info)
        AdditionalTextAndAllAttributeGlobsCombined = 147, // Step 4e: Resource/File data combined with all AttributeGlobInfo
        MatchingTextAndAttributeSelected = 148, // Step 4f: (AttributeContext, EmbeddedResource) selected
        AdditionalTextsGroupedByAttributeContext = 150, // Step 5: Resources grouped into lookup
        GenerationInputPrepared = 160,           // Step 6: Final data prepared for output (AttributeContext, Resources, Options)

        // Execution/Action Steps
        GeneratingDocComment = 145,              // Action: Doc comment generation started
        DiagnosticTraceLogWritten = 180          // Action: Trace log output written (if enabled)
    }

    public static class TrackingNameDescriptions
    {
        /// <summary>
        /// Gets a dictionary mapping event and counter IDs to their human-readable names.
        /// </summary>
        public static Dictionary<int, string> EventNameMap => LazyEventNameMap.Value;

        // GeneratorStageDescriptions.GeneratorStageNameMap is not available when this static class is initialized,
        // so we use a Lazy<T> to ensure that the dictionary is created only when it's first accessed.
        // at which time the GeneratorStageDescriptions.GeneratorStageNameMap will be available.
        private static readonly Lazy<Dictionary<int, string>> LazyEventNameMap = new Lazy<Dictionary<int, string>>(CreateEventNameMap);

        private static Dictionary<int, string> CreateEventNameMap()
        {
            var map = new Dictionary<int, string>(GeneratorStageDescriptions.GeneratorStageNameMap)
            {
                // Pipeline Stages
                { (int)TrackingNames.AttributesAndOptionsCombined, "Combined Attributes and Options" },
                { (int)TrackingNames.AttributeGlobInfoSelected, "Selected Attribute Glob Info (Path/Ext)" },
                { (int)TrackingNames.AttributeGlobsSelected, "Selected Attribute Globs (Path/Ext)" },
                { (int)TrackingNames.FileInfoAndGlobsCombined, "Combined File Info and AdditionalText Globs" },
                { (int)TrackingNames.MatchingFilesFiltered, "Filtered Files Matching Globs" },
                { (int)TrackingNames.AdditionalTextExtracted, "Extracted AdditionalText Content (with File/Glob info)" },
                { (int)TrackingNames.AdditionalTextAndAllAttributeGlobsCombined, "Combined AdditionalText/File Data and All Attribute Glob Info" },
                { (int)TrackingNames.MatchingTextAndAttributeSelected, "Selected Matching (AttributeContext, AdditionalTextContent)" },
                { (int)TrackingNames.AdditionalTextsGroupedByAttributeContext, "Grouped AdditionalTextContents by AttributeContext into Lookup" },
                { (int)TrackingNames.GenerationInputPrepared, "Prepared Final Generation Input (AttrContext, AdditionalTextContents, Options)" },
                
                // Execution/Action Steps
                { (int)TrackingNames.GeneratingDocComment, "Generating Doc Comment" },
                { (int)TrackingNames.DiagnosticTraceLogWritten, "Diagnostic Trace Log Written" },
            };

            return map;
        }
    }
}