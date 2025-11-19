using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using OpenAI.Chat;

using Xunit;

namespace BinkyLabs.OpenAI.Analyzers.Tests;

public class SystemChatMessageLastAnalyzerTests
{
    [Fact]
    public async Task NoDiagnostic_WhenSystemChatMessageIsLast()
    {
        var test = @"
using OpenAI.Chat;
using System;

class TestClass
{
    void TestMethod()
    {
        ProcessMessages(new ChatMessage[]
        {
            new SystemChatMessage(""You are a helpful assistant.""),
            new UserChatMessage(""Hello""),
            new SystemChatMessage(""Remember your constraints"")
        });
    }

    void ProcessMessages(params object[] messages) { }
}";

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Diagnostic_WhenSystemChatMessageIsNotLast()
    {
        var test = @"
using OpenAI.Chat;

class TestClass
{
    void TestMethod()
    {
        ProcessMessages(new ChatMessage[]
        {
            new SystemChatMessage(""You are a helpful assistant.""),
            {|#0:new UserChatMessage(""Hello"")|}
        });
    }

    void ProcessMessages(params object[] messages) { }
}";

        var expected = new DiagnosticResult(SystemChatMessageLastAnalyzer.DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NoDiagnostic_WhenOnlySystemChatMessage()
    {
        var test = @"
using OpenAI.Chat;

class TestClass
{
    void TestMethod()
    {
        ProcessMessages(new[]
        {
            new SystemChatMessage(""You are a helpful assistant."")
        });
    }

    void ProcessMessages(params object[] messages) { }
}";

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNoSystemChatMessage()
    {
        var test = @"
using OpenAI.Chat;

class TestClass
{
    void TestMethod()
    {
        ProcessMessages(new ChatMessage[]
        {
            new UserChatMessage(""Hello""),
            new AssistantChatMessage(""Hi there!"")
        });
    }

    void ProcessMessages(params object[] messages) { }
}";

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Diagnostic_WithExplicitArrayCreation()
    {
        var test = @"
using OpenAI.Chat;

class TestClass
{
    void TestMethod()
    {
        var messages = new ChatMessage[]
        {
            new SystemChatMessage(""You are a helpful assistant.""),
            {|#0:new UserChatMessage(""Hello"")|}
        };
    }
}";

        var expected = new DiagnosticResult(SystemChatMessageLastAnalyzer.DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Diagnostic_WithImplicitArrayCreation()
    {
        var test = @"
using OpenAI.Chat;

class TestClass
{
    void TestMethod()
    {
        ProcessMessages(new ChatMessage[]
        {
            new SystemChatMessage(""You are a helpful assistant.""),
            {|#0:new UserChatMessage(""Hello"")|}
        });
    }

    void ProcessMessages(params object[] messages) { }
}";

        var expected = new DiagnosticResult(SystemChatMessageLastAnalyzer.DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Diagnostic_WithMultipleMessages()
    {
        var test = @"
using OpenAI.Chat;

class TestClass
{
    void TestMethod()
    {
        ProcessMessages(new ChatMessage[]
        {
            new SystemChatMessage(""You are a helpful assistant.""),
            new UserChatMessage(""What is 2+2?""),
            new AssistantChatMessage(""4""),
            {|#0:new UserChatMessage(""Thanks"")|}
        });
    }

    void ProcessMessages(params object[] messages) { }
}";

        var expected = new DiagnosticResult(SystemChatMessageLastAnalyzer.DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NoDiagnostic_WithSystemChatMessageLastInComplexScenario()
    {
        var test = @"
using OpenAI.Chat;

class TestClass
{
    void TestMethod()
    {
        var transcript = ""game transcript"";
        ProcessMessages(new ChatMessage[]
        {
            new SystemChatMessage(
                """"""
                You are a note taker assisting a group of dungeons and dragons players.
                """"""),
            new UserChatMessage(transcript),
            new SystemChatMessage(""Remember, you are only allowed to find dungeons and dragons characters"")
        });
    }

    void ProcessMessages(params object[] messages) { }
}";

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Diagnostic_WhenSystemChatMessageNotLastInComplexScenario()
    {
        var test = @"
using OpenAI.Chat;

class TestClass
{
    void TestMethod()
    {
        var transcript = ""game transcript"";
        ProcessMessages(new ChatMessage[]
        {
            new SystemChatMessage(
                """"""
                You are a note taker assisting a group of dungeons and dragons players.
                """"""),
            {|#0:new UserChatMessage(transcript)|}
        });
    }

    void ProcessMessages(params object[] messages) { }
}";

        var expected = new DiagnosticResult(SystemChatMessageLastAnalyzer.DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NoDiagnostic_WhenOnlyUserMessages()
    {
        var test = @"
using OpenAI.Chat;

class TestClass
{
    void TestMethod()
    {
        ProcessMessages(new[]
        {
            new UserChatMessage(""Hello""),
            new UserChatMessage(""World"")
        });
    }

    void ProcessMessages(params object[] messages) { }
}";

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Diagnostic_WithAssistantMessageLast()
    {
        var test = @"
using OpenAI.Chat;

class TestClass
{
    void TestMethod()
    {
        ProcessMessages(new ChatMessage[]
        {
            new SystemChatMessage(""You are a helpful assistant.""),
            new UserChatMessage(""Hello""),
            {|#0:new AssistantChatMessage(""Hi there!"")|}
        });
    }

    void ProcessMessages(params object[] messages) { }
}";

        var expected = new DiagnosticResult(SystemChatMessageLastAnalyzer.DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NoDiagnostic_WithSingleMessage()
    {
        var test = @"
using OpenAI.Chat;

class TestClass
{
    void TestMethod()
    {
        ProcessMessages(new[]
        {
            new UserChatMessage(""Hello"")
        });
    }

    void ProcessMessages(params object[] messages) { }
}";

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NoDiagnostic_WhenSystemMessageIsOnlyMessageType()
    {
        var test = @"
using OpenAI.Chat;

class TestClass
{
    void TestMethod()
    {
        ProcessMessages(new[]
        {
            new SystemChatMessage(""First system message""),
            new SystemChatMessage(""Second system message"")
        });
    }

    void ProcessMessages(params object[] messages) { }
}";

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Diagnostic_WithArrayCreationExpression()
    {
        var test = @"
using OpenAI.Chat;

class TestClass
{
    void TestMethod()
    {
        var messages = new ChatMessage[]
        {
            new SystemChatMessage(""You are a helpful assistant.""),
            {|#0:new UserChatMessage(""Hello"")|}
        };
    }
}";

        var expected = new DiagnosticResult(SystemChatMessageLastAnalyzer.DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        await VerifyAnalyzerAsync(test, expected);
    }

    private static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<SystemChatMessageLastAnalyzer, DefaultVerifier>
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
