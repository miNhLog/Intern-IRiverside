using System;
using System.Collections.Generic;

namespace HeThongDatTiecCuoi_WEB.Models.MyBookings;

public class MyBookingViewModel
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Shift { get; set; } = string.Empty;
    public int TableCount { get; set; }
    public int GuestCount { get; set; }
    public string MenuName { get; set; } = string.Empty;
    public decimal MenuPricePerTable { get; set; }
    public string DecorName { get; set; } = string.Empty;
    public decimal DecorPrice { get; set; }
    public decimal EstimatedTotal { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime BookedAt { get; set; }
    public string? SpecialRequests { get; set; }
}
