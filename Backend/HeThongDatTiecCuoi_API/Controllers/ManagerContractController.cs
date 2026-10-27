using System.Data;
using System.Security.Claims;
using System.Text.Json;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Contract;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/manager/contracts")]
[Authorize(Roles = RoleNames.Manager)]
public sealed class ManagerContractController : ControllerBase
{
    private const int PageSize = 10;
    private static readonly string[] ContractActions =
    [
        AuditActions.ContractCreated,
        AuditActions.ContractUpdated,
        AuditActions.ContractActivated,
        AuditActions.ContractCompleted,
        AuditActions.ContractCancelled,
        AuditActions.DepositCommitmentConfirmed
    ];

    private readonly ApplicationDbContext _context;
    private readonly IStatusService _statusService;
    private readonly IContractTemplateProvider _templates;
    private readonly IContractCodeGenerator _codeGenerator;
    private readonly IContractPdfGenerator _pdfGenerator;
    private readonly IEmailService? _emailService;
    private readonly ILogger<ManagerContractController>? _logger;

    public ManagerContractController(
        ApplicationDbContext context,
        IStatusService statusService,
        IContractTemplateProvider templates,
        IContractCodeGenerator codeGenerator,
        IContractPdfGenerator pdfGenerator,
        IEmailService? emailService = null,
        ILogger<ManagerContractController>? logger = null)
    {
        _context = context;
        _statusService = statusService;
        _templates = templates;
        _codeGenerator = codeGenerator;
        _pdfGenerator = pdfGenerator;
        _emailService = emailService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ManagerContractListResponseDto>> GetContracts(
        [FromQuery] ManagerContractFilterDto filter,
        CancellationToken cancellationToken)
    {
        var query = _context.Contracts.AsNoTracking().AsQueryable();
        var keyword = Normalize(filter.Keyword);
        if (keyword is not null)
        {
            query = query.Where(contract =>
                contract.ContractCode.Contains(keyword) ||
                contract.Booking.BookingCode.Contains(keyword) ||
                contract.Booking.Customer.FullName.Contains(keyword) ||
                contract.Booking.Customer.PhoneNumber.Contains(keyword));
        }

        var status = NormalizeCode(filter.Status);
        if (status is not null && status != "ALL")
        {
            if (!IsContractStatus(status))
                return BadRequest(new { message = "Trạng thái hợp đồng không hợp lệ." });
            query = query.Where(contract => contract.Status.StatusCode == status);
        }

        if (filter.FromDate.HasValue)
            query = query.Where(contract => contract.CreatedDate >= filter.FromDate.Value.Date);
        if (filter.ToDate.HasValue)
            query = query.Where(contract => contract.CreatedDate < filter.ToDate.Value.Date.AddDays(1));

        var totalItems = await query.CountAsync(cancellationToken);
        var page = Math.Max(1, filter.Page);
        var items = await query
            .OrderByDescending(contract => contract.CreatedDate)
            .ThenByDescending(contract => contract.ContractId)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(contract => new ManagerContractListItemDto
            {
                ContractId = contract.ContractId,
                ContractCode = contract.ContractCode,
                BookingId = contract.BookingId,
                BookingCode = contract.Booking.BookingCode,
                CustomerName = contract.Booking.Customer.FullName,
                CustomerPhone = contract.Booking.Customer.PhoneNumber,
                CreatedDate = contract.CreatedDate,
                TotalValue = contract.TotalValue,
                StatusCode = contract.Status.StatusCode,
                StatusName = contract.Status.StatusName,
                PaidAmount = contract.Payments
                    .Where(payment => payment.Status.StatusCode == PaymentStatusCodes.Success)
                    .Sum(payment => (decimal?)payment.Amount) ?? 0m
            })
            .ToListAsync(cancellationToken);

        var statistics = new ManagerContractStatisticsDto
        {
            TotalContracts = await _context.Contracts.AsNoTracking().CountAsync(cancellationToken),
            ActiveContracts = await _context.Contracts.AsNoTracking()
                .CountAsync(contract => contract.Status.StatusCode == ContractStatusCodes.Active, cancellationToken),
            AwaitingPaymentContracts = await _context.Contracts.AsNoTracking()
                .CountAsync(contract => contract.Status.StatusCode == ContractStatusCodes.Active &&
                    contract.TotalValue > (contract.Payments
                        .Where(payment => payment.Status.StatusCode == PaymentStatusCodes.Success)
                        .Sum(payment => (decimal?)payment.Amount) ?? 0m), cancellationToken),
            TotalCollectedRevenue = await _context.Payments.AsNoTracking()
                .Where(payment => payment.Status.StatusCode == PaymentStatusCodes.Success)
                .SumAsync(payment => (decimal?)payment.Amount, cancellationToken) ?? 0m
        };

        return Ok(new ManagerContractListResponseDto
        {
            Items = items,
            Statistics = statistics,
            Page = page,
            PageSize = PageSize,
            TotalItems = totalItems
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ManagerContractDetailDto>> GetContract(
        int id,
        CancellationToken cancellationToken)
    {
        var detail = await BuildDetailQuery(id).SingleOrDefaultAsync(cancellationToken);
        if (detail is null) return NotFound(new { message = "Không tìm thấy hợp đồng." });

        detail.History = await _context.AuditLogs.AsNoTracking()
            .Where(log => log.EntityName == AuditEntityNames.Contract &&
                log.EntityId == id && ContractActions.Contains(log.Action))
            .OrderByDescending(log => log.Timestamp)
            .Select(log => new ContractAuditEventDto
            {
                Action = log.Action,
                Timestamp = log.Timestamp,
                Notes = log.Notes
            })
            .ToListAsync(cancellationToken);
        return Ok(detail);
    }

    [HttpGet("eligible-bookings")]
    public async Task<ActionResult<List<EligibleBookingDto>>> GetEligibleBookings(
        [FromQuery] string? keyword,
        CancellationToken cancellationToken)
    {
        var query = _context.WeddingBookings.AsNoTracking()
            .Where(booking => booking.Status.StatusCode == BookingStatusCodes.Confirmed && booking.Contract == null);
        var normalizedKeyword = Normalize(keyword);
        if (normalizedKeyword is not null)
        {
            query = query.Where(booking => booking.BookingCode.Contains(normalizedKeyword) ||
                booking.Customer.FullName.Contains(normalizedKeyword) ||
                booking.Customer.PhoneNumber.Contains(normalizedKeyword));
        }

        var bookings = await query.OrderBy(booking => booking.HallSchedule.Date)
            .Take(50)
            .Select(booking => new EligibleBookingDto
            {
                BookingId = booking.BookingId,
                BookingCode = booking.BookingCode,
                CustomerName = booking.Customer.FullName,
                CustomerPhone = booking.Customer.PhoneNumber,
                CustomerEmail = booking.Customer.Email,
                EventDate = booking.HallSchedule.Date,
                Shift = booking.HallSchedule.Shift,
                HallName = booking.HallSchedule.Hall.HallName,
                GuestCount = booking.GuestCount,
                MenuName = booking.Menu != null ? booking.Menu.MenuName : null,
                DecorationPackageName = booking.DecorationPackage != null ? booking.DecorationPackage.TenGoi : null,
                Services = booking.BookingServices
                    .Where(item => item.DataStatus.DataStatusCode == DataStatusCodes.Existing)
                    .OrderBy(item => item.BookingServiceId)
                    .Select(item => item.ServiceItem.TenDichVu).ToList(),
                HallPrice = booking.FinalHallPrice,
                MenuPricePerTable = booking.FinalMenuPrice,
                DecorationPrice = booking.FinalDecorationPrice,
                ServiceLines = booking.BookingServices
                    .Where(item => item.DataStatus.DataStatusCode == DataStatusCodes.Existing)
                    .OrderBy(item => item.BookingServiceId)
                    .Select(item => new ContractServiceLineDto
                    {
                        Name = item.ServiceItem.TenDichVu,
                        Quantity = item.Quantity,
                        UnitPrice = item.FinalUnitPrice
                    }).ToList(),
                EstimatedTotal = booking.EstimatedTotal ?? 0m
            })
            .ToListAsync(cancellationToken);

        foreach (var booking in bookings)
        {
            var templateData = ToTemplateData(booking, booking.EstimatedTotal);
            booking.DefaultContractContent = _templates.BuildContractContent(templateData);
            booking.DefaultPaymentTerms = _templates.BuildPaymentTerms();
        }
        return Ok(bookings);
    }

    [HttpPost]
    public async Task<ActionResult<ContractActionResponseDto>> CreateContract(
        [FromBody] CreateContractRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var booking = await _context.WeddingBookings
            .Include(item => item.Status)
            .Include(item => item.Contract)
            .Include(item => item.Customer)
            .Include(item => item.HallSchedule).ThenInclude(item => item.Hall)
            .Include(item => item.Menu)
            .Include(item => item.DecorationPackage)
            .Include(item => item.BookingServices).ThenInclude(item => item.ServiceItem)
            .Include(item => item.BookingServices).ThenInclude(item => item.DataStatus)
            .SingleOrDefaultAsync(item => item.BookingId == request.BookingId, cancellationToken);
        if (booking is null) return NotFound(new { message = "Không tìm thấy booking." });
        if (!ContractBusinessRules.IsEligibleBooking(booking.Status.StatusCode, booking.Contract is not null))
        {
            if (booking.Contract is not null)
                return Conflict(new { message = "Booking này đã có hợp đồng." });
            return Conflict(new { message = "Chỉ booking đã xác nhận mới được tạo hợp đồng." });
        }

        var totalValue = request.TotalValue ?? booking.EstimatedTotal ?? 0m;
        if (totalValue < 0) return BadRequest(new { message = "Tổng giá trị hợp đồng không được âm." });
        if (request.RequiredDepositAmount.HasValue && !PaymentBusinessRules.IsValidCommitment(request.RequiredDepositAmount, totalValue))
            return BadRequest(new { message = "Mức cọc phải dương, tối đa hai số lẻ và không vượt tổng hợp đồng." });
        var defaultContent = _templates.BuildContractContent(ToTemplateData(booking, booking.EstimatedTotal ?? 0m));
        var requestedContent = Normalize(request.ContractContent);
        var contractContent = requestedContent is null ||
            NormalizeComparable(requestedContent) == NormalizeComparable(defaultContent)
            ? _templates.BuildContractContent(ToTemplateData(booking, totalValue))
            : requestedContent;
        var contract = new Contract
        {
            BookingId = booking.BookingId,
            ContractCode = await _codeGenerator.GenerateAsync(cancellationToken),
            CreatedDate = DateTime.Now.Date,
            TotalValue = totalValue,
            RequiredDepositAmount = request.RequiredDepositAmount,
            ContractContent = contractContent,
            PaymentTerms = Normalize(request.PaymentTerms) ?? _templates.BuildPaymentTerms(),
            StatusId = await _statusService.GetStatusIdAsync(StatusGroups.Contract, ContractStatusCodes.Draft, cancellationToken)
        };
        _context.Contracts.Add(contract);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            AddAudit(actorId, AuditActions.ContractCreated, contract, null,
                new { contract.ContractCode, contract.BookingId, contract.TotalValue, contract.RequiredDepositAmount, Status = ContractStatusCodes.Draft },
                $"Tạo hợp đồng nháp {contract.ContractCode} cho booking {booking.BookingCode}.");
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict(new { message = "Booking đã có hợp đồng hoặc mã hợp đồng vừa được sử dụng. Vui lòng thử lại." });
        }

        return Ok(new ContractActionResponseDto
        {
            ContractId = contract.ContractId,
            ContractCode = contract.ContractCode,
            StatusCode = ContractStatusCodes.Draft,
            Message = "Đã lưu bản nháp hợp đồng."
        });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ContractActionResponseDto>> UpdateDraft(
        int id,
        [FromBody] UpdateDraftContractRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        if (request.TotalValue < 0) return BadRequest(new { message = "Tổng giá trị hợp đồng không được âm." });

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var contract = await _context.Contracts.FromSqlInterpolated($"SELECT * FROM [HopDong] WITH (UPDLOCK, HOLDLOCK) WHERE [HopDongID] = {id}").Include(item => item.Status)
            .Include(item => item.Booking).ThenInclude(item => item.Customer)
            .Include(item => item.Booking).ThenInclude(item => item.HallSchedule).ThenInclude(item => item.Hall)
            .Include(item => item.Booking).ThenInclude(item => item.Menu)
            .Include(item => item.Booking).ThenInclude(item => item.DecorationPackage)
            .Include(item => item.Booking).ThenInclude(item => item.BookingServices).ThenInclude(item => item.ServiceItem)
            .Include(item => item.Booking).ThenInclude(item => item.BookingServices).ThenInclude(item => item.DataStatus)
            .SingleOrDefaultAsync(item => item.ContractId == id, cancellationToken);
        if (contract is null) return NotFound(new { message = "Không tìm thấy hợp đồng." });
        if (contract.Status.StatusCode != ContractStatusCodes.Draft)
            return Conflict(new { message = "Chỉ hợp đồng bản nháp mới được chỉnh sửa." });
        if (request.RequiredDepositAmount.HasValue && !PaymentBusinessRules.IsValidCommitment(request.RequiredDepositAmount, request.TotalValue))
            return BadRequest(new { message = "Mức cọc không hợp lệ hoặc vượt tổng hợp đồng." });
        if (await _context.Payments.AnyAsync(p => p.ContractId == id, cancellationToken) &&
            (request.TotalValue != contract.TotalValue ||
                (request.RequiredDepositAmount ?? contract.RequiredDepositAmount) != contract.RequiredDepositAmount))
            return Conflict(new { message = "Hợp đồng đã phát sinh giao dịch, không được sửa cam kết hoặc tổng giá trị qua bản nháp." });

        var oldData = new
        {
            contract.TotalValue,
            contract.RequiredDepositAmount,
            ContractContentLength = contract.ContractContent?.Length ?? 0,
            PaymentTermsLength = contract.PaymentTerms?.Length ?? 0
        };
        var previousContent = contract.ContractContent;
        var previousTotalValue = contract.TotalValue;
        contract.TotalValue = request.TotalValue;
        // Older clients omitting the additive field must not erase a verified commitment.
        contract.RequiredDepositAmount = request.RequiredDepositAmount ?? contract.RequiredDepositAmount;
        if (contract.RequiredDepositAmount > contract.TotalValue)
            return BadRequest(new { message = "Tổng hợp đồng không được nhỏ hơn mức cọc cam kết." });
        var providedContent = Normalize(request.ContractContent);
        // BR04: nội dung do Manager tự soạn luôn được giữ; chỉ sinh lại bảng giá khi nội dung
        // đang lưu vẫn là template của tổng cũ và tổng giá trị vừa được sửa hợp lệ.
        string? contractContent;
        if (providedContent is null)
            contractContent = previousContent;
        else if (request.TotalValue != previousTotalValue && NormalizeComparable(providedContent) ==
            NormalizeComparable(_templates.BuildContractContent(ToTemplateData(contract.Booking, previousTotalValue))))
            contractContent = _templates.BuildContractContent(ToTemplateData(contract.Booking, request.TotalValue));
        else
            contractContent = providedContent;
        contract.ContractContent = contractContent;
        contract.PaymentTerms = Normalize(request.PaymentTerms);
        AddAudit(actorId, AuditActions.ContractUpdated, contract, oldData,
            new
            {
                contract.TotalValue,
                contract.RequiredDepositAmount,
                ContractContentLength = contract.ContractContent?.Length ?? 0,
                PaymentTermsLength = contract.PaymentTerms?.Length ?? 0
            },
            $"Cập nhật bản nháp {contract.ContractCode}.");
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ActionResult(contract, ContractStatusCodes.Draft, "Đã cập nhật bản nháp hợp đồng.");
    }

    [HttpPost("{id:int}/deposit-commitment")]
    public async Task<ActionResult<ContractActionResponseDto>> ConfirmDeposit(int id,
        [FromBody] ConfirmDepositRequestDto request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var contract = await _context.Contracts.FromSqlInterpolated(
                $"SELECT * FROM [HopDong] WITH (UPDLOCK, HOLDLOCK) WHERE [HopDongID] = {id}")
            .Include(c => c.Status).SingleOrDefaultAsync(cancellationToken);
        if (contract is null) return NotFound(new { message = "Không tìm thấy hợp đồng." });
        if (contract.RequiredDepositAmount.HasValue || contract.Status.StatusCode is not (ContractStatusCodes.Draft or ContractStatusCodes.Active))
            return Conflict(new { message = "Chỉ xác minh một lần cho hợp đồng nháp/hiệu lực chưa có mức cọc." });
        if (contract.Status.StatusGroup != StatusGroups.Contract)
            return Conflict(new { message = "Trạng thái hợp đồng sai nhóm nghiệp vụ." });
        if (!PaymentBusinessRules.IsValidCommitment(request.RequiredDepositAmount, contract.TotalValue))
            return BadRequest(new { message = "Mức cọc phải dương, tối đa hai số lẻ và không vượt tổng hợp đồng." });
        contract.RequiredDepositAmount = request.RequiredDepositAmount;
        AddAudit(actorId, AuditActions.DepositCommitmentConfirmed, contract,
            new { RequiredDepositAmount = (decimal?)null }, new { contract.RequiredDepositAmount },
            "Manager xác minh mức cọc theo hợp đồng hiện hữu; giữ nguyên trạng thái và lịch sử.");
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ActionResult(contract, contract.Status.StatusCode, "Đã xác minh mức cọc cam kết.");
    }

    [HttpPost("{id:int}/activate")]
    public async Task<ActionResult<ContractActionResponseDto>> Activate(int id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var contract = await _context.Contracts.FromSqlInterpolated($"SELECT * FROM [HopDong] WITH (UPDLOCK, HOLDLOCK) WHERE [HopDongID] = {id}").Include(item => item.Status)
            .Include(item => item.Booking).ThenInclude(item => item.Status)
            .SingleOrDefaultAsync(item => item.ContractId == id, cancellationToken);
        if (contract is null) return NotFound(new { message = "Không tìm thấy hợp đồng." });
        var activationError = ContractBusinessRules.ValidateActivation(
            contract.Status.StatusCode, contract.TotalValue, contract.ContractContent,
            contract.PaymentTerms, contract.Booking.Status.StatusCode, contract.RequiredDepositAmount,
            await _context.Payments.Where(p => p.ContractId == id && p.PaymentType == PaymentTypes.Deposit &&
                p.Status.StatusGroup == StatusGroups.Payment && p.Status.StatusCode == PaymentStatusCodes.Success)
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m);
        if (activationError is not null) return Conflict(new { message = activationError });
        var paid = await _context.Payments.Where(p => p.ContractId == id && p.Status.StatusGroup == StatusGroups.Payment &&
            p.Status.StatusCode == PaymentStatusCodes.Success).SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        if (paid > contract.TotalValue) return Conflict(new { message = "Dữ liệu thanh toán vượt tổng hợp đồng. Cần đối soát trước khi kích hoạt." });

        contract.StatusId = await _statusService.GetStatusIdAsync(StatusGroups.Contract, ContractStatusCodes.Active, cancellationToken);
        AddAudit(actorId, AuditActions.ContractActivated, contract,
            new { Status = ContractStatusCodes.Draft }, new { Status = ContractStatusCodes.Active },
            $"Kích hoạt hợp đồng {contract.ContractCode}.");
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await TryNotifyContractStatusAsync(contract.ContractId);
        return ActionResult(contract, ContractStatusCodes.Active, "Đã kích hoạt hợp đồng.");
    }

    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<ContractActionResponseDto>> Complete(int id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var contract = await _context.Contracts.FromSqlInterpolated($"SELECT * FROM [HopDong] WITH (UPDLOCK, HOLDLOCK) WHERE [HopDongID] = {id}").Include(item => item.Status)
            .Include(item => item.Payments).ThenInclude(item => item.Status)
            .Include(item => item.Booking).ThenInclude(item => item.Status)
            .Include(item => item.Booking).ThenInclude(item => item.HallSchedule)
            .SingleOrDefaultAsync(item => item.ContractId == id, cancellationToken);
        if (contract is null) return NotFound(new { message = "Không tìm thấy hợp đồng." });
        var paid = contract.Payments.Where(payment => payment.Status.StatusCode == PaymentStatusCodes.Success)
            .Sum(payment => payment.Amount);
        var completionError = ContractBusinessRules.ValidateCompletion(
            contract.Status.StatusCode, contract.TotalValue, paid,
            contract.Booking.Status.StatusCode, contract.Booking.HallSchedule.Date, DateTime.Now.Date);
        if (completionError is not null) return Conflict(new { message = completionError });

        contract.StatusId = await _statusService.GetStatusIdAsync(StatusGroups.Contract, ContractStatusCodes.Completed, cancellationToken);
        AddAudit(actorId, AuditActions.ContractCompleted, contract,
            new { Status = ContractStatusCodes.Active }, new { Status = ContractStatusCodes.Completed, PaidAmount = paid },
            $"Hoàn tất hợp đồng {contract.ContractCode} sau khi đã thanh toán đủ.");
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await TryNotifyContractStatusAsync(contract.ContractId);
        return ActionResult(contract, ContractStatusCodes.Completed, "Đã hoàn tất hợp đồng.");
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<ContractActionResponseDto>> Cancel(int id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var contract = await _context.Contracts.FromSqlInterpolated($"SELECT * FROM [HopDong] WITH (UPDLOCK, HOLDLOCK) WHERE [HopDongID] = {id}").Include(item => item.Status)
            .SingleOrDefaultAsync(item => item.ContractId == id, cancellationToken);
        if (contract is null) return NotFound(new { message = "Không tìm thấy hợp đồng." });
        var oldStatus = contract.Status.StatusCode;
        if (!ContractBusinessRules.CanCancel(oldStatus))
            return Conflict(new { message = "Chỉ hợp đồng bản nháp hoặc đang hiệu lực mới được hủy." });

        contract.StatusId = await _statusService.GetStatusIdAsync(StatusGroups.Contract, ContractStatusCodes.Cancelled, cancellationToken);
        AddAudit(actorId, AuditActions.ContractCancelled, contract,
            new { Status = oldStatus }, new { Status = ContractStatusCodes.Cancelled },
            $"Hủy hợp đồng {contract.ContractCode}. Booking và lịch sử thanh toán được giữ nguyên.");
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await TryNotifyContractStatusAsync(contract.ContractId);
        return ActionResult(contract, ContractStatusCodes.Cancelled, "Đã hủy hợp đồng. Booking và thanh toán không bị thay đổi.");
    }

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> DownloadPdf(int id, CancellationToken cancellationToken)
    {
        var detail = await BuildDetailQuery(id).SingleOrDefaultAsync(cancellationToken);
        if (detail is null) return NotFound(new { message = "Không tìm thấy hợp đồng." });
        var bytes = _pdfGenerator.Generate(detail);
        return File(bytes, "application/pdf", $"HopDong_{detail.ContractCode}.pdf");
    }

    private IQueryable<ManagerContractDetailDto> BuildDetailQuery(int id) =>
        _context.Contracts.AsNoTracking().Where(contract => contract.ContractId == id)
            .Select(contract => new ManagerContractDetailDto
            {
                ContractId = contract.ContractId,
                ContractCode = contract.ContractCode,
                CreatedDate = contract.CreatedDate,
                TotalValue = contract.TotalValue,
                RequiredDepositAmount = contract.RequiredDepositAmount,
                DepositPaidAmount = contract.Payments.Where(p => p.PaymentType == PaymentTypes.Deposit &&
                    p.Status.StatusGroup == StatusGroups.Payment && p.Status.StatusCode == PaymentStatusCodes.Success)
                    .Sum(p => (decimal?)p.Amount) ?? 0m,
                ContractContent = contract.ContractContent ?? string.Empty,
                PaymentTerms = contract.PaymentTerms ?? string.Empty,
                StatusCode = contract.Status.StatusCode,
                StatusName = contract.Status.StatusName,
                PaidAmount = contract.Payments.Where(payment => payment.Status.StatusCode == PaymentStatusCodes.Success)
                    .Sum(payment => (decimal?)payment.Amount) ?? 0m,
                Booking = new ManagerContractBookingDto
                {
                    BookingId = contract.BookingId,
                    BookingCode = contract.Booking.BookingCode,
                    CustomerName = contract.Booking.Customer.FullName,
                    CustomerPhone = contract.Booking.Customer.PhoneNumber,
                    CustomerEmail = contract.Booking.Customer.Email,
                    EventDate = contract.Booking.HallSchedule.Date,
                    Shift = contract.Booking.HallSchedule.Shift,
                    HallName = contract.Booking.HallSchedule.Hall.HallName,
                    GuestCount = contract.Booking.GuestCount,
                    MenuName = contract.Booking.Menu != null ? contract.Booking.Menu.MenuName : null,
                    DecorationPackageName = contract.Booking.DecorationPackage != null ? contract.Booking.DecorationPackage.TenGoi : null,
                    Services = contract.Booking.BookingServices
                        .Where(item => item.DataStatus.DataStatusCode == DataStatusCodes.Existing)
                        .OrderBy(item => item.BookingServiceId)
                        .Select(item => item.ServiceItem.TenDichVu).ToList(),
                    EstimatedTotal = contract.Booking.EstimatedTotal ?? 0m,
                    StatusCode = contract.Booking.Status.StatusCode,
                    StatusName = contract.Booking.Status.StatusName
                },
                Payments = contract.Payments.OrderByDescending(payment => payment.PaymentDate)
                    .Select(payment => new ManagerPaymentListItemDto
                    {
                        PaymentId = payment.PaymentId,
                        TransactionCode = payment.TransactionCode,
                        ContractId = contract.ContractId,
                        ContractCode = contract.ContractCode,
                        BookingCode = contract.Booking.BookingCode,
                        CustomerName = contract.Booking.Customer.FullName,
                        CustomerPhone = contract.Booking.Customer.PhoneNumber,
                        PaymentType = payment.PaymentType,
                        Amount = payment.Amount,
                        PaymentDate = payment.PaymentDate,
                        PaymentMethod = payment.PaymentMethod,
                        StatusCode = payment.Status.StatusCode,
                        StatusName = payment.Status.StatusName
                    }).ToList()
            });

    private static ContractTemplateData ToTemplateData(EligibleBookingDto booking, decimal contractTotalValue) => new(
        booking.BookingCode, booking.CustomerName, booking.EventDate, booking.Shift,
        booking.HallName, booking.GuestCount, booking.MenuName,
        booking.DecorationPackageName, booking.Services,
        booking.HallPrice, booking.MenuPricePerTable, booking.DecorationPrice,
        booking.ServiceLines
            .Select(line => new ContractTemplateServiceLine(line.Name, line.Quantity, line.UnitPrice)).ToList(),
        booking.EstimatedTotal, contractTotalValue);

    private static ContractTemplateData ToTemplateData(WeddingBooking booking, decimal contractTotalValue)
    {
        var services = booking.BookingServices
            .Where(item => item.DataStatus.DataStatusCode == DataStatusCodes.Existing)
            .OrderBy(item => item.BookingServiceId)
            .ToList();
        return new(
            booking.BookingCode, booking.Customer.FullName, booking.HallSchedule.Date,
            booking.HallSchedule.Shift, booking.HallSchedule.Hall.HallName, booking.GuestCount,
            booking.Menu?.MenuName, booking.DecorationPackage?.TenGoi,
            services.Select(item => item.ServiceItem.TenDichVu).ToList(),
            booking.FinalHallPrice, booking.FinalMenuPrice, booking.FinalDecorationPrice,
            services.Select(item => new ContractTemplateServiceLine(item.ServiceItem.TenDichVu, item.Quantity, item.FinalUnitPrice)).ToList(),
            booking.EstimatedTotal, contractTotalValue);
    }

    /// <summary>
    /// FR15 / UC16: chỉ gửi email sau khi transaction đã commit; lỗi SMTP không ảnh hưởng kết quả đã lưu.
    /// </summary>
    private async Task TryNotifyContractStatusAsync(int contractId)
    {
        if (_emailService is null) return;
        try
        {
            var info = await _context.Contracts.AsNoTracking()
                .Where(contract => contract.ContractId == contractId)
                .Select(contract => new
                {
                    contract.ContractCode,
                    contract.TotalValue,
                    CustomerEmail = contract.Booking.Customer.Email,
                    CustomerName = contract.Booking.Customer.FullName,
                    BookingCode = contract.Booking.BookingCode,
                    EventDate = contract.Booking.HallSchedule.Date,
                    Shift = contract.Booking.HallSchedule.Shift,
                    HallName = contract.Booking.HallSchedule.Hall.HallName,
                    StatusName = contract.Status.StatusName,
                    PaidAmount = contract.Payments
                        .Where(payment => payment.Status.StatusCode == PaymentStatusCodes.Success)
                        .Sum(payment => (decimal?)payment.Amount) ?? 0m
                })
                .SingleOrDefaultAsync();
            if (info is null || string.IsNullOrWhiteSpace(info.CustomerEmail)) return;
            await _emailService.SendContractStatusChangedAsync(
                info.CustomerEmail.Trim(), info.CustomerName, info.ContractCode, info.BookingCode,
                info.StatusName, info.TotalValue, info.PaidAmount, info.EventDate, info.Shift, info.HallName);
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception, "Không gửi được email cập nhật hợp đồng {ContractId}.", contractId);
        }
    }

    private void AddAudit(int actorId, string action, Contract contract, object? oldData, object? newData, string notes) =>
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = actorId,
            Action = action,
            EntityName = AuditEntityNames.Contract,
            EntityId = contract.ContractId,
            OldData = oldData is null ? null : JsonSerializer.Serialize(oldData),
            NewData = newData is null ? null : JsonSerializer.Serialize(newData),
            Timestamp = DateTime.Now,
            Notes = notes
        });

    private ActionResult<ContractActionResponseDto> ActionResult(Contract contract, string statusCode, string message) =>
        Ok(new ContractActionResponseDto
        {
            ContractId = contract.ContractId,
            ContractCode = contract.ContractCode,
            StatusCode = statusCode,
            Message = message
        });

    private bool TryGetActorId(out int actorId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizeComparable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Replace("\r\n", "\n").Trim();
    private static string? NormalizeCode(string? value) => Normalize(value)?.ToUpperInvariant();
    private static bool IsContractStatus(string value) => value is ContractStatusCodes.Draft or
        ContractStatusCodes.Active or ContractStatusCodes.Completed or ContractStatusCodes.Cancelled;
}
