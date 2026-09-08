using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace BDVM.Domain;

public enum FinancingKind { Loan, CreditLine, Guarantee, RecordedDebt }
public enum FinancingState { Offered, Active, Defaulted, Settled, Cancelled, WrittenOff }
public enum FinancingCommandState { Succeeded, Rejected }

[DataContract]
public sealed class FinancingPool
{
    [DataMember(Name = "poolId", Order = 1)] public string PoolId { get; set; } = "";
    [DataMember(Name = "availableCapital", Order = 2)] public long AvailableCapital { get; set; }
    [DataMember(Name = "initialCapital", Order = 3)] public long InitialCapital { get; set; }
    [DataMember(Name = "receivedPayments", Order = 4)] public long ReceivedPayments { get; set; }
    [DataMember(Name = "writtenOff", Order = 5)] public long WrittenOff { get; set; }
    [DataMember(Name = "version", Order = 6)] public long Version { get; set; }
}

[DataContract]
public sealed class FinancingContract
{
    [DataMember(Name = "contractId", Order = 1)] public string ContractId { get; set; } = "";
    [DataMember(Name = "kind", Order = 2)] public FinancingKind Kind { get; set; }
    [DataMember(Name = "debtor", Order = 3)] public AccountRef Debtor { get; set; } = new AccountRef();
    [DataMember(Name = "poolId", Order = 4)] public string? PoolId { get; set; }
    [DataMember(Name = "principalLimit", Order = 5)] public long PrincipalLimit { get; set; }
    [DataMember(Name = "reservedCapital", Order = 6)] public long ReservedCapital { get; set; }
    [DataMember(Name = "outstandingPrincipal", Order = 7)] public long OutstandingPrincipal { get; set; }
    [DataMember(Name = "accruedInterest", Order = 8)] public long AccruedInterest { get; set; }
    [DataMember(Name = "interestBasisPoints", Order = 9)] public int InterestBasisPoints { get; set; }
    [DataMember(Name = "minimumInstallment", Order = 10)] public long MinimumInstallment { get; set; }
    [DataMember(Name = "intervalTicks", Order = 11)] public long IntervalTicks { get; set; }
    [DataMember(Name = "acceptedTick", Order = 12)] public long? AcceptedTick { get; set; }
    [DataMember(Name = "nextDueTick", Order = 13)] public long NextDueTick { get; set; }
    [DataMember(Name = "maturityTick", Order = 14)] public long MaturityTick { get; set; }
    [DataMember(Name = "guaranteeAmount", Order = 15)] public long GuaranteeAmount { get; set; }
    [DataMember(Name = "heldGuarantee", Order = 16)] public long HeldGuarantee { get; set; }
    [DataMember(Name = "terms", Order = 17)] public string Terms { get; set; } = "";
    [DataMember(Name = "state", Order = 18)] public FinancingState State { get; set; }
    [DataMember(Name = "version", Order = 19)] public long Version { get; set; }
}

[DataContract]
public sealed class FinancingCommandRecord
{
    [DataMember(Name = "commandId", Order = 1)] public string CommandId { get; set; } = "";
    [DataMember(Name = "fingerprint", Order = 2)] public string Fingerprint { get; set; } = "";
    [DataMember(Name = "contractId", Order = 3)] public string ContractId { get; set; } = "";
    [DataMember(Name = "state", Order = 4)] public FinancingCommandState State { get; set; }
    [DataMember(Name = "resultCode", Order = 5)] public string ResultCode { get; set; } = "";
    [DataMember(Name = "amount", Order = 6)] public long Amount { get; set; }
}

[DataContract]
public sealed class FinancingStateStore
{
    [DataMember(Name = "pools", Order = 1)] public List<FinancingPool> Pools { get; set; } = new List<FinancingPool>();
    [DataMember(Name = "contracts", Order = 2)] public List<FinancingContract> Contracts { get; set; } = new List<FinancingContract>();
    [DataMember(Name = "commands", Order = 3)] public List<FinancingCommandRecord> Commands { get; set; } = new List<FinancingCommandRecord>();
    [DataMember(Name = "lastProcessedTick", Order = 4)] public long LastProcessedTick { get; set; }
}

public sealed class FinancingCompanyContractCancellationPort : ICompanyContractCancellationPort
{
    private readonly VehicleAcquisitionSnapshot state;
    private readonly FinancingEngine engine;
    public FinancingCompanyContractCancellationPort(VehicleAcquisitionSnapshot state, INetworkRoleDetector authority) { this.state = state; engine = new FinancingEngine(state, authority); }
    public WorldOwnershipOutcome Cancel(string operationId, string companyId) { try { engine.SettleCompanyForLiquidation(operationId, companyId); return WorldOwnershipOutcome.Applied; } catch { return WorldOwnershipOutcome.Unknown; } }
    public WorldOwnershipOutcome Inspect(string companyId) => state.Financing.Contracts.Any(x => x.Debtor.Kind == AccountKind.Company && x.Debtor.OwnerId == companyId && !Terminal(x.State)) ? WorldOwnershipOutcome.NotApplied : WorldOwnershipOutcome.Applied;
    private static bool Terminal(FinancingState state) => state == FinancingState.Settled || state == FinancingState.Cancelled || state == FinancingState.WrittenOff;
}

public sealed class FinancingEngine
{
    private readonly object gate = new object();
    private readonly VehicleAcquisitionSnapshot state;
    private readonly INetworkRoleDetector authority;
    public FinancingEngine(VehicleAcquisitionSnapshot state, INetworkRoleDetector authority) { this.state = state ?? throw new ArgumentNullException(nameof(state)); this.authority = authority ?? throw new ArgumentNullException(nameof(authority)); VehicleAcquisitionPersistence.Validate(state); }

    public FinancingPool RegisterPool(string commandId, string poolId, long externallyBackedCapital)
    {
        lock (gate)
        {
            RequireHost(); var fingerprint = $"pool|{poolId}|{externallyBackedCapital}"; var replay = Known(commandId, fingerprint); if (replay != null) return state.Financing.Pools.Single(x => x.PoolId == replay.ContractId);
            if (string.IsNullOrWhiteSpace(poolId) || externallyBackedCapital < 0 || state.Financing.Pools.Any(x => x.PoolId == poolId)) throw new ArgumentException("A unique pool and explicit non-negative backing are required.");
            var pool = new FinancingPool { PoolId = poolId.Trim(), AvailableCapital = externallyBackedCapital, InitialCapital = externallyBackedCapital, Version = 1 }; state.Financing.Pools.Add(pool); Record(commandId, fingerprint, pool.PoolId, "financing-pool-registered", externallyBackedCapital); return pool;
        }
    }

    public FinancingContract OfferCredit(string commandId, string requesterId, string contractId, FinancingKind kind, AccountRef debtor, string poolId,
        long principalLimit, int interestBasisPoints, long minimumInstallment, long intervalTicks, long maturityTicks, long guaranteeAmount)
    {
        lock (gate)
        {
            RequireHost(); var fingerprint = string.Join("|", "offer", requesterId, contractId, kind, debtor.Key, poolId, principalLimit, interestBasisPoints, minimumInstallment, intervalTicks, maturityTicks, guaranteeAmount); var replay = Known(commandId, fingerprint); if (replay != null) return Contract(replay.ContractId);
            if (kind != FinancingKind.Loan && kind != FinancingKind.CreditLine) throw new ArgumentException("Only loans and credit lines use funded offers.");
            RequireContractInput(commandId, requesterId, contractId, debtor!); if (principalLimit <= 0 || interestBasisPoints < 0 || interestBasisPoints > 10000 || minimumInstallment <= 0 || intervalTicks <= 0 || maturityTicks < intervalTicks || guaranteeAmount < 0) throw new ArgumentException("Invalid financing terms.");
            EnsureControls(requesterId, debtor!); EnsureNoCircularCredit(debtor!); var pool = Pool(poolId); if (pool.AvailableCapital < principalLimit) throw new InvalidOperationException("The lender pool has insufficient explicitly backed capital.");
            pool.AvailableCapital -= principalLimit; pool.Version++;
            var contract = new FinancingContract { ContractId = contractId, Kind = kind, Debtor = Clone(debtor), PoolId = pool.PoolId, PrincipalLimit = principalLimit, ReservedCapital = principalLimit, InterestBasisPoints = interestBasisPoints, MinimumInstallment = minimumInstallment, IntervalTicks = intervalTicks, MaturityTick = maturityTicks, GuaranteeAmount = guaranteeAmount, Terms = $"principalLimit={principalLimit};interestBps={interestBasisPoints};installment={minimumInstallment};interval={intervalTicks};maturity={maturityTicks};guarantee={guaranteeAmount}", State = FinancingState.Offered, Version = 1 };
            state.Financing.Contracts.Add(contract); Record(commandId, fingerprint, contractId, "financing-offer-created", principalLimit); return contract;
        }
    }

    public FinancingCommandRecord Accept(string commandId, string requesterId, string contractId)
    {
        lock (gate)
        {
            RequireHost(); var fingerprint = $"accept|{requesterId}|{contractId}"; var known = Known(commandId, fingerprint); if (known != null) return known; var contract = Contract(contractId); var record = Record(commandId, fingerprint, contractId, "prepared", 0);
            if (contract.State != FinancingState.Offered) return Reject(record, "financing-offer-not-available");
            try { EnsureControls(requesterId, contract.Debtor); } catch { return Reject(record, "financing-debtor-permission-denied"); }
            var wallet = Wallet(contract.Debtor); if (wallet.Balance < contract.GuaranteeAmount) return Reject(record, "financing-guarantee-unfunded");
            if (contract.GuaranteeAmount > 0) { wallet.Balance -= contract.GuaranteeAmount; wallet.Version++; contract.HeldGuarantee = contract.GuaranteeAmount; AddLedger(commandId + ":guarantee", LedgerEntryKind.FinancingGuarantee, wallet.Account, null, contract.GuaranteeAmount, contractId); }
            contract.AcceptedTick = state.LeaseClock.ActiveTick; contract.NextDueTick = checked(state.LeaseClock.ActiveTick + contract.IntervalTicks); contract.MaturityTick = checked(state.LeaseClock.ActiveTick + contract.MaturityTick); contract.State = FinancingState.Active;
            if (contract.Kind == FinancingKind.Loan) TransferReservedToWallet(contract, contract.PrincipalLimit, commandId + ":principal");
            contract.Version++; record.ResultCode = "financing-accepted"; record.Amount = contract.Kind == FinancingKind.Loan ? contract.PrincipalLimit : 0; return record;
        }
    }

    public FinancingCommandRecord Draw(string commandId, string requesterId, string contractId, long amount)
    {
        lock (gate)
        {
            RequireHost(); var fingerprint = $"draw|{requesterId}|{contractId}|{amount}"; var known = Known(commandId, fingerprint); if (known != null) return known; var contract = Contract(contractId); var record = Record(commandId, fingerprint, contractId, "prepared", amount);
            if (contract.Kind != FinancingKind.CreditLine || contract.State != FinancingState.Active || amount <= 0 || amount > contract.ReservedCapital) return Reject(record, "credit-line-draw-refused");
            try { EnsureControls(requesterId, contract.Debtor); } catch { return Reject(record, "financing-debtor-permission-denied"); }
            TransferReservedToWallet(contract, amount, commandId + ":draw"); contract.Version++; record.ResultCode = "credit-line-drawn"; return record;
        }
    }

    public FinancingCommandRecord PlaceGuarantee(string commandId, string requesterId, string contractId, AccountRef debtor, long amount, string reason)
    {
        lock (gate)
        {
            RequireHost(); var fingerprint = string.Join("|", "guarantee", requesterId, contractId, debtor.Key, amount, reason); var known = Known(commandId, fingerprint); if (known != null) return known; RequireContractInput(commandId, requesterId, contractId, debtor!); EnsureControls(requesterId, debtor!); if (amount <= 0 || string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A funded guarantee and explicit reason are required.");
            var wallet = Wallet(debtor!); var record = Record(commandId, fingerprint, contractId, "prepared", amount); if (wallet.Balance < amount) return Reject(record, "guarantee-insufficient-funds"); wallet.Balance -= amount; wallet.Version++; state.Financing.Contracts.Add(new FinancingContract { ContractId = contractId, Kind = FinancingKind.Guarantee, Debtor = Clone(debtor), PrincipalLimit = amount, GuaranteeAmount = amount, HeldGuarantee = amount, Terms = reason.Trim(), AcceptedTick = state.LeaseClock.ActiveTick, State = FinancingState.Active, Version = 1 }); AddLedger(commandId + ":guarantee", LedgerEntryKind.FinancingGuarantee, wallet.Account, null, amount, contractId); record.ResultCode = "guarantee-held"; return record;
        }
    }

    public FinancingContract RecordDebt(string commandId, string requesterId, string contractId, AccountRef debtor, long principal,
        int interestBasisPoints, long minimumInstallment, long intervalTicks, long maturityTicks, string externalEvidence)
    {
        lock (gate)
        {
            RequireHost(); var fingerprint = string.Join("|", "debt", requesterId, contractId, debtor.Key, principal, interestBasisPoints, minimumInstallment, intervalTicks, maturityTicks, externalEvidence); var replay = Known(commandId, fingerprint); if (replay != null) return Contract(replay.ContractId);
            RequireContractInput(commandId, requesterId, contractId, debtor); EnsureControls(requesterId, debtor); if (principal <= 0 || interestBasisPoints < 0 || interestBasisPoints > 10000 || minimumInstallment <= 0 || intervalTicks <= 0 || maturityTicks < intervalTicks || string.IsNullOrWhiteSpace(externalEvidence)) throw new ArgumentException("Recorded debt requires bounded terms and external evidence.");
            var contract = new FinancingContract { ContractId = contractId, Kind = FinancingKind.RecordedDebt, Debtor = Clone(debtor), PrincipalLimit = principal, OutstandingPrincipal = principal, InterestBasisPoints = interestBasisPoints, MinimumInstallment = minimumInstallment, IntervalTicks = intervalTicks, AcceptedTick = state.LeaseClock.ActiveTick, NextDueTick = checked(state.LeaseClock.ActiveTick + intervalTicks), MaturityTick = checked(state.LeaseClock.ActiveTick + maturityTicks), Terms = "externalEvidence=" + externalEvidence.Trim(), State = FinancingState.Active, Version = 1 }; state.Financing.Contracts.Add(contract); Record(commandId, fingerprint, contractId, "external-debt-recorded-no-wallet-credit", principal); return contract;
        }
    }

    public FinancingCommandRecord Repay(string commandId, string requesterId, string contractId, long amount)
    {
        lock (gate)
        {
            RequireHost(); var fingerprint = $"repay|{requesterId}|{contractId}|{amount}"; var known = Known(commandId, fingerprint); if (known != null) return known; var contract = Contract(contractId); var record = Record(commandId, fingerprint, contractId, "prepared", amount);
            try { EnsureControls(requesterId, contract.Debtor); } catch { return Reject(record, "financing-debtor-permission-denied"); }
            if ((contract.State != FinancingState.Active && contract.State != FinancingState.Defaulted) || amount <= 0) return Reject(record, "financing-repayment-refused");
            var paid = PayFromWallet(contract, Math.Min(amount, checked(contract.OutstandingPrincipal + contract.AccruedInterest)), commandId + ":repayment"); if (paid <= 0) return Reject(record, "financing-repayment-unfunded"); FinalizeIfPaid(contract); record.ResultCode = "financing-repaid"; record.Amount = paid; return record;
        }
    }

    public long ProcessClock(string commandId, long tick)
    {
        lock (gate)
        {
            RequireHost(); var fingerprint = $"clock|{tick}"; var known = Known(commandId, fingerprint); if (known != null) return known.Amount; if (tick < state.Financing.LastProcessedTick || tick < 0) throw new InvalidOperationException("Financing clock cannot move backwards."); long paidTotal = 0;
            foreach (var contract in state.Financing.Contracts.Where(x => x.State == FinancingState.Active || x.State == FinancingState.Defaulted).OrderBy(x => x.ContractId, StringComparer.Ordinal).ToArray())
            {
                while (contract.OutstandingPrincipal > 0 && contract.NextDueTick > 0 && contract.NextDueTick <= tick)
                {
                    var interest = checked((contract.OutstandingPrincipal * contract.InterestBasisPoints + 9999L) / 10000L); contract.AccruedInterest = checked(contract.AccruedInterest + interest);
                    var due = contract.NextDueTick >= contract.MaturityTick ? checked(contract.OutstandingPrincipal + contract.AccruedInterest) : Math.Min(checked(contract.OutstandingPrincipal + contract.AccruedInterest), checked(contract.MinimumInstallment + interest));
                    var paid = PayFromWallet(contract, due, commandId + ":due:" + contract.ContractId + ":" + contract.NextDueTick); paidTotal = checked(paidTotal + paid); contract.State = paid < due ? FinancingState.Defaulted : FinancingState.Active; contract.NextDueTick = checked(contract.NextDueTick + contract.IntervalTicks); contract.Version++;
                    if (contract.NextDueTick > contract.MaturityTick && contract.OutstandingPrincipal > 0) { contract.State = FinancingState.Defaulted; break; }
                }
                FinalizeIfPaid(contract);
            }
            state.Financing.LastProcessedTick = tick; Record(commandId, fingerprint, "clock", "financing-clock-processed", paidTotal); return paidTotal;
        }
    }

    public long SettleCompanyForLiquidation(string commandId, string companyId)
    {
        lock (gate)
        {
            RequireHost(); var fingerprint = $"liquidate|{companyId}"; var known = Known(commandId, fingerprint); if (known != null) return known.Amount; if (string.IsNullOrWhiteSpace(companyId)) throw new ArgumentException("Company identity is required."); long writtenOff = 0;
            foreach (var contract in state.Financing.Contracts.Where(x => x.Debtor.Kind == AccountKind.Company && x.Debtor.OwnerId == companyId && !Terminal(x.State)).ToArray())
            {
                if (contract.Kind == FinancingKind.Guarantee) { ReleaseGuarantee(contract, true, commandId + ":guarantee:" + contract.ContractId); continue; }
                ApplyGuaranteeToDebt(contract, commandId + ":held:" + contract.ContractId); var due = checked(contract.OutstandingPrincipal + contract.AccruedInterest); PayFromWallet(contract, due, commandId + ":wallet:" + contract.ContractId); due = checked(contract.OutstandingPrincipal + contract.AccruedInterest); ReturnUndrawn(contract);
                if (due > 0) { if (!string.IsNullOrWhiteSpace(contract.PoolId)) { var pool = Pool(contract.PoolId!); pool.WrittenOff = checked(pool.WrittenOff + due); pool.Version++; } writtenOff = checked(writtenOff + due); contract.OutstandingPrincipal = 0; contract.AccruedInterest = 0; contract.State = FinancingState.WrittenOff; } else contract.State = FinancingState.Settled; contract.Version++;
            }
            Record(commandId, fingerprint, companyId, "financing-liquidation-settled", writtenOff); return writtenOff;
        }
    }

    private void TransferReservedToWallet(FinancingContract contract, long amount, string operationId) { var wallet = Wallet(contract.Debtor); contract.ReservedCapital -= amount; contract.OutstandingPrincipal = checked(contract.OutstandingPrincipal + amount); wallet.Balance = checked(wallet.Balance + amount); wallet.Version++; AddLedger(operationId, LedgerEntryKind.FinancingPrincipal, null, wallet.Account, amount, contract.ContractId); }
    private long PayFromWallet(FinancingContract contract, long requested, string operationId) { if (requested <= 0) return 0; var wallet = Wallet(contract.Debtor); var paid = Math.Min(wallet.Balance, requested); if (paid <= 0) return 0; wallet.Balance -= paid; wallet.Version++; var interestPaid = Math.Min(contract.AccruedInterest, paid); contract.AccruedInterest -= interestPaid; contract.OutstandingPrincipal -= paid - interestPaid; if (!string.IsNullOrWhiteSpace(contract.PoolId)) { var pool = Pool(contract.PoolId!); pool.AvailableCapital = checked(pool.AvailableCapital + paid); pool.ReceivedPayments = checked(pool.ReceivedPayments + paid); pool.Version++; } AddLedger(operationId, LedgerEntryKind.FinancingRepayment, wallet.Account, null, paid, contract.ContractId); return paid; }
    private void ApplyGuaranteeToDebt(FinancingContract contract, string operationId) { var due = checked(contract.OutstandingPrincipal + contract.AccruedInterest); var applied = Math.Min(contract.HeldGuarantee, due); if (applied <= 0) return; contract.HeldGuarantee -= applied; var interest = Math.Min(contract.AccruedInterest, applied); contract.AccruedInterest -= interest; contract.OutstandingPrincipal -= applied - interest; var pool = Pool(contract.PoolId!); pool.AvailableCapital = checked(pool.AvailableCapital + applied); pool.ReceivedPayments = checked(pool.ReceivedPayments + applied); pool.Version++; AddLedger(operationId, LedgerEntryKind.FinancingGuaranteeApplied, null, null, applied, contract.ContractId); }
    private void FinalizeIfPaid(FinancingContract contract) { if (contract.OutstandingPrincipal != 0 || contract.AccruedInterest != 0) return; ReturnUndrawn(contract); ReleaseGuarantee(contract, true, "financing-release:" + contract.ContractId); contract.State = FinancingState.Settled; contract.Version++; }
    private void ReturnUndrawn(FinancingContract contract) { if (contract.ReservedCapital <= 0 || string.IsNullOrWhiteSpace(contract.PoolId)) return; var pool = Pool(contract.PoolId!); pool.AvailableCapital = checked(pool.AvailableCapital + contract.ReservedCapital); pool.Version++; contract.ReservedCapital = 0; }
    private void ReleaseGuarantee(FinancingContract contract, bool toDebtor, string operationId) { if (contract.HeldGuarantee <= 0) { if (contract.Kind == FinancingKind.Guarantee) contract.State = FinancingState.Cancelled; return; } var amount = contract.HeldGuarantee; contract.HeldGuarantee = 0; if (toDebtor) { var wallet = Wallet(contract.Debtor); wallet.Balance = checked(wallet.Balance + amount); wallet.Version++; AddLedger(operationId, LedgerEntryKind.FinancingGuaranteeRelease, null, wallet.Account, amount, contract.ContractId); } contract.State = contract.Kind == FinancingKind.Guarantee ? FinancingState.Cancelled : contract.State; contract.Version++; }
    private void EnsureNoCircularCredit(AccountRef debtor) { if (state.Financing.Contracts.Any(x => x.Debtor.Key == debtor.Key && (x.Kind == FinancingKind.Loan || x.Kind == FinancingKind.CreditLine) && !Terminal(x.State))) throw new InvalidOperationException("Only one active funded financing contract is allowed per debtor; circular refinancing is refused."); }
    private void EnsureControls(string requesterId, AccountRef debtor) { var player = state.Economy.Players.SingleOrDefault(x => x.PlayerId == requesterId) ?? throw new InvalidOperationException("Unknown requester."); if (debtor.Kind == AccountKind.Player) { if (debtor.OwnerId != requesterId) throw new InvalidOperationException("Personal financing cannot be transferred."); return; } var company = state.Economy.Companies.SingleOrDefault(x => x.CompanyId == debtor.OwnerId) ?? throw new InvalidOperationException("Unknown company."); if (company.Liquidating || player.CompanyId != company.CompanyId || (company.LeaderId != requesterId && !(company.DelegatedPermissions.TryGetValue(requesterId, out var rights) && rights.Contains(CompanyPermission.ManageFunds)))) throw new InvalidOperationException("Company fund permission is required."); }
    private void RequireContractInput(string commandId, string requesterId, string contractId, AccountRef debtor) { if (string.IsNullOrWhiteSpace(commandId) || string.IsNullOrWhiteSpace(requesterId) || string.IsNullOrWhiteSpace(contractId) || debtor == null || string.IsNullOrWhiteSpace(debtor.OwnerId) || state.Financing.Contracts.Any(x => x.ContractId == contractId)) throw new ArgumentException("A unique financing contract and explicit debtor are required."); }
    private FinancingContract Contract(string id) => state.Financing.Contracts.Single(x => x.ContractId == id);
    private FinancingPool Pool(string id) => state.Financing.Pools.Single(x => x.PoolId == id);
    private Wallet Wallet(AccountRef account) => state.Economy.Wallets.Single(x => x.Account.Key == account.Key);
    private FinancingCommandRecord? Known(string id, string fingerprint) { var known = state.Financing.Commands.SingleOrDefault(x => x.CommandId == id); if (known != null && known.Fingerprint != fingerprint) throw new InvalidOperationException("Financing command ID payload conflict."); return known; }
    private FinancingCommandRecord Record(string id, string fingerprint, string contractId, string code, long amount) { var r = new FinancingCommandRecord { CommandId = id, Fingerprint = fingerprint, ContractId = contractId, State = FinancingCommandState.Succeeded, ResultCode = code, Amount = amount }; state.Financing.Commands.Add(r); return r; }
    private static FinancingCommandRecord Reject(FinancingCommandRecord record, string code) { record.State = FinancingCommandState.Rejected; record.ResultCode = code; return record; }
    private void AddLedger(string id, LedgerEntryKind kind, AccountRef? debit, AccountRef? credit, long amount, string detail) { if (state.Economy.Ledger.Any(x => x.EntryId == id)) return; state.Economy.Ledger.Add(new LedgerEntry { EntryId = id, CommandId = id, Kind = kind, Debit = debit == null ? null : Clone(debit), Credit = credit == null ? null : Clone(credit), Amount = amount, Detail = detail }); }
    private void RequireHost() { if (!NetworkAuthorityPolicy.CanExecuteEconomy(authority.Detect(), out var reason)) throw new InvalidOperationException(reason); }
    private static AccountRef Clone(AccountRef account) => account.Kind == AccountKind.Player ? AccountRef.Player(account.OwnerId) : AccountRef.Company(account.OwnerId);
    private static bool Terminal(FinancingState value) => value == FinancingState.Settled || value == FinancingState.Cancelled || value == FinancingState.WrittenOff;
}

public static class FinancingValidation
{
    public static void Validate(FinancingStateStore store, VehicleAcquisitionSnapshot state)
    {
        if (store == null || store.LastProcessedTick < 0 || store.Pools.GroupBy(x => x.PoolId).Any(x => x.Count() != 1) || store.Contracts.GroupBy(x => x.ContractId).Any(x => x.Count() != 1) || store.Commands.GroupBy(x => x.CommandId).Any(x => x.Count() != 1)) throw new InvalidOperationException("Invalid or duplicate financing state.");
        foreach (var pool in store.Pools) if (string.IsNullOrWhiteSpace(pool.PoolId) || pool.AvailableCapital < 0 || pool.InitialCapital < 0 || pool.ReceivedPayments < 0 || pool.WrittenOff < 0 || pool.Version < 0) throw new InvalidOperationException("Invalid financing pool.");
        foreach (var contract in store.Contracts) if (string.IsNullOrWhiteSpace(contract.ContractId) || contract.Debtor == null || string.IsNullOrWhiteSpace(contract.Debtor.OwnerId) || contract.PrincipalLimit < 0 || contract.ReservedCapital < 0 || contract.OutstandingPrincipal < 0 || contract.AccruedInterest < 0 || contract.GuaranteeAmount < 0 || contract.HeldGuarantee < 0 || contract.InterestBasisPoints < 0 || contract.InterestBasisPoints > 10000 || !state.Economy.Wallets.Any(x => x.Account.Key == contract.Debtor.Key)) throw new InvalidOperationException("Invalid financing contract.");
    }
}
