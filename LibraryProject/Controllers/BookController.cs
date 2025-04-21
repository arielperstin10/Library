using LibraryProject.Data;
using LibraryProject.Data.Enums;
using LibraryProject.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Client;

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

    [HttpGet("Book/ViewBook")]
    public async Task<IActionResult> ViewBook()
    {
        var book = await _context.Books.FirstOrDefaultAsync();

        if (book == null)
        {
            return NotFound("No books found.");
        }

        return RedirectToAction("ViewBook", "Book", new { id = book.BookId });
    }

    [HttpGet("Book/ViewBook/{id}")]
    public async Task<IActionResult> ViewBook(int id)
    {
        Console.WriteLine($"ViewBook called with id: {id}");
        var book = await _context.Books.FirstOrDefaultAsync(b => b.BookId == id);
        if (book == null)
        {
            return NotFound();
        }

        ViewBag.Role = 0;
        string username = HttpContext.Session.GetString("Username");
        if (username != null)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user.Role == UserRole.Admin)
            {
                ViewBag.Role = 1;
            }
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
        var waitingList = await _context.WaitingList
            .Where(w => w.BookId == id)
            .OrderBy(w => w.Position)
            .Join(_context.Users,
                w => w.Username,
                u => u.Username,
                (w, u) => new
                {
                    WaitingListId = w.WaitingListId,
                    Position = w.Position,
                    Username = w.Username,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    JoinDate = w.JoinDate
                })
            .ToListAsync();

        ViewBag.WaitingList = waitingList;

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

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user.Role != UserRole.Admin)
        {
            return RedirectToAction("Index", "Home");
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
        // Check if discounted price is higher than buy price
        if (book.DiscountedBuyPrice.HasValue && book.DiscountedBuyPrice > book.BuyPrice)
        {
            ModelState.AddModelError("DiscountedBuyPrice", "Discounted price cannot be higher than the original price.");
            return View(book);
        }

        int nextBookId;
        using (var connection = new OracleConnection(_context.Database.GetConnectionString()))
        {
            await connection.OpenAsync();
            // Fixed the SQL command by adding "SELECT"
            using (var command = new OracleCommand("SELECT PERSTIN.BOOKS_SEQ.NEXTVAL FROM DUAL", connection))
            {
                nextBookId = Convert.ToInt32(await command.ExecuteScalarAsync());
            }
        }

        if (ModelState.IsValid)
        {
            try
            {
                // Set discount dates if discounted price is provided
                if (book.DiscountedBuyPrice.HasValue)
                {
                    // Additional validation to double-check
                    if (book.DiscountedBuyPrice > book.BuyPrice)
                    {
                        ModelState.AddModelError("DiscountedBuyPrice", "Discounted price cannot be higher than the original price.");
                        return View(book);
                    }

                    book.DiscountStartDate = DateTime.Now;
                    book.DiscountEndDate = DateTime.Now.AddDays(7);
                }

                var newBook = new Book
                {
                    BookId = nextBookId,
                    Title = book.Title,
                    Author = book.Author,
                    Genre = book.Genre,
                    AgeRestriction = book.AgeRestriction,
                    Publisher = book.Publisher,
                    PublishYear = book.PublishYear,
                    BuyPrice = book.BuyPrice,
                    BorrowPrice = book.BorrowPrice,
                    TotalCopies = 1000,
                    AvailableCopies = 3,
                    IsAvailableToBuy = true,
                    IsAvailableToBorrow = true,
                    IsEpubAvailable = book.IsEpubAvailable,
                    IsMobiAvailable = book.IsMobiAvailable,
                    IsF2bAvailable = book.IsF2bAvailable,
                    IsPdfAvailable = book.IsPdfAvailable,
                    DiscountedBuyPrice = book.DiscountedBuyPrice,
                    DiscountStartDate = book.DiscountStartDate,
                    DiscountEndDate = book.DiscountEndDate,
                    ImageUrl = book.ImageUrl ?? "/images/default-book.jpg"
                };

                _context.Books.Add(newBook);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while registering book.");
                return View(book);
            }
        }

        return View(book);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteBook(int id)
    {
        try
        {
            // Step 1: Fetch all reviews associated with the bookId
            var reviews = _context.Reviews.Where(r => r.BookId == id);

            // Step 2: Delete the associated reviews
            _context.Reviews.RemoveRange(reviews);

            // Step 3: Delete the book itself
            var book = new Book { BookId = id }; // Create a stub entity with only the ID
            _context.Books.Attach(book); // Attach the stub to the context
            _context.Books.Remove(book);

            // Step 4: Save changes
            await _context.SaveChangesAsync();

            // Return success response
            return Json(new { success = true, message = "Book and associated reviews deleted successfully" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }


    [HttpGet]
    public async Task<IActionResult> Store(string searchQuery = null, string genre = null)
    {
        string username = HttpContext.Session.GetString("Username");
        ViewBag.Username = username;

        ViewBag.Role = 0;
        if (username != null)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user.Role == UserRole.Admin)
            {
                ViewBag.Role = 1;
            }
        }

        var query = _context.Books.AsQueryable();

        // Apply search query
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            searchQuery = searchQuery.Trim().ToLower();
            query = query.Where(b =>
                b.Title.ToLower().Contains(searchQuery) ||
                b.Author.ToLower().Contains(searchQuery) ||
                b.Publisher.ToLower().Contains(searchQuery));
        }

        if (!string.IsNullOrEmpty(genre) && genre.ToLower() != "all")
        {
            if (Enum.TryParse<Genre>(genre, true, out Genre genreEnum))
            {
                query = query.Where(b => b.Genre == genreEnum);
            }
        }

        var books = await query.ToListAsync();
        foreach (var book in books)
        {
            Console.WriteLine($"Book: {book.Title}");
            Console.WriteLine($"Image path being used: /images/BookCovers/{book.ImageUrl}");
            var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "BookCovers",
                book.ImageUrl ?? "");
            Console.WriteLine($"Physical path: {physicalPath}");
            Console.WriteLine($"File exists: {System.IO.File.Exists(physicalPath)}");
            Console.WriteLine("-------------------");
        }

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
    
    [HttpPost]
    public async Task<IActionResult> AdminRemoveFromWaitingList([FromBody] RemoveWaitingListRequest request)
    {
        try 
        {
            string username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
            {
                return Json(new { success = false, message = "User not logged in" });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user.Role != UserRole.Admin)
            {
                return Json(new { success = false, message = "Admin access required" });
            }

            var entry = await _context.WaitingList.FindAsync(request.WaitingListId);
            if (entry == null)
            {
                return Json(new { success = false, message = "Waiting list entry not found" });
            }

            // Store position before removing
            int removedPosition = entry.Position;
            int bookId = entry.BookId;

            // Remove the entry
            _context.WaitingList.Remove(entry);

            // Update positions for remaining users
            var remainingEntries = await _context.WaitingList
                .Where(w => w.BookId == bookId && w.Position > removedPosition)
                .ToListAsync();

            foreach (var remainingEntry in remainingEntries)
            {
                remainingEntry.Position--;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Successfully removed from waiting list" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    public class RemoveWaitingListRequest
    {
        public int WaitingListId { get; set; }
        public int BookId { get; set; }
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
        AgeRestriction? ageRestriction = null,
        bool onlyDiscounted = false,
        bool onlyBorrowable = false)
    {
    
        var query = _context.Books.AsQueryable();
    
        if (onlyBorrowable)
        {
            Console.WriteLine("Applying borrowable filter");
            query = query.Where(b => b.IsAvailableToBorrow);
            
        }
        
        if (!string.IsNullOrEmpty(genre) && genre.ToLower() != "all")
        {
            if (Enum.TryParse<Genre>(genre, true, out Genre genreEnum))
            {
                query = query.Where(b => b.Genre == genreEnum);
            }
        }

        // Apply discount filter
        if (onlyDiscounted)
        {
            var currentDate = DateTime.Now;
            query = query.Where(b =>
                b.DiscountedBuyPrice.HasValue &&
                b.DiscountStartDate <= currentDate &&
                b.DiscountEndDate >= currentDate);
        }

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            searchQuery = searchQuery.Trim().ToLower();
            query = query.Where(b =>
                b.Title.ToLower().Contains(searchQuery) ||
                b.Author.ToLower().Contains(searchQuery) ||
                b.Publisher.ToLower().Contains(searchQuery));
        }

        // Apply price range filter
        if (minPrice.HasValue || maxPrice.HasValue)
        {
            if (minPrice.HasValue)
            {
                query = query.Where(b =>
                    (b.DiscountedBuyPrice.HasValue ? b.DiscountedBuyPrice : b.BuyPrice) >= (double)minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(b =>
                    (b.DiscountedBuyPrice.HasValue ? b.DiscountedBuyPrice : b.BuyPrice) <= (double)maxPrice.Value);
            }
        }

        // Apply age restriction filter
        if (ageRestriction.HasValue)
        {
            query = query.Where(b => b.AgeRestriction == ageRestriction.Value);
        }

        // Apply publish year filter
        if (publishYear.HasValue)
        {
            query = query.Where(b => b.PublishYear == publishYear.Value);
        }

        // Apply sorting
        query = sortBy?.ToLower() switch
        {
            "price_asc" => query.OrderBy(b =>
                b.DiscountedBuyPrice.HasValue ? b.DiscountedBuyPrice.Value : b.BuyPrice),
            "price_desc" => query.OrderByDescending(b =>
                b.DiscountedBuyPrice.HasValue ? b.DiscountedBuyPrice.Value : b.BuyPrice),
            "year_asc" => query.OrderBy(b => b.PublishYear),
            "year_desc" => query.OrderByDescending(b => b.PublishYear),
            "popular" => query.OrderByDescending(b => 
                _context.Orders.Count(o => 
                    o.BookId == b.BookId && 
                    o.IsRemoved == 0)),
            "on_sale" => query.OrderByDescending(b => b.DiscountedBuyPrice), // Changed to sort instead of filter
            _ => query.OrderBy(b => b.Title)  // Default case
        };

// Move the on_sale filter to where it belongs - with other filters
        if (sortBy?.ToLower() == "on_sale")
        {
            query = query.Where(b =>
                b.DiscountedBuyPrice.HasValue &&
                b.DiscountStartDate <= DateTime.Now &&
                b.DiscountEndDate >= DateTime.Now);
        }

        // Select all required properties
        var books = await query.Select(b => new
        {
            b.BookId,
            b.Title,
            b.Author,
            b.Publisher,
            b.PublishYear,
            b.Genre,
            b.AgeRestriction,
            b.IsAvailableToBuy,
            b.IsAvailableToBorrow,
            b.TotalCopies,
            b.AvailableCopies,
            b.BorrowPrice,
            b.BuyPrice,
            b.DiscountedBuyPrice,
            b.DiscountStartDate,
            b.DiscountEndDate,
            b.IsEpubAvailable,
            b.IsF2bAvailable,
            b.IsMobiAvailable,
            b.IsPdfAvailable,
            b.ImageUrl,
            IsOnDiscount = b.DiscountedBuyPrice.HasValue &&
                           b.DiscountStartDate <= DateTime.Now &&
                           b.DiscountEndDate >= DateTime.Now
        }).ToListAsync();
        return Json(books);
    }

    [HttpPost]
    public async Task<IActionResult> BuyBook([FromBody] int bookId)
    {
        string username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized();
        }

        // Check if book already exists in cart with any action
        var existingInCart = await _context.ShoppingCarts.FirstOrDefaultAsync(sc =>
            sc.BookId == bookId &&
            sc.Username == username);
        //We need to check that since it was the cause to not allow to add somebooks (multi times)
        // if (existingInCart != null)
        // {
        //     return Json(new { success = false, message = "This book is already in your cart." });
        // }

        var book = await _context.Books.FirstOrDefaultAsync(b => b.BookId == bookId);
        var ExistsShoppingCart = _context.ShoppingCarts.FirstOrDefault(sc =>
            sc.BookId == book.BookId &&
            sc.Username == username &&
            sc.Action == "Buy");

        // Get the current price (either discounted or original)
        double currentPrice = book.IsOnDiscount ? book.DiscountedBuyPrice.Value : book.BuyPrice;

        if (ExistsShoppingCart != null)
        {
            ExistsShoppingCart.Quantity += 1;
            ExistsShoppingCart.Price += currentPrice; // Use the current price
        }
        else
        {
            var shoppingCart = new ShoppingCart
            {
                Username = username,
                BookId = bookId,
                Action = "Buy",
                Quantity = 1,
                Price = currentPrice
            };
            _context.ShoppingCarts.Add(shoppingCart);
        }

        await _context.SaveChangesAsync();

        //return View("BookStore"); 

        return Json(new { success = true, message = "Book successfully added to cart!" });
    }

    [HttpPost]
    public async Task<IActionResult> BorrowBook([FromBody] int bookId)
    {
        string username = HttpContext.Session.GetString("Username");
   
    if (string.IsNullOrEmpty(username))
    {
        return Unauthorized();
    }
    
    // Check both cart and active orders
    var cartBorrowCount = await _context.ShoppingCarts
        .CountAsync(sc => sc.Username == username && 
                          sc.Action.ToLower() == "borrow");

    var activeBorrowCount = await _context.Orders
        .CountAsync(o => o.Username == username && 
                         o.Action.ToLower() == "borrow" && 
                         (o.IsReturned == 0 || o.IsReturned == null) && 
                         (o.IsRemoved == 0 || o.IsRemoved == null));

    if ((cartBorrowCount + activeBorrowCount) >= 3)
    {
        return Json(new { 
            success = false, 
            message = "You can only borrow up to 3 books at a time."
        });
    }

    var book = await _context.Books.FirstOrDefaultAsync(b => b.BookId == bookId);
    Console.WriteLine($"BorrowBook attempt - BookId: {bookId}, Username: {username}");
    Console.WriteLine($"Book state - IsReserved: {book.IsReserved}, ReservedFor: {book.ReservedForUsername}");
    if (book == null)
    {
        return NotFound();
    }

    // Check if book is reserved
    if (book.IsReserved)
    {
        // If reserved for someone else, deny access
        if (book.ReservedForUsername != username)
        {
            return Json(new { 
                success = false, 
                message = "This book is currently reserved for another user." 
            });
        }

        // If reservation has expired, deny access
        if (book.ReservationExpiry < DateTime.Now)
        {
            return Json(new { 
                success = false, 
                message = "Your reservation has expired." 
            });
        }
    }

    // Check if the book has already been borrowed and not returned
    var alreadyBorrowed = await _context.Orders
        .AnyAsync(o => o.BookId == bookId && o.Username == username && o.IsReturned == 0);

    if (alreadyBorrowed)
    {
        return Json(new { 
            success = false, 
            message = "You have already borrowed this book and not returned it yet." 
        });
    }
    
    var borrowedCount = await _context.ShoppingCarts
        .CountAsync(sc => sc.Username == username && sc.Action == "Borrow");
        
    if (borrowedCount >= 3)
    {
        return Json(new { 
            success = false, 
            message = "You have reached the maximum limit of 3 borrowed books." 
        });
    }

    // Add to shopping cart
    var shoppingCart = new ShoppingCart
    {
        Username = username,
        BookId = bookId,
        Action = "Borrow",
        Quantity = 1,
        Price = book.BorrowPrice
    };

    _context.ShoppingCarts.Add(shoppingCart);
    book.AvailableCopies--;

    // If this was a reserved book, clear the reservation
    if (book.IsReserved && book.ReservedForUsername == username)
    {
        book.IsReserved = false;
        book.ReservedForUsername = null;
        book.ReservationExpiry = null;
    }

    await _context.SaveChangesAsync();
    return Json(new { success = true, message = "Book successfully added to cart!" });
}
    [HttpGet]
    public async Task<IActionResult> CheckBookStatus(int bookId)
    {
        var book = await _context.Books.FindAsync(bookId);
        if (book == null)
        {
            return NotFound();
        }

        bool needsQueue = book.IsAvailableToBorrow && book.AvailableCopies == 0;

        // Check if there's already a waiting list
        var hasWaitingList = await _context.WaitingList
            .AnyAsync(w => w.BookId == bookId);

        return Json(new { 
            needsQueue = needsQueue || hasWaitingList,
            availableCopies = book.AvailableCopies
        });
    }

    [HttpPost]
    public async Task<IActionResult> AddToWaitingList([FromBody] int bookId)
    {
    try
    {
        // Get the username from the session
        string username = HttpContext.Session.GetString("Username");

        Console.WriteLine($"AddToWaitingList called - BookId: {bookId}, Username: {username}");

        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized("User must be logged in");
        }

        // Verify the book exists
        var book = await _context.Books.FindAsync(bookId);
        if (book == null)
        {
            return BadRequest("Book not found");
        }

        // Check if user is already in waiting list
        var existingEntry = await _context.WaitingList
            .FirstOrDefaultAsync(w => w.Username == username && w.BookId == bookId);

        if (existingEntry != null)
        {
            return BadRequest("You are already in the waiting list for this book");
        }

        // Get current position in the waiting list
        var currentPosition = await _context.WaitingList
            .Where(w => w.BookId == bookId)
            .CountAsync();

        var waitingList = new WaitingList
        {
            Username = username,
            BookId = bookId,
            JoinDate = DateTime.Now,
            Position = currentPosition + 1,
            IsNotified = false,
            NotificationDate = DateTime.Now.AddDays(25), 
            ExpiryDate = DateTime.Now.AddDays(30)
        };

        // Add to the waiting list and save changes
        _context.WaitingList.Add(waitingList);
        await _context.SaveChangesAsync();

        return Ok($"Successfully joined the waiting list at position {waitingList.Position}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in AddToWaitingList: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        return StatusCode(500, $"Internal server error: {ex.Message}");
    }
}
    [HttpPost]
    public async Task<IActionResult> CheckWaitingListStatus([FromBody] int bookId)
    {
        string username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized();
        }

        var waitingListEntry = await _context.WaitingList
            .FirstOrDefaultAsync(w => w.Username == username && w.BookId == bookId);

        var totalInQueue = await _context.WaitingList
            .CountAsync(w => w.BookId == bookId);

        return Json(new { 
            isInQueue = waitingListEntry != null,
            position = waitingListEntry?.Position ?? 0,
            totalInQueue = totalInQueue
        });
    }
    [HttpPost]
    public async Task<IActionResult> RemoveFromWaitingList([FromBody] int bookId)
    {
        string username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var entry = await _context.WaitingList
            .FirstOrDefaultAsync(w => w.Username == username && w.BookId == bookId);
    
        if (entry == null) return NotFound();

        _context.WaitingList.Remove(entry);

        // Update positions for users behind in queue
        var laterEntries = await _context.WaitingList
            .Where(w => w.BookId == bookId && w.Position > entry.Position)
            .ToListAsync();

        foreach (var item in laterEntries)
        {
            item.Position--;
        }

        await _context.SaveChangesAsync();
        return Ok("Successfully removed from waiting list");
    }

    [HttpPost]
    public async Task<IActionResult> UpdatePrice(int bookId, string type, double price)
    {
        var book = await _context.Books.FindAsync(bookId);
        if (book == null)
            return NotFound();

        if (type == "buy")
        {
            if (price < book.BorrowPrice)
                return BadRequest("Buy price cannot be lower than borrow price");
            book.BuyPrice = price;
        }
        else if (type == "borrow")
        {
            if (price > book.BuyPrice)
                return BadRequest("Borrow price cannot be higher than buy price");
            book.BorrowPrice = price;
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> SetDiscount(int bookId, double discountPrice)
    {
        var book = await _context.Books.FindAsync(bookId);
        if (book == null)
            return NotFound();

        if (discountPrice > book.BuyPrice)
            return BadRequest("Discount price cannot be higher than buy price");

        book.DiscountedBuyPrice = discountPrice;
        book.DiscountStartDate = DateTime.Now;
        book.DiscountEndDate = DateTime.Now.AddDays(7);

        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> RemoveDiscount(int bookId)
    {
        var book = await _context.Books.FindAsync(bookId);
        if (book == null)
            return NotFound();

        book.DiscountedBuyPrice = null;
        book.DiscountStartDate = null;
        book.DiscountEndDate = null;

        await _context.SaveChangesAsync();
        return Ok();
    }

    
    
}
