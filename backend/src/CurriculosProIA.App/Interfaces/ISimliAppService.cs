using CurriculosProIA.Domain.Signatures.Simli;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.App.Interfaces;

public interface ISimliAppService
{
    IActionResult GetConfig();
    Task<IActionResult> CreateSession(CreateSimliSessionSignature body, CancellationToken cancellationToken = default);
    Task<IActionResult> SynthesizeSpeech(SimliSpeechSignature body, CancellationToken cancellationToken = default);
}
