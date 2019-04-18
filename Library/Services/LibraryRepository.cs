using Library.API.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using Library.API.Helpers;

namespace Library.API.Services
{
    public class LibraryRepository : ILibraryRepository
    {
        private LibraryContext _context;

        public LibraryRepository(LibraryContext context)
        {
            _context = context;
        }

        public void AddAuthor(Author author)
        {
            author.Id = Guid.NewGuid();
            _context.Authors.Add(author);
            
            // the repository fills the id (instead of using identity columns)
            if (author.Books.Any())
            {
                foreach (var book in author.Books)
                {
                    book.Id = Guid.NewGuid();
                }
            }
        }

        public void AddBookForAuthor(Guid authorId, Book book)
        {
            var author = GetAuthor(authorId);
            if (author != null)
            {
                // we should generate one if there isn't any id filled out (e.g. we're not upserting),
                if (book.Id == Guid.Empty)
                {
                    book.Id = Guid.NewGuid();
                }
                author.Books.Add(book);
            }
        }

        public bool AuthorExists(Guid authorId)
        {
            return _context.Authors.Any(a => a.Id == authorId);
        }

        public void DeleteAuthor(Author author)
        {
            _context.Authors.Remove(author);
        }

        public void DeleteBook(Book book)
        {
            _context.Books.Remove(book);
        }

        public Author GetAuthor(Guid authorId)
        {
            return _context.Authors.FirstOrDefault(a => a.Id == authorId);
        }
        
        public PagedList<Author> GetAuthors(AuthorsResourceParameters authorsResourceParameters)
        {
            var collectionBeforePaging = _context.Authors
                .OrderBy(a => a.FirstName)
                .ThenBy(a => a.LastName).AsQueryable();
            
            if (!string.IsNullOrEmpty(authorsResourceParameters.Genre))
            {
                var genreForWhereClause = authorsResourceParameters.Genre
                    .Trim().ToLowerInvariant();
                collectionBeforePaging = collectionBeforePaging
                    .Where(a => a.Genre.ToLowerInvariant() == genreForWhereClause);
            }

            if (!string.IsNullOrEmpty(authorsResourceParameters.SearchQuery))
            {
                var searchQueryForWhereClause = authorsResourceParameters.SearchQuery
                    .Trim().ToLowerInvariant();

                collectionBeforePaging = collectionBeforePaging
                    .Where(a => a.Genre.ToLowerInvariant().Contains(searchQueryForWhereClause)
                                || a.FirstName.ToLowerInvariant().Contains(searchQueryForWhereClause)
                                || a.LastName.ToLowerInvariant().Contains(searchQueryForWhereClause));
            }

            return PagedList<Author>.Create(collectionBeforePaging,
                authorsResourceParameters.PageNumber,
                authorsResourceParameters.PageSize);
        }

        public void UpdateAuthor(Author author)
        {
            // no code in this implementation
        }

        public Book GetBookForAuthor(Guid authorId, Guid bookId)
        {
            return _context.Books.FirstOrDefault(b => b.AuthorId == authorId && b.Id == bookId);
        }

        public IEnumerable<Book> GetBooksForAuthor(Guid authorId)
        {
            return _context.Books
                .Where(b => b.AuthorId == authorId)
                .OrderBy(b => b.Title).ToList();
        }

        public void UpdateBookForAuthor(Book book)
        {
            // no code in this implementation
        }

        public bool Save()
        {
            return (_context.SaveChanges() >= 0);
        }
    }
}
