using System.Collections.Generic;

namespace Library.API.Model
{
    public class AuthorForCreationDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Genre { get; set; }
        public ICollection<BookForCreationDto> Books { get; set; }
        = new List<BookForCreationDto>();
    }
}
