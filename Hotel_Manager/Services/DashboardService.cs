using Hotel_Manager.Data;
using Microsoft.EntityFrameworkCore;

namespace Hotel_Manager.Services
{
    public class DashboardService
    {
        private readonly ApplicationDbContext _context;

        public DashboardService(ApplicationDbContext context)
        {
            _context = context;
        }

        public double GetCurrentOccupancy()
        {
            var now = DateTime.Now;

            var totalRooms = _context.Rooms.Count();

            var bookedRooms = _context.Reservations
                .Where(r => r.CheckInDate <= now && r.CheckOutDate >= now)
                .Select(r => r.Id)
                .Distinct()
                .Count();

            if (totalRooms == 0) return 0;

            return (double)bookedRooms / totalRooms * 100;
        }
    }
}
