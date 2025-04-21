using Microsoft.EntityFrameworkCore;
using LibraryProject.Models;

namespace LibraryProject.Data
{
    public class MVCProjectContext : DbContext
    {
        public MVCProjectContext(DbContextOptions<MVCProjectContext> options)
            : base(options)
        {
        }
        public DbSet<User> Users { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<Wishlist> Wishlist { get; set; }
        public DbSet<ShoppingCart> ShoppingCarts { get; set; }
        public DbSet<SiteReview> SiteReviews { get; set; }
        public DbSet<WaitingList> WaitingList { get; set; }
        
        public DbSet<Contact> Contacts { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema("PERSTIN");
            
            modelBuilder.HasSequence<int>("REVIEWS_SEQ", schema: "PERSTIN").StartsAt(0).IncrementsBy(1);
            modelBuilder.HasSequence<int>("BOOKS_SEQ", schema: "PERSTIN").StartsAt(28).IncrementsBy(1);
            modelBuilder.HasSequence<int>("ORDER_SEQ", schema: "PERSTIN").StartsAt(6).IncrementsBy(1);
            modelBuilder.HasSequence<int>("SITEREVIEWS_SEQ", schema: "PERSTIN").StartsAt(1).IncrementsBy(1);
            modelBuilder.HasSequence<int>("WAITINGLIST_SEQ", schema: "PERSTIN").StartsAt(1).IncrementsBy(1);
            modelBuilder.HasSequence<int>("CONTACTS_SEQ", schema: "PERSTIN").StartsAt(1).IncrementsBy(1);
            
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("USERS","PERSTIN");
                entity.HasKey(e => e.Username);
                entity.Property(e => e.Username).HasColumnName("USERNAME");
                entity.Property(e => e.Password).HasColumnName("PASSWORD");
                entity.Property(e => e.Email).HasColumnName("EMAIL");
                entity.Property(e => e.FirstName).HasColumnName("FIRSTNAME");  // Note: FirstName maps to FIRSTNAME
                entity.Property(e => e.LastName).HasColumnName("LASTNAME");
                entity.Property(e => e.MaxBorrowed).HasColumnName("MAXBORROWED");
                entity.Property(e => e.IsPasswordChanged).HasColumnName("ISPASSWORDCHANGED");
                entity.Property(e => e.id).HasColumnName("ID");
                entity.Property(e => e.cardNumber).HasColumnName("CREDIT_CARD_NUMBER");
                entity.Property(e => e.validDate).HasColumnName("VALID_DATE");
                entity.Property(e => e.cvc).HasColumnName("CVC");
    
                // For the enum properties, we need to tell EF Core to convert them to strings
                entity.Property(e => e.Gender)
                    .HasColumnName("GENDER")
                    .HasConversion<string>();
        
                entity.Property(e => e.Role)
                    .HasColumnName("ROLE")
                    .HasConversion<string>();
                
            });
            
            modelBuilder.Entity<Book>(entity =>
            {
                entity.ToTable("BOOKS","PERSTIN");
                entity.HasKey(e => e.BookId);
                entity.Property(e => e.BookId).HasColumnName("BOOKID");
                entity.Property(e => e.Title).HasColumnName("TITLE");
                entity.Property(e => e.Author).HasColumnName("AUTHOR");
                entity.Property(e => e.Genre).HasColumnName("GENRE").HasConversion<string>();
                entity.Property(e => e.AgeRestriction).HasColumnName("AGERESTRICTION").HasConversion<string>();
                entity.Property(e => e.Publisher).HasColumnName("PUBLISHER");
                entity.Property(e => e.PublishYear).HasColumnName("PUBLISHYEAR");
                entity.Property(e => e.BuyPrice).HasColumnName("BUYPRICE");
                entity.Property(e => e.BorrowPrice).HasColumnName("BORROWPRICE");
                entity.Property(e => e.TotalCopies).HasColumnName("TOTALCOPIES");
                entity.Property(e => e.AvailableCopies).HasColumnName("AVAILABLECOPIES");
                entity.Property(e => e.IsAvailableToBuy).HasColumnName("ISAVAILABLETOBUY");
                entity.Property(e => e.IsAvailableToBorrow).HasColumnName("ISAVAILABLETOBORROW");
                entity.Property(e => e.IsEpubAvailable).HasColumnName("ISEPUB");
                entity.Property(e => e.IsF2bAvailable).HasColumnName("ISF2B");
                entity.Property(e => e.IsMobiAvailable).HasColumnName("ISMOBI");
                entity.Property(e => e.IsPdfAvailable).HasColumnName("ISPDF");
                entity.Property(e => e.DiscountedBuyPrice).HasColumnName("DISCOUNTEDBUYPRICE");
                entity.Property(e => e.DiscountStartDate).HasColumnName("DISCOUNTSTARTDATE"); 
                entity.Property(e => e.DiscountEndDate).HasColumnName("DISCOUNTENDDATE");
                entity.Property(e => e.ImageUrl).HasColumnName("IMAGEURL");
                entity.Property(e => e.IsReserved)
                    .HasColumnName("ISRESERVED")
                    .HasDefaultValue(false);
                entity.Property(e => e.ReservedForUsername)
                    .HasColumnName("RESERVEDFORUSERNAME");
                entity.Property(e => e.ReservationExpiry)
                    .HasColumnName("RESERVATIONEXPIRY");
            });

            modelBuilder.Entity<Review>(entity =>
            {
                entity.ToTable("REVIEWS", "PERSTIN");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("REVIEWID");
                entity.Property(e => e.Username).HasColumnName("USERNAME");
                entity.Property(e => e.BookId).HasColumnName("BOOKID");
                entity.Property(e => e.Title).HasColumnName("TITLE");
                entity.Property(e => e.Content).HasColumnName("CONTENT");
                entity.Property(e => e.Rating).HasColumnName("RATING");
                entity.Property(e => e.CreatedAt).HasColumnName("REVIEWDATE");
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.ToTable("ORDERS", "PERSTIN");
                entity.HasKey(e => new { e.OrderId, e.Username, e.BookId });
                entity.Property(e => e.OrderId).HasColumnName("ORDERID");
                entity.Property(e => e.Username).HasColumnName("USERNAME");
                entity.Property(e => e.BookId).HasColumnName("BOOKID");
                entity.Property(e => e.Action).HasColumnName("ACTION");
                entity.Property(e => e.Price).HasColumnName("PRICE");
                entity.Property(e => e.Quantity).HasColumnName("QUANTITY");
                entity.Property(e => e.OrderDate).HasColumnName("ORDERDATE");
                entity.Property(e => e.BorrowStartDate).HasColumnName("BORROWSTARTDATE");
                entity.Property(e => e.BorrowEndDate).HasColumnName("BORROWENDDATE");
                entity.Property(e => e.IsReturned).HasColumnName("ISRETURNED");
                entity.Property(e => e.IsRemoved).HasColumnName("ISREMOVED");
            });
            
            modelBuilder.Entity<Wishlist>(entity =>
            {
                entity.ToTable("WISHLIST", "PERSTIN");
                entity.HasKey(e => new { e.Username, e.BookId });
                entity.Property(e => e.Username).HasColumnName("USERNAME");
                entity.Property(e => e.BookId).HasColumnName("BOOKID");
                //Foreign key
                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(e => e.Username)
                    .HasConstraintName("FK_WISHLIST_USERS");

                entity.HasOne<Book>()
                    .WithMany()
                    .HasForeignKey(e => e.BookId)
                    .HasConstraintName("FK_WISHLIST_BOOKS");
            });

            modelBuilder.Entity<ShoppingCart>(entity =>
            {
                entity.ToTable("SHOPPINGCART", "PERSTIN");
                entity.HasKey(e => new { e.Username, e.BookId, e.Action });
                entity.Property(e => e.Username).HasColumnName("USERNAME");
                entity.Property(e => e.BookId).HasColumnName("BOOKID");
                entity.Property(e => e.Action).HasColumnName("ACTION");
                entity.Property(e => e.Quantity).HasColumnName("QUANTITY");
                entity.Property(e => e.Price).HasColumnName("PRICE");
            });

            modelBuilder.Entity<SiteReview>(entity =>
            {
                entity.ToTable("SITEREVIEWS", "PERSTIN");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("REVIEWID");
                entity.Property(e => e.Username).HasColumnName("USERNAME");
                entity.Property(e => e.Title).HasColumnName("TITLE");
                entity.Property(e => e.Content).HasColumnName("CONTENT");
                entity.Property(e => e.Rating).HasColumnName("RATING");
                entity.Property(e => e.CreatedAt).HasColumnName("REVIEWDATE");
            });
            

            modelBuilder.Entity<WaitingList>(entity =>
            {
                entity.ToTable("WAITINGLIST", "PERSTIN");
                entity.HasKey(e => e.WaitingListId);
                entity.HasIndex(e => new { e.Username, e.BookId })
                    .IsUnique()
                    .HasDatabaseName("UK_WAITINGLIST_USERNAME_BOOKID");
                entity.Property(e => e.WaitingListId)
                    .HasColumnName("WAITINGLISTID")
                    .ValueGeneratedOnAdd()
                    .HasDefaultValueSql("PERSTIN.WAITINGLIST_SEQ.NEXTVAL");
                entity.Property(e => e.Username).HasColumnName("USERNAME");
                entity.Property(e => e.BookId).HasColumnName("BOOKID");
                entity.Property(e => e.JoinDate)
                    .HasColumnName("JOINDATE")
                    .HasDefaultValueSql("SYSDATE");
                entity.Property(e => e.Position).HasColumnName("POSITION");
                entity.Property(e => e.IsNotified)
                    .HasColumnName("ISNOTIFIED")
                    .HasDefaultValue(0);
                entity.Property(e => e.NotificationDate).HasColumnName("NOTIFICATIONDATE");
                entity.Property(e => e.ExpiryDate).HasColumnName("EXPIRYDATE");
            });
            
            modelBuilder.Entity<Contact>(entity =>
            {
                entity.ToTable("CONTACTS", "PERSTIN");
                entity.HasKey(e => e.ContactId);
                entity.Property(e => e.ContactId)
                    .HasColumnName("CONTACT_ID")
                    .ValueGeneratedOnAdd()
                    .HasDefaultValueSql("PERSTIN.CONTACTS_SEQ.NEXTVAL");
                entity.Property(e => e.FullName)
                    .HasColumnName("FULL_NAME")
                    .IsRequired();
                entity.Property(e => e.Email)
                    .HasColumnName("EMAIL")
                    .IsRequired();
                entity.Property(e => e.Message)
                    .HasColumnName("MESSAGE")
                    .IsRequired();
                entity.Property(e => e.SubmissionDate)
                    .HasColumnName("SUBMISSION_DATE")
                    .HasDefaultValueSql("SYSTIMESTAMP");
            });
        }
    }
}