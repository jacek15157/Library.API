﻿using System.ComponentModel.DataAnnotations;

namespace Library.API.Model
{
    public class BookForCreationDto
    {
        [Required (ErrorMessage = "Please fill out the title")]
        [MaxLength(500, ErrorMessage = "Max 500 characters")]
        public string  Title { get; set; }

        [MaxLength(500, ErrorMessage = "Max 500 characters")]
        public string  Description { get; set; }
    }
}
