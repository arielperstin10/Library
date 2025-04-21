using System.Diagnostics;
using LibraryProject.Data;
using LibraryProject.Data.Enums;
using LibraryProject.Data;
using Microsoft.AspNetCore.Mvc;
using LibraryProject.Models;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;

namespace LibraryProject.Controllers;

public class HomeController : Controller
{
    private readonly MVCProjectContext _context;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger, MVCProjectContext context)
    {
        _logger = logger;
        _context = context;
    }


    public async Task<IActionResult> Index()
    {
        ViewBag.Username = HttpContext.Session.GetString("Username");
        string username = HttpContext.Session.GetString("Username");
        ViewBag.Role = 0;
        ViewBag.isLogged = 0;
        ViewBag.CanReview = false;
        ViewBag.IsPasswordChanged = 0;

        if (username != null)
        {
            ViewBag.isLogged = 1;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user.Role == UserRole.Admin)
            {
                ViewBag.Role = 1;
            }

            var hasOrders = await _context.Orders.AnyAsync(o => o.Username == username);
            if (hasOrders)
            {
                ViewBag.CanReview = true;
            }

            if (user.IsPasswordChanged == 1)
            {
                ViewBag.IsPasswordChanged = 1;
            }
        }

        var siteReviews = await _context.SiteReviews
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .ToListAsync();

        return View(siteReviews);
    }

    [HttpPost]
    public async Task<IActionResult> AddSiteReview([FromBody] SiteReviewViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return Json(new { success = false, message = "Invalid form data." });
        }

        var username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
        {
            return Json(new { success = false, message = "You must be logged in to add a review." });
        }

        var hasOrders = await _context.Orders.AnyAsync(o => o.Username == username);
        if (!hasOrders)
        {
            return Json(new { success = false, message = "You must have at least one order to add a review." });
        }

        int nextReviewId;
        using (var connection = new OracleConnection(_context.Database.GetConnectionString()))
        {
            await connection.OpenAsync();
            using (var command = new OracleCommand("SELECT PERSTIN.SITEREVIEWS_SEQ.NEXTVAL FROM DUAL", connection))
            {
                nextReviewId = Convert.ToInt32(await command.ExecuteScalarAsync());
            }
        }

        var review = new SiteReview
        {
            Id = nextReviewId,
            Username = username,
            Title = model.Title,
            Content = model.Content,
            Rating = model.Rating,
            CreatedAt = DateTime.Now
        };

        _context.SiteReviews.Add(review);
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    public class SiteReviewViewModel
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public int Rating { get; set; }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] Contact contact)
    {
        try
        {
            // Explicitly set ContactId from sequence
            int nextId;
            using (var connection = new OracleConnection(_context.Database.GetConnectionString()))
            {
                await connection.OpenAsync();
                using var command = new OracleCommand("SELECT PERSTIN.CONTACTS_SEQ.NEXTVAL FROM DUAL", connection);
                nextId = Convert.ToInt32(await command.ExecuteScalarAsync());
            }

            var newContact = new Contact
            {
                ContactId = nextId,
                FullName = contact.FullName,
                Email = contact.Email,
                Message = contact.Message,
                SubmissionDate = DateTime.Now
            };

            _context.Contacts.Add(newContact);
            await _context.SaveChangesAsync();
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving contact");
            return StatusCode(500, "Failed to send message");
        }
    }
}