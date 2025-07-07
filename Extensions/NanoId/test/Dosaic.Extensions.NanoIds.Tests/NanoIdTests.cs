using System.Reflection;
using AwesomeAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Dosaic.Extensions.NanoIds.Tests;

[NanoId(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L2)]
public class TestModel : INanoId
{
    public NanoId Id { get; set; }
    public string Name { get; set; }

    public static TestModel GetModel(string name = "Group 1") =>
        new() { Id = NanoId.NewId<TestModel>(), Name = name };
}

[NanoId(NanoIdConfig.Lengths.NoLookAlikeDigitsAndLetters.L2, "prefix_")]
public class PrefixedTestModel : INanoId
{
    public required NanoId Id { get; set; }
}

public class NanoIdTests
{
    [Test]
    [Explicit]
    public void GenerateStaticIdsForDatabaseSeedData()
    {
        // change the line accordingly NanoId.NewId<TYPE-THAT-YOU-WANT-TO-GENERATE-IDS-FOR>()
        var modelType = typeof(TestModel);
        for (var i = 0; i < 10; i++)
        {
            TestContext.Out.WriteLine($"{modelType.Name}: {NanoId.NewId(modelType)}");
        }
    }

    [Test]
    public void ConstructorShouldSetValue()
    {
        var value = "testvalue";
        var nanoId = new NanoId(value);

        nanoId.Value.Should().Be(value);
    }

    [Test]
    public void LengthWithPrefixShouldBeCorrect()
    {

        var nanoIdAttribute = typeof(PrefixedTestModel).GetCustomAttribute<NanoIdAttribute>();

        nanoIdAttribute!.Prefix.Should().StartWith("prefix_");
        nanoIdAttribute.Length.Should().Be(2);
        nanoIdAttribute.LengthWithPrefix.Should().Be(9);

    }

    [Test]
    public void NewIdGenericShouldCreateNewNanoId()
    {
        var nanoId = NanoId.NewId<PrefixedTestModel>();

        nanoId.Value.Should().StartWith("prefix_");
        nanoId.Value.Length.Should().Be(9);
    }

    [Test]
    public void NewIdTypeShouldCreateNewNanoId()
    {
        var type = typeof(PrefixedTestModel);
        var nanoId = NanoId.NewId(type);

        nanoId.Value.Should().StartWith("prefix_");
        nanoId.Value.Length.Should().Be(9);
    }

    [Test]
    public void EqualsShouldReturnTrueForSameValues()
    {
        var nanoId1 = new NanoId("testvalue");
        var nanoId2 = new NanoId("testvalue");

        nanoId1.Equals(nanoId2).Should().BeTrue();
        (nanoId1 == nanoId2).Should().BeTrue();
        (nanoId1 != nanoId2).Should().BeFalse();
    }

    [Test]
    public void EqualsShouldReturnFalseForDifferentValues()
    {
        var nanoId1 = new NanoId("value1");
        var nanoId2 = new NanoId("value2");

        nanoId1.Equals(nanoId2).Should().BeFalse();
        (nanoId1 == nanoId2).Should().BeFalse();
        (nanoId1 != nanoId2).Should().BeTrue();
    }

    [Test]
    public void EqualsShouldThrowNullExceptionForNullValue()
    {
        var nanoId1 = new NanoId("value");
        nanoId1.Equals(null).Should().BeFalse();

        var act = () => { _ = new NanoId(null!); };
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void GetHashCodeShouldReturnSameForSameValues()
    {
        var nanoId1 = new NanoId("testvalue");
        var nanoId2 = new NanoId("testvalue");

        nanoId1.GetHashCode().Should().Be(nanoId2.GetHashCode());
    }

    [Test]
    public void CompareToShouldReturn0ForSameValues()
    {
        var nanoId1 = new NanoId("testvalue");
        var nanoId2 = new NanoId("testvalue");

        nanoId1.CompareTo(nanoId2).Should().Be(0);
    }

    [Test]
    public void CompareToShouldReturnNonZeroForDifferentValues()
    {
        var nanoId1 = new NanoId("value1");
        var nanoId2 = new NanoId("value2");

        nanoId1.CompareTo(nanoId2).Should().NotBe(0);
    }

    [Test]
    public void CompareToObjectShouldThrowArgumentExceptionForNonNanoId()
    {
        var nanoId = new NanoId("value");

        // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
        Action act = () => nanoId.CompareTo(new object());
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void ToStringShouldReturnValue()
    {
        var value = "testvalue";
        var nanoId = new NanoId(value);

        nanoId.ToString().Should().Be(value);
    }

    [Test]
    public void ImplicitOperatorStringToNanoId()
    {
        var value = "testvalue";
        NanoId nanoId = value;

        nanoId.Value.Should().Be(value);
    }

    [Test]
    public void ImplicitOperatorNanoIdToString()
    {
        var nanoId = new NanoId("testvalue");
        string value = nanoId;

        value.Should().Be(nanoId.Value);
    }

    [Test]
    public void TryFormatShouldWriteToDestination()
    {
        var value = "testvalue";
        var nanoId = new NanoId(value);
        Span<char> destination = stackalloc char[100];
        nanoId.TryFormat(destination, out int charsWritten, ReadOnlySpan<char>.Empty, null).Should().BeTrue();

        new string(destination.Slice(0, charsWritten)).Should().Be($"Value: {value}");
    }

    [Test]
    public void ToStringWithFormatAndProviderShouldReturnFormattedString()
    {
        var value = "testvalue";
        var nanoId = new NanoId(value);
        var formatProvider = Substitute.For<IFormatProvider>();

        var result = nanoId.ToString(null, formatProvider);

        result.Should().Be($"Value: {value}");
    }

    [Test]
    public void ParseShouldReturnNanoIdWithCorrectValue()
    {
        var value = "testid123";
        var nanoId = NanoId.Parse(value);

        nanoId.Should().NotBeNull();
        nanoId.Value.Should().Be(value);
    }

    [Test]
    public void ParseShouldReturnNullForNullInput()
    {
        var nanoId = NanoId.Parse(null);

        nanoId.Should().BeNull();
    }

    [Test]
    public void EqualsShouldReturnTrueForSameValueSameType()
    {
        var nanoId = new NanoId("testvalue");
        var other = "testvalue";

        nanoId.Equals(other).Should().BeTrue();
    }

    [Test]
    public void EqualsOverrideNullShouldReturnFalse()
    {
        var nanoId = new NanoId("testvalue");

        nanoId.Equals(null).Should().BeFalse();
    }

    [Test]
    public void CompareToShouldReturnPositiveForNull()
    {
        var nanoId = new NanoId("testvalue");

        nanoId.CompareTo((NanoId)null).Should().Be(1);
    }

    [Test]
    public void CompareToShouldReturnPositiveForLesserValue()
    {
        var nanoId1 = new NanoId("valueB");
        var nanoId2 = new NanoId("valueA");

        nanoId1.CompareTo(nanoId2).Should().BePositive();
    }

    [Test]
    public void CompareToShouldReturnNegativeForGreaterValue()
    {
        var nanoId1 = new NanoId("valueA");
        var nanoId2 = new NanoId("valueB");

        nanoId1.CompareTo(nanoId2).Should().BeNegative();
    }

    [Test]
    public void EqualityOperatorShouldReturnTrueForSameReference()
    {
        var nanoId = new NanoId("testvalue");
        // ReSharper disable once EqualExpressionComparison
#pragma warning disable CS1718
        var result = nanoId == nanoId;
#pragma warning restore CS1718
        result.Should().BeTrue();
    }

    [Test]
    public void EqualityOperatorShouldReturnFalseForNullLeft()
    {
        NanoId nanoId = null;
        var other = new NanoId("testvalue");

        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        var result = nanoId == other;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        result.Should().BeFalse();
    }

    [Test]
    public void EqualsWithSameObjectShouldReturnTrue()
    {
        var nanoId = new NanoId("test123");
        object sameRef = nanoId;

        nanoId.Equals(sameRef).Should().BeTrue();
    }

    [Test]
    public void EqualsWithBoxedNanoIdSameShouldReturnTrue()
    {
        var nanoId = new NanoId("test123");
        object boxed = new NanoId("test123");

        nanoId.Equals(boxed).Should().BeTrue();
    }

    [Test]
    public void EqualsWithBoxedNanoIdDiffShouldReturnFalse()
    {
        var nanoId = new NanoId("test123");
        object boxed = new NanoId("test456");

        nanoId.Equals(boxed).Should().BeFalse();
    }

    [Test]
    public void EqualsWithDerivedTypeShouldReturnFalse()
    {
        var nanoId = new NanoId("test123");
        object derived = new DerivedNanoId("test123");

        nanoId.Equals(derived).Should().BeFalse();
    }

    [Test]
    public void EqualsWithStringOfSameValueShouldReturnFalse()
    {
        var nanoId = new NanoId("test123");
        object str = "test123";

        nanoId.Equals(str).Should().BeFalse();
    }

    [Test]
    public void EqualsWithIntShouldReturnFalse()
    {
        var nanoId = new NanoId("123");
        object num = 123;

        nanoId.Equals(num).Should().BeFalse();
    }

    private class DerivedNanoId : NanoId
    {
        public DerivedNanoId(string value) : base(value)
        {
        }
    }

    [Test]
    public void CompareToObjectNullShouldReturnOne()
    {
        var nanoId = new NanoId("test123");
        object nullObj = null;

        // ReSharper disable once ExpressionIsAlwaysNull
        nanoId.CompareTo(nullObj).Should().Be(1);
    }

    [Test]
    public void CompareToObjectSameValueShouldReturnZero()
    {
        var nanoId = new NanoId("test123");
        object other = new NanoId("test123");

        nanoId.CompareTo(other).Should().Be(0);
    }

    [Test]
    public void CompareToObjectLesserValueShouldReturnPositive()
    {
        var nanoId = new NanoId("valueB");
        object other = new NanoId("valueA");

        nanoId.CompareTo(other).Should().BePositive();
    }

    [Test]
    public void CompareToObjectGreaterValueShouldReturnNegative()
    {
        var nanoId = new NanoId("valueA");
        object other = new NanoId("valueB");

        nanoId.CompareTo(other).Should().BeNegative();
    }

    [Test]
    public void CompareToObjectNotNanoIdShouldThrowArgumentException()
    {
        var nanoId = new NanoId("test123");
        object other = "test123";

        var act = () => nanoId.CompareTo(other);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*not a NanoId*");
    }
}
