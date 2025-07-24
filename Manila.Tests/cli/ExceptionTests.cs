using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.ClearScript;
using NUnit.Framework;
using Shiron.Manila.API;
using Shiron.Manila.Attributes;
using Shiron.Manila.CLI;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;

namespace Shiron.Manila.Tests.CLI;

[TestFixture]
public class ExceptionTests {
    public readonly ILogger Logger = new Mock.EmptyMockLogger();

    private static IEnumerable<TestCaseData> ExceptionTestCases {
        get {
            yield return new TestCaseData(new ManilaException("Test"), ExitCodes.SCRIPTING_ERROR)
                .SetName("HandleException_ScriptingException_ReturnsCorrectCode");

            yield return new TestCaseData(new ManilaException("Test"), ExitCodes.PLUGIN_ERROR)
                .SetName("HandleException_PluginException_ReturnsCorrectCode");

            yield return new TestCaseData(new ManilaException("Test"), ExitCodes.BUILD_ERROR)
                .SetName("HandleException_BuildTimeException_ReturnsCorrectCode");

            yield return new TestCaseData(new ManilaException("Test"), ExitCodes.CONFIGURATION_ERROR)
                .SetName("HandleException_ConfigurationTimeException_ReturnsCorrectCode");

            yield return new TestCaseData(new ManilaException("Test"), ExitCodes.RUNTIME_ERROR)
                .SetName("HandleException_RuntimeException_ReturnsCorrectCode");

            yield return new TestCaseData(new ManilaException("Test"), ExitCodes.ANY_KNOWN_ERROR)
                .SetName("HandleException_ManilaException_ReturnsCorrectCode");

            yield return new TestCaseData(new Exception("Test"), ExitCodes.UNKNOWN_ERROR)
                .SetName("HandleException_GenericException_ReturnsCorrectCode");
        }
    }

    [Test]
    [TestCaseSource(nameof(ExceptionTestCases))]
    public void HandleException_Returns_CorrectExitCode(Exception ex, int expectedExitCode) {
        var actualExitCode = ErrorHandler.ManilaException(Logger, ex, LogOptions.None);

        Assert.That(actualExitCode, Is.EqualTo(expectedExitCode));
    }
}
