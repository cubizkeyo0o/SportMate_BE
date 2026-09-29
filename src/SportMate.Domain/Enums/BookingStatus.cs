namespace SportMate.Domain.Enums;

public enum BookingStatus : byte
{
    Pending = 1,
    Confirmed = 2,
    Cancelled = 3,
    Completed = 4,
    NoShow = 5
}
