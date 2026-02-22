using Hotel_Manager.Data;
using Hotel_Manager.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Hotel_Manager.Services;


namespace Hotel_Manager.Controllers
{

    [Authorize(Roles = "Admin")]

    public class RoomsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly RoomAvailabilityService _roomAvailabilityService;
        public RoomsController(ApplicationDbContext context, RoomAvailabilityService roomAvailabilityService)
        {
            _context = context; 
            _roomAvailabilityService = roomAvailabilityService;
        }

        // GET: Rooms
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Rooms.Include(r => r.RoomType);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Rooms/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var room = await _context.Rooms
                .Include(r => r.RoomType)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (room == null)
            {
                return NotFound();
            }

            return View(room);
        }

        // GET: Rooms/Create
        public IActionResult Create()
        {
            ViewData["RoomTypeId"] = new SelectList(_context.RoomTypes, "Id", "Name");
            return View();
        }

        // POST: Rooms/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,RoomNumber,IsAvailable,RoomTypeId")] Room room)
        {
            ModelState.Remove("RoomType");

            var exists = await _context.RoomTypes.AnyAsync(rt => rt.Id == room.RoomTypeId);
            if (!exists)
                ModelState.AddModelError("RoomTypeId", "Моля избери валиден тип стая.");

            if (!ModelState.IsValid)
            {
                ViewBag.RoomTypeId = new SelectList(_context.RoomTypes, "Id", "Name", room.RoomTypeId);
                return View(room);
            }

            _context.Rooms.Add(room);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Rooms/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var room = await _context.Rooms.FindAsync(id);
            if (room == null)
            {
                return NotFound();
            }

            if (await _roomAvailabilityService.IsRoomLockedAsync(room.Id))
            {
                TempData["Error"] = "Стаята е заета и не може да се редактира, докато резервацията не приключи/не бъде изтрита.";
                return RedirectToAction(nameof(Index));
            }

            ViewData["RoomTypeId"] = new SelectList(_context.RoomTypes, "Id", "Id", room.RoomTypeId);
            return View(room);
        }

        // POST: Rooms/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,RoomNumber,RoomTypeId")] Room room)
        {
            
            if (id != room.Id)
            {
                return NotFound();
            }

            ModelState.Remove("RoomType");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(room);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RoomExists(room.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.RoomTypeId = new SelectList(_context.RoomTypes, "Id", "Name", room.RoomTypeId);
            return View(room);
        }

        // GET: Rooms/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var room = await _context.Rooms
                .Include(r => r.RoomType)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (room == null)
            {
                return NotFound();
            }

            return View(room);
        }

        // POST: Rooms/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room != null)
            {
                _context.Rooms.Remove(room);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool RoomExists(int id)
        {
            return _context.Rooms.Any(e => e.Id == id);
        }
    }
}
