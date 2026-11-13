using OrderFlow.Domain.Payments;
using Xunit;

namespace OrderFlow.Integration.IAppForms.Tests;

/// <summary>Translation between the OrderFlow and the iAppForms vocabularies.</summary>
public sealed class IAppFormsVocabularyTests
{
    [Theory]
    [InlineData(PaymentStatus.Pending, "Pending")]
    [InlineData(PaymentStatus.Processing, "Pending")]
    [InlineData(PaymentStatus.Paid, "Paid")]
    [InlineData(PaymentStatus.Failed, "Failed")]
    [InlineData(PaymentStatus.Cancelled, "Cancelled")]
    [InlineData(PaymentStatus.Refunded, "Refunded")]
    public void ToProviderStatus_TranslatesTheSettlementStatus(
        PaymentStatus status,
        string expected)
    {
        Assert.Equal(expected, IAppFormsVocabulary.ToProviderStatus(status));
    }

    [Theory]
    [InlineData(PaymentStatus.Pending, "InReview")]
    [InlineData(PaymentStatus.Processing, "InReview")]
    [InlineData(PaymentStatus.Paid, "Approved")]
    [InlineData(PaymentStatus.Cancelled, "Cancelled")]
    [InlineData(PaymentStatus.Refunded, "Approved")]
    public void ToPlanPaymentStatus_TranslatesTheCommercialStatus(
        PaymentStatus status,
        string expected)
    {
        Assert.Equal(expected, IAppFormsVocabulary.ToPlanPaymentStatus(status, hasProviderReference: true));
    }

    [Fact]
    public void ToPlanPaymentStatus_WithADecline_IsRejected()
    {
        Assert.Equal(
            "Rejected",
            IAppFormsVocabulary.ToPlanPaymentStatus(PaymentStatus.Failed, hasProviderReference: true));
    }

    [Fact]
    public void ToPlanPaymentStatus_WithoutAProviderAnswer_IsAnError()
    {
        Assert.Equal(
            "Error",
            IAppFormsVocabulary.ToPlanPaymentStatus(PaymentStatus.Failed, hasProviderReference: false));
    }

    [Theory]
    [InlineData("Pix", PaymentMethod.Pix)]
    [InlineData("CreditCard", PaymentMethod.CreditCard)]
    [InlineData("DebitCard", PaymentMethod.DebitCard)]
    [InlineData("Boleto", PaymentMethod.Boleto)]
    [InlineData("pix", PaymentMethod.Pix)]
    public void TryParseMethod_AcceptsTheIAppFormsCodes(string code, PaymentMethod expected)
    {
        Assert.True(IAppFormsVocabulary.TryParseMethod(code, out var method));
        Assert.Equal(expected, method);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Crypto")]
    public void TryParseMethod_RejectsUnknownCodes(string? code)
    {
        Assert.False(IAppFormsVocabulary.TryParseMethod(code, out _));
    }

    [Fact]
    public void ToMethodCode_UsesTheSameToken()
    {
        Assert.Equal("Boleto", IAppFormsVocabulary.ToMethodCode(PaymentMethod.Boleto));
    }

    [Theory]
    [InlineData("Stripe", PaymentProvider.Stripe)]
    [InlineData("mercadopago", PaymentProvider.MercadoPago)]
    [InlineData("ASAAS", PaymentProvider.Asaas)]
    [InlineData("Simulated", PaymentProvider.Simulated)]
    public void TryParseProvider_AcceptsTheAdapterNames(string name, PaymentProvider expected)
    {
        Assert.True(IAppFormsVocabulary.TryParseProvider(name, out var provider));
        Assert.Equal(expected, provider);
    }

    [Fact]
    public void ToProviderName_UsesTheAdapterName()
    {
        Assert.Equal("MercadoPago", IAppFormsVocabulary.ToProviderName(PaymentProvider.MercadoPago));
    }
}
