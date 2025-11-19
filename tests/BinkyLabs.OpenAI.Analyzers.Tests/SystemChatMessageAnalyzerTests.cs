using System.Collections.Immutable;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

using OpenAI.Chat;

using Xunit;

namespace BinkyLabs.OpenAI.Analyzers.Tests;

public class SystemChatMessageAnalyzerTests
{
    [Fact]
    public async Task NoDiagnostic_WhenSystemChatMessageHasNoInterpolation()
    {
        var test = @"
using OpenAI.Chat;

class TestClass
{
    void TestMethod()
    {
        var message = new SystemChatMessage(""You are a helpful assistant."");
    }
}";

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Diagnostic_WhenSystemChatMessageHasInterpolation()
    {
        var test = @"
using OpenAI.Chat;

class TestClass
{
    void TestMethod()
    {
        var userInput = ""some input"";
        var message = new SystemChatMessage({|#0:$""You are a helpful assistant. {userInput}""|}); 
    }
}";

        var expected = new DiagnosticResult(SystemChatMessageAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0);

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Diagnostic_WhenSystemChatMessageHasRawStringInterpolation()
    {
        var test = @"
using OpenAI.Chat;

class TestClass
{
    void TestMethod()
    {
        var transcript = ""game transcript"";
        var message = new SystemChatMessage({|#0:$""""""
            You are a note taker.
            {transcript}
            """"""|}); 
    }
}";

        var expected = new DiagnosticResult(SystemChatMessageAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0);

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NoDiagnostic_WhenUserChatMessageHasInterpolation()
    {
        var test = @"
using OpenAI.Chat;

class TestClass
{
    void TestMethod()
    {
        var userInput = ""some input"";
        var message = new UserChatMessage($""Process this: {userInput}"");
    }
}";

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Diagnostic_WithImplicitObjectCreation()
    {
        var test = @"
using OpenAI.Chat;

class TestClass
{
    void TestMethod()
    {
        var userInput = ""input"";
        SystemChatMessage message = new({|#0:$""System prompt {userInput}""|}); 
    }
}";

        var expected = new DiagnosticResult(SystemChatMessageAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0);

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Diagnostic_WithTextPartExplicit()
    {
        var test = @"
using OpenAI.Chat;

class TestClass
{
    void TestMethod()
    {
        var userInput = ""input"";
        var part = ChatMessageContentPart.CreateTextPart({|#0:$""System prompt {userInput}""|});
        var message = new SystemChatMessage(part);
    }
}";

        var expected = new DiagnosticResult(SystemChatMessageAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0);

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Diagnostic_WithTextPartImplicit()
    {
        var test = @"
using OpenAI.Chat;

class TestClass
{
    void TestMethod()
    {
        var userInput = ""input"";
        var part = ChatMessageContentPart.CreateTextPart({|#0:$""System prompt {userInput}""|});
        SystemChatMessage message = new(part);
    }
}";

        var expected = new DiagnosticResult(SystemChatMessageAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0);

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Diagnostic_WithTextPartListExplicit()
    {
        var test = @"
using OpenAI.Chat;
using System.Collections.Generic;

class TestClass
{
    void TestMethod()
    {
        var userInput = ""input"";
        var parts = new List<ChatMessageContentPart> { ChatMessageContentPart.CreateTextPart({|#0:$""System prompt {userInput}""|}) };
        var message = new SystemChatMessage(parts);
    }
}";

        var expected = new DiagnosticResult(SystemChatMessageAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0);

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Diagnostic_WithTextPartListImplicit()
    {
        var test = @"
using OpenAI.Chat;
using System.Collections.Generic;

class TestClass
{
    void TestMethod()
    {
        var userInput = ""input"";
        var parts = new List<ChatMessageContentPart> { ChatMessageContentPart.CreateTextPart({|#0:$""System prompt {userInput}""|}) };
        SystemChatMessage message = new (parts);
    }
}";

        var expected = new DiagnosticResult(SystemChatMessageAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0);

        await VerifyAnalyzerAsync(test, expected);
    }

    private static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<SystemChatMessageAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { source },
                AdditionalReferences =
                {
                    MetadataReference.CreateFromFile(typeof(SystemChatMessage).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.ClientModel.Primitives.ActivityExtensions).Assembly.Location),
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            },
        };

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }
}