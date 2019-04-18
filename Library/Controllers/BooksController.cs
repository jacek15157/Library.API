using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Library.API.Entities;
using Library.API.Helpers;
using Library.API.Model;
using Library.API.Services;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers
{
    [Route(("api/authors/{authorId}/books"))]
    public class BooksController : Controller
    {
        private ILibraryRepository _libraryRepository;
        private IUrlHelper _urlHelper;

        public BooksController(ILibraryRepository libraryRepository,
            IUrlHelper urlHelper)
        {
            _libraryRepository = libraryRepository;
            _urlHelper = urlHelper;
        }

        [HttpGet(Name="GetBooksForAuthor")]
        public IActionResult GetBooksForAuthor(Guid authorId, [FromHeader(Name = "Accept")] string mediaType)
        {
            var author = _libraryRepository.GetAuthor(authorId);
            if (author is null)
            {
                return NotFound();
            }

            var booksFromRepo = _libraryRepository.GetBooksForAuthor(authorId);
            var booksForAuthor = Mapper.Map<IEnumerable<BookDto>>(booksFromRepo);
            if (mediaType == "application/vnd.marvin.hateoas+json")
            {
                var linkToCollection = CreateLinkForBooks(authorId);

                // null because books don't have any kind of shaping
                var shaped = booksForAuthor.ShapeData(null);

                var booksWithLinks = shaped.Select(b =>
                {
                    var bookAsDictionary = b as IDictionary<string, object>;
                    var bookLinks = CreateLinksForBook((Guid) bookAsDictionary["Id"]);

                    bookAsDictionary.Add("links", bookLinks);

                    return bookAsDictionary;
                });

                var linked = new
                {
                    value = booksWithLinks,
                    links = linkToCollection
                };

                return Ok(linked);
            }
            else
            {
                return Ok(booksForAuthor);
            }
        }

        [HttpGet("{id}",Name="GetBookForAuthor")]
        public IActionResult GetBookForAuthor(Guid id, Guid authorId, [FromHeader(Name = "Accept")] string mediaType)
        {
            var author = _libraryRepository.GetAuthor(authorId);
            if (author is null)
            {
                return NotFound();
            }
            var book = _libraryRepository.GetBookForAuthor(authorId, id);
            if (book is null)
            {
                return NotFound();
            }
            var bookForAuthor = Mapper.Map<BookDto>(book);

            if (mediaType == "application/vnd.marvin.hateoas+json")
            {    
                var links = CreateLinksForBook(bookForAuthor.Id);
                var linkedResourceToReturn = bookForAuthor.ShapeData(null) as IDictionary<string, object>;

                linkedResourceToReturn.Add("links", links);

                return Ok(linkedResourceToReturn);
            }
            else
            {
                return Ok(bookForAuthor);
            }
        }

        [HttpPost(Name = "CreateBookForAuthor")]
        public IActionResult CreateBookForAuthor(Guid authorId, [FromBody] BookForCreationDto book, [FromHeader(Name = "Accept")] string mediaType)
        {
            if (book is null)
            {
                return BadRequest();
            }

            if (book.Description==book.Title)
            {
                ModelState.AddModelError(nameof(BookForCreationDto), "description can not be the same as title");
            }

            if (!ModelState.IsValid)
            {
                return new UnprocessableEntityObjectResult(ModelState);
            }

            if (!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound(); 
            }

            var bookEntity = Mapper.Map<Book>(book);
            _libraryRepository.AddBookForAuthor(authorId,bookEntity);

            if (!_libraryRepository.Save())
            {
                throw new Exception($"book fails in save for author {authorId}");
            }

            var bookToReturn = Mapper.Map<BookDto>(bookEntity);

            if (mediaType == "application/vnd.marvin.hateoas+json")
            {

                var links = CreateLinksForBook(bookToReturn.Id);
                var linkedResourceToReturn = bookToReturn.ShapeData(null) as IDictionary<string, object>;

                linkedResourceToReturn.Add("links", links);

                return CreatedAtRoute("GetBookForAuthor",
                    new {authorId = authorId, id = bookToReturn.Id},
                    linkedResourceToReturn);
            }
            else
            {
                return CreatedAtRoute("GetBookForAuthor",
                    new { authorId = authorId, id = bookToReturn.Id },
                    bookToReturn);
            }
        }

        [HttpDelete("{id}", Name = "DeleteBookForAuthor")]
        public IActionResult DeleteBookForAuthor(Guid authorId, Guid id)
        {
            if (!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }

            var bookForAuthorFromRepo = _libraryRepository.GetBookForAuthor(authorId,id);

            if (bookForAuthorFromRepo is null)
            {
                return NotFound();
            }

            _libraryRepository.DeleteBook(bookForAuthorFromRepo);

            if (!_libraryRepository.Save())
            {
                throw new Exception("deleting fails on save");
            }

            return NoContent();
        }

        [HttpPut("{id}", Name = "UpdateBookForAuthor")]
        public IActionResult UpdateBookForAuthor(Guid authorId, Guid id, [FromBody]BookForUpdateDto book, [FromHeader(Name = "Accept")] string mediaType)
        {
            if (book is null)
            {
                return BadRequest();
            }

            if (!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }

            if (book.Description == book.Title)
            {
                ModelState.AddModelError(nameof(BookForUpdateDto), "description can not be the same as title");
            }

            if (!ModelState.IsValid)
            {
                return new UnprocessableEntityObjectResult(ModelState);
            }

            var bookForAuthorFromRepo = _libraryRepository.GetBookForAuthor(authorId, id);

            if (bookForAuthorFromRepo is null)
            {
                var bookToAdd = Mapper.Map<Book>(book);
                bookToAdd.Id = id;

                _libraryRepository.AddBookForAuthor(authorId,bookToAdd);

                if (!_libraryRepository.Save())
                {
                    throw new Exception($"Upserting book for author fails in save");
                }

                var bookToReturn = Mapper.Map<BookDto>(bookToAdd);
                if (mediaType == "application/vnd.marvin.hateoas+json")
                {
                    var links = CreateLinksForBook(bookToReturn.Id);
                    var linkedResourceToReturn = bookToReturn.ShapeData(null) as IDictionary<string, object>;

                    linkedResourceToReturn.Add("links", links);

                   return CreatedAtRoute("GetBookForAuthor",
                        new {id = bookToReturn.Id, authorId = bookToReturn.AuthorId},
                        linkedResourceToReturn);
                }
                else
                {
                  return  CreatedAtRoute("GetBookForAuthor",
                        new { id = bookToReturn.Id, authorId = bookToReturn.AuthorId },
                        bookToReturn);
                }
            }

            Mapper.Map(book, bookForAuthorFromRepo); 
            _libraryRepository.UpdateBookForAuthor(bookForAuthorFromRepo); //  it's is here because we work on repository contract not an implementation
                                                                           // and there could be other implementation of this repository which will use this method
                                                                           // e.g implementation for testing
            if (!_libraryRepository.Save())
            {
                throw new Exception($"update fails in save");
            }

            return NoContent(); 
        }

        [HttpPatch("{id}", Name = "PartialUpdateBookForAuthor")]
        public IActionResult PartialUpdateBookForAuthor(Guid authorId, Guid id,
            [FromBody] JsonPatchDocument<BookForUpdateDto> patchDoc,
            [FromHeader(Name = "Accept")] string mediaType)
        {
            if (patchDoc is null)
            {
                return BadRequest();
            }
            
            if (!_libraryRepository.AuthorExists(authorId))
            {
                return NotFound();
            }

            var bookForAuthorFromRepo = _libraryRepository.GetBookForAuthor(authorId, id);

            if (bookForAuthorFromRepo is null)
            {
                var bookDto = new BookForUpdateDto();
                patchDoc.ApplyTo(bookDto, ModelState);

                if (bookDto.Description == bookDto.Title)
                {
                    ModelState.AddModelError(nameof(BookForCreationDto), "description can not be the same as title");
                }

                TryValidateModel(bookDto);

                if (!ModelState.IsValid)
                {
                    return new UnprocessableEntityObjectResult(ModelState);
                }
                
                var bookToAdd = Mapper.Map<Book>(bookDto);
                bookToAdd.Id = id;
                _libraryRepository.AddBookForAuthor(authorId,bookToAdd);
                if (!_libraryRepository.Save())
                {
                    throw new Exception($"Upserting book for author fails in save");
                }

                var bookToReturn = Mapper.Map<BookDto>(bookToAdd);

                if (mediaType == "application/vnd.marvin.hateoas+json")
                {
                    var links = CreateLinksForBook(bookToReturn.Id);
                    var linkedResourceToReturn = bookToReturn.ShapeData(null) as IDictionary<string, object>;

                    linkedResourceToReturn.Add("links", links);

                    return CreatedAtRoute("GetBookForAuthor",
                        new
                        {
                            id = bookToReturn.Id,
                            authorId = bookToReturn.AuthorId
                        },
                        linkedResourceToReturn);
                }
                else
                {
                    return CreatedAtRoute("GetBookForAuthor",
                        new
                        {
                            id = bookToReturn.Id,
                            authorId = bookToReturn.AuthorId
                        },
                        bookToReturn);
                }
            }

            var bookToPatch = Mapper.Map<BookForUpdateDto>(bookForAuthorFromRepo);

            if (bookToPatch.Description == bookToPatch .Title)
            {
                ModelState.AddModelError(nameof(BookForCreationDto), "description can not be the same as title");
            }
            
            TryValidateModel(bookToPatch);
            patchDoc.ApplyTo(bookToPatch, ModelState);
            
            if (!ModelState.IsValid)
            {
                return new UnprocessableEntityObjectResult(ModelState);
            }

            Mapper.Map(bookToPatch, bookForAuthorFromRepo);

            if (!_libraryRepository.Save())
            {
                throw new Exception("patch book fails on save");
            }

            return NoContent();
        }

        private IEnumerable<LinkDto> CreateLinksForBook(Guid bookId)
        {
            var links = new List<LinkDto>();

                links.Add(new LinkDto(_urlHelper.Link("GetBookForAuthor",
                    new { id = bookId }),
                "self",
                "GET"));

                links.Add(
                new LinkDto(_urlHelper.Link("DeleteBookForAuthor",
                        new { id = bookId }),
                    "delete_book",
                    "DELETE"));

                links.Add(
                new LinkDto(_urlHelper.Link("UpdateBookForAuthor",
                        new { id = bookId }),
                    "update_book",
                    "PUT"));

                links.Add(
                new LinkDto(_urlHelper.Link("PartialUpdateBookForAuthor",
                        new { id = bookId }),
                    "partial_update_book",
                    "PATCH"));

            return links;
        }

        private LinkDto CreateLinkForBooks(Guid authorId)
        {
            var link = new LinkDto(_urlHelper.Link("GetBooksForAuthor",
                    new { id = authorId}),
                "self",
                "GET");

            return link;
        }

    }
}