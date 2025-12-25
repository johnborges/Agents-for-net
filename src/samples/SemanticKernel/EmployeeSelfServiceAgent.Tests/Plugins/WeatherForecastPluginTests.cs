// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using EmployeeSelfServiceAgent.Plugins;
using Microsoft.Agents.Builder;
using Moq;
using Xunit;

namespace EmployeeSelfServiceAgent.Tests.Plugins;

/// <summary>
/// Unit tests for WeatherForecastPlugin.
/// Tests plugin methods in isolation with mocked dependencies.
/// </summary>
public class WeatherForecastPluginTests
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
    public async Task GetForecastForDate_ReturnsValidForecast()
    {
        // Arrange
        var mockTurnContext = CreateMockTurnContext();
        var plugin = new WeatherForecastPlugin(mockTurnContext.Object);
        
        // Act
        var result = await plugin.GetForecastForDate("2025-12-25", "Seattle");
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("2025-12-25", result.Date);
        Assert.InRange(result.TemperatureC, -20, 55);
    }

    [Fact]
    public async Task GetForecastForDate_CallsStreamingResponse()
    {
        // Arrange
        var mockTurnContext = CreateMockTurnContext();
        var mockStreamingResponse = mockTurnContext.Object.StreamingResponse as Mock<IStreamingResponse> 
            ?? Mock.Get(mockTurnContext.Object.StreamingResponse);
        
        var plugin = new WeatherForecastPlugin(mockTurnContext.Object);
        
        // Act
        await plugin.GetForecastForDate("2025-12-25", "Seattle");
        
        // Assert
        Mock.Get(mockTurnContext.Object.StreamingResponse)
            .Verify(x => x.QueueInformativeUpdateAsync(
                It.Is<string>(s => s.Contains("Seattle") && s.Contains("2025")),
                default), 
                Times.Once);
    }

    [Theory]
    [InlineData("December 25, 2025", "New York")]
    [InlineData("2025-01-01", "London")]
    [InlineData("01/15/2025", "Tokyo")]
    [InlineData("2025-03-15T10:30:00", "Paris")]
    public async Task GetForecastForDate_HandlesVariousDateFormats(string date, string location)
    {
        // Arrange
        var mockTurnContext = CreateMockTurnContext();
        var plugin = new WeatherForecastPlugin(mockTurnContext.Object);
        
        // Act
        var result = await plugin.GetForecastForDate(date, location);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(date, result.Date);
        Assert.InRange(result.TemperatureC, -20, 55);
    }

    [Fact]
    public async Task GetForecastForDate_FormatsDateCorrectly_WhenParseable()
    {
        // Arrange
        var mockTurnContext = CreateMockTurnContext();
        var plugin = new WeatherForecastPlugin(mockTurnContext.Object);
        var parseableDate = "2025-12-25";
        
        // Act
        await plugin.GetForecastForDate(parseableDate, "Seattle");
        
        // Assert - Verify the streaming response includes the formatted long date
        Mock.Get(mockTurnContext.Object.StreamingResponse)
            .Verify(x => x.QueueInformativeUpdateAsync(
                It.Is<string>(s => s.Contains("Thursday, December 25, 2025")),
                default),
                Times.Once);
    }

    [Fact]
    public async Task GetForecastForDate_UsesOriginalDate_WhenNotParseable()
    {
        // Arrange
        var mockTurnContext = CreateMockTurnContext();
        var plugin = new WeatherForecastPlugin(mockTurnContext.Object);
        var unparsableDate = "not-a-date";
        
        // Act
        await plugin.GetForecastForDate(unparsableDate, "Seattle");
        
        // Assert - Verify the streaming response uses the original unparseable string
        Mock.Get(mockTurnContext.Object.StreamingResponse)
            .Verify(x => x.QueueInformativeUpdateAsync(
                It.Is<string>(s => s.Contains("not-a-date")),
                default),
                Times.Once);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("2025-12-25", "")]
    [InlineData("", "Seattle")]
    public async Task GetForecastForDate_HandlesEmptyInputs(string date, string location)
    {
        // Arrange
        var mockTurnContext = CreateMockTurnContext();
        var plugin = new WeatherForecastPlugin(mockTurnContext.Object);
        
        // Act
        var result = await plugin.GetForecastForDate(date, location);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(date, result.Date);
        Assert.InRange(result.TemperatureC, -20, 55);
    }

    [Fact]
    public async Task GetForecastForDate_ReturnsTemperatureInExpectedRange()
    {
        // Arrange
        var mockTurnContext = CreateMockTurnContext();
        var plugin = new WeatherForecastPlugin(mockTurnContext.Object);
        
        // Act - Run multiple times to check randomization
        for (int i = 0; i < 10; i++)
        {
            var result = await plugin.GetForecastForDate("2025-12-25", "Seattle");
            
            // Assert
            Assert.InRange(result.TemperatureC, -20, 55);
        }
    }
}
