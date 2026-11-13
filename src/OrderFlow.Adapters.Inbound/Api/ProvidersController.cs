using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Application.Dtos;
using OrderFlow.Application.Payments.ListProviders;

namespace OrderFlow.Adapters.Inbound.Api;

/// <summary>
/// Inbound HTTP adapter that exposes the catalogue of payment providers.
/// </summary>
/// <remarks>
/// The test screen uses it to render the provider/method pickers and to tell
/// the user which providers still need credentials. No secret is exposed.
/// </remarks>
[ApiController]
[Route("api/providers")]
[Produces("application/json")]
public sealed class ProvidersController : ControllerBase
{
    private readonly ListPaymentProvidersUseCase _listProviders;

    /// <summary>Creates the controller.</summary>
    /// <param name="listProviders">List-providers use case.</param>
    public ProvidersController(ListPaymentProvidersUseCase listProviders)
    {
        _listProviders = listProviders;
    }

    /// <summary>Lists the registered payment providers.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One entry per provider.</returns>
    /// <response code="200">Catalogue returned.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<PaymentProviderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<PaymentProviderDto>>> ListProviders(
        CancellationToken cancellationToken)
    {
        var providers = await _listProviders.ExecuteAsync(cancellationToken);
        return Ok(providers);
    }
}
