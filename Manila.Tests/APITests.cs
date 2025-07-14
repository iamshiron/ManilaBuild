using NUnit.Framework;
using Shiron.Manila.Utils;
using Shiron.Manila.API;
using System.Dynamic;
using System.Reflection;
using Microsoft.ClearScript;
using Shiron.Manila.Attributes;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Shiron.Manila.Tests;

[TestFixture]
public class ProjectFilterTests {
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
        var project1 = new Project("core-lib", ".", null);
        var project2 = new Project("client-app", ".", null);

        Assert.That(filter.Predicate(project1), Is.True);
        Assert.That(filter.Predicate(project2), Is.False);
    }
}
