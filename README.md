# BDVM - Market

`BDVM.Market` provides the economic layer around railway equipment and operating access: a finite catalog, dynamic pricing signals, financing and license-related costs.

## Status

| Property | Value |
| --- | --- |
| Module kind | Market and financing feature |
| Target framework | .NET Framework 4.8 (`net48`) |
| Required modules | `BDVM.Common`, `BDVM.Companies`, `BDVM.Fleet` |
| Standalone install | Not yet |
| Current runtime host | `BDVM.Full` |

## Responsibilities

- Maintain finite catalog entries, stock levels and listings for existing assets or new orders.
- Reserve stock and coordinate purchase delivery without duplicating money or rolling stock after retries.
- Expose economic metrics used for dynamic prices and profitability guardrails.
- Model loans, credit lines, guarantees and recorded debts with explicit lifecycle states.
- Replace frustrating hard license gates with configurable economic mechanisms: permanent purchase, per-operation fee, insurance premium or refundable deposit.
- Project compatibility state to vanilla or third-party license systems through optional ports while keeping BDVM's stable identifiers authoritative.
- Cancel or settle financing consistently when a company dissolves.

## Key surfaces

`FiniteMarketEngine`, the dynamic economy model, `LicenseEconomyEngine` and `FinancingEngine` are the main services. `MarketCatalogEntry`, `MarketStockEntry`, `MarketListing`, `LicenseRuleDefinition`, `FinancingPool` and `FinancingContract` are durable domain models. Delivery and third-party compatibility are isolated behind explicit interfaces.

## Boundaries

Market does not spawn Unity vehicles itself, own wallets, assign jobs or manufacture unlimited free stock. Delivery belongs to a runtime adapter; ownership belongs to Fleet; debits, credits and ledger history belong to Companies. If a required external integration cannot confirm an effect, the operation is compensated or marked for reconciliation.

The source declares optional ports for Multiplayer, DVCustomLicenses and Passenger Jobs license migration. These ports are not runtime dependencies and no implementation is activated in this module. A future adapter must document and validate each external dependency before enabling projection, purchase suppression or refund handling.

## Dependencies and composition

The project references Common, Companies and Fleet. Its `Domain/` sources are currently linked into `BDVM.Full` rather than compiled into the small module DLL. This temporary composition detail will change only with the planned standalone packaging work.

## Build

With all dependencies checked out as siblings under `src/`:

```powershell
dotnet build .\BDVM.Market.csproj -c Release
```

Build `BDVM.Full` to include the domain implementation in the current game runtime.

## Testing and installation

The domain suite validates finite stock, reservation races, compensation, price guardrails, license charges/refunds and financing transitions. No standalone Unity Mod Manager package is published yet; use the matching `BDVM.Full` build for integration testing.

## Compatibility

Economic rules are configuration data and require validation before activation. Stable license and listing IDs must not be rebound silently. Optional compatibility ports fail closed and do not grant authority to clients.

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE).
