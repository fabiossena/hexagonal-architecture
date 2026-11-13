namespace OrderFlow.Domain.Payments;

/// <summary>Payment instruments (methods) accepted by the providers.</summary>
public enum PaymentMethod
{
    /// <summary>Credit card.</summary>
    CreditCard = 0,

    /// <summary>Debit card.</summary>
    DebitCard = 1,

    /// <summary>Pix (instant bank transfer, Brazil).</summary>
    Pix = 2,

    /// <summary>Boleto (bank slip, Brazil).</summary>
    Boleto = 3,

    /// <summary>Bank transfer (PIX-like transfer handled by the provider).</summary>
    BankTransfer = 4
}
