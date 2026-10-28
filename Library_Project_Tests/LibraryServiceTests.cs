using Library_Project.Model;
using Library_Project.Services;
using Library_Project.Services.Interfaces;
using Moq;

namespace Library_Project_Tests
{
    public class LibraryServiceTests
    {
        private readonly Mock<IBookRepository> _repoMock;
        private readonly Mock<IMemberService> _memberMock;
        private readonly Mock<INotificationService> _notifMock;
        private readonly LibraryService _service;

        public LibraryServiceTests()
        {
            _repoMock = new Mock<IBookRepository>();
            _memberMock = new Mock<IMemberService>();
            _notifMock = new Mock<INotificationService>();

            _service = new LibraryService(_repoMock.Object, _memberMock.Object, _notifMock.Object);
        }

        #region AddBook

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("        ")]
        public void AddBook_EmptyTitle_ThrowsException(string? title)
        {
            const int copies = 1;
            var ex = Assert.Throws<ArgumentException>(() => _service.AddBook(title!, copies));
            Assert.Equal("Title required.", ex.Message);
            _repoMock.Verify(r => r.SaveBook(It.IsAny<Book>()), Times.Never);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(-10000)]
        public void AddBook_NonPositiveCopies_ThrowsException(int copies)
        {
            const string title = "Test Book";
            var ex = Assert.Throws<ArgumentException>(() => _service.AddBook(title!, copies));
            Assert.Equal("Copies must be positive.", ex.Message);
            _repoMock.Verify(r => r.SaveBook(It.IsAny<Book>()), Times.Never);
        }

        [Fact]
        public void AddBook_NewBook_SaveWithCorrectData()
        {
            const string title = "Test Book";
            const int copies = 3;
            Book? existing = null;
            _repoMock.Setup(r => r.FindBook(title)).Returns((Book?)null);
            _repoMock.Setup(r => r.SaveBook(It.IsAny<Book>())).Callback<Book>(b => existing = b);

            _service.AddBook(title, copies);
            
            Assert.NotNull(existing);
            Assert.Equal(copies, existing.Copies);
            _repoMock.Verify(r => r.SaveBook(It.Is<Book>(b => b.Title == title && b.Copies == copies)), Times.Once);
        }

        [Fact]
        public void AddBook_ExistingBook_IncreaseCopies()
        {
            const string title = "Test Book";
            const int copies = 3;
            const int existingCopies = 2;
            Book? existing = new Book() { Title = title, Copies = existingCopies };
            _repoMock.Setup(r => r.FindBook(title)).Returns(existing);
            _service.AddBook(title, copies);

            const int expectedCopies = 5;

            Assert.NotNull(existing);
            Assert.Equal(expectedCopies, existing.Copies);
            Assert.NotEqual(existingCopies, existing.Copies);

            _repoMock.Verify(r => r.SaveBook(existing), Times.Once);
        }

        #endregion

        #region BorrowBook

        [Fact]
        public void BorrowBook_InvalidMember_ThrowsException()
        {
            const int memberId = -1;
            const bool isValidMember = false;
            const string title = "Test Book";
            _memberMock.Setup(r => r.IsValidMember(memberId)).Returns(isValidMember);

            Assert.Throws<InvalidOperationException>(() => _service.BorrowBook(memberId, title));

            _memberMock.Verify(r => r.IsValidMember(memberId), Times.Once);
            _notifMock.VerifyNoOtherCalls();
            _repoMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void BorrowBook_BookNotExist_ReturnsFalse()
        {
            const int memberId = 1;
            const bool isValidMember = true;
            const string title = "Test Book";
            Book? existing = null;
            _memberMock.Setup(r => r.IsValidMember(memberId)).Returns(isValidMember);
            _repoMock.Setup(r => r.FindBook(title)).Returns(existing!);

            var result = _service.BorrowBook(memberId, title);
            Assert.False(result);
            
            _repoMock.Verify(r => r.FindBook(title), Times.Once);
            _repoMock.VerifyNoOtherCalls();
            _notifMock.VerifyNoOtherCalls();
        }

        [Fact]
        public void BorrowBook_BookHasNoCopies_ReturnsFalse()
        {
            const int memberId = 1;
            const bool isValidMember = true;
            const string title = "Test Book";
            const int copies = 0;
            Book? existing = new Book() { Title = title, Copies = copies };
            _memberMock.Setup(r => r.IsValidMember(memberId)).Returns(isValidMember);
            _repoMock.Setup(r => r.FindBook(title)).Returns(existing);

            var result = _service.BorrowBook(memberId, title);

            Assert.False(result);
            Assert.Equal(copies, existing.Copies);
            
            _repoMock.Verify(r => r.FindBook(title), Times.Once);
            _repoMock.VerifyNoOtherCalls();
            _notifMock.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(2, 1)]
        [InlineData(100, 99)]
        public void BorrowBook_HasBookCopies_ReturnsTrue(int existingCopies, int expectedCopies)
        {
            const int memberId = 1;
            const bool isValidMember = true;
            const string title = "Test Book";
            Book existing = new Book() { Title = title, Copies = existingCopies };
            _memberMock.Setup(r => r.IsValidMember(memberId)).Returns(isValidMember);
            _repoMock.Setup(r => r.FindBook(title)).Returns(existing);

            var result = _service.BorrowBook(memberId, title);

            Assert.True(result);
            Assert.Equal(expectedCopies, existing.Copies);
            Assert.NotEqual(existingCopies, existing.Copies);

            _repoMock.Verify(
                r => r.SaveBook(It.Is<Book>(b => b.Title == title && b.Copies == expectedCopies)),
                Times.Once
            );
            _notifMock.Verify(r => r.NotifyBorrow(memberId, title), Times.Once);
        }

        #endregion

        #region ReturnBook

        [Fact]
        public void ReturnBook_BookNotExist_ReturnsFalse()
        {
            const int memberId = 1;
            const string title = "Test Book";
            Book? existing = null;
            _repoMock.Setup(r => r.FindBook(title)).Returns(existing!);

            var result = _service.ReturnBook(memberId, title);
            Assert.False(result);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 2)]
        [InlineData(99, 100)]
        public void ReturnBook_ReturnsTrue(int existingCopies, int expectedCopies)
        {
            const int memberId = 1;
            const string title = "Test Book";
            Book existing = new Book() { Title = title, Copies = existingCopies };
            _repoMock.Setup(r => r.FindBook(title)).Returns(existing!);

            var result = _service.ReturnBook(memberId, title);
            Assert.True(result);
            Assert.Equal(expectedCopies, existing.Copies);

            _repoMock.Verify(
                r => r.SaveBook(It.Is<Book>(b => b.Title == title && b.Copies == expectedCopies)),
                Times.Once);
            _notifMock.Verify(r => r.NotifyReturn(memberId, title), Times.Once);
        }

        #endregion


        #region GetAvailableBooks

        [Fact]
        public void GetAvailableBooks_NoBooks_ReturnsEmpty()
        {
            _repoMock.Setup(r => r.GetAllBooks()).Returns(new List<Book> { });

            var result = _service.GetAvailableBooks();
            Assert.Empty(result);
        }

        [Fact]
        public void GetAvailableBooks_AllBorrowed_ReturnsEmpty()
        {
            var unavailableA = new Book { Title = "A", Copies = 0 };
            var unavailableB = new Book { Title = "B", Copies = 0 };
            _repoMock.Setup(r => r.GetAllBooks()).Returns([unavailableA, unavailableB]);
            
            var result = _service.GetAvailableBooks();
            Assert.Empty(result);
        }

        [Fact]
        public void GetAvailableBooks_AllAvailable_ReturnsAkk()
        {
            var availableA = new Book { Title = "A", Copies = 1 };
            var availableB = new Book { Title = "B", Copies = 1 };
            _repoMock.Setup(r => r.GetAllBooks()).Returns([availableA, availableB]);

            var result = _service.GetAvailableBooks();

            Assert.NotEmpty(result);
            Assert.Equal(2, result.Count);
            Assert.Contains(availableA, result);
            Assert.Contains(availableB, result);

        }

        [Fact]
        public void GetAvailableBooks_Mixed_ReturnsOnlyBooksWithCopies()
        {
            var available = new Book { Title = "A", Copies = 1 };
            var unavailable = new Book { Title = "B", Copies = 0 };
            _repoMock.Setup(r => r.GetAllBooks()).Returns([available, unavailable]);

            var result = _service.GetAvailableBooks();
            
            Assert.NotEmpty(result);
            Assert.Single(result);
            Assert.Contains(available, result);
            Assert.DoesNotContain(unavailable, result);
        }

        #endregion
    }
}