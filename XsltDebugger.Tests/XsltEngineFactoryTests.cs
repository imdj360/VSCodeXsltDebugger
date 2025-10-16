using System;
using FluentAssertions;
using Xunit;
using XsltDebugger.DebugAdapter;

namespace XsltDebugger.Tests;

/// <summary>
/// Tests for XsltEngineFactory to ensure correct engine creation and error handling
/// </summary>
public class XsltEngineFactoryTests
{
    [Fact]
    public void CreateEngine_WithXsltCompiledType_ShouldReturnXsltCompiledEngine()
    {
        // Act
        var engine = XsltEngineFactory.CreateEngine(XsltEngineType.Compiled);

        // Assert
        engine.Should().NotBeNull();
        engine.Should().BeOfType<XsltCompiledEngine>();
    }

    [Fact]
    public void CreateEngine_WithSaxonType_ShouldReturnSaxonEngine()
    {
        // Act
        var engine = XsltEngineFactory.CreateEngine(XsltEngineType.SaxonNet);

        // Assert
        engine.Should().NotBeNull();
        engine.Should().BeOfType<SaxonEngine>();
    }

    [Theory]
    [InlineData("Compiled")]
    [InlineData("compiled")]
    [InlineData("COMPILED")]
    public void CreateEngine_WithCompiledString_CaseInsensitive_ShouldReturnXsltCompiledEngine(string engineType)
    {
        // Act
        var engine = XsltEngineFactory.CreateEngine(engineType);

        // Assert
        engine.Should().NotBeNull();
        engine.Should().BeOfType<XsltCompiledEngine>();
    }

    [Theory]
    [InlineData("SaxonNet")]
    [InlineData("saxonnet")]
    [InlineData("SAXONNET")]
    public void CreateEngine_WithSaxonNetString_CaseInsensitive_ShouldReturnSaxonEngine(string engineType)
    {
        // Act
        var engine = XsltEngineFactory.CreateEngine(engineType);

        // Assert
        engine.Should().NotBeNull();
        engine.Should().BeOfType<SaxonEngine>();
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("UnknownEngine")]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateEngine_WithInvalidString_ShouldThrowArgumentException(string engineType)
    {
        // Act
        Action act = () => XsltEngineFactory.CreateEngine(engineType);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid engine type*");
    }

    [Fact]
    public void CreateEngine_WithNullString_ShouldThrowArgumentException()
    {
        // Act
        Action act = () => XsltEngineFactory.CreateEngine((string)null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateEngine_WithInvalidEnumValue_ShouldThrowArgumentException()
    {
        // Arrange
        var invalidEnumValue = (XsltEngineType)999;

        // Act
        Action act = () => XsltEngineFactory.CreateEngine(invalidEnumValue);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Unsupported engine type*");
    }

    [Fact]
    public void CreateEngine_ShouldCreateNewInstanceEachTime()
    {
        // Act
        var engine1 = XsltEngineFactory.CreateEngine(XsltEngineType.Compiled);
        var engine2 = XsltEngineFactory.CreateEngine(XsltEngineType.Compiled);

        // Assert
        engine1.Should().NotBeNull();
        engine2.Should().NotBeNull();
        engine1.Should().NotBeSameAs(engine2);
    }

    [Fact]
    public void CreateEngine_StringOverload_ShouldMatchEnumOverload()
    {
        // Act
        var engineFromString = XsltEngineFactory.CreateEngine("SaxonNet");
        var engineFromEnum = XsltEngineFactory.CreateEngine(XsltEngineType.SaxonNet);

        // Assert
        engineFromString.Should().BeOfType(engineFromEnum.GetType());
    }
}
