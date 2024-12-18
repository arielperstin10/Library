using LibraryProject.Data;
using LibraryProject.Data.Enums;
using LibraryProject.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryProject.Controllers;

public class BookController : Controller
{
    private readonly MVCProjectContext _context;

    public BookController(MVCProjectContext context)
    {
        _context = context;
    }
    
    // GET
    public IActionResult Index()
    {
        return View();
    }
    
    public async Task<IActionResult> ViewBook(int id)
    {
        Console.WriteLine($"ViewBook called with id: {id}");
        var book = await _context.Books.FirstOrDefaultAsync(b => b.BookId == id);
        if (book == null)
        {
            return NotFound();
        }

        // Fetch reviews for the book
        var reviews = await _context.Reviews
            .Where(r => r.BookId == id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        // Fetch related user details
        var usernames = reviews.Select(r => r.Username).Distinct();
        var users = await _context.Users
            .Where(u => usernames.Contains(u.Username))
            .ToDictionaryAsync(u => u.Username);

        // Attach user details to reviews
        var reviewDetails = reviews.Select(r => new
        {
            r.Title,
            r.Content,
            r.Rating,
            r.CreatedAt,
            User = users.TryGetValue(r.Username, out var user)
                ? new { user.FirstName, user.LastName }
                : null // Handle the case if a user doesn't exist
        }).ToList();

        ViewBag.Reviews = reviewDetails;

        // Get next and previous book IDs
        var nextBook = await _context.Books
            .Where(b => b.BookId > id)
            .OrderBy(b => b.BookId)
            .FirstOrDefaultAsync();

        var prevBook = await _context.Books
            .Where(b => b.BookId < id)
            .OrderByDescending(b => b.BookId)
            .FirstOrDefaultAsync();

        ViewBag.NextBookId = nextBook?.BookId;
        ViewBag.PrevBookId = prevBook?.BookId;

        return View(book);
    }
    
    [HttpGet]
    public async Task<IActionResult> AddBook()
    {
        string username = HttpContext.Session.GetString("Username");

        if (string.IsNullOrEmpty(username))
        {
            return RedirectToAction("Login", "User");
        }

        var userRole = await _context.Users
            .Where(u => u.Username == username)
            .Select(u => u.Role)
            .FirstOrDefaultAsync();

        if (userRole == null)
        {
            return RedirectToAction("Login", "User");
        }

        if (userRole.ToString() != UserRole.Admin.ToString())
        {
            TempData["Message"] = "Access Denied! You do not have the required permissions.";
            return RedirectToAction("Index", "Home");
        }
        
        // An action for user login.
        return View(new Book());
    }

    [HttpPost]
    public async Task<IActionResult> AddBook(Book book)
    {

        if (ModelState.IsValid)
        {
            try
            {
                _context.Books.Add(book);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occured while registering book.");
                return View(book);
            }
        }
        return View(book);
    }
    
    [HttpGet]
    public async Task<IActionResult> Store(string searchQuery = null, string genre = null)
    {
        string username = HttpContext.Session.GetString("Username");
        ViewBag.Username = username;

        var query = _context.Books.AsQueryable();

        if (!string.IsNullOrEmpty(searchQuery))
        {
            query = query.Where(b => b.Title.Contains(searchQuery) || b.Author.Contains(searchQuery));
        }

        if (!string.IsNullOrEmpty(genre) && genre.ToLower() != "all")
        {
            if (Enum.TryParse<Genre>(genre, true, out Genre genreEnum))
            {
                query = query.Where(b => b.Genre == genreEnum);
            }
        }

        var books = await query.ToListAsync();

        // Retrieve the user's wishlist items
        var wishlistItems = string.IsNullOrEmpty(username)
            ? new List<int>()
            : await _context.Wishlist
                .Where(w => w.Username == username)
                .Select(w => w.BookId)
                .ToListAsync();

        ViewBag.Wishlist = wishlistItems;
        ViewBag.Username = username;

        return View("BookStore", books);
    }

    [HttpPost]
    public async Task<IActionResult> AddToWishlist([FromBody] int bookId)
    {
        try
        {
            string username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized("User is not logged in.");
            }

            if (bookId <= 0 || !_context.Books.Any(b => b.BookId == bookId))
            {
                return BadRequest("Invalid BookId.");
            }

            if (_context.Wishlist.Any(w => w.Username == username && w.BookId == bookId))
            {
                return Conflict("This book is already in your wishlist.");
            }

            var wishlist = new Wishlist
            {
                Username = username,
                BookId = bookId
            };

            _context.Wishlist.Add(wishlist);
            await _context.SaveChangesAsync();

            return Ok("Book added to wishlist.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return StatusCode(500, "An internal server error occurred.");
        }
    }

    
    [HttpPost]
    public async Task<IActionResult> RemoveFromWishlist([FromBody] int bookId)
    {
        try
        {
            string username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized("User is not logged in.");
            }

            var wishlistItem = await _context.Wishlist
                .FirstOrDefaultAsync(w => w.Username == username && w.BookId == bookId);

            if (wishlistItem == null)
            {
                return NotFound("Book not found in wishlist.");
            }

            _context.Wishlist.Remove(wishlistItem);
            await _context.SaveChangesAsync();

            return Ok("Book removed from wishlist.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return StatusCode(500, "An internal server error occurred.");
        }
    }
    
    [HttpGet]
    public async Task<IActionResult> FilterBooks(
        string genre = "all", 
        string searchQuery = "", 
        string sortBy = "",
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string author = "",
        int? publishYear = null,
        AgeRestriction? ageRestriction = null)
    {
        var query = _context.Books.AsQueryable();
        // Apply genre filter
        if (!string.IsNullOrEmpty(genre) && genre.ToLower() != "all")
        {
            if (Enum.TryParse<Genre>(genre, true, out Genre genreEnum))
            {
                query = query.Where(b => b.Genre == genreEnum);
            }
        }
        // Apply search query
        if (!string.IsNullOrEmpty(searchQuery))
        {
            query = query.Where(b => 
                b.Title.Contains(searchQuery) || 
                b.Author.Contains(searchQuery));
        }
        // Apply author filter
        if (!string.IsNullOrEmpty(author))
        {
            query = query.Where(b => b.Author.Contains(author));
        }
        // Apply price range filter
        if (minPrice.HasValue)
        {
            query = query.Where(b => b.BuyPrice >= (double)minPrice.Value);
        }
        if (maxPrice.HasValue)
        {
            query = query.Where(b => b.BuyPrice <= (double)maxPrice.Value);
        }
        // Apply publish year filter
        if (publishYear.HasValue)
        {
            query = query.Where(b => b.PublishYear == publishYear.Value);
        }
        // Apply age restriction filter
        if (ageRestriction.HasValue)
        {
            query = query.Where(b => b.AgeRestriction == ageRestriction.Value);
        }
        // Apply sorting
        query = sortBy?.ToLower() switch
        {
            "price_asc" => query.OrderBy(b => b.BuyPrice),
            "price_desc" => query.OrderByDescending(b => b.BuyPrice),
            "year_asc" => query.OrderBy(b => b.PublishYear),
            "year_desc" => query.OrderByDescending(b => b.PublishYear),
            "popular" => query.OrderByDescending(b => b.TotalCopies - b.AvailableCopies),
            _ => query.OrderBy(b => b.Title)
        };
        var books = await query.ToListAsync();
        return Json(books);
    }

    ///////////////////////
    [HttpPost]
    public async Task<IActionResult> BuyBook([FromBody] int bookId)
    {   
        //int x = bookId;
        string username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
        {
            // User is not logged in, redirect to the Login page
            return RedirectToAction("Login", "User");
        }
        
        var book = await _context.Books.FirstOrDefaultAsync(b => b.BookId == bookId);
        var ExistsShoppingCart = _context.ShoppingCarts.FirstOrDefault(sc => 
            sc.BookId == book.BookId &&
            sc.Username == username &&
            sc.Action == "Buy");

        if (ExistsShoppingCart != null)
        {
            ExistsShoppingCart.Quantity += 1;
            ExistsShoppingCart.Price += book.BuyPrice;
        }
        else
        {
            var shoppingCart = new ShoppingCart
            {
                Username = username,
                BookId = bookId,
                Action = "Buy",
                Quantity =  1,
                Price = book.BuyPrice
            };
            _context.ShoppingCarts.Add(shoppingCart);
        }

        await _context.SaveChangesAsync();
        
        return View("BookStore"); 
    }

    public async Task<IActionResult> BorrowBook([FromBody] int bookId)
    {   
        //int x = bookId;
        string username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
        {
            // User is not logged in, redirect to the Login page
            return RedirectToAction("Login", "User");
        }
        
        var book = await _context.Books.FirstOrDefaultAsync(b => b.BookId == bookId);
        var ExistsShoppingCart = _context.ShoppingCarts.FirstOrDefault(sc => 
            sc.BookId == book.BookId &&
            sc.Username == username &&
            sc.Action == "Borrow");
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

        if (user.MaxBorrowed < 3)
        {
            if (ExistsShoppingCart != null)
            {
                ExistsShoppingCart.Quantity += 1;
                ExistsShoppingCart.Price += book.BorrowPrice;
            }
            else
            {
                var shoppingCart = new ShoppingCart
                {
                    Username = username,
                    BookId = bookId,
                    Action = "Borrow",
                    Quantity = 1,
                    Price = book.BorrowPrice
                };
                _context.ShoppingCarts.Add(shoppingCart);
            }

            book.AvailableCopies--;
            user.MaxBorrowed++;
            await _context.SaveChangesAsync();
        }
        else
        {
            return Json(new { success = false, message = "You have reached the maximum limit of 3 borrowed books." });
        }

        return View("BookStore"); 
    }
}
