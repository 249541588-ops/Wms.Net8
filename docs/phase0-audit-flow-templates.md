# Phase 0 Audit: FlowTemplate External-Call Boundary Compliance

**Audit Date**: 2026-07-10
**Auditor**: Phase 0 Refactor Implementer
**Scope**: All 9 FlowTemplate definitions in `FlowTemplateSeeder.cs`
**Goal**: Verify external-call nodes (SendWcsTask/NotifyHangKe/UploadMes/HttpCallback) and self-managed-transaction nodes (MergeUnitloads/WasteDisposalCapture) have proper `IsTransactionBoundary` before them.

## Background

Task 9 changed `FlowEngineService.ExecuteAsync` to use segmented transactions. The engine:
1. Opens an initial transaction before the node loop
2. After each `IsTransactionBoundary=true` node executes successfully: commits current segment, opens new segment
3. After the loop: commits the final segment (success) or rolls back (failure)

**Critical rule**: External-call nodes and self-managed-transaction nodes must run with all prior DB state already committed. This means the node immediately preceding them must have `IsTransactionBoundary=true`.

## Template-by-Template Audit

### Template 1: INBOUND_STANDARD_REQUEST (Request Phase, IsActive=true)

| Step | NodeType | IsTransactionBoundary | External/Self-Tx | Status |
|------|----------|-----------------------|------------------|--------|
| 1 | ValidateParams | - | No | OK |
| 2 | FindUnitload | - | No | OK |
| 3 | CheckUnitloadStatus | - | No | OK |
| 4 | MatchTag | - | No | OK |
| 5 | CheckLocationLimit | - | No | OK |
| 6 | AllocateLocation | - | No | OK |
| 7 | CreateTransTask | **true** | No | OK |
| 8 | UpdateUnitload | - | No | OK |
| 9 | UpdateLocationCount | **true** (FIXED) | No | **FIXED** |
| 10 | **SendWcsTask** | - | **External (WCS)** | OK after fix |

**Issue found**: Step 10 `SendWcsTask` (external WCS call) was preceded by step 9 without boundary. Steps 8-9 DB changes were uncommitted when WCS call executes.
**Fix applied**: Added `IsTransactionBoundary = true` to step 9 (`UpdateLocationCount`).

### Template 2: INBOUND_STANDARD_COMPLETION (Completion Phase, IsActive=true)

| Step | NodeType | IsTransactionBoundary | IsPostTransaction | External | Status |
|------|----------|-----------------------|-------------------|----------|--------|
| 1 | UpdateUnitload | **true** | No | No | OK |
| 2 | AdvanceOperation | - | No | No | OK |
| 3 | UpdateLocationCount | - | No | No | OK |
| 4 | RecordFlow | - | No | No | OK |
| 5 | ArchiveTask | - | No | No | OK |
| 6 | UploadMes | - | **Yes** | **MES** | OK |
| 7 | NotifyHangKe | - | **Yes** | **HangKe** | OK |

**No issue**: External calls (MES, HangKe) use `IsPostTransaction=true` mechanism. `ExecuteCompletionAsync` commits the entire Phase 1 transaction before running Phase 2 (PostTransaction) nodes.

### Template 3: INBOUND_DOUBLE_REQUEST (Request Phase, IsActive=true)

| Step | NodeType | IsTransactionBoundary | External/Self-Tx | Status |
|------|----------|-----------------------|------------------|--------|
| 1 | ValidateParams | - | No | OK |
| 2 | FindUnitload | - | No | OK |
| 3 | CheckUnitloadStatus | - | No | OK |
| 4 | MatchTag | - | No | OK |
| 5 | CheckLocationLimit | - | No | OK |
| 6 | AllocateLocation | - | No | OK |
| 7 | CreateTransTask | **true** | No | OK |
| 8 | UpdateUnitload | - | No | OK |
| 9 | UpdateLocationCount | **true** (FIXED) | No | **FIXED** |
| 10 | **SendWcsTask** | - | **External (WCS)** | OK after fix |

**Issue found**: Same as Template 1. `SendWcsTask` preceded by uncommitted DB changes.
**Fix applied**: Added `IsTransactionBoundary = true` to step 9 (`UpdateLocationCount`).

### Template 4: INBOUND_DOUBLE_COMPLETION (Completion Phase, IsActive=true)

| Step | NodeType | IsTransactionBoundary | IsPostTransaction | External | Status |
|------|----------|-----------------------|-------------------|----------|--------|
| 1 | UpdateUnitload | **true** | No | No | OK |
| 2 | AdvanceOperation | - | No | No | OK |
| 3 | UpdateLocationCount | - | No | No | OK |
| 4 | RecordFlow | - | No | No | OK |
| 5 | ArchiveTask | - | No | No | OK |
| 6 | UploadMes | - | **Yes** | **MES** | OK |
| 7 | NotifyHangKe | - | **Yes** | **HangKe** | OK |

**No issue**: Same structure as Template 2. PostTransaction mechanism handles external calls.

### Template 5: OUTBOUND_STANDARD_REQUEST (Request Phase, IsActive=true)

| Step | NodeType | IsTransactionBoundary | External/Self-Tx | Status |
|------|----------|-----------------------|------------------|--------|
| 1 | ValidateParams | - | No | OK |
| 2 | FindUnitload | - | No | OK |
| 3 | CheckUnitloadStatus | - | No | OK |
| 4 | CreateTransTask | **true** | No | OK |
| 5 | UpdateUnitload | - | No | OK |
| 6 | UpdateLocationCount | **true** (FIXED) | No | **FIXED** |
| 7 | **SendWcsTask** | - | **External (WCS)** | OK after fix |

**Issue found**: Step 7 `SendWcsTask` preceded by step 6 without boundary.
**Fix applied**: Added `IsTransactionBoundary = true` to step 6 (`UpdateLocationCount`).

### Template 6: OUTBOUND_STANDARD_COMPLETION (Completion Phase, IsActive=true)

| Step | NodeType | IsTransactionBoundary | IsPostTransaction | External | Status |
|------|----------|-----------------------|-------------------|----------|--------|
| 1 | RecordFlow | - | No | No | OK |
| 2 | UpdateUnitload | - | No | No | OK |
| 3 | CleanupEmptyTray | - | No | No | OK |
| 4 | UpdateLocationCount | - | No | No | OK |
| 5 | SplitUnitload | - | No | No | OK |
| 6 | ArchiveTask | - | No | No | OK |
| 7 | UploadMes | - | **Yes** | **MES** | OK |
| 8 | NotifyHangKe | - | **Yes** | **HangKe** | OK |

**No issue**: External calls use PostTransaction mechanism.

### Template 7: MOVE_STANDARD_REQUEST (Request Phase, IsActive=false)

| Step | NodeType | IsTransactionBoundary | External/Self-Tx | Status |
|------|----------|-----------------------|------------------|--------|
| 1 | ValidateParams | - | No | OK |
| 2 | FindUnitload | - | No | OK |
| 3 | CheckUnitloadStatus | - | No | OK |
| 4 | CheckLocationLimit | - | No | OK |
| 5 | AllocateLocation | - | No | OK |
| 6 | CreateTransTask | **true** | No | OK |
| 7 | UpdateUnitload | - | No | OK |
| 8 | UpdateLocationCount | **true** (FIXED) | No | **FIXED** |
| 9 | **SendWcsTask** | - | **External (WCS)** | OK after fix |

**Issue found**: Step 9 `SendWcsTask` preceded by step 8 without boundary.
**Fix applied**: Added `IsTransactionBoundary = true` to step 8 (`UpdateLocationCount`).

### Template 8: WASTE_DISPOSAL_REQUEST (Request Phase, IsActive=false)

| Step | NodeType | IsTransactionBoundary | External/Self-Tx | Status |
|------|----------|-----------------------|------------------|--------|
| 1 | WasteDisposalRequest | - | No | OK |

**No issue**: Single node, no external call, no self-managed transaction.

### Template 9: WASTE_DISPOSAL_CAPTURE_REQUEST (Request Phase, IsActive=false)

| Step | NodeType | IsTransactionBoundary | External/Self-Tx | Status |
|------|----------|-----------------------|------------------|--------|
| 1 | WasteDisposalCapture | - | **Self-managed tx (REMOVED)** | **FIXED** |

**Issue found**: `WasteDisposalCaptureNode` (line 126) opened its own transaction via `context.Db.Database.BeginTransactionAsync()`. When running inside FlowEngine's `ExecuteAsync` (which always opens an outer transaction), this causes an EF Core nested transaction exception (`InvalidOperationException`).

**Fix applied**: Removed the self-managed transaction from `WasteDisposalCaptureNode.cs`. The node now delegates transaction management to FlowEngine's segmented transaction. The node's `SaveChangesAsync` will be committed by FlowEngine at the final segment commit.

**Concern**: The node also makes a HangKe `CancelTrayAsync` call (line 177) after the DB work. With the self-managed transaction removed, this HangKe call now executes inside the FlowEngine's open transaction (before final commit). If the HangKe call succeeds but the FlowEngine's subsequent commit fails, we get an inconsistency (HangKe tray cancelled but DB not committed). However:
- This template is `IsActive=false` (disabled by default)
- The HangKe call is wrapped in try/catch with warning log (non-blocking)
- The production code path uses `WasteDisposalCaptureRequestHandler` (hardcoded handler), not this template
- The proper long-term fix is to split into two nodes (DB cleanup + PostTransaction HangKe notification)

## Self-Managed Transaction Node Audit

### MergeUnitloadsHandler.cs

**Original**: Line 53 -- `await using var tx = await context.Db.Database.BeginTransactionAsync()`

**Status**: **FIXED** -- Removed self-managed transaction. Node now relies on FlowEngine's segmented transaction.

**Rationale**: `MergeUnitloadsHandler` is registered as `INodeHandler` and would be used in future templates via FlowEngine. Running inside FlowEngine's open transaction while trying to start its own would throw `InvalidOperationException`.

**Template usage**: Not currently used in any active template (no template references `MergeUnitloads` NodeType). The fix is preventive for future template additions.

### WasteDisposalCaptureNode.cs

**Original**: Line 126 -- `await using var tx = await context.Db.Database.BeginTransactionAsync()`

**Status**: **FIXED** -- Removed self-managed transaction. Node now relies on FlowEngine's segmented transaction.

**Template usage**: Used in Template 9 (`WASTE_DISPOSAL_CAPTURE_REQUEST`, IsActive=false).

## Seeder Modification Summary

File: `src/Wms.Core.WebApi/Services/FlowTemplateSeeder.cs`

| Template | Node Modified | Change |
|----------|--------------|--------|
| INBOUND_STANDARD_REQUEST | UpdateLocationCount (step 9) | Added `IsTransactionBoundary = true` |
| INBOUND_DOUBLE_REQUEST | UpdateLocationCount (step 9) | Added `IsTransactionBoundary = true` |
| OUTBOUND_STANDARD_REQUEST | UpdateLocationCount (step 6) | Added `IsTransactionBoundary = true` |
| MOVE_STANDARD_REQUEST | UpdateLocationCount (step 8) | Added `IsTransactionBoundary = true` |

## Node Handler Modification Summary

| File | Change |
|------|--------|
| `MergeUnitloadsHandler.cs` | Removed self-managed `BeginTransactionAsync` + `CommitAsync`/`RollbackAsync`. Node delegates to FlowEngine transaction. |
| `WasteDisposalCaptureNode.cs` | Removed self-managed `BeginTransactionAsync` + `CommitAsync`. Node delegates to FlowEngine transaction. |

## Build Verification

```
0 errors, 964 warnings (pre-existing)
Build time: 00:00:09.26
```

## Audit Conclusions

1. **4 request-phase templates** had missing `IsTransactionBoundary` before `SendWcsTask`. Fixed.
2. **2 self-managed transaction node handlers** would conflict with FlowEngine's segmented transaction. Fixed by removing self-managed transactions.
3. **Completion-phase templates** (3 total) are safe -- they use `IsPostTransaction` mechanism which correctly executes external calls after the main transaction commits.
4. **WasteDisposalCapture template** (IsActive=false) has a residual concern: HangKe `CancelTrayAsync` call inside the node executes before FlowEngine's final commit. This is acceptable for now given the template is disabled and production uses the hardcoded handler.

## Concerns for Production Deployment

1. **Seeder incremental sync**: The seeder's incremental sync logic preserves user-modified `IsTransactionBoundary` values. For already-deployed environments, the new `IsTransactionBoundary=true` values will only apply to newly inserted nodes. Existing deployed nodes need manual review to ensure boundary flags are set correctly.

2. **WasteDisposalCapture HangKe timing**: If Template 9 is ever activated, the HangKe `CancelTrayAsync` call within the node will execute before the FlowEngine's final transaction commit. The proper fix is to split the node into two: (a) DB cleanup node + (b) PostTransaction HangKe notification node.
