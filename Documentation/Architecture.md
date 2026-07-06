# Dealer Incentive Management Portal Architecture

## Layers

- MVC/UI: Bootstrap 5 Razor views, jQuery AJAX forms, toast feedback, responsive admin shell.
- API: JWT protected REST endpoints under `/api` for Flutter/mobile use.
- Services: business workflows for authentication, bank approval, scheme validation, import, calculation, Tally integration placeholders.
- Repositories/Data: EF Core DbContext, generic repository, SQL Server schema, soft-delete filters, audit log capture.
- Domain: branch, party, bank, schemes, sales, calculations, adjustment ledger, transfers, imports, audit logs.

## Branch Isolation

`ICurrentUser` reads `branchId` from claims. Branch users are filtered to their branch in service queries. Super Admin, HO Finance, and Auditor can see all branches.

## Approval Workflow

Bank details are never written directly from the UI. Users create `BankApprovalRequests`; HO Finance or Super Admin approves, and only then is `BankDetails` created.

## Tally Integration

`ITallyIntegrationService` is intentionally separated with ledger sync, outstanding sync, and voucher XML import methods. Replace the no-op implementation with Tally HTTP/XML client code when credentials and endpoints are available.
