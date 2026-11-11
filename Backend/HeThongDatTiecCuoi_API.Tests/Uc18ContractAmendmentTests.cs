using System.Text.Json;
using HeThongDatTiecCuoi_API.DTOs.Contract;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class Uc18ContractAmendmentTests
{
    [Fact]
    public void Snapshot_UsesSharedDefaultSystemTextJsonPropertyNames()
    {
        var snapshot = new Uc18ContractAmendmentSnapshot
        {
            OriginalEvent = Event("Original Hall", new DateTime(2027, 1, 2)),
            PreviousEvent = Event("Original Hall", new DateTime(2027, 1, 2)),
            CurrentEvent = Event("Updated Hall", new DateTime(2027, 2, 3)),
            Reason = "Customer request"
        };

        var json = JsonSerializer.Serialize(snapshot);
        var restored = JsonSerializer.Deserialize<Uc18ContractAmendmentSnapshot>(json);

        Assert.Contains("\"OriginalEvent\"", json);
        Assert.Contains("\"PreviousEvent\"", json);
        Assert.Contains("\"CurrentEvent\"", json);
        Assert.Contains("\"DecorationPackageName\"", json);
        Assert.Equal("Original Hall", restored!.OriginalEvent!.HallName);
        Assert.Equal("Updated Hall", restored.CurrentEvent!.HallName);
    }

    [Fact]
    public void AmendmentAppendixPdf_GeneratesInMemoryFromStoredBeforeAfterAndAgreement()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var bytes = new ContractPdfGenerator().GenerateAmendmentAppendix(new ManagerContractAmendmentDto
        {
            Kind = Uc18BookingOperation.Reschedule,
            RecordedAtUtc = new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc),
            SignedPriceDelta = 2_000_000m,
            FeeAmount = 5_000_000m,
            EffectiveTotal = 127_000_000m,
            PolicyVersion = "UC18-v1",
            ManagerAgreementReference = "agreement-123",
            Snapshot = new Uc18ContractAmendmentSnapshot
            {
                PreviousEvent = Event("Sảnh cũ", new DateTime(2027, 1, 2)),
                CurrentEvent = Event("Sảnh mới", new DateTime(2027, 2, 3)),
                Reason = "Đổi lịch theo yêu cầu"
            },
            SnapshotAvailable = true
        });

        Assert.True(bytes.Length > 1_000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void OriginalEventSnapshot_ChangesOnlyPdfBookingHeaderAndIgnoresCorruptJson()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var detail = new ManagerContractDetailDto
        {
            StatusCode = "ACTIVE",
            TotalValue = 125_000_000m,
            ContractContent = "Signed content",
            PaymentTerms = "Signed terms",
            Booking = new ManagerContractBookingDto
            {
                EventDate = new DateTime(2027, 2, 3),
                Shift = "Ca tối",
                HallName = "Sảnh mới",
                GuestCount = 300,
                MenuName = "Menu mới",
                Services = ["Ánh sáng"]
            }
        };
        var json = JsonSerializer.Serialize(new Uc18ContractAmendmentSnapshot
        {
            OriginalEvent = Event("Sảnh gốc", new DateTime(2027, 1, 2))
        });

        Assert.True(ContractPdfGenerator.ApplyOriginalEventSnapshot(detail, json));
        Assert.Equal(new DateTime(2027, 1, 2), detail.Booking.EventDate);
        Assert.Equal("Sảnh gốc", detail.Booking.HallName);
        Assert.Equal(250, detail.Booking.GuestCount);
        Assert.Equal(125_000_000m, detail.TotalValue);
        Assert.Equal("Signed content", detail.ContractContent);
        Assert.Equal("Signed terms", detail.PaymentTerms);
        var bytes = new ContractPdfGenerator().Generate(detail);
        Assert.True(bytes.Length > 1_000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.False(ContractPdfGenerator.ApplyOriginalEventSnapshot(detail, "{"));
        Assert.Equal("Sảnh gốc", detail.Booking.HallName);
    }

    [Fact]
    public void DraftContractPdf_UsesCurrentBookingTermsInsteadOfAmendmentOriginalSnapshot()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var detail = new ManagerContractDetailDto
        {
            StatusCode = "DRAFT", ContractCode = "HD-DRAFT", TotalValue = 140_000_000m,
            ContractContent = "Current draft terms", PaymentTerms = "Current draft payment terms",
            Booking = new ManagerContractBookingDto
            {
                EventDate = new DateTime(2027, 2, 3), Shift = "Ca tối", HallName = "Current hall",
                GuestCount = 300, MenuName = "Current menu", Services = ["Current service"]
            }
        };
        var snapshotJson = JsonSerializer.Serialize(new Uc18ContractAmendmentSnapshot
        { OriginalEvent = Event("Signed original hall", new DateTime(2027, 1, 2)) });

        Assert.False(ContractPdfGenerator.ApplyOriginalEventSnapshot(detail, snapshotJson));
        var bytes = new ContractPdfGenerator().Generate(detail);

        Assert.Equal("Current hall", detail.Booking.HallName);
        Assert.Equal(new DateTime(2027, 2, 3), detail.Booking.EventDate);
        Assert.Equal("Current draft terms", detail.ContractContent);
        Assert.True(bytes.Length > 1_000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void OriginalEventSnapshot_RejectsMissingOrInvalidOriginalEvent()
    {
        var detail = new ManagerContractDetailDto
        {
            Booking = new ManagerContractBookingDto { HallName = "Live hall" }
        };

        Assert.False(ContractPdfGenerator.ApplyOriginalEventSnapshot(detail,
            JsonSerializer.Serialize(new Uc18ContractAmendmentSnapshot())));
        Assert.False(ContractPdfGenerator.ApplyOriginalEventSnapshot(detail,
            JsonSerializer.Serialize(new Uc18ContractAmendmentSnapshot
            {
                OriginalEvent = new Uc18ContractEventSnapshot { HallName = "", Shift = "Ca tối", Guests = 100 }
            })));
        Assert.False(ContractPdfGenerator.ApplyOriginalEventSnapshot(detail,
            "{\"OriginalEvent\":{\"Date\":\"2027-01-02T00:00:00\",\"Shift\":\"Ca tối\",\"HallName\":\"Sảnh\",\"Guests\":100,\"Services\":[]}}"));
        Assert.Equal("Live hall", detail.Booking.HallName);
    }

    private static Uc18ContractEventSnapshot Event(string hall, DateTime date) => new()
    {
        Date = date,
        Shift = "Ca tối",
        HallName = hall,
        MenuName = "Thực đơn Riverside",
        Guests = 250,
        DecorationPackageName = "Thanh lịch",
        Services = ["Âm thanh"]
    };
}
