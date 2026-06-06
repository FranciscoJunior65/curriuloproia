using CurriculosProIA.App.Interfaces;
using CurriculosProIA.Domain.Signatures.Simli;
using CurriculosProIA.Service.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.App.Implementations;

public class SimliAppService : ISimliAppService
{
    private readonly ISimliService _simli;

    public SimliAppService(ISimliService simli) => _simli = simli;

    public IActionResult GetConfig()
    {
        var config = _simli.GetConfig();
        return new OkObjectResult(new { success = true, config });
    }

    public async Task<IActionResult> CreateSession(
        CreateSimliSessionSignature body,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await _simli.CreateSessionAsync(
                body.FaceId,
                body.PersonaInitials,
                cancellationToken);

            return new OkObjectResult(new
            {
                success = true,
                sessionToken = session.SessionToken,
                faceId = session.FaceId
            });
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(new { success = false, error = ex.Message });
        }
        catch (Exception)
        {
            return new ObjectResult(new { success = false, error = "Erro ao criar sessão Simli." })
            {
                StatusCode = 500
            };
        }
    }

    public async Task<IActionResult> SynthesizeSpeech(
        SimliSpeechSignature body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.Text))
        {
            return new BadRequestObjectResult(new { success = false, error = "Texto é obrigatório." });
        }

        try
        {
            var audio = await _simli.SynthesizeSpeechMp3Async(body.Text, body.Voice, cancellationToken);
            return new FileContentResult(audio, "audio/mpeg")
            {
                FileDownloadName = "speech.mp3"
            };
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(new { success = false, error = ex.Message });
        }
        catch (Exception)
        {
            return new ObjectResult(new { success = false, error = "Erro ao sintetizar fala." })
            {
                StatusCode = 500
            };
        }
    }
}
