using CurriculosProIA.Service.Helpers;

namespace CurriculosProIA.Service.Tests;

public class GroqApiKeyValidatorTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("sua-chave-groq-aqui", false)]
    [InlineData("gsk_test123456789012345678901234567890", true)]
    public void TryValidate_DetectsConfiguredKeys(string? apiKey, bool expected)
    {
        var result = GroqApiKeyValidator.TryValidate(apiKey, out _);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExtractText_ParsesOpenAiCompatiblePayload()
    {
        const string payload = """
            {
              "choices": [
                {
                  "message": {
                    "content": "Resposta de teste"
                  }
                }
              ]
            }
            """;

        var text = GroqChatClient.ExtractText(payload);
        Assert.Equal("Resposta de teste", text);
    }
}
