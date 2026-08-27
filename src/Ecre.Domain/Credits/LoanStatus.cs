namespace Ecre.Domain.Credits;

public enum LoanStatus
{
    Draft       = 0,
    UnderReview = 1,
    Approved    = 2,
    Disbursed   = 3,
    Active      = 4,
    FullyPaid   = 5,
    Defaulted   = 6
}