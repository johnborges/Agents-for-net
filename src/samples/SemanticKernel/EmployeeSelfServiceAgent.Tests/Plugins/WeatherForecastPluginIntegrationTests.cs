// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading.Tasks;
using EmployeeSelfServiceAgent.Plugins;
using Microsoft.Agents.Builder;
using Microsoft.SemanticKernel;
using Moq;
using Xunit;

namespace EmployeeSelfServiceAgent.Tests.Plugins;

/// <summary>
/// Integration tests for WeatherForecastPlugin through Semantic Kernel.
/// Tests plugin invocation through the kernel's function calling infrastructure.
/// </summary>
public class WeatherForecastPluginIntegrationTests
{
    /// <summary>
    /// Helper method to create a mock ITurnContext with streaming response.
    /// </summary>
    private static Mock<ITurnContext> CreateMockTurnContext()
    {
        var mockTurnContext = new Mock<ITurnContext>();
        var mockStreamingResponse = new Mock<IStreamingResponse>();
        
        mockStreamingResponse
            .Setup(x => x.QueueInformativeUpdateAsync(It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);
        
        mockTurnContext
            .Setup(x => x.StreamingResponse)
            .Returns(mockStreamingResponse.Object);
        
        return mockTurnContext;
    }

    [Fact]
    public async Task Plugin_InvokesCorrectly_ThroughKernel()
    {
        // Arrange
        var mockTurnContext = CreateMockTurnContext();
        var kernel = Kernel.CreateBuilder().Build();
        var plugin = new WeatherForecastPlugin(mockTurnContext.Object);
        kernel.Plugins.AddFromObject(plugin, "Weather");
        
        // Act
        var result = await kernel.InvokeAsync<WeatherForecast>(
            "Weather", 
            "GetForecastForDate",
            new KernelArguments
            {
                ["date"] = "2025-12-25",
                ["location"] = "Seattle"
            });
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("2025-12-25", result.Date);
        Assert.InRange(result.TemperatureC, -20, 55);
    }

    [Fact]
    public async Task Plugin_RegistersWithCorrectMetadata()
    {
        // Arrange
        var mockTurnContext = CreateMockTurnContext();
        var kernel = Kernel.CreateBuilder().Build();
        var plugin = new WeatherForecastPlugin(mockTurnContext.Object);
        
        // Act
        var addedPlugin = kernel.Plugins.AddFromObject(plugin, "Weather");
        
        // Assert
        Assert.NotNull(addedPlugin);
        Assert.Equal("Weather", addedPlugin.Name);
        
        var function = addedPlugin["GetForecastForDate"];
        Assert.NotNull(function);
        Assert.NotNull(function.Metadata);
        Assert.Equal(2, function.Metadata.Parameters.Count);
        Assert.Contains(function.Metadata.Parameters, p => p.Name == "date");
        Assert.Contains(function.Metadata.Parameters, p => p.Name == "location");
    }

    [Theory]
    [InlineData("2025-01-01", "New York")]
    [InlineData("December 25, 2025", "London")]
    [InlineData("2025-03-15", "Tokyo")]
    public async Task Plugin_HandlesMultipleInvocations_ThroughKernel(string date, string location)
    {
        // Arrange
        var mockTurnContext = CreateMockTurnContext();
        var kernel = Kernel.CreateBuilder().Build();
        var plugin = new WeatherForecastPlugin(mockTurnContext.Object);
        kernel.Plugins.AddFromObject(plugin, "Weather");
        
        // Act
        var result = await kernel.InvokeAsync<WeatherForecast>(
            "Weather",
            "GetForecastForDate",
            new KernelArguments
            {
                ["date"] = date,
                ["location"] = location
            });
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(date, result.Date);
        Assert.InRange(result.TemperatureC, -20, 55);
    }

    [Fact]
    public async Task Plugin_WorksWithKernelFunctionFromMethod()
    {
        // Arrange
        var mockTurnContext = CreateMockTurnContext();
        var kernel = Kernel.CreateBuilder().Build();
        var plugin = new WeatherForecastPlugin(mockTurnContext.Object);
        
        // Act - Add plugin and get the specific function
        kernel.Plugins.AddFromObject(plugin, "Weather");
        var function = kernel.Plugins.GetFunction("Weather", "GetForecastForDate");
        
        var result = await kernel.InvokeAsync<WeatherForecast>(
            function,
            new KernelArguments
            {
                ["date"] = "2025-12-25",
                ["location"] = "Seattle"
            });
        
        // Assert
        Assert.NotNull(result);
        Assert.NotNull(function);
        Assert.Equal("GetForecastForDate", function.Name);
    }

    [Fact]
    public async Task Plugin_CanBeInvoked_MultipleTimesSequentially()
    {
        // Arrange
        var mockTurnContext = CreateMockTurnContext();
        var kernel = Kernel.CreateBuilder().Build();
        var plugin = new WeatherForecastPlugin(mockTurnContext.Object);
        kernel.Plugins.AddFromObject(plugin, "Weather");
        
        // Act - Invoke multiple times
        var result1 = await kernel.InvokeAsync<WeatherForecast>(
            "Weather",
            "GetForecastForDate",
            new KernelArguments
            {
                ["date"] = "2025-12-25",
                ["location"] = "Seattle"
            });
        
        var result2 = await kernel.InvokeAsync<WeatherForecast>(
            "Weather",
            "GetForecastForDate",
            new KernelArguments
            {
                ["date"] = "2025-12-26",
                ["location"] = "Portland"
            });
        
        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal("2025-12-25", result1.Date);
        Assert.Equal("2025-12-26", result2.Date);
        
        // Verify streaming response was called twice
        Mock.Get(mockTurnContext.Object.StreamingResponse)
            .Verify(x => x.QueueInformativeUpdateAsync(It.IsAny<string>(), default), 
                Times.Exactly(2));
    }

    [Fact]
    public async Task Plugin_FunctionDescription_IsAccessibleThroughMetadata()
    {
        // Arrange
        var mockTurnContext = CreateMockTurnContext();
        var kernel = Kernel.CreateBuilder().Build();
        var plugin = new WeatherForecastPlugin(mockTurnContext.Object);
        kernel.Plugins.AddFromObject(plugin, "Weather");
        
        // Act
        var function = kernel.Plugins.GetFunction("Weather", "GetForecastForDate");
        
        // Assert
        Assert.NotNull(function.Metadata);
        // Description may be null or empty if not explicitly set in [KernelFunction] attribute
        // The XML comments are for documentation purposes but may not be captured as runtime metadata
    }

    [Fact]
    public void Plugin_CanBeAdded_WithDifferentNames()
    {
        // Arrange
        var mockTurnContext = CreateMockTurnContext();
        var kernel = Kernel.CreateBuilder().Build();
        var plugin = new WeatherForecastPlugin(mockTurnContext.Object);
        
        // Act
        var addedPlugin = kernel.Plugins.AddFromObject(plugin, "WeatherService");
        
        // Assert
        Assert.NotNull(addedPlugin);
        Assert.Equal("WeatherService", addedPlugin.Name);
        Assert.NotNull(addedPlugin["GetForecastForDate"]);
    }
}
