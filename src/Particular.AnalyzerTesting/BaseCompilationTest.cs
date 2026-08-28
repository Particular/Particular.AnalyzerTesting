namespace Particular.AnalyzerTesting;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// A base class for <see cref="AnalyzerTest" /> and <see cref="SourceGeneratorTest" />.
/// </summary>
public abstract class BaseCompilationTest<TSelf> where TSelf : BaseCompilationTest<TSelf>
{
    private protected readonly string outputAssemblyName;
    private protected readonly List<DiagnosticAnalyzer> analyzers = [];
    private protected OutputKind buildOutputType = OutputKind.DynamicallyLinkedLibrary;
    private protected bool suppressCompilationErrors;
    private protected readonly Dictionary<string, string> features = [];
    private protected readonly Dictionary<string, string> editorConfigOptions = [];
    private protected readonly Dictionary<string, Dictionary<string, string>> editorConfigOptionsByFilename = [];
    private protected readonly Dictionary<string, ReportDiagnostic> diagnosticSeverities = [];
    private protected readonly Dictionary<string, Dictionary<string, ReportDiagnostic>> diagnosticSeveritiesByFilename = [];
    private protected readonly Dictionary<string, ReportDiagnostic> globalDiagnosticSeverities = [];

    private protected BaseCompilationTest(string? outputAssemblyName = null)
    {
        this.outputAssemblyName = outputAssemblyName ?? "TestAssembly";

        foreach (var assembly in AssemblyLoadContext.Default.Assemblies)
        {
            if (!assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            {
                References.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
        }

        foreach (var assemblyPath in Directory.EnumerateFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"))
        {
            using var stream = File.OpenRead(assemblyPath);
            using var file = new PEReader(stream);

            if (file.HasMetadata)
            {
                References.Add(MetadataReference.CreateFromFile(assemblyPath));
            }
        }
    }

    private protected TSelf Self => (TSelf)this;

    /// <summary>
    /// Reference assemblies for the compilation. Can be added fluently using <see cref="AddReferences(MetadataReference[])" /> or <see cref="AddReferences(IEnumerable&lt;MetadataReference&gt;)" />.
    /// </summary>
    public List<MetadataReference> References { get; } = [];

    /// <summary>
    /// The C# version used to compile the test code, defaulting to the highest version for the Roslyn SDK you are using in the test. Set with <see cref="WithLangVersion" />.
    /// </summary>
    public LanguageVersion LangVersion { get; private set; } = LangVersionHelper.LatestForCurrentRoslynSdk;

    /// <summary>
    /// Add a Roslyn analyzer to the test.
    /// </summary>
    public TSelf WithAnalyzer<TAnalyzer>() where TAnalyzer : DiagnosticAnalyzer, new()
    {
        analyzers.Add(new TAnalyzer());
        return Self;
    }

    /// <summary>
    /// Set the C# version for the test.
    /// </summary>
    public TSelf WithLangVersion(LanguageVersion langVersion)
    {
        LangVersion = langVersion;
        return Self;
    }

    /// <summary>
    /// Add reference assemblies for the test.
    /// </summary>
    public TSelf AddReferences(params MetadataReference[] references)
        => AddReferences(references.AsEnumerable());

    /// <summary>
    /// Add reference assemblies for the test.
    /// </summary>
    public TSelf AddReferences(IEnumerable<MetadataReference> references)
    {
        References.AddRange(references);
        return Self;
    }

    /// <summary>
    /// Change the build output from ClassLibrary to another <see cref="OutputKind" />.
    /// </summary>
    public TSelf BuildAs(OutputKind outputKind)
    {
        buildOutputType = outputKind;
        return Self;
    }

    /// <summary>
    /// Suppress compilation errors in the test, for analyzers that need to run on code that does not compile,
    /// for example when a code fix exists to update from an obsolete API to a new one.
    /// </summary>
    public TSelf SuppressCompilationErrors()
    {
        suppressCompilationErrors = true;
        return Self;
    }

    /// <summary>
    /// Add an interceptors namespace to the project properties, more easily than using <see cref="WithProperty" /> directly.
    /// </summary>
    public TSelf WithInterceptorNamespace(string interceptorNamespace)
    {
        const string key = "InterceptorsNamespaces";
        if (features.TryGetValue(key, out var value))
        {
            features[key] = $"{value};{interceptorNamespace}";
        }
        else
        {
            features[key] = interceptorNamespace;
        }

        return Self;
    }

    /// <summary>
    /// Add a build property to the compilation. The property is available through both the global and syntax-tree analyzer config options.
    /// </summary>
    public TSelf WithProperty(string name, string value)
    {
        features.Add(name, value);
        return Self;
    }

    /// <summary>
    /// Add an EditorConfig option that is available through syntax-tree analyzer config options.
    /// Omit <paramref name="filename" /> to apply the option to every source file.
    /// The option is not added to global analyzer config options.
    /// </summary>
    public TSelf WithEditorConfigOption(string name, string value, string? filename = null)
    {
        if (filename is null)
        {
            editorConfigOptions.Add(name, value);
            return Self;
        }

        if (!editorConfigOptionsByFilename.TryGetValue(filename, out var fileOptions))
        {
            fileOptions = [];
            editorConfigOptionsByFilename.Add(filename, fileOptions);
        }

        fileOptions.Add(name, value);
        return Self;
    }

    /// <summary>
    /// Configure the severity of a diagnostic id for every source file in the test, equivalent to
    /// <c>dotnet_diagnostic.&lt;id&gt;.severity = ...</c> in an .editorconfig applied to all files.
    /// The severity is available to Roslyn through the compilation's <see cref="SyntaxTreeOptionsProvider" />.
    /// </summary>
    public TSelf WithDiagnosticSeverity(string diagnosticId, ReportDiagnostic severity)
    {
        diagnosticSeverities[diagnosticId] = severity;
        return Self;
    }

    /// <summary>
    /// Configure the severity of a diagnostic id for a specific source file, equivalent to a
    /// file-scoped <c>[filename]</c> .editorconfig section. A file-specific severity overrides the
    /// all-source severity for that file.
    /// </summary>
    public TSelf WithDiagnosticSeverity(string diagnosticId, ReportDiagnostic severity, string filename)
    {
        if (!diagnosticSeveritiesByFilename.TryGetValue(filename, out var fileSeverities))
        {
            fileSeverities = [];
            diagnosticSeveritiesByFilename.Add(filename, fileSeverities);
        }

        fileSeverities[diagnosticId] = severity;
        return Self;
    }

    /// <summary>
    /// Configure the severity of a diagnostic id globally for the compilation, equivalent to a global
    /// configuration file. A global severity only applies when no tree-level severity (file-specific
    /// or all-source) is configured for the diagnostic.
    /// </summary>
    public TSelf WithGlobalDiagnosticSeverity(string diagnosticId, ReportDiagnostic severity)
    {
        globalDiagnosticSeverities[diagnosticId] = severity;
        return Self;
    }

    private protected SyntaxTreeOptionsProvider CreateSyntaxTreeOptionsProvider()
        => SyntaxTreeOptionsProviderFactory.Create(diagnosticSeverities, diagnosticSeveritiesByFilename, globalDiagnosticSeverities);
}