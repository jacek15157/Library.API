using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Library.API.Entities;
using Library.API.Helpers;
using Library.API.Model;
using Library.API.Services;
using Microsoft.AspNetCore.Mvc;
using ActionResult = Microsoft.AspNetCore.Mvc.ActionResult;
using Controller = Microsoft.AspNetCore.Mvc.Controller;

namespace Library.API.Controllers
{
    [Route("api/authors")]
    public class AuthorsController : Controller
    {
        private ILibraryRepository _libraryRepository;
        private IUrlHelper _urlHelper;
        private ITypeHelperService _typeHelperService;

        public AuthorsController(ILibraryRepository libraryRepository,
            IUrlHelper urlHelper,
            ITypeHelperService typeHelperService)
        {
            _libraryRepository = libraryRepository;
            _urlHelper = urlHelper;
            _typeHelperService = typeHelperService;
        }

        [HttpGet(Name="GetAuthors")]
        public IActionResult GetAuthors(AuthorsResourceParameters authorsResourceParameters,[FromHeader(Name="Accept")] string mediaType)
        {
            if (!_typeHelperService.TypeHasProperties<AuthorDto>
                (authorsResourceParameters.Fields))
            {
                return BadRequest();
            }

            var authorsFromRepo = _libraryRepository.GetAuthors(authorsResourceParameters);

            var authors = Mapper.Map<IEnumerable<AuthorDto>>(authorsFromRepo);

            if (mediaType == "application/vnd.marvin.hateoas+json")
            {
                var paginationMetadata = new
                {
                    totalCount = authorsFromRepo.TotalCount,
                    pageSize = authorsFromRepo.PageSize,
                    currentPage = authorsFromRepo.CurrentPage,
                    totalPages = authorsFromRepo.TotalPages,
                };

                Response.Headers.Add("X-Pagination",
                    Newtonsoft.Json.JsonConvert.SerializeObject(paginationMetadata));
                
                var links = CreateLinksForAuthors(authorsResourceParameters,
                    authorsFromRepo.HasNext, authorsFromRepo.HasPrevious);

                var shapedAuthors = authors.ShapeData(authorsResourceParameters.Fields);

                var shapedAuthorsWithLinks = shapedAuthors.Select(author =>
                {
                    var authorAsDictionary = author as IDictionary<string, object>;
                    var authorLinks = CreateLinksForAuthor(
                        (Guid) authorAsDictionary["Id"], authorsResourceParameters.Fields);

                    authorAsDictionary.Add("links", authorLinks);
                    return authorAsDictionary;
                });

                var linkedCollectionResource = new
                {
                    value = shapedAuthorsWithLinks,
                    links = links
                };

                return Ok(linkedCollectionResource);
            }
            else
            {
                var previousPageLink = authorsFromRepo.HasPrevious ?
                    CreateAuthorsResourceUri(authorsResourceParameters,
                        ResourceUriType.PreviousPage) : null;

                var nextPageLink = authorsFromRepo.HasNext ?
                    CreateAuthorsResourceUri(authorsResourceParameters,
                        ResourceUriType.NextPage) : null;

                var paginationMetadata = new
                {
                    previousPageLink = previousPageLink,
                    nextPageLink = nextPageLink,
                    totalCount = authorsFromRepo.TotalCount,
                    pageSize = authorsFromRepo.PageSize,
                    currentPage = authorsFromRepo.CurrentPage,
                    totalPages = authorsFromRepo.TotalPages
                };

                Response.Headers.Add("X-Pagination",
                    Newtonsoft.Json.JsonConvert.SerializeObject(paginationMetadata));

                return Ok(authors.ShapeData(authorsResourceParameters.Fields));
            }
        }
        
        [HttpGet("{id}", Name = "GetAuthor")]
        public IActionResult GetAuthor(Guid id,[FromQuery] string fields, [FromHeader(Name = "Accept")] string mediaType)
        {
            if (!_typeHelperService.TypeHasProperties<AuthorDto>
                (fields))
            {
                return BadRequest();
            }

            var authorFromRepo = _libraryRepository.GetAuthor(id);

            if (authorFromRepo is null)
            {
                return NotFound();
            }
            var author = Mapper.Map<AuthorDto>(authorFromRepo);

            if (mediaType == "application/vnd.marvin.hateoas+json")
            {
                var links = CreateLinksForAuthor(id, fields);
                var linkedResourceToReturn = author.ShapeData(fields)
                    as IDictionary<string, object>;

                linkedResourceToReturn.Add("links", links);

                return Ok(linkedResourceToReturn);
            }
            else
            {
                return Ok(author.ShapeData(fields));
            }
           
        }
        
        [HttpPost(Name= "CreateAuthor")]
        public ActionResult CreateAuthor([FromBody] AuthorForCreationDto author, [FromHeader(Name = "Accept")] string mediaType)
        {
            if (author is null)
            {
                return BadRequest();
            }

            var authorEntity = Mapper.Map<Author>(author);
            _libraryRepository.AddAuthor(authorEntity);

            if (!_libraryRepository.Save())
            { 
                throw new Exception("Failed on save");  
            }

            var authorToReturn = Mapper.Map<AuthorDto>(authorEntity);
            if (mediaType == "application/vnd.marvin.hateoas+json")
            {
                var links = CreateLinksForAuthor(authorToReturn.Id, null);
                var linkedResourceToReturn = authorToReturn.ShapeData(null) as IDictionary<string, object>;

                linkedResourceToReturn.Add("links", links);

                return CreatedAtRoute("GetAuthor",
                    new { id = linkedResourceToReturn["Id"] },
                    linkedResourceToReturn);
            }
            else
            {
                return CreatedAtRoute("GetAuthor",
                    new { id = authorToReturn.Id },
                    authorToReturn);
            }
            
        }

        [HttpPost("{id}")]
        public IActionResult BlockAuthorCreation(Guid id)
        {
            if (_libraryRepository.AuthorExists(id))
            {
                return Conflict();
            }

            return NotFound();
        }

        [HttpDelete("{id}", Name= "DeleteAuthor")]
        public IActionResult DeleteAuthor(Guid id)
        {
            var authorFromRepo = _libraryRepository.GetAuthor(id);
            if (authorFromRepo is null)
            {
                return NotFound();
            }

            _libraryRepository.DeleteAuthor(authorFromRepo);

            if (!_libraryRepository.Save())
            {
                throw new Exception("deleting failed on save");
            }

            return NoContent();
        }

        private IEnumerable<LinkDto> CreateLinksForAuthor(Guid id, string fields)
        {
            var links = new List<LinkDto>();

            if (string.IsNullOrWhiteSpace(fields))
            {
                links.Add(
                    new LinkDto(_urlHelper.Link("GetAuthor", new { id = id }),
                        "self",
                        "GET"));
            }
            else
            {
                links.Add(
                    new LinkDto(_urlHelper.Link("GetAuthor", new { id = id, fields = fields }),
                        "self",
                        "GET"));
            }

            links.Add(
                new LinkDto(_urlHelper.Link("DeleteAuthor", new { id = id }),
                    "delete_author",
                    "DELETE"));

            links.Add(
                new LinkDto(_urlHelper.Link("CreateBookForAuthor", new { authorId = id }),
                    "create_book_for_author",
                    "POST"));

            links.Add(
                new LinkDto(_urlHelper.Link("GetBooksForAuthor", new { authorId = id }),
                    "books",
                    "GET"));

            return links;
        }

        private IEnumerable<LinkDto> CreateLinksForAuthors(
            AuthorsResourceParameters authorsResourceParameters,
            bool hasNext, bool hasPrevious)
        {
            var links = new List<LinkDto>();
            
            links.Add(
                new LinkDto(CreateAuthorsResourceUri(authorsResourceParameters,
                        ResourceUriType.Current)
                    , "self", "GET"));

            if (hasNext)
            {
                links.Add(
                    new LinkDto(CreateAuthorsResourceUri(authorsResourceParameters,
                            ResourceUriType.NextPage),
                        "nextPage", "GET"));
            }

            if (hasPrevious)
            {
                links.Add(
                    new LinkDto(CreateAuthorsResourceUri(authorsResourceParameters,
                            ResourceUriType.PreviousPage),
                        "previousPage", "GET"));
            }

            return links;
        }

        private string CreateAuthorsResourceUri(
            AuthorsResourceParameters authorsResourceParameters,
            ResourceUriType type)
        {
            switch (type)
            {
                case ResourceUriType.PreviousPage:
                    return _urlHelper.Link("GetAuthors",
                        new
                        {
                            fields = authorsResourceParameters.Fields,
                            searchQuery = authorsResourceParameters.SearchQuery,
                            genre = authorsResourceParameters.Genre,
                            pageNumber = authorsResourceParameters.PageNumber - 1,
                            pageSize = authorsResourceParameters.PageSize
                        });
                case ResourceUriType.NextPage:
                    return _urlHelper.Link("GetAuthors",
                        new
                        {
                            fields = authorsResourceParameters.Fields,
                            searchQuery = authorsResourceParameters.SearchQuery,
                            genre = authorsResourceParameters.Genre,
                            pageNumber = authorsResourceParameters.PageNumber + 1,
                            pageSize = authorsResourceParameters.PageSize
                        });
                case ResourceUriType.Current:
                default:
                    return _urlHelper.Link("GetAuthors",
                        new
                        {
                            fields = authorsResourceParameters.Fields,
                            searchQuery = authorsResourceParameters.SearchQuery,
                            genre = authorsResourceParameters.Genre,
                            pageNumber = authorsResourceParameters.PageNumber,
                            pageSize = authorsResourceParameters.PageSize
                        });
            }

        }
    }
}