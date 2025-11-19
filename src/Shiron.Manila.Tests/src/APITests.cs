using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shiron.Logging;
using Shiron.Manila.API;
using Shiron.Utils;

namespace Shiron.Manila.Tests;

[TestFixture]
public class ProjectFilterTests {
    public static readonly ILogger Logger = new Mock.EmptyMockLogger();

    [Test]
    public void From_String_CreatesProjectFilterName() {
        var filter = ProjectFilter.From("my-project");
        Assert.That(filter, Is.InstanceOf<ProjectFilterName>());
    }

    [Test]
    public void From_WildcardString_CreatesProjectFilterAll() {
        var filter = ProjectFilter.From("*");
        Assert.That(filter, Is.InstanceOf<ProjectFilterAll>());
    }

    [Test]
    public void From_ListOfObjects_CreatesProjectFilterArray() {
        var filter = ProjectFilter.From(new List<object> { "proj1", "proj2" });
        Assert.That(filter, Is.InstanceOf<ProjectFilterArray>());
    }

    [Test]
    public void ProjectFilterRegex_Predicate_MatchesCorrectly() {
        var regex = new Regex("^core-.*");
        var filter = new ProjectFilterRegex(regex);
        var workspace = new Workspace(Logger, ".");
        var project1 = new Project(Logger, "core-lib", "./core-lib", ".", workspace);
        var project2 = new Project(Logger, "client-app", "./client-app", ".", workspace);

        using (Assert.EnterMultipleScope()) {
            Assert.That(filter.Predicate(project1), Is.True);
            Assert.That(filter.Predicate(project2), Is.False);
        }
    }
}
