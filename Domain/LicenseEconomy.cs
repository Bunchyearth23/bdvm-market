using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace BDVM.Domain;

public enum LicenseChargeKind { PermanentPurchase, PerOperationFee, InsurancePremium, RefundableDeposit }
public enum LicenseQuoteStatus { Quoted, Charged, Refunded, RuleMissing, RuleInvalid, Rejected }

[DataContract]
public sealed class LicenseChargeRule
{
    [DataMember(Name = "kind", Order = 1)] public LicenseChargeKind Kind { get; set; }
    [DataMember(Name = "amount", Order = 2)] public long? Amount { get; set; }
}

[DataContract]
public sealed class LicenseRuleDefinition
{
    [DataMember(Name = "ruleId", Order = 1)] public string RuleId { get; set; } = "";
    [DataMember(Name = "stableLicenseId", Order = 2)] public string StableLicenseId { get; set; } = "";
    [DataMember(Name = "categoryId", Order = 3)] public string CategoryId { get; set; } = "";
    [DataMember(Name = "version", Order = 4)] public long Version { get; set; }
    [DataMember(Name = "charges", Order = 5)] public List<LicenseChargeRule> Charges { get; set; } = new List<LicenseChargeRule>();
}

[DataContract]
public sealed class LicenseEconomicRequest
{
    [DataMember(Name = "idempotencyKey", Order = 1)] public string IdempotencyKey { get; set; } = "";
    [DataMember(Name = "requesterId", Order = 2)] public string RequesterId { get; set; } = "";
    [DataMember(Name = "stableLicenseId", Order = 3)] public string StableLicenseId { get; set; } = "";
    [DataMember(Name = "categoryId", Order = 4)] public string CategoryId { get; set; } = "";
    [DataMember(Name = "operationId", Order = 5)] public string OperationId { get; set; } = "";
    [DataMember(Name = "payer", Order = 6)] public AccountRef Payer { get; set; } = new AccountRef();
    [DataMember(Name = "expectedRuleVersion", Order = 7)] public long ExpectedRuleVersion { get; set; }
    [DataMember(Name = "expectedWalletVersion", Order = 8)] public long ExpectedWalletVersion { get; set; }
}

[DataContract]
public sealed class FrozenLicenseCharge
{
    [DataMember(Name = "kind", Order = 1)] public LicenseChargeKind Kind { get; set; }
    [DataMember(Name = "amount", Order = 2)] public long Amount { get; set; }
}

[DataContract]
public sealed class LicenseEconomicRecord
{
    [DataMember(Name = "idempotencyKey", Order = 1)] public string IdempotencyKey { get; set; } = "";
    [DataMember(Name = "requestFingerprint", Order = 2)] public string RequestFingerprint { get; set; } = "";
    [DataMember(Name = "requesterId", Order = 3)] public string RequesterId { get; set; } = "";
    [DataMember(Name = "stableLicenseId", Order = 4)] public string StableLicenseId { get; set; } = "";
    [DataMember(Name = "categoryId", Order = 5)] public string CategoryId { get; set; } = "";
    [DataMember(Name = "operationId", Order = 6)] public string OperationId { get; set; } = "";
    [DataMember(Name = "ruleId", Order = 7)] public string RuleId { get; set; } = "";
    [DataMember(Name = "ruleVersion", Order = 8)] public long RuleVersion { get; set; }
    [DataMember(Name = "payer", Order = 9)] public AccountRef Payer { get; set; } = new AccountRef();
    [DataMember(Name = "charges", Order = 10)] public List<FrozenLicenseCharge> Charges { get; set; } = new List<FrozenLicenseCharge>();
    [DataMember(Name = "totalDebit", Order = 11)] public long TotalDebit { get; set; }
    [DataMember(Name = "refundableDeposit", Order = 12)] public long RefundableDeposit { get; set; }
    [DataMember(Name = "status", Order = 13)] public LicenseQuoteStatus Status { get; set; }
    [DataMember(Name = "resultCode", Order = 14)] public string ResultCode { get; set; } = "";
    [DataMember(Name = "debitEntryId", Order = 15)] public string? DebitEntryId { get; set; }
    [DataMember(Name = "refundEntryId", Order = 16)] public string? RefundEntryId { get; set; }
    [DataMember(Name = "expectedWalletVersion", Order = 17)] public long ExpectedWalletVersion { get; set; }
    [DataMember(Name = "refundIdempotencyKey", Order = 18)] public string? RefundIdempotencyKey { get; set; }
}

[DataContract]
public sealed class LicenseEntitlement
{
    [DataMember(Name = "stableLicenseId", Order = 1)] public string StableLicenseId { get; set; } = "";
    [DataMember(Name = "beneficiary", Order = 2)] public AccountRef Beneficiary { get; set; } = new AccountRef();
    [DataMember(Name = "sourceIdempotencyKey", Order = 3)] public string SourceIdempotencyKey { get; set; } = "";
}

[DataContract]
public sealed class LicenseEconomyState
{
    [DataMember(Name = "rules", Order = 1)] public List<LicenseRuleDefinition> Rules { get; set; } = new List<LicenseRuleDefinition>();
    [DataMember(Name = "records", Order = 2)] public List<LicenseEconomicRecord> Records { get; set; } = new List<LicenseEconomicRecord>();
    [DataMember(Name = "entitlements", Order = 3)] public List<LicenseEntitlement> Entitlements { get; set; } = new List<LicenseEntitlement>();
}

public interface IVanillaLicenseProjectionPort { void ProjectCompatibilityState(string operationId, string stableLicenseId); }
public interface IMultiplayerLicenseIntentPort { void SubmitHostIntent(LicenseEconomicRequest request); }
public interface IDVCustomLicensesResolutionPort { bool TryResolveStableId(string stableLicenseId, out object runtimeLicenseId); }
public interface IPassengerJobsLicenseMigrationPort { void SuppressLegacyChargeOrRefund(string operationId, string stableLicenseId); }

public static class LicenseEconomyValidation
{
    public static void ValidateState(LicenseEconomyState? state)
    {
        if (state == null) throw new InvalidDataException("License economy state is missing.");
        if (state.Rules.GroupBy(x => x.RuleId, StringComparer.Ordinal).Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Count() != 1)) throw new InvalidDataException("Duplicate or empty license rule identity.");
        if (state.Rules.GroupBy(x => x.StableLicenseId + "\n" + x.CategoryId, StringComparer.Ordinal).Any(x => x.Count() != 1)) throw new InvalidDataException("A license/category pair must resolve to exactly one rule.");
        if (state.Records.GroupBy(x => x.IdempotencyKey, StringComparer.Ordinal).Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Count() != 1)) throw new InvalidDataException("Duplicate or empty license idempotency key.");
        if (state.Entitlements.GroupBy(x => x.Beneficiary.Key + "\n" + x.StableLicenseId, StringComparer.Ordinal).Any(x => x.Count() != 1)) throw new InvalidDataException("Duplicate permanent license entitlement.");
    }

    public static string? RuleError(LicenseRuleDefinition rule)
    {
        if (rule == null || string.IsNullOrWhiteSpace(rule.RuleId) || string.IsNullOrWhiteSpace(rule.StableLicenseId) || string.IsNullOrWhiteSpace(rule.CategoryId) || rule.Version < 0) return "invalid-rule-identity";
        if (rule.Charges == null || rule.Charges.Count == 0) return "rule-has-no-economic-mechanism";
        if (rule.Charges.GroupBy(x => x.Kind).Any(x => x.Count() != 1)) return "duplicate-rule-component";
        if (rule.Charges.Any(x => !x.Amount.HasValue)) return "unconfigured-amount";
        if (rule.Charges.Any(x => x.Amount!.Value < 0)) return "negative-amount";
        return null;
    }
}

public sealed class LicenseEconomyEngine
{
    private readonly object gate = new object();
    private readonly CompanyEconomySnapshot economy;
    private readonly INetworkRoleDetector authority;

    public LicenseEconomyEngine(CompanyEconomySnapshot economy, INetworkRoleDetector authority)
    {
        this.economy = economy ?? throw new ArgumentNullException(nameof(economy));
        this.authority = authority ?? throw new ArgumentNullException(nameof(authority));
        CompanyEconomyPersistence.Validate(economy);
    }

    public LicenseEconomicRecord Quote(LicenseEconomicRequest request)
    {
        lock (gate)
        {
            RequireRequest(request);
            var fingerprint = Fingerprint(request);
            var known = economy.LicenseEconomy.Records.SingleOrDefault(x => x.IdempotencyKey == request.IdempotencyKey);
            if (known != null)
            {
                if (known.RequestFingerprint != fingerprint) throw new InvalidOperationException("idempotency-key-payload-mismatch");
                return known;
            }
            var record = NewRecord(request, fingerprint);
            economy.LicenseEconomy.Records.Add(record);
            if (!NetworkAuthorityPolicy.CanExecuteEconomy(authority.Detect(), out _)) return Reject(record, "host-authority-required");
            var matches = economy.LicenseEconomy.Rules.Where(x => x.StableLicenseId == request.StableLicenseId && x.CategoryId == request.CategoryId).ToArray();
            if (matches.Length == 0) return Set(record, LicenseQuoteStatus.RuleMissing, "rule-missing-access-remains-economically-unblocked");
            if (matches.Length != 1) return Set(record, LicenseQuoteStatus.RuleInvalid, "ambiguous-rule-access-remains-economically-unblocked");
            var rule = matches[0]; record.RuleId = rule.RuleId; record.RuleVersion = rule.Version;
            var error = LicenseEconomyValidation.RuleError(rule);
            if (error != null) return Set(record, LicenseQuoteStatus.RuleInvalid, error + "-access-remains-economically-unblocked");
            if (request.ExpectedRuleVersion != rule.Version) return Reject(record, "expected-rule-version-mismatch");
            var wallet = Wallet(request.Payer);
            if (wallet.Version != request.ExpectedWalletVersion) return Reject(record, "expected-wallet-version-mismatch");
            var payerError = Authorize(request.RequesterId, request.Payer);
            if (payerError != null) return Reject(record, payerError);
            var hasPermanent = economy.LicenseEconomy.Entitlements.Any(x => x.StableLicenseId == request.StableLicenseId && x.Beneficiary.Key == request.Payer.Key);
            record.Charges = rule.Charges.Where(x => x.Kind != LicenseChargeKind.PermanentPurchase || !hasPermanent).Select(x => new FrozenLicenseCharge { Kind = x.Kind, Amount = x.Amount!.Value }).ToList();
            record.TotalDebit = CheckedSum(record.Charges.Select(x => x.Amount));
            record.RefundableDeposit = CheckedSum(record.Charges.Where(x => x.Kind == LicenseChargeKind.RefundableDeposit).Select(x => x.Amount));
            return Set(record, LicenseQuoteStatus.Quoted, "quoted-access-economically-authorized");
        }
    }

    public LicenseEconomicRecord Commit(string idempotencyKey)
    {
        lock (gate)
        {
            var record = Record(idempotencyKey);
            if (record.Status == LicenseQuoteStatus.Charged || record.Status == LicenseQuoteStatus.Refunded) return record;
            if (record.Status != LicenseQuoteStatus.Quoted) return record;
            if (!NetworkAuthorityPolicy.CanExecuteEconomy(authority.Detect(), out _)) return Reject(record, "host-authority-required");
            var wallet = Wallet(record.Payer);
            if (wallet.Version != record.ExpectedWalletVersion) return Reject(record, "expected-wallet-version-mismatch");
            if (wallet.Balance < record.TotalDebit) return Reject(record, "insufficient-funds");
            wallet.Balance -= record.TotalDebit; wallet.Version++;
            record.DebitEntryId = record.IdempotencyKey + ":license-debit";
            if (!economy.Ledger.Any(x => x.EntryId == record.DebitEntryId)) economy.Ledger.Add(new LedgerEntry { EntryId = record.DebitEntryId, CommandId = record.IdempotencyKey, Kind = LedgerEntryKind.LicenseCharge, Debit = record.Payer, Amount = record.TotalDebit, Detail = Audit(record) });
            if (record.Charges.Any(x => x.Kind == LicenseChargeKind.PermanentPurchase) && !economy.LicenseEconomy.Entitlements.Any(x => x.StableLicenseId == record.StableLicenseId && x.Beneficiary.Key == record.Payer.Key))
                economy.LicenseEconomy.Entitlements.Add(new LicenseEntitlement { StableLicenseId = record.StableLicenseId, Beneficiary = record.Payer, SourceIdempotencyKey = record.IdempotencyKey });
            return Set(record, LicenseQuoteStatus.Charged, "charged-access-economically-authorized");
        }
    }

    public LicenseEconomicRecord RefundDeposit(string refundIdempotencyKey, string originalIdempotencyKey, string requesterId)
    {
        lock (gate)
        {
            if (string.IsNullOrWhiteSpace(refundIdempotencyKey)) throw new ArgumentException("refund idempotency key is required");
            var original = Record(originalIdempotencyKey);
            if (original.Status == LicenseQuoteStatus.Refunded) return original;
            if (original.Status != LicenseQuoteStatus.Charged) return original;
            if (!NetworkAuthorityPolicy.CanExecuteEconomy(authority.Detect(), out _)) return Reject(original, "host-authority-required");
            var payerError = Authorize(requesterId, original.Payer); if (payerError != null) return Reject(original, payerError);
            if (original.RefundableDeposit == 0) return Reject(original, "no-refundable-deposit");
            var entryId = original.IdempotencyKey + ":deposit-refund";
            if (economy.Ledger.Any(x => x.EntryId == entryId)) return Set(original, LicenseQuoteStatus.Refunded, "deposit-already-refunded");
            var wallet = Wallet(original.Payer); wallet.Balance += original.RefundableDeposit; wallet.Version++;
            original.RefundIdempotencyKey = refundIdempotencyKey;
            original.RefundEntryId = entryId;
            economy.Ledger.Add(new LedgerEntry { EntryId = entryId, CommandId = refundIdempotencyKey, Kind = LedgerEntryKind.LicenseDepositRefund, Credit = original.Payer, Amount = original.RefundableDeposit, Detail = Audit(original) });
            return Set(original, LicenseQuoteStatus.Refunded, "deposit-refunded-once");
        }
    }

    private string? Authorize(string requester, AccountRef payer)
    {
        if (payer == null || string.IsNullOrWhiteSpace(payer.OwnerId)) return "explicit-payer-required";
        if (payer.Kind == AccountKind.Player) return payer.OwnerId == requester ? null : "personal-account-owner-required";
        var company = economy.Companies.SingleOrDefault(x => x.CompanyId == payer.OwnerId);
        var player = economy.Players.SingleOrDefault(x => x.PlayerId == requester);
        if (company == null || player == null || player.CompanyId != company.CompanyId || company.Liquidating) return "invalid-company-relation";
        if (company.LeaderId == requester) return null;
        return company.DelegatedPermissions.TryGetValue(requester, out var rights) && rights.Contains(CompanyPermission.ManageFunds) ? null : "manage-funds-permission-required";
    }

    private Wallet Wallet(AccountRef account) => economy.Wallets.SingleOrDefault(x => x.Account.Key == account.Key) ?? throw new InvalidOperationException("unknown-payer-account");
    private LicenseEconomicRecord Record(string key) => economy.LicenseEconomy.Records.SingleOrDefault(x => x.IdempotencyKey == key) ?? throw new InvalidOperationException("unknown-license-quote");
    private static LicenseEconomicRecord NewRecord(LicenseEconomicRequest r, string fingerprint) => new LicenseEconomicRecord { IdempotencyKey = r.IdempotencyKey, RequestFingerprint = fingerprint, RequesterId = r.RequesterId, StableLicenseId = r.StableLicenseId, CategoryId = r.CategoryId, OperationId = r.OperationId, Payer = new AccountRef { Kind = r.Payer.Kind, OwnerId = r.Payer.OwnerId }, ExpectedWalletVersion = r.ExpectedWalletVersion };
    private static LicenseEconomicRecord Set(LicenseEconomicRecord r, LicenseQuoteStatus status, string code) { r.Status = status; r.ResultCode = code; return r; }
    private static LicenseEconomicRecord Reject(LicenseEconomicRecord r, string code) => Set(r, LicenseQuoteStatus.Rejected, code);
    private static long CheckedSum(IEnumerable<long> values) { checked { long total = 0; foreach (var value in values) total += value; return total; } }
    private static string Fingerprint(LicenseEconomicRequest r) => string.Join("|", r.RequesterId, r.StableLicenseId, r.CategoryId, r.OperationId, r.Payer.Key, r.ExpectedRuleVersion, r.ExpectedWalletVersion);
    private static string Audit(LicenseEconomicRecord r) => "license=" + r.StableLicenseId + ";category=" + r.CategoryId + ";operation=" + r.OperationId + ";rule=" + r.RuleId + ";ruleVersion=" + r.RuleVersion + ";payer=" + r.Payer.Key + ";deposit=" + r.RefundableDeposit;
    private static void RequireRequest(LicenseEconomicRequest r) { if (r == null || string.IsNullOrWhiteSpace(r.IdempotencyKey) || string.IsNullOrWhiteSpace(r.RequesterId) || string.IsNullOrWhiteSpace(r.StableLicenseId) || string.IsNullOrWhiteSpace(r.CategoryId) || string.IsNullOrWhiteSpace(r.OperationId) || r.Payer == null) throw new ArgumentException("complete-license-request-required"); }
}
