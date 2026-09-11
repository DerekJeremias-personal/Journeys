using System;
using System.Collections.Generic;
using Journeys.Core.Extensions;
using Journeys.Core.Models;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Models;

namespace Journeys.Core.Services;

/// <summary>
/// Validates <see cref="PointAccountTypeDto"/> before persist (UpsertPointAccountType).
/// </summary>
public static class PointAccountTypeValidator
{
    private static readonly HashSet<string> AllowedLedgerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        PointLedgerTypeStrings.ESCROW,
        PointLedgerTypeStrings.SPENDABLE,
        PointLedgerTypeStrings.EXPIRED,
        PointLedgerTypeStrings.NONSPENDABLE,
        PointLedgerTypeStrings.ARCHIVE
    };

    private static readonly HashSet<string> LedgerTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        PointLedgerTypeStrings.ESCROW,
        PointLedgerTypeStrings.SPENDABLE,
        PointLedgerTypeStrings.EXPIRED,
        PointLedgerTypeStrings.NONSPENDABLE,
        PointLedgerTypeStrings.ARCHIVE
    };

    public static void Validate(PointAccountTypeDto? pat)
    {
        if (pat == null)
        {
            throw new APIErrorsException(new Dictionary<string, string>
            {
                ["pointaccounttype.validation.0"] =
                    "[violation=PAT_MISSING_DTO] field=PointAccountType Point account type data is required."
            });
        }

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(pat.Name))
        {
            errors.Add(
                "[violation=PAT_MISSING_NAME] field=Name Point account type Name is required.");
        }

        if (string.IsNullOrWhiteSpace(pat.Status))
        {
            errors.Add(
                "[violation=PAT_MISSING_STATUS] field=Status Point account type Status is required.");
        }

        var ledgerType = pat.LedgerType?.Trim();
        if (string.IsNullOrWhiteSpace(ledgerType))
        {
            errors.Add(
                "[violation=PAT_MISSING_LEDGER_TYPE] field=LedgerType Point account type LedgerType is required.");
        }
        else if (!AllowedLedgerTypes.Contains(ledgerType))
        {
            errors.Add(
                $"[violation=PAT_INVALID_LEDGER_TYPE] field=LedgerType LedgerType '{pat.LedgerType}' is not valid. Use Escrow, Spendable, Expired, NonSpendable, or Archive (tier-qualification buckets: NonSpendable with isSpendable false).");
        }

        if (pat.RoundingDecimalPlaces < 0)
        {
            errors.Add(
                "[violation=PAT_INVALID_ROUNDING_PLACES] field=RoundingDecimalPlaces roundingDecimalPlaces must be >= 0.");
        }

        if (!string.IsNullOrWhiteSpace(pat.RoundingOptionString)
            && !Enum.TryParse<MidpointRounding>(pat.RoundingOptionString, ignoreCase: true, out _))
        {
            errors.Add(
                "[violation=PAT_INVALID_ROUNDING_OPTION] field=RoundingOptionString roundingOptionString must be a MidpointRounding value (AwayFromZero, ToEven, ToZero, ToNegativeInfinity, ToPositiveInfinity).");
        }

        if (!string.IsNullOrWhiteSpace(pat.ExpiresToPointAccountTypeId)
            && LedgerTypeNames.Contains(pat.ExpiresToPointAccountTypeId.Trim()))
        {
            errors.Add(
                "[violation=PAT_EXPIRES_TO_NOT_PAT_ID] field=ExpiresToPointAccountTypeId expiresToPointAccountTypeId must be a destination PAT id from GetPointAccountType — not a ledger type name like 'Expired'.");
        }

        if (!string.IsNullOrWhiteSpace(pat.ExpiresToPointAccountTypeId)
            && !string.IsNullOrWhiteSpace(pat.Id)
            && string.Equals(pat.ExpiresToPointAccountTypeId.Trim(), pat.Id.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                "[violation=PAT_EXPIRES_TO_SELF] field=ExpiresToPointAccountTypeId expiresToPointAccountTypeId cannot equal this PAT's id.");
        }

        var hasRollingLifespan = pat.PointsLifespanDays is > 0;
        var hasFixedEndDate = !pat.PointsLifespanEndDate.IsNullOrMinDate();

        if (hasRollingLifespan && hasFixedEndDate)
        {
            errors.Add(
                "[violation=PAT_CONFLICTING_LIFESPAN] field=PointsLifespanDays Prefer rolling expiration: set pointsLifespanDays and leave pointsLifespanEndDate null — do not set both.");
        }

        if (!string.IsNullOrWhiteSpace(pat.ExpiresToPointAccountTypeId) && !hasRollingLifespan && !hasFixedEndDate)
        {
            errors.Add(
                "[violation=PAT_EXPIRES_TO_REQUIRES_LIFESPAN] field=ExpiresToPointAccountTypeId When expiresToPointAccountTypeId is set, set pointsLifespanDays (preferred rolling window) or pointsLifespanEndDate on this PAT.");
        }

        if (string.Equals(ledgerType, PointLedgerTypeStrings.ESCROW, StringComparison.OrdinalIgnoreCase)
            && pat.IsSpendable == true)
        {
            errors.Add(
                "[violation=PAT_ESCROW_MUST_NOT_BE_SPENDABLE] field=IsSpendable Escrow PATs are hold buckets; set isSpendable false.");
        }

        if (pat.IsSpendable == true)
        {
            if (!string.Equals(ledgerType, PointLedgerTypeStrings.SPENDABLE, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    "[violation=PAT_SPENDABLE_LEDGER_MISMATCH] field=IsSpendable isSpendable true requires ledgerType Spendable.");
            }
        }
        else if (pat.IsSpendable == false)
        {
            if (string.Equals(ledgerType, PointLedgerTypeStrings.SPENDABLE, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    "[violation=PAT_NONSPENDABLE_LEDGER_MISMATCH] field=IsSpendable ledgerType Spendable cannot have isSpendable false — use NonSpendable for counters.");
            }
        }

        if (errors.Count == 0)
            return;

        var dict = new Dictionary<string, string>();
        for (var i = 0; i < errors.Count; i++)
            dict[$"pointaccounttype.validation.{i}"] = errors[i];

        throw new APIErrorsException(dict);
    }
}
