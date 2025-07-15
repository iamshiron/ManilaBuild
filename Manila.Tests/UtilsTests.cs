using NUnit.Framework;
using Shiron.Manila.Utils;
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Shiron.Manila.Tests;

[TestFixture]
public class FunctionUtilsTests {
    private class TestClass {
        public void ActionNoParams() { }
        public void ActionWithParams(int a, string b) { }
        public int FuncNoParams() => 1;
        public string FuncWithParams(int a, bool b) => $"{a}-{b}";
        public static void StaticAction() { }
        public static int StaticFunc() => 2;
        public void OverloadedMethod(int a) { }
        public void OverloadedMethod(string a) { }
        public void TooManyParams(int a, int b, int c, int d, int e) { }
    }

    [Test]
    public void ToDelegate_ActionNoParams_CreatesCorrectDelegate() {
        var instance = new TestClass();
        var method = typeof(TestClass).GetMethod("ActionNoParams");
        var del = FunctionUtils.ToDelegate(instance, method!);
        Assert.That(del, Is.TypeOf<Action>());
        Assert.DoesNotThrow(() => del.DynamicInvoke());
    }

    [Test]
    public void ToDelegate_ActionWithParams_CreatesCorrectDelegate() {
        var instance = new TestClass();
        var method = typeof(TestClass).GetMethod("ActionWithParams");
        var del = FunctionUtils.ToDelegate(instance, method!);
        Assert.That(del, Is.TypeOf<Action<int, string>>());
        Assert.DoesNotThrow(() => del.DynamicInvoke(1, "test"));
    }

    [Test]
    public void ToDelegate_FuncWithReturnValue_CreatesCorrectDelegate() {
        var instance = new TestClass();
        var method = typeof(TestClass).GetMethod("FuncNoParams");
        var del = FunctionUtils.ToDelegate(instance, method!);
        Assert.That(del, Is.TypeOf<Func<int>>());
        var result = del.DynamicInvoke();
        Assert.That(result, Is.EqualTo(1));
    }

    [Test]
    public void ToDelegate_StaticFunc_CreatesCorrectDelegate() {
        var method = typeof(TestClass).GetMethod("StaticFunc");
        var del = FunctionUtils.ToDelegate(null, method!);
        Assert.That(del, Is.TypeOf<Func<int>>());
        var result = del.DynamicInvoke();
        Assert.That(result, Is.EqualTo(2));
    }

    [Test]
    public void ToDelegate_TooManyParams_ThrowsNotSupportedException() {
        var instance = new TestClass();
        var method = typeof(TestClass).GetMethod("TooManyParams");
        Assert.Throws<ArgumentException>(() => FunctionUtils.ToDelegate(instance, method!));
    }

    [Test]
    public void SameParametes_MatchingParams_ReturnsTrue() {
        var method = typeof(TestClass).GetMethod("ActionWithParams");
        object[] args = { 1, "hello" };
        Assert.That(FunctionUtils.SameParametes(method!, args), Is.True);
    }

    [Test]
    public void SameParametes_MismatchedParamCount_ReturnsFalse() {
        var method = typeof(TestClass).GetMethod("ActionWithParams");
        object[] args = { 1 };
        Assert.That(FunctionUtils.SameParametes(method!, args), Is.False);
    }

    [Test]
    public void SameParametes_MismatchedParamType_ReturnsFalse() {
        var method = typeof(TestClass).GetMethod("ActionWithParams");
        object[] args = { 1, true };
        Assert.That(FunctionUtils.SameParametes(method!, args), Is.False);
    }
}

[TestFixture]
public class HashUtilsTests {
    private string _tempDir;

    [SetUp]
    public void Setup() {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void Teardown() {
        Directory.Delete(_tempDir, true);
    }

    private string CreateTestFile(string content) {
        var filePath = Path.Combine(_tempDir, Path.GetRandomFileName());
        File.WriteAllText(filePath, content);
        return filePath;
    }

    [Test]
    public void CreateFingerprint_SameFilesDifferentOrder_ProducesSameHash() {
        var file1 = CreateTestFile("hello");
        var file2 = CreateTestFile("world");

        var fingerprint1 = HashUtils.CreateFingerprint([file1, file2]);
        var fingerprint2 = HashUtils.CreateFingerprint([file2, file1]);

        Assert.That(fingerprint1, Is.EqualTo(fingerprint2));
    }

    [Test]
    public void CreateFingerprint_DifferentFileContent_ProducesDifferentHash() {
        var file1 = CreateTestFile("hello");
        var file2 = CreateTestFile("world");
        var file3 = CreateTestFile("different");

        var fingerprint1 = HashUtils.CreateFingerprint([file1, file2]);
        var fingerprint2 = HashUtils.CreateFingerprint([file1, file3]);

        Assert.That(fingerprint1, Is.Not.EqualTo(fingerprint2));
    }
}
