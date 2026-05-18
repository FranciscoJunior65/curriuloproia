using CurriculosProIA.Domain.Signatures.Purchase;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.App.Interfaces;

public interface IPurchaseAppService
{
    IActionResult Test();
    Task<IActionResult> CreateMockPurchase(MockPurchaseSignature body, CancellationToken cancellationToken = default);
    Task<IActionResult> GetHistory(int limit = 50, CancellationToken cancellationToken = default);
    Task<IActionResult> GetCreditHistory(int limit = 50, CancellationToken cancellationToken = default);
    Task<IActionResult> RecordCreditUse(RecordCreditUseSignature body, CancellationToken cancellationToken = default);
}
