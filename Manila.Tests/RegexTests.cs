using NUnit.Framework;
using Shiron.Manila.Utils;

namespace Shiron.Manila.Tests.Utils;

[TestFixture]
public class RegexUtilsTests {
    [Test]
    [TestCase("build", null, null, "build")]
    [TestCase("myproj:build", "myproj", null, "build")]
    [TestCase("myproj/mylib:build", "myproj", "mylib", "build")]
    [TestCase("another_proj:some_task", "another_proj", null, "some_task")]
    public void MatchTasks_ValidCases_ShouldSucceed(string input, string? expectedProject, string? expectedArtifact, string expectedTask) {
        var result = RegexUtils.MatchTasks(input);

        Assert.That(result, Is.Not.Null, $"Input: '{input}' should produce a match.");
        using (Assert.EnterMultipleScope()) {
            Assert.That(result.Project, Is.EqualTo(expectedProject), $"Input: '{input}' Project mismatch.");
            Assert.That(result.Artifact, Is.EqualTo(expectedArtifact), $"Input: '{input}' Artifact mismatch.");
            Assert.That(result.Task, Is.EqualTo(expectedTask), $"Input: '{input}' Task mismatch.");
            Assert.That(RegexUtils.IsValidTask(input), Is.True, $"Input: '{input}' should be valid.");
        }

        var reconstructed = result!.Format();
        Assert.That(reconstructed, Is.EqualTo(input), $"Input: '{input}' Round-trip failed.");
    }

    [Test]
    [TestCase("")] // Empty string
    [TestCase(" ")] // Whitespace
    [TestCase(":build")] // Missing project/artifact
    [TestCase("myproj:")] // Missing task
    [TestCase("myproj/")] // Missing task
    [TestCase("my-proj:build")] // Invalid character '-' in project (not in \w)
    [TestCase("myproj/my-lib:build")] // Invalid character '-' in artifact (not in \w)
    [TestCase("myproj:build-task")] // Invalid character '-' in task (not in \w)
    [TestCase("myproj/:build")] // Empty artifact
    [TestCase("myproj/mylib:")] // Missing task
    [TestCase("myproj//mylib:build")] // Double slash
    [TestCase("myproj:mylib:build")] // Extra colon
    [TestCase("myproj@1.0:build")] // Invalid character '@'
    [TestCase("myproj/mylib/sublib:build")] // Too many slashes
    [TestCase("myproj/mylib:build:extra")] // Extra colon after task
    [TestCase("myproj/mylib@1.0:build")] // Invalid character '@' in artifact
    public void MatchTasks_InvalidCases_ShouldFail(string input) {
        var result = RegexUtils.MatchTasks(input);

        Assert.That(result, Is.Null, $"Input: '{input}' should not produce a match.");
        Assert.That(RegexUtils.IsValidTask(input), Is.False, $"Input: '{input}' should be invalid.");
    }

    [Test]
    [TestCase("my-plugin", null, "my-plugin", null)]
    [TestCase("my-group:my-plugin", "my-group", "my-plugin", null)]
    [TestCase("my-plugin@1.0.0", null, "my-plugin", "1.0.0")]
    [TestCase("my-plugin@1", null, "my-plugin", "1")]
    [TestCase("my-group:my-plugin@2.5", "my-group", "my-plugin", "2.5")]
    [TestCase("plugin-with-hyphens", null, "plugin-with-hyphens", null)]
    public void MatchPlugin_ValidCases_ShouldSucceed(string input, string? expectedGroup, string expectedPlugin, string? expectedVersion) {
        var result = RegexUtils.MatchPlugin(input);

        Assert.That(result, Is.Not.Null, $"Input: '{input}' should produce a match.");
        using (Assert.EnterMultipleScope()) {
            Assert.That(result.Group, Is.EqualTo(expectedGroup), $"Input: '{input}' Group mismatch.");
            Assert.That(result.Plugin, Is.EqualTo(expectedPlugin), $"Input: '{input}' Plugin mismatch.");
            Assert.That(result.Version, Is.EqualTo(expectedVersion), $"Input: '{input}' Version mismatch.");
            Assert.That(RegexUtils.IsValidPlugin(input), Is.True, $"Input: '{input}' should be valid.");
        }

        var reconstructed = result!.Format();
        Assert.That(reconstructed, Is.EqualTo(input), $"Input: '{input}' Round-trip failed.");
    }

    [Test]
    [TestCase("")] // Empty string
    [TestCase(" ")] // Whitespace
    [TestCase(":my-plugin")] // Missing group name
    [TestCase("my-group:")] // Missing plugin name
    [TestCase("@1.0")] // Missing plugin name
    [TestCase("my-plugin@")] // Missing version
    [TestCase("my-plugin@extra")] // Invalid character '@' in plugin name
    [TestCase("my-plugin/extra")] // Extra slash
    [TestCase("my-group::my-plugin")] // Double colon
    [TestCase("my-group:my-plugin@")] // Missing version after @
    [TestCase("another-plugin@beta")] // Invalid version format (no letters allowed)
    [TestCase("my-plugin@1.0.0.beta")] // Invalid version format (no letters allowed)
    [TestCase("my-plugin@1..2")] // Invalid version format (double dot)
    [TestCase("my-plugin@1.")] // Invalid version format (trailing dot)
    public void MatchPlugin_InvalidCases_ShouldFail(string input) {
        var result = RegexUtils.MatchPlugin(input);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.Null, $"Input: '{input}' should not produce a match.");
            Assert.That(RegexUtils.IsValidPlugin(input), Is.False, $"Input: '{input}' should be invalid.");
        }
    }

    [Test]
    [TestCase("my-plugin/comp-1", null, "my-plugin", null, "comp-1")]
    [TestCase("my-group:my-plugin/comp-1", "my-group", "my-plugin", null, "comp-1")]
    [TestCase("my-plugin@1.2.3/comp-1", null, "my-plugin", "1.2.3", "comp-1")]
    [TestCase("my-plugin@1/comp-1", null, "my-plugin", "1", "comp-1")]
    [TestCase("my-group:my-plugin@1.2.3/comp-1", "my-group", "my-plugin", "1.2.3", "comp-1")]
    [TestCase("plugin-with-hyphens/component-with-hyphens", null, "plugin-with-hyphens", null, "component-with-hyphens")]
    public void MatchPluginComponent_ValidCases_ShouldSucceed(string input, string? expectedGroup, string expectedPlugin, string? expectedVersion, string expectedComponent) {
        var result = RegexUtils.MatchPluginComponent(input);

        Assert.That(result, Is.Not.Null, $"Input: '{input}' should produce a match.");
        using (Assert.EnterMultipleScope()) {
            Assert.That(result.Group, Is.EqualTo(expectedGroup), $"Input: '{input}' Group mismatch.");
            Assert.That(result.Plugin, Is.EqualTo(expectedPlugin), $"Input: '{input}' Plugin mismatch.");
            Assert.That(result.Version, Is.EqualTo(expectedVersion), $"Input: '{input}' Version mismatch.");
            Assert.That(result.Component, Is.EqualTo(expectedComponent), $"Input: '{input}' Component mismatch.");
            Assert.That(RegexUtils.IsValidPluginComponent(input), Is.True, $"Input: '{input}' should be valid.");
        }

        var reconstructed = result!.Format();
        Assert.That(reconstructed, Is.EqualTo(input), $"Input: '{input}' Round-trip failed.");
    }

    [Test]
    [TestCase("")] // Empty string
    [TestCase(" ")] // Whitespace
    [TestCase("my-plugin")] // Missing component
    [TestCase("my-group:my-plugin")] // Missing component
    [TestCase("my-plugin:")] // Missing component name
    [TestCase(":my-plugin:my-comp")] // Missing group name
    [TestCase("my-plugin@1.2.3")] // Missing component
    [TestCase("my-plugin@:comp-1")] // Missing version number
    [TestCase("my-plugin@1.2.3:")] // Missing component name
    [TestCase("my-group::my-plugin:comp-1")] // Double colon in group
    [TestCase("my-plugin:comp:extra")] // Extra colon after component
    [TestCase("my-plugin@1.0.0.beta:comp-1")] // Invalid version format
    [TestCase("my:plugin:comp-1")] // Colon in plugin name (should fail with new regex)
    [TestCase("my@plugin:comp-1")] // At-symbol in plugin name (should fail with new regex)
    [TestCase("my/plugin:comp-1")] // Slash in plugin name (should fail with new regex)
    public void MatchPluginComponent_InvalidCases_ShouldFail(string input) {
        var result = RegexUtils.MatchPluginComponent(input);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.Null, $"Input: '{input}' should not produce a match.");
            Assert.That(RegexUtils.IsValidPluginComponent(input), Is.False, $"Input: '{input}' should be invalid.");
        }
    }

    [Test]
    [TestCase("my-plugin/ApiClass", null, "my-plugin", null, "ApiClass")]
    [TestCase("my-group:my-plugin/ApiClass", "my-group", "my-plugin", null, "ApiClass")]
    [TestCase("my-plugin@1.0.0/ApiClass", null, "my-plugin", "1.0.0", "ApiClass")]
    [TestCase("my-plugin@2/ApiClass", null, "my-plugin", "2", "ApiClass")]
    [TestCase("my-group:my-plugin@1.0/ApiClass", "my-group", "my-plugin", "1.0", "ApiClass")]
    [TestCase("plugin-with-hyphens/Api-Class-With-Hyphens", null, "plugin-with-hyphens", null, "Api-Class-With-Hyphens")]
    public void MatchPluginApiClass_ValidCases_ShouldSucceed(string input, string? expectedGroup, string expectedPlugin, string? expectedVersion, string expectedApiClass) {
        var result = RegexUtils.MatchPluginApiClass(input);

        Assert.That(result, Is.Not.Null, $"Input: '{input}' should produce a match.");
        using (Assert.EnterMultipleScope()) {
            Assert.That(result.Group, Is.EqualTo(expectedGroup), $"Input: '{input}' Group mismatch.");
            Assert.That(result.Plugin, Is.EqualTo(expectedPlugin), $"Input: '{input}' Plugin mismatch.");
            Assert.That(result.Version, Is.EqualTo(expectedVersion), $"Input: '{input}' Version mismatch.");
            Assert.That(result.ApiClass, Is.EqualTo(expectedApiClass), $"Input: '{input}' ApiClass mismatch.");
            Assert.That(RegexUtils.IsValidPluginApiClass(input), Is.True, $"Input: '{input}' should be valid.");
        }

        var reconstructed = result!.Format();
        Assert.That(reconstructed, Is.EqualTo(input), $"Input: '{input}' Round-trip failed.");
    }

    [Test]
    [TestCase("")] // Empty string
    [TestCase(" ")] // Whitespace
    [TestCase("my-plugin")] // Missing API Class
    [TestCase("my-plugin:ApiClass")] // Wrong separator (colon instead of slash)
    [TestCase("my-group:my-plugin")] // Missing API Class
    [TestCase("my-plugin/")] // Missing API Class name
    [TestCase("/ApiClass")] // Missing plugin
    [TestCase("my-plugin@1.0.0")] // Missing API Class
    [TestCase("my-plugin@/ApiClass")] // Missing version number
    [TestCase("my-plugin@1.0.0/")] // Missing API Class name
    [TestCase("my-group::my-plugin/ApiClass")] // Double colon in group
    [TestCase("my-plugin/ApiClass/extra")] // Extra slash after API Class
    [TestCase("my-plugin@1.0.0.beta/ApiClass")] // Invalid version format
    [TestCase("my@plugin/ApiClass")] // At-symbol in plugin name (should fail with new regex)
    [TestCase("my/plugin/ApiClass")] // Slash in plugin name (should fail with new regex)
    public void MatchPluginApiClass_InvalidCases_ShouldFail(string input) {
        var result = RegexUtils.MatchPluginApiClass(input);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.Null, $"Input: '{input}' should not produce a match.");
            Assert.That(RegexUtils.IsValidPluginApiClass(input), Is.False, $"Input: '{input}' should be invalid.");
        }
    }
}
