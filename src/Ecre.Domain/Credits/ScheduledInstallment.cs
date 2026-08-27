using Ecre.Domain.Common;

namespace Ecre.Domain.Credits;

public readonly record struct ScheduledInstallment(
    int Number,
    DateOnly DueDate,
    Money Principal,
    Money Interest,
    Money Total,
    Money EndingBalance);