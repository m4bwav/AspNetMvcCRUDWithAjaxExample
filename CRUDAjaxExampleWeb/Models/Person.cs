using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CRUDAjaxExampleWeb.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;

    public partial class Person
    {
        public System.Guid Id { get; set; }

        [DisplayName("First Name")]
        public string FirstName { get; set; }

        [DisplayName("Middle Name")]
        public string MiddleName { get; set; }

        [Required]
        [DisplayName("Last Name")]
        public string LastName { get; set; }

        [Required]
        [DisplayName("Birth Date")]
        public System.DateTime BirthDate { get; set; }

        [DisplayName("Email Address")]
        public string EmailAddress { get; set; }

        public int Age
        {
            get
            {
                var zeroTime = new  DateTime(1,1,1);

                var span = DateTime.Now - BirthDate;

                return (zeroTime + span).Year - 1;
            }
        }
    }
}