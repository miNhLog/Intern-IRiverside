using HeThongDatTiecCuoi_API.DTOs.Policies;

namespace HeThongDatTiecCuoi_API.Services;

public static class PolicyValidator
{
    public const int CurrentSchemaVersion = 1;

    public static IReadOnlyDictionary<string, string[]> Validate(UpdatePolicyRequestDto request)
    {
        var errors = new Dictionary<string, List<string>>();

        if (request.ExpectedVersion is null or <= 0)
        {
            Add(errors, "expectedVersion", "Expected policy version must be greater than 0.");
        }

        ValidateDeposit(request.Deposit, errors);
        ValidateCancellation(request.Cancellation, errors);

        return errors.ToDictionary(x => x.Key, x => x.Value.ToArray());
    }

    public static PolicyContent Normalize(UpdatePolicyRequestDto request) => new()
    {
        Deposit = new DepositPolicyDto
        {
            TotalPercentageOfContractValue = request.Deposit!.TotalPercentageOfContractValue!.Value,
            Installments = request.Deposit.Installments!
                .Select(item => new DepositInstallmentDto
                {
                    Sequence = item.Sequence,
                    DueBeforeEventDays = item.DueBeforeEventDays,
                    PercentageOfContractValue = item.PercentageOfContractValue,
                    Note = NormalizeNote(item.Note)
                })
                .ToList()
        },
        Cancellation = new CancellationPolicyDto
        {
            Tiers = request.Cancellation!.Tiers!
                .Select(item => new CancellationTierDto
                {
                    Sequence = item.Sequence,
                    NoticeDaysFrom = item.NoticeDaysFrom,
                    NoticeDaysTo = item.NoticeDaysTo,
                    DepositPenaltyPercentage = item.DepositPenaltyPercentage,
                    DepositRefundPercentage = item.DepositRefundPercentage,
                    Note = NormalizeNote(item.Note)
                })
                .ToList()
        }
    };

    public static IReadOnlyDictionary<string, string[]> ValidateStored(PolicyResponseDto document)
    {
        var errors = new Dictionary<string, List<string>>();

        if (document.SchemaVersion != CurrentSchemaVersion)
        {
            Add(errors, "schemaVersion", "The policy schema version is not supported.");
        }

        if (document.Version <= 0)
        {
            Add(errors, "version", "Policy version must be greater than 0.");
        }

        if (!string.Equals(
                TextNormalization.Required(document.Currency),
                "VND",
                StringComparison.OrdinalIgnoreCase))
        {
            Add(errors, "currency", "Currency must be VND.");
        }

        var request = new UpdatePolicyRequestDto
        {
            ExpectedVersion = document.Version,
            Deposit = new DepositPolicyRequestDto
            {
                TotalPercentageOfContractValue = document.Deposit?.TotalPercentageOfContractValue,
                Installments = document.Deposit?.Installments?
                    .Select(item => new DepositInstallmentRequestDto
                    {
                        Sequence = item.Sequence,
                        DueBeforeEventDays = item.DueBeforeEventDays,
                        PercentageOfContractValue = item.PercentageOfContractValue,
                        Note = item.Note
                    })
                    .ToList()
            },
            Cancellation = new CancellationPolicyRequestDto
            {
                Tiers = document.Cancellation?.Tiers?
                    .Select(item => new CancellationTierRequestDto
                    {
                        Sequence = item.Sequence,
                        NoticeDaysFrom = item.NoticeDaysFrom,
                        NoticeDaysTo = item.NoticeDaysTo,
                        DepositPenaltyPercentage = item.DepositPenaltyPercentage,
                        DepositRefundPercentage = item.DepositRefundPercentage,
                        Note = item.Note
                    })
                    .ToList()
            }
        };

        foreach (var item in Validate(request))
        {
            foreach (var message in item.Value)
            {
                Add(errors, item.Key, message);
            }
        }

        return errors.ToDictionary(x => x.Key, x => x.Value.ToArray());
    }

    public static PolicyResponseDto NormalizeStored(PolicyResponseDto document) => new()
    {
        SchemaVersion = document.SchemaVersion,
        Version = document.Version,
        UpdatedAtUtc = document.UpdatedAtUtc,
        Currency = TextNormalization.Required(document.Currency),
        Deposit = new DepositPolicyDto
        {
            TotalPercentageOfContractValue = document.Deposit.TotalPercentageOfContractValue,
            Installments = document.Deposit.Installments
                .Select(item => new DepositInstallmentDto
                {
                    Sequence = item.Sequence,
                    DueBeforeEventDays = item.DueBeforeEventDays,
                    PercentageOfContractValue = item.PercentageOfContractValue,
                    Note = NormalizeNote(item.Note)
                })
                .ToList()
        },
        Cancellation = new CancellationPolicyDto
        {
            Tiers = document.Cancellation.Tiers
                .Select(item => new CancellationTierDto
                {
                    Sequence = item.Sequence,
                    NoticeDaysFrom = item.NoticeDaysFrom,
                    NoticeDaysTo = item.NoticeDaysTo,
                    DepositPenaltyPercentage = item.DepositPenaltyPercentage,
                    DepositRefundPercentage = item.DepositRefundPercentage,
                    Note = NormalizeNote(item.Note)
                })
                .ToList()
        }
    };

    private static void ValidateDeposit(
        DepositPolicyRequestDto? deposit,
        IDictionary<string, List<string>> errors)
    {
        if (deposit is null)
        {
            Add(errors, "deposit", "Deposit policy is required.");
            return;
        }

        if (deposit.TotalPercentageOfContractValue is null ||
            deposit.TotalPercentageOfContractValue < 0 ||
            deposit.TotalPercentageOfContractValue > 100)
        {
            Add(errors, "deposit.totalPercentageOfContractValue", "Total deposit percentage must be between 0 and 100%.");
        }

        var installments = deposit.Installments;
        if (installments is null || installments.Count == 0)
        {
            Add(errors, "deposit.installments", "At least one deposit installment is required.");
            return;
        }

        decimal total = 0;
        for (var index = 0; index < installments.Count; index++)
        {
            var item = installments[index];
            var prefix = $"deposit.installments[{index}]";

            if (item.Sequence != index + 1)
            {
                Add(errors, $"{prefix}.sequence", "Sequence must be contiguous and start at 1.");
            }

            if (item.DueBeforeEventDays < 0)
            {
                Add(errors, $"{prefix}.dueBeforeEventDays", "Days must be greater than or equal to 0.");
            }

            if (item.PercentageOfContractValue < 0 || item.PercentageOfContractValue > 100)
            {
                Add(errors, $"{prefix}.percentageOfContractValue", "Deposit percentage must be between 0 and 100%.");
            }

            if (index > 0 && item.DueBeforeEventDays >= installments[index - 1].DueBeforeEventDays)
            {
                Add(errors, $"{prefix}.dueBeforeEventDays", "Later installments must be due closer to the event.");
            }

            ValidateNote(item.Note, $"{prefix}.note", errors);
            total += item.PercentageOfContractValue;
        }

        if (deposit.TotalPercentageOfContractValue.HasValue &&
            total != deposit.TotalPercentageOfContractValue.Value)
        {
            Add(
                errors,
                "deposit.installments",
                "Installment percentages must equal the policy total deposit percentage.");
        }
    }

    private static void ValidateCancellation(
        CancellationPolicyRequestDto? cancellation,
        IDictionary<string, List<string>> errors)
    {
        if (cancellation is null)
        {
            Add(errors, "cancellation", "Cancellation policy is required.");
            return;
        }

        var tiers = cancellation.Tiers;
        if (tiers is null || tiers.Count == 0)
        {
            Add(errors, "cancellation.tiers", "At least one cancellation tier is required.");
            return;
        }

        for (var index = 0; index < tiers.Count; index++)
        {
            var item = tiers[index];
            var prefix = $"cancellation.tiers[{index}]";

            if (item.Sequence != index + 1)
            {
                Add(errors, $"{prefix}.sequence", "Sequence must be contiguous and start at 1.");
            }

            if (item.NoticeDaysFrom < 0)
            {
                Add(errors, $"{prefix}.noticeDaysFrom", "Days must be greater than or equal to 0.");
            }

            if (index == 0 && item.NoticeDaysFrom != 0)
            {
                Add(errors, $"{prefix}.noticeDaysFrom", "The first tier must start at day 0.");
            }

            if (item.NoticeDaysTo.HasValue && item.NoticeDaysFrom > item.NoticeDaysTo.Value)
            {
                Add(errors, $"{prefix}.noticeDaysTo", "The upper bound cannot be lower than the lower bound.");
            }

            if (index < tiers.Count - 1 && !item.NoticeDaysTo.HasValue)
            {
                Add(errors, $"{prefix}.noticeDaysTo", "Only the final tier may be open-ended.");
            }

            if (index == tiers.Count - 1 && item.NoticeDaysTo.HasValue)
            {
                Add(errors, $"{prefix}.noticeDaysTo", "The final tier must be open-ended.");
            }

            if (index > 0)
            {
                var previous = tiers[index - 1];
                if (!previous.NoticeDaysTo.HasValue ||
                    item.NoticeDaysFrom != previous.NoticeDaysTo.Value + 1)
                {
                    Add(errors, $"{prefix}.noticeDaysFrom", "Tiers must be contiguous without gaps or overlaps.");
                }
            }

            if (item.DepositPenaltyPercentage < 0 || item.DepositPenaltyPercentage > 100)
            {
                Add(errors, $"{prefix}.depositPenaltyPercentage", "Penalty percentage must be between 0 and 100%.");
            }

            if (item.DepositRefundPercentage < 0 || item.DepositRefundPercentage > 100)
            {
                Add(errors, $"{prefix}.depositRefundPercentage", "Refund percentage must be between 0 and 100%.");
            }

            if (item.DepositPenaltyPercentage + item.DepositRefundPercentage != 100)
            {
                Add(errors, prefix, "Penalty and refund percentages must total 100% of the collected deposit.");
            }

            ValidateNote(item.Note, $"{prefix}.note", errors);
        }
    }

    private static void ValidateNote(
        string? note,
        string key,
        IDictionary<string, List<string>> errors)
    {
        var normalized = TextNormalization.Optional(note);
        if (note is not null && normalized is null)
        {
            Add(errors, key, "The value cannot be empty after trimming.");
        }
        else if (normalized is not null && normalized.Length > 500)
        {
            Add(errors, key, "The value cannot exceed 500 characters.");
        }
    }

    private static string? NormalizeNote(string? note) =>
        TextNormalization.Optional(note);

    private static void Add(
        IDictionary<string, List<string>> errors,
        string key,
        string message)
    {
        if (!errors.TryGetValue(key, out var messages))
        {
            messages = [];
            errors[key] = messages;
        }

        if (!messages.Contains(message, StringComparer.Ordinal))
        {
            messages.Add(message);
        }
    }
}
