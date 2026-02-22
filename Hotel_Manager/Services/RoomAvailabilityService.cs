using Microsoft.EntityFrameworkCore;
using Hotel_Manager.Data;

public class RoomAvailabilityService
{
    private readonly ApplicationDbContext _context;

    public RoomAvailabilityService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task SetAvailabilityByReservationIdAsync(int reservationId, bool isAvailable)
    {
        var roomIds = await _context.ReservationRooms
            .Where(rr => rr.ReservationId == reservationId)
            .Select(rr => rr.RoomId)
            .Distinct()
            .ToListAsync();

        if (!roomIds.Any()) return;

        var rooms = await _context.Rooms
            .Where(r => roomIds.Contains(r.Id))
            .ToListAsync();

        foreach (var room in rooms)
            room.IsAvailable = isAvailable;

        _context.Rooms.UpdateRange(rooms);
    }
    public async Task AutoCompleteExpiredReservationsAsync()
    {
        var today = DateTime.Today;

        var expiredIds = await _context.Reservations
            .Where(r => r.CheckOutDate.Date < today
                        && r.Status != "Completed"
                        && r.Status != "Cancelled")
            .Select(r => r.Id)
            .ToListAsync();

        if (!expiredIds.Any()) return;

        await SetAvailabilityByReservationIdAsyncMultiple(expiredIds, true);

        var reservations = await _context.Reservations
            .Where(r => expiredIds.Contains(r.Id))
            .ToListAsync();

        foreach (var r in reservations)
            r.Status = "Completed";

        await _context.SaveChangesAsync();
    }

    private async Task SetAvailabilityByReservationIdAsyncMultiple(List<int> reservationIds, bool isAvailable)
    {
        var roomIds = await _context.ReservationRooms
            .Where(rr => reservationIds.Contains(rr.ReservationId))
            .Select(rr => rr.RoomId)
            .Distinct()
            .ToListAsync();

        if (!roomIds.Any()) return;

        var rooms = await _context.Rooms
            .Where(r => roomIds.Contains(r.Id))
            .ToListAsync();

        foreach (var room in rooms)
            room.IsAvailable = isAvailable;

        _context.Rooms.UpdateRange(rooms);
    }
    public async Task<bool> IsRoomLockedAsync(int roomId)
    {
        var today = DateTime.Today;

        return await _context.ReservationRooms
            .Include(rr => rr.Reservation)
            .AnyAsync(rr =>
                rr.RoomId == roomId &&
                rr.Reservation.Status != "Completed" &&
                rr.Reservation.Status != "Cancelled" &&
                rr.Reservation.CheckOutDate.Date >= today
            );
    }
}