using System.Security.Claims;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class Uc18RefundConfirmationTests
{
    [Fact]
    public void Endpoint_IsManagerOnly()
    {
        var authorization = Assert.Single(typeof(ManagerRefundConfirmationController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
        Assert.Equal(RoleNames.Manager, authorization.Roles);
    }

    [Fact]
    public async Task ExactReplayReturnsReceiptAndDifferentPayloadIsRejected()
    {
        await using var db = CreateDb();
        Seed(db);
        var controller = CreateController(db, 7);
        var request = Request("receipt-1", 50m, 20m, 5m);

        var first = await controller.Confirm(1, request, CancellationToken.None);
        var replay = await controller.Confirm(1, request, CancellationToken.None);
        var normalizedScaleReplay = await controller.Confirm(1, request with { PrincipalPaid = 50.00m }, CancellationToken.None);
        var changed = await controller.Confirm(1, request with { PrincipalPaid = 51m }, CancellationToken.None);
        var differentActor = await CreateController(db, 8).Confirm(1, request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(first);
        var replayBody = Assert.IsType<OkObjectResult>(replay).Value!;
        Assert.True((bool)replayBody.GetType().GetProperty("Replayed")!.GetValue(replayBody)!);
        Assert.True((bool)Assert.IsType<OkObjectResult>(normalizedScaleReplay).Value!.GetType()
            .GetProperty("Replayed")!.GetValue(Assert.IsType<OkObjectResult>(normalizedScaleReplay).Value)!);
        Assert.IsType<ConflictObjectResult>(changed);
        Assert.IsType<ConflictObjectResult>(differentActor);
        Assert.Single(await db.Set<Uc18RefundConfirmation>().ToListAsync());
        Assert.Single(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task RefundCapsAreEnforcedAndPaymentReceiptsRemainUnchanged()
    {
        await using var db = CreateDb();
        Seed(db);
        var paymentBefore = await db.Payments.AsNoTracking().SingleAsync(x => x.PaymentId == 1);

        var result = await CreateController(db, 7).Confirm(1, Request("receipt-2", 101m, 60m, 0m), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Empty(await db.Set<Uc18RefundConfirmation>().ToListAsync());
        var paymentAfter = await db.Payments.AsNoTracking().SingleAsync(x => x.PaymentId == 1);
        Assert.Equal(paymentBefore.PaymentId, paymentAfter.PaymentId);
        Assert.Equal(paymentBefore.Amount, paymentAfter.Amount);
        Assert.Equal(paymentBefore.ContractId, paymentAfter.ContractId);
    }

    [Fact]
    public async Task EvidenceReferenceCannotBeReusedWithNewRequestId()
    {
        await using var db = CreateDb();
        Seed(db);
        var controller = CreateController(db, 7);

        var first = await controller.Confirm(1, Request("receipt-shared", 10m, 5m, 0m), CancellationToken.None);
        var second = await controller.Confirm(1, Request("receipt-shared", 10m, 5m, 0m), CancellationToken.None);

        Assert.IsType<OkObjectResult>(first);
        Assert.IsType<ConflictObjectResult>(second);
        Assert.Single(await db.Set<Uc18RefundConfirmation>().ToListAsync());
    }

    [Fact]
    public async Task ContractWideCapsPreventTwoOperationsRefundingTheSameReceiptsTwice()
    {
        await using var db = CreateDb();
        Seed(db);
        db.Contracts.Single().TotalValue = 200m;
        db.Payments.Single(x => x.PaymentId == 1).Amount = 100m;
        db.Payments.Single(x => x.PaymentId == 2).Amount = 100m;
        db.Set<Uc18BookingOperation>().Add(new Uc18BookingOperation
        {
            RequestId = Guid.Parse("d487a265-4f9a-49f1-a5c6-9ae1c5e0a836"), BookingId = 1, ContractId = 1,
            ActorUserId = 7, Kind = Uc18BookingOperation.PriorRefund, PayloadHash = new string('B', 64),
            PrincipalRefundDue = 300m, DepositPrincipalPortion = 100m, CompensationDue = 0m
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, 7);
        Assert.IsType<OkObjectResult>(await controller.Confirm(1, Request("allocation-1", 100m, 60m, 0m), CancellationToken.None));
        var overContractLimit = await controller.Confirm(1, RequestFor("allocation-2", 61m, 0m, 0m), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(overContractLimit);
        Assert.Single(await db.Set<Uc18RefundConfirmation>().ToListAsync());
    }

    [Fact]
    public async Task FractionalVndIsRejectedBeforeAnExistingRequestCanReplay()
    {
        await using var db = CreateDb();
        Seed(db);
        var controller = CreateController(db, 7);
        var request = Request("receipt-fractional", 10m, 5m, 0m);
        Assert.IsType<OkObjectResult>(await controller.Confirm(1, request, CancellationToken.None));

        var invalidReplay = await controller.Confirm(1, request with { PrincipalPaid = 10.001m }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(invalidReplay);
        Assert.Single(await db.Set<Uc18RefundConfirmation>().ToListAsync());
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static void Seed(ApplicationDbContext db)
    {
        db.Statuses.Add(new Status { StatusId = 2, StatusGroup = StatusGroups.Payment, StatusCode = PaymentStatusCodes.Success, StatusName = "Success" });
        db.Contracts.Add(new Contract { ContractId = 1, BookingId = 1, ContractCode = "C1", TotalValue = 100m });
        db.Set<Uc18BookingOperation>().Add(new Uc18BookingOperation
        {
            RequestId = Guid.Parse("1ec24fc7-0406-4b74-a73a-12076ca73ab9"), BookingId = 1, ContractId = 1,
            ActorUserId = 7, Kind = Uc18BookingOperation.Cancel, PayloadHash = new string('A', 64),
            PrincipalRefundDue = 100m, DepositPrincipalPortion = 60m, CompensationDue = 10m
        });
        db.Payments.AddRange(
            new Payment { PaymentId = 1, ContractId = 1, Amount = 60m, PaymentType = "Đặt cọc", PaymentDate = DateTime.UtcNow, StatusId = 2 },
            new Payment { PaymentId = 2, ContractId = 1, Amount = 40m, PaymentType = "Thanh toán đợt", PaymentDate = DateTime.UtcNow, StatusId = 2 });
        db.SaveChanges();
    }

    private static ManagerRefundConfirmationController CreateController(ApplicationDbContext db, int actorId)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, actorId.ToString()), new Claim(ClaimTypes.Role, RoleNames.Manager)
        ], "Test"));
        return new ManagerRefundConfirmationController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
        };
    }

    private static ManagerRefundConfirmationController.RefundConfirmationRequest Request(string reference,
        decimal principal, decimal deposit, decimal compensation) => new(Guid.NewGuid(),
            Guid.Parse("1ec24fc7-0406-4b74-a73a-12076ca73ab9"), reference, principal, deposit, compensation);

    private static ManagerRefundConfirmationController.RefundConfirmationRequest RequestFor(string reference,
        decimal principal, decimal deposit, decimal compensation) => new(Guid.NewGuid(),
            Guid.Parse("d487a265-4f9a-49f1-a5c6-9ae1c5e0a836"), reference, principal, deposit, compensation);
}
