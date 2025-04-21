using LibraryProject.Data;
using LibraryProject.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;

namespace LibraryProject.Controllers;

public class ReviewController : Controller
{
    private readonly MVCProjectContext _context;

    public ReviewController(MVCProjectContext context)
    {
        _context = context;
    }

    // GET
    public IActionResult Index()
    {
        return View();
    }
    
    public async Task<IActionResult> Create(int bookId)
    {
        string username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
        {
            return RedirectToAction("Login", "User");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        var book = await _context.Books.FirstOrDefaultAsync(b => b.BookId == bookId);

        if (book == null)
        {
            return NotFound();
        }

        ViewBag.User = user;
        ViewBag.Book = book;
        
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int bookId, string title, string content, int rating)
    {
        string username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
        {
            return RedirectToAction("Login", "User");
        }

        int nextReviewId;
        using (var connection = new OracleConnection(_context.Database.GetConnectionString()))
        {
            await connection.OpenAsync();
            using (var command = new OracleCommand("SELECT PERSTIN.REVIEWS_SEQ.NEXTVAL FROM DUAL", connection))
            {
                nextReviewId = Convert.ToInt32(await command.ExecuteScalarAsync());
            }
        }

        var review = new Review
        {
            Id = nextReviewId,
            Username = username,
            BookId = bookId,
            Title = title,
            Content = content,
            Rating = rating,
            CreatedAt = DateTime.Now
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        return RedirectToAction("ViewBook", "Book", new { id = bookId });
    }
}